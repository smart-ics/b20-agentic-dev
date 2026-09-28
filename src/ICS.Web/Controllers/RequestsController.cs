namespace ICS.Web.Controllers;

using System.Diagnostics;
using ICS.Core.Auth;
using ICS.Modules.Customer;
using ICS.Modules.Customer.Application.DTOs;
using ICS.Modules.Organization;
using ICS.Modules.Organization.Application.DTOs;
using ICS.Modules.Product;
using ICS.Modules.Product.Application.DTOs;
using ICS.Modules.Request;
using ICS.Modules.Request.Application;
using ICS.Modules.Request.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// REST API Controller managing Request lifecycle, queries, transitions, and dropdown lookups per
/// Architecture §7, §8, §9, §16, §18, §19.4, §19.6, §20, and SCR-REQ-001..005 (Slice P4-S19).
/// All endpoints are protected by [Authorize].
/// </summary>
[ApiController]
[Route("api/v1/requests")]
[Authorize]
public class RequestsController : ControllerBase
{
    private readonly IRequestService _requestService;
    private readonly IRequestQueryService _requestQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICurrentContextProvider _currentContextProvider;
    private readonly ILogger<RequestsController> _logger;

    public RequestsController(
        IRequestService requestService,
        IRequestQueryService requestQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        IOrganizationQueryService organizationQueryService,
        ICurrentContextProvider currentContextProvider,
        ILogger<RequestsController> logger)
    {
        _requestService = requestService ?? throw new ArgumentNullException(nameof(requestService));
        _requestQueryService = requestQueryService ?? throw new ArgumentNullException(nameof(requestQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _currentContextProvider = currentContextProvider ?? throw new ArgumentNullException(nameof(currentContextProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a paginated, filtered grid of requests for SCR-REQ-001 and SCR-REQ-005.
    /// GET /api/v1/requests
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(RequestGridResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRequests(
        [FromQuery] string? searchTerm,
        [FromQuery] Guid? customerId,
        [FromQuery] Guid? productId,
        [FromQuery] Guid? ownerPersonId,
        [FromQuery] string? statuses,
        [FromQuery] string? priority,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var filter = new RequestGridFilterDto(
            SearchTerm: searchTerm,
            CustomerId: customerId,
            ProductId: productId,
            OwnerPersonId: ownerPersonId,
            Statuses: statuses,
            Priority: priority,
            FromDate: fromDate,
            ToDate: toDate,
            Skip: Math.Max(0, skip),
            Take: take <= 0 ? 50 : Math.Min(100, take));

        var result = await _requestQueryService.GetFilteredRequestGridAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets a Request by identifier including assignments and resolution for SCR-REQ-003.
    /// GET /api/v1/requests/{id}
    /// </summary>
    [HttpGet("{id:guid}", Name = "GetRequestById")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRequestById(Guid id, CancellationToken cancellationToken)
    {
        var request = await _requestQueryService.GetRequestByIdAsync(id, cancellationToken);
        if (request == null)
        {
            return RequestNotFound(id);
        }

        return Ok(request);
    }

    /// <summary>
    /// Creates / records a new customer request for SCR-REQ-002 (UC-REQ-001).
    /// POST /api/v1/requests
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordRequest([FromBody] RecordRequestApiRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequestProblem("Request payload is required.");
        }

        var ambientPersonId = _currentContextProvider.CurrentPersonId;
        var requesterPersonId = request.RequesterPersonId ?? ambientPersonId;
        var assignedByPersonId = request.AssignedByPersonId ?? ambientPersonId;

        var result = await _requestService.RecordRequestAsync(
            title: request.Title,
            description: request.Description,
            type: request.Type,
            priority: request.Priority,
            requesterPersonId: requesterPersonId,
            requesterContactId: request.RequesterContactId,
            requesterName: request.RequesterName,
            customerId: request.CustomerId,
            productId: request.ProductId,
            workPackageId: request.WorkPackageId,
            initialOwnerPersonId: request.InitialOwnerPersonId,
            assignedByPersonId: assignedByPersonId,
            requestId: request.RequestId,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Request '{RequestId}' recorded successfully by person '{PersonId}'.", result.RequestId, ambientPersonId);

        return CreatedAtRoute("GetRequestById", new { id = result.RequestId }, result);
    }

    /// <summary>
    /// Lists active requests assigned to the current authenticated person for SCR-REQ-004 (UC-COL-004).
    /// GET /api/v1/requests/my-assigned
    /// </summary>
    [HttpGet("my-assigned")]
    [ProducesResponseType(typeof(MyAssignedRequestsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListMyAssignedRequests([FromQuery] bool includeClosed = false, CancellationToken cancellationToken = default)
    {
        var personId = _currentContextProvider.CurrentPersonId;
        if (personId == null || personId == Guid.Empty)
        {
            return BadRequestProblem("Authenticated user is not linked to an organizational Person.");
        }

        var result = await _requestQueryService.ListMyAssignedRequestsAsync(personId.Value, includeClosed, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves chronological state transition audit history for SCR-REQ-003 and SCR-REQ-005 (FEAT-COL-003).
    /// GET /api/v1/requests/{id}/history
    /// </summary>
    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<RequestStateHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRequestHistory(Guid id, CancellationToken cancellationToken)
    {
        var existing = await _requestQueryService.GetRequestByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return RequestNotFound(id);
        }

        var history = await _requestQueryService.GetRequestStateHistoryAsync(id, cancellationToken);
        return Ok(history);
    }

    /// <summary>
    /// Assigns request ownership to an organizational person (UC-REQ-002).
    /// POST /api/v1/requests/{id}/assign
    /// </summary>
    [HttpPost("{id:guid}/assign")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignOwner(Guid id, [FromBody] AssignRequestOwnerApiRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.NewOwnerPersonId == Guid.Empty)
        {
            return BadRequestProblem("A valid NewOwnerPersonId is required.");
        }

        var assignedBy = request.AssignedByPersonId ?? _currentContextProvider.CurrentPersonId ?? Guid.Empty;
        if (assignedBy == Guid.Empty)
        {
            return BadRequestProblem("AssignedByPersonId is required or must be resolved from current identity.");
        }

        var result = await _requestService.AssignRequestOwnerAsync(
            requestId: id,
            newOwnerPersonId: request.NewOwnerPersonId,
            assignedByPersonId: assignedBy,
            note: request.Note,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Request '{RequestId}' assigned to owner '{OwnerId}' by '{AssignedBy}'.", id, request.NewOwnerPersonId, assignedBy);

        return Ok(result);
    }

    /// <summary>
    /// Starts request evaluation (CAPTURED -> EVALUATING) (UC-REQ-003).
    /// POST /api/v1/requests/{id}/evaluate
    /// </summary>
    [HttpPost("{id:guid}/evaluate")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EvaluateRequest(Guid id, [FromBody] EvaluateRequestApiRequest? request, CancellationToken cancellationToken)
    {
        var evaluatedBy = request?.EvaluatedByPersonId ?? _currentContextProvider.CurrentPersonId ?? Guid.Empty;
        if (evaluatedBy == Guid.Empty)
        {
            return BadRequestProblem("EvaluatedByPersonId is required or must be resolved from current identity.");
        }

        var result = await _requestService.EvaluateRequestAsync(
            requestId: id,
            evaluatedByPersonId: evaluatedBy,
            notes: request?.Notes,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Request '{RequestId}' evaluated by '{EvaluatedBy}'.", id, evaluatedBy);

        return Ok(result);
    }

    /// <summary>
    /// Accepts request responsibility (EVALUATING -> ACCEPTED) (UC-REQ-004).
    /// POST /api/v1/requests/{id}/accept
    /// </summary>
    [HttpPost("{id:guid}/accept")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptResponsibility(Guid id, [FromBody] AcceptRequestApiRequest? request, CancellationToken cancellationToken)
    {
        var acceptedBy = request?.AcceptedByPersonId ?? _currentContextProvider.CurrentPersonId ?? Guid.Empty;
        if (acceptedBy == Guid.Empty)
        {
            return BadRequestProblem("AcceptedByPersonId is required or must be resolved from current identity.");
        }

        var result = await _requestService.AcceptRequestResponsibilityAsync(
            requestId: id,
            acceptedByPersonId: acceptedBy,
            notes: request?.Notes,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Request '{RequestId}' accepted by '{AcceptedBy}'.", id, acceptedBy);

        return Ok(result);
    }

    /// <summary>
    /// Rejects request with justification (-> REJECTED) (UC-REQ-005).
    /// POST /api/v1/requests/{id}/reject
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectRequest(Guid id, [FromBody] RejectRequestApiRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequestProblem("A non-empty rejection Reason is required.");
        }

        var rejectedBy = request.RejectedByPersonId ?? _currentContextProvider.CurrentPersonId ?? Guid.Empty;
        if (rejectedBy == Guid.Empty)
        {
            return BadRequestProblem("RejectedByPersonId is required or must be resolved from current identity.");
        }

        var result = await _requestService.RejectRequestAsync(
            requestId: id,
            rejectedByPersonId: rejectedBy,
            reason: request.Reason,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Request '{RequestId}' rejected by '{RejectedBy}'. Reason: {Reason}", id, rejectedBy, request.Reason);

        return Ok(result);
    }

    /// <summary>
    /// Starts execution progress (ACCEPTED -> IN_PROGRESS).
    /// POST /api/v1/requests/{id}/start-progress
    /// </summary>
    [HttpPost("{id:guid}/start-progress")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartProgress(Guid id, [FromBody] StartRequestProgressApiRequest? request, CancellationToken cancellationToken)
    {
        var actor = request?.ActorPersonId ?? _currentContextProvider.CurrentPersonId ?? Guid.Empty;
        if (actor == Guid.Empty)
        {
            return BadRequestProblem("ActorPersonId is required or must be resolved from current identity.");
        }

        var result = await _requestService.StartRequestProgressAsync(
            requestId: id,
            actorPersonId: actor,
            notes: request?.Notes,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Request '{RequestId}' progress started by '{Actor}'.", id, actor);

        return Ok(result);
    }

    /// <summary>
    /// Escalates request to management attention (IN_PROGRESS -> ESCALATED) (UC-REQ-006).
    /// POST /api/v1/requests/{id}/escalate
    /// </summary>
    [HttpPost("{id:guid}/escalate")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EscalateRequest(Guid id, [FromBody] EscalateRequestApiRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequestProblem("A non-empty escalation Reason is required.");
        }

        var escalatedBy = request.EscalatedByPersonId ?? _currentContextProvider.CurrentPersonId ?? Guid.Empty;
        if (escalatedBy == Guid.Empty)
        {
            return BadRequestProblem("EscalatedByPersonId is required or must be resolved from current identity.");
        }

        var result = await _requestService.EscalateRequestAsync(
            requestId: id,
            escalatedByPersonId: escalatedBy,
            reason: request.Reason,
            requiredAssistance: request.RequiredAssistance,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Request '{RequestId}' escalated by '{EscalatedBy}'.", id, escalatedBy);

        return Ok(result);
    }

    /// <summary>
    /// Requests a management decision docket on an active request (UC-REQ-007).
    /// POST /api/v1/requests/{id}/management-decision
    /// </summary>
    [HttpPost("{id:guid}/management-decision")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestManagementDecision(Guid id, [FromBody] RequestManagementDecisionApiRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequestProblem("A non-empty decision Question is required.");
        }

        var requestedBy = request.RequestedByPersonId ?? _currentContextProvider.CurrentPersonId ?? Guid.Empty;
        if (requestedBy == Guid.Empty)
        {
            return BadRequestProblem("RequestedByPersonId is required or must be resolved from current identity.");
        }

        var result = await _requestService.RequestManagementDecisionAsync(
            requestId: id,
            requestedByPersonId: requestedBy,
            question: request.Question,
            options: request.Options,
            impact: request.Impact,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Management decision requested for Request '{RequestId}' by '{RequestedBy}'.", id, requestedBy);

        return Ok(result);
    }

    /// <summary>
    /// Completes the request upon review (IN_PROGRESS -> COMPLETED) (UC-REQ-008).
    /// POST /api/v1/requests/{id}/complete
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteRequest(Guid id, [FromBody] CompleteRequestApiRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Summary))
        {
            return BadRequestProblem("A non-empty resolution Summary is required.");
        }

        var reviewer = request.ReviewerPersonId ?? _currentContextProvider.CurrentPersonId ?? Guid.Empty;
        if (reviewer == Guid.Empty)
        {
            return BadRequestProblem("ReviewerPersonId is required or must be resolved from current identity.");
        }

        var result = await _requestService.ReviewRequestCompletionAsync(
            requestId: id,
            reviewerPersonId: reviewer,
            acceptResolution: true,
            summaryOrFeedback: request.Summary,
            outcome: string.IsNullOrWhiteSpace(request.Outcome) ? "RESOLVED" : request.Outcome,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Request '{RequestId}' completed by reviewer '{Reviewer}'.", id, reviewer);

        return Ok(result);
    }

    /// <summary>
    /// Requests rework during completion review (remains in IN_PROGRESS with feedback) (UC-REQ-008).
    /// POST /api/v1/requests/{id}/rework
    /// </summary>
    [HttpPost("{id:guid}/rework")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestRework(Guid id, [FromBody] ReworkRequestApiRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Feedback))
        {
            return BadRequestProblem("A non-empty rework Feedback is required.");
        }

        var reviewer = request.ReviewerPersonId ?? _currentContextProvider.CurrentPersonId ?? Guid.Empty;
        if (reviewer == Guid.Empty)
        {
            return BadRequestProblem("ReviewerPersonId is required or must be resolved from current identity.");
        }

        var result = await _requestService.ReviewRequestCompletionAsync(
            requestId: id,
            reviewerPersonId: reviewer,
            acceptResolution: false,
            summaryOrFeedback: request.Feedback,
            outcome: null,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Rework requested for Request '{RequestId}' by '{Reviewer}'.", id, reviewer);

        return Ok(result);
    }

    /// <summary>
    /// Reassigns request ownership to a different organizational Person (UC-MGT-001).
    /// POST /api/v1/requests/{id}/reassign
    /// </summary>
    [HttpPost("{id:guid}/reassign")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReassignOwner(Guid id, [FromBody] ReassignRequestApiRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.NewOwnerPersonId == Guid.Empty)
        {
            return BadRequestProblem("A valid NewOwnerPersonId is required.");
        }

        var reassignedBy = request.ReassignedByPersonId ?? _currentContextProvider.CurrentPersonId ?? Guid.Empty;
        if (reassignedBy == Guid.Empty)
        {
            return BadRequestProblem("ReassignedByPersonId is required or must be resolved from current identity.");
        }

        var result = await _requestService.ReassignRequestOwnershipAsync(
            requestId: id,
            newOwnerPersonId: request.NewOwnerPersonId,
            reassignedByPersonId: reassignedBy,
            reason: request.Reason,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Request '{RequestId}' ownership reassigned to '{NewOwnerId}' by '{ReassignedBy}'.", id, request.NewOwnerPersonId, reassignedBy);

        return Ok(result);
    }

    /// <summary>
    /// Resolves escalation and returns Request to IN_PROGRESS.
    /// POST /api/v1/requests/{id}/resolve-escalation
    /// </summary>
    [HttpPost("{id:guid}/resolve-escalation")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveEscalation(Guid id, [FromBody] ResolveEscalationApiRequest? request, CancellationToken cancellationToken)
    {
        var actor = request?.ActorPersonId ?? _currentContextProvider.CurrentPersonId ?? Guid.Empty;
        if (actor == Guid.Empty)
        {
            return BadRequestProblem("ActorPersonId is required or must be resolved from current identity.");
        }

        var result = await _requestService.ResolveEscalationAsync(
            requestId: id,
            actorPersonId: actor,
            notes: request?.Notes,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Request '{RequestId}' escalation resolved by '{Actor}'.", id, actor);

        return Ok(result);
    }

    /// <summary>
    /// Supplies active customers for request dropdown selectors.
    /// GET /api/v1/requests/customers
    /// GET /api/v1/requests/dropdowns/customers
    /// </summary>
    [HttpGet("customers")]
    [HttpGet("dropdowns/customers")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveCustomersDropdown(CancellationToken cancellationToken)
    {
        var customers = await _customerQueryService.ListActiveCustomersAsync(cancellationToken);
        return Ok(customers);
    }

    /// <summary>
    /// Supplies active products for request dropdown selectors.
    /// GET /api/v1/requests/products
    /// GET /api/v1/requests/dropdowns/products
    /// </summary>
    [HttpGet("products")]
    [HttpGet("dropdowns/products")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveProductsDropdown(CancellationToken cancellationToken)
    {
        var products = await _productQueryService.ListActiveProductsAsync(cancellationToken);
        return Ok(products);
    }

    /// <summary>
    /// Supplies active organization persons for owner and assignee dropdown selectors.
    /// GET /api/v1/requests/persons
    /// GET /api/v1/requests/dropdowns/persons
    /// </summary>
    [HttpGet("persons")]
    [HttpGet("dropdowns/persons")]
    [ProducesResponseType(typeof(IReadOnlyList<PersonDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivePersonsDropdown(CancellationToken cancellationToken)
    {
        var persons = await _organizationQueryService.ListActivePersonsAsync(cancellationToken);
        return Ok(persons);
    }

    private IActionResult RequestNotFound(Guid id)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            Title = "Request Not Found",
            Status = StatusCodes.Status404NotFound,
            Detail = $"Request with ID '{id}' was not found.",
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["errorCode"] = "REQUEST_NOT_FOUND";
        problem.Extensions["traceId"] = traceId;
        problem.Extensions["timestamp"] = DateTime.UtcNow;

        return NotFound(problem);
    }

    private IActionResult BadRequestProblem(string detail)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "Bad Request",
            Status = StatusCodes.Status400BadRequest,
            Detail = detail,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["errorCode"] = "BAD_REQUEST";
        problem.Extensions["traceId"] = traceId;
        problem.Extensions["timestamp"] = DateTime.UtcNow;

        return BadRequest(problem);
    }
}

/// <summary>
/// HTTP request model for creating / recording a request.
/// </summary>
public sealed record RecordRequestApiRequest(
    string Title,
    string Description,
    string Type,
    string? Priority = null,
    Guid? RequesterPersonId = null,
    Guid? RequesterContactId = null,
    string? RequesterName = null,
    Guid? CustomerId = null,
    Guid? ProductId = null,
    Guid? WorkPackageId = null,
    Guid? InitialOwnerPersonId = null,
    Guid? AssignedByPersonId = null,
    Guid? RequestId = null);

/// <summary>
/// HTTP request model for assigning an initial owner.
/// </summary>
public sealed record AssignRequestOwnerApiRequest(
    Guid NewOwnerPersonId,
    Guid? AssignedByPersonId = null,
    string? Note = null);

/// <summary>
/// HTTP request model for starting request evaluation.
/// </summary>
public sealed record EvaluateRequestApiRequest(
    Guid? EvaluatedByPersonId = null,
    string? Notes = null);

/// <summary>
/// HTTP request model for accepting request responsibility.
/// </summary>
public sealed record AcceptRequestApiRequest(
    Guid? AcceptedByPersonId = null,
    string? Notes = null);

/// <summary>
/// HTTP request model for rejecting a request.
/// </summary>
public sealed record RejectRequestApiRequest(
    string Reason,
    Guid? RejectedByPersonId = null);

/// <summary>
/// HTTP request model for starting progress on an accepted request.
/// </summary>
public sealed record StartRequestProgressApiRequest(
    Guid? ActorPersonId = null,
    string? Notes = null);

/// <summary>
/// HTTP request model for escalating a request.
/// </summary>
public sealed record EscalateRequestApiRequest(
    string Reason,
    string? RequiredAssistance = null,
    Guid? EscalatedByPersonId = null);

/// <summary>
/// HTTP request model for requesting a management decision.
/// </summary>
public sealed record RequestManagementDecisionApiRequest(
    string Question,
    string? Options = null,
    string? Impact = null,
    Guid? RequestedByPersonId = null);

/// <summary>
/// HTTP request model for completing a request upon review.
/// </summary>
public sealed record CompleteRequestApiRequest(
    string Summary,
    string? Outcome = "RESOLVED",
    Guid? ReviewerPersonId = null);

/// <summary>
/// HTTP request model for requesting rework during completion review.
/// </summary>
public sealed record ReworkRequestApiRequest(
    string Feedback,
    Guid? ReviewerPersonId = null);

/// <summary>
/// HTTP request model for reassigning ownership.
/// </summary>
public sealed record ReassignRequestApiRequest(
    Guid NewOwnerPersonId,
    string? Reason = null,
    Guid? ReassignedByPersonId = null);

/// <summary>
/// HTTP request model for resolving escalation.
/// </summary>
public sealed record ResolveEscalationApiRequest(
    Guid? ActorPersonId = null,
    string? Notes = null);
