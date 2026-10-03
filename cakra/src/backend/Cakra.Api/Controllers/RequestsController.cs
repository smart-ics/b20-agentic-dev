using System.Diagnostics;
using Cakra.Modules.Request;
using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;

namespace Cakra.Api.Controllers;

/// <summary>
/// REST API controller for the Request module (<c>SCR-REQ-001..005</c>, <c>UC-REQ-001..008</c>,
/// <c>UC-COL-002..004</c>, <c>UC-MGT-001</c>; Architecture §7, §8, §9, §18, §19.5, §19.6).
/// Exposes <c>/api/v1/requests/*</c> endpoints protected by <see cref="AuthorizeAttribute"/>,
/// wiring all <see cref="RequestService"/> MediatR command handlers and <see cref="IRequestQueryService"/>
/// query methods to HTTP endpoints.
/// </summary>
[Authorize]
[Route("api/v1/requests")]
public sealed class RequestsController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly IRequestQueryService _requestQueryService;
    private readonly ILogger<RequestsController> _logger;

    public RequestsController(
        IMediator mediator,
        IRequestQueryService requestQueryService,
        ILogger<RequestsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _requestQueryService = requestQueryService ?? throw new ArgumentNullException(nameof(requestQueryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves a filtered and paginated request grid (<c>RequestQueryService.GetFilteredRequestGrid</c>;
    /// Architecture §7, §8 — <c>UC-COL-002..004</c>, <c>SCR-REQ-001</c>, <c>SCR-REQ-005</c>).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedRequestGridResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedRequestGridResult>> GetFilteredRequestGrid(
        [FromQuery] string? status = null,
        [FromQuery] Guid? assigneeId = null,
        [FromQuery] Guid? assigneePersonId = null,
        [FromQuery] Guid? ownerPersonId = null,
        [FromQuery] Guid? assignee = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] Guid? customer = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] Guid? product = null,
        [FromQuery] Guid? workPackageId = null,
        [FromQuery] Guid? workPackage = null,
        [FromQuery] string? search = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? offset = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedAssigneeId = FirstNonEmptyGuid(assigneeId, assigneePersonId, ownerPersonId, assignee);
        var resolvedCustomerId = FirstNonEmptyGuid(customerId, customer);
        var resolvedProductId = FirstNonEmptyGuid(productId, product);
        var resolvedWorkPackageId = FirstNonEmptyGuid(workPackageId, workPackage);
        var resolvedSearchTerm = FirstNonWhiteSpace(searchTerm, search, q);

        var query = new GetFilteredRequestGridQuery(
            Status: status,
            AssigneePersonId: resolvedAssigneeId,
            CustomerId: resolvedCustomerId,
            ProductId: resolvedProductId,
            Page: page,
            PageSize: pageSize,
            Offset: offset,
            WorkPackageId: resolvedWorkPackageId,
            SearchTerm: resolvedSearchTerm);

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all requests assigned to the current authenticated user
    /// (<c>RequestQueryService.ListMyAssignedRequests</c>; Architecture §7, §8 — <c>UC-COL-004</c>, <c>SCR-REQ-004</c>).
    /// </summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(IReadOnlyList<RequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<RequestDto>>> ListMyAssignedRequests(
        [FromQuery] Guid? personId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedPersonId = FirstNonEmptyGuid(personId);
        var items = await _mediator.Send(new ListMyAssignedRequestsQuery(resolvedPersonId), cancellationToken);
        return Ok(items);
    }

    /// <summary>
    /// Retrieves a single request's details by unique identifier
    /// (<c>RequestQueryService.GetRequestById</c>; Architecture §7, §8 — <c>SCR-REQ-003</c>).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RequestDto>> GetRequestById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var request = await _mediator.Send(new GetRequestByIdQuery(id), cancellationToken)
            ?? throw new KeyNotFoundException($"Request '{id}' was not found.");

        return Ok(request);
    }

    /// <summary>
    /// Retrieves the chronological state transition and ownership audit history for a request
    /// (<c>RequestQueryService.GetRequestStateHistory</c>; Architecture §7, §8, §18 — <c>UC-COL-002</c>, <c>UC-COL-003</c>, <c>SCR-REQ-003</c>, <c>SCR-REQ-005</c>).
    /// </summary>
    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<RequestAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<RequestAssignmentDto>>> GetRequestStateHistory(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var history = await _mediator.Send(new GetRequestStateHistoryQuery(id), cancellationToken);
        if (history.Count == 0 && !await _requestQueryService.RequestExistsAsync(id, cancellationToken))
        {
            throw new KeyNotFoundException($"Request '{id}' was not found.");
        }

        return Ok(history);
    }

    /// <summary>
    /// Records a new operational request in <c>CAPTURED</c> state
    /// (<c>RequestService.RecordRequest</c>; Architecture §7, §8 — <c>UC-REQ-001</c>, <c>SCR-REQ-002</c>).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RecordRequest(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RecordRequestBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new RecordRequestCommand(
            Title: request?.Title ?? string.Empty,
            Description: request?.Description ?? string.Empty,
            CustomerId: FirstNonEmptyGuid(request?.CustomerId),
            ProductId: FirstNonEmptyGuid(request?.ProductId),
            RequestType: request?.ResolvedRequestType ?? "GENERAL",
            Priority: request?.ResolvedPriority ?? "NORMAL",
            ActorPersonId: FirstNonEmptyGuid(request?.ActorPersonId),
            WorkPackageId: FirstNonEmptyGuid(request?.WorkPackageId),
            Complexity: request?.Complexity);

        try
        {
            var created = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(created.Id, cancellationToken) ?? created;

            _logger.LogInformation(
                "Recorded request '{RequestId}' ({Title}) in status '{Status}' with complexity {Complexity}.",
                enriched.Id,
                enriched.Title,
                enriched.Status,
                enriched.Complexity);

            return CreatedAtAction(nameof(GetRequestById), new { id = enriched.Id }, enriched);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateForbiddenProblem(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Assigns an organizational owner to a request, transitioning <c>CAPTURED -&gt; EVALUATING</c>
    /// (<c>RequestService.AssignRequestOwner</c>; Architecture §7, §8 — <c>UC-REQ-002</c>, <c>SCR-REQ-003</c>).
    /// </summary>
    [HttpPost("{id:guid}/assign")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AssignRequestOwner(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AssignRequestOwnerBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new AssignRequestOwnerCommand(
            RequestId: id,
            OwnerPersonId: request?.ResolvedOwnerPersonId ?? Guid.Empty,
            Notes: request?.Notes,
            ActorPersonId: FirstNonEmptyGuid(request?.ActorPersonId));

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Assigned request '{RequestId}' to owner '{OwnerPersonId}', transitioning to '{Status}'.",
                enriched.Id,
                enriched.OwnerPersonId,
                enriched.Status);

            return Ok(enriched);
        }
        catch (InvalidOperationException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Records triage evaluation notes on a request in <c>EVALUATING</c> state
    /// (<c>RequestService.EvaluateRequest</c>; Architecture §7, §8 — <c>UC-REQ-003</c>, <c>SCR-REQ-003</c>).
    /// </summary>
    [HttpPost("{id:guid}/evaluate")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> EvaluateRequest(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] EvaluateRequestBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new EvaluateRequestCommand(
            RequestId: id,
            EvaluationNotes: request?.ResolvedEvaluationNotes ?? string.Empty,
            ActorPersonId: FirstNonEmptyGuid(request?.ActorPersonId),
            Complexity: request?.Complexity);

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Evaluated request '{RequestId}' in status '{Status}'.",
                enriched.Id,
                enriched.Status);

            return Ok(enriched);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateForbiddenProblem(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Updates the complexity rating (1 to 5) of an operational request
    /// (<c>RequestService.UpdateRequestComplexity</c>; Architecture §4 TD-004, TD-005).
    /// </summary>
    [HttpPatch("{id:guid}/complexity")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRequestComplexity(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] UpdateRequestComplexityBody? request = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return CreateBadRequestProblem("Request ID is required.");
        }

        if (request is null)
        {
            return CreateBadRequestProblem("Request body cannot be null.");
        }

        if (request.Complexity < 1 || request.Complexity > 5)
        {
            return CreateBadRequestProblem("Complexity must be an integer between 1 and 5.");
        }

        var command = new UpdateRequestComplexityCommand(
            RequestId: id,
            Complexity: request.Complexity,
            Reason: request.Reason,
            ActorPersonId: FirstNonEmptyGuid(request.ActorPersonId));

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Updated complexity for request '{RequestId}' to {Complexity}.",
                enriched.Id,
                enriched.Complexity);

            return Ok(enriched);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateForbiddenProblem(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Accepts operational responsibility for a request, transitioning it to <c>IN_PROGRESS</c>
    /// (<c>RequestService.AcceptRequestResponsibility</c>; Architecture §7, §8 — <c>UC-REQ-004</c>, <c>SCR-REQ-003</c>).
    /// </summary>
    [HttpPost("{id:guid}/accept")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AcceptRequestResponsibility(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AcceptRequestBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new AcceptRequestResponsibilityCommand(
            RequestId: id,
            Notes: request?.Notes,
            ActorPersonId: FirstNonEmptyGuid(request?.ActorPersonId));

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Accepted responsibility for request '{RequestId}', transitioning to '{Status}'.",
                enriched.Id,
                enriched.Status);

            return Ok(enriched);
        }
        catch (InvalidOperationException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Rejects a request during evaluation, transitioning <c>EVALUATING -&gt; REJECTED</c>
    /// (<c>RequestService.RejectRequest</c>; Architecture §7, §8 — <c>UC-REQ-005</c>, <c>SCR-REQ-003</c>).
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RejectRequest(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RejectRequestBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new RejectRequestCommand(
            RequestId: id,
            Reason: request?.ResolvedReason ?? string.Empty,
            ActorPersonId: FirstNonEmptyGuid(request?.ActorPersonId));

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Rejected request '{RequestId}', transitioning to '{Status}'.",
                enriched.Id,
                enriched.Status);

            return Ok(enriched);
        }
        catch (InvalidOperationException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Escalates a request in <c>EVALUATING</c> or <c>IN_PROGRESS</c> state to <c>ESCALATED</c>
    /// (<c>RequestService.EscalateRequest</c>; Architecture §7, §8 — <c>UC-REQ-006</c>, <c>SCR-REQ-003</c>).
    /// </summary>
    [HttpPost("{id:guid}/escalate")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EscalateRequest(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] EscalateRequestBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new EscalateRequestCommand(
            RequestId: id,
            Reason: request?.ResolvedReason ?? string.Empty,
            ActorPersonId: FirstNonEmptyGuid(request?.ActorPersonId));

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Escalated request '{RequestId}', transitioning to '{Status}'.",
                enriched.Id,
                enriched.Status);

            return Ok(enriched);
        }
        catch (InvalidOperationException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Records a management decision request or determination on an active or escalated request
    /// (<c>RequestService.RequestManagementDecision</c>; Architecture §7, §8 — <c>UC-REQ-007</c>, <c>SCR-REQ-003</c>).
    /// </summary>
    [HttpPost("{id:guid}/management-decision")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RequestManagementDecision(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RequestManagementDecisionBody? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetStatus = RequestStatusNames.FromNullableName(request?.TargetStatus);
            var command = new RequestManagementDecisionCommand(
                RequestId: id,
                DecisionDetails: request?.ResolvedDecisionDetails ?? string.Empty,
                ActorPersonId: FirstNonEmptyGuid(request?.ActorPersonId),
                TargetStatus: targetStatus);

            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Recorded management decision on request '{RequestId}' in status '{Status}'.",
                enriched.Id,
                enriched.Status);

            return Ok(enriched);
        }
        catch (InvalidOperationException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Reviews and completes work on a request in <c>IN_PROGRESS</c> state, transitioning it to <c>COMPLETED</c>
    /// (<c>RequestService.ReviewRequestCompletion</c>; Architecture §7, §8 — <c>UC-REQ-008</c>, <c>SCR-REQ-003</c>).
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReviewRequestCompletion(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CompleteRequestBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new ReviewRequestCompletionCommand(
            RequestId: id,
            ResolutionDescription: request?.ResolvedResolutionDescription ?? string.Empty,
            ActorPersonId: FirstNonEmptyGuid(request?.ActorPersonId));

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Completed request '{RequestId}', transitioning to '{Status}'.",
                enriched.Id,
                enriched.Status);

            return Ok(enriched);
        }
        catch (InvalidOperationException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Reassigns request ownership to a new active person in Organization
    /// (<c>RequestService.ReassignRequestOwnership</c>; Architecture §7, §8 — <c>UC-MGT-001</c>, <c>SCR-REQ-003</c>).
    /// </summary>
    [HttpPost("{id:guid}/reassign")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReassignRequestOwnership(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReassignRequestOwnershipBody? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetStatus = RequestStatusNames.FromNullableName(request?.ResolvedTargetStatus);
            var command = new ReassignRequestOwnershipCommand(
                RequestId: id,
                NewOwnerPersonId: request?.ResolvedNewOwnerPersonId ?? Guid.Empty,
                Notes: request?.ResolvedNotes,
                ActorPersonId: FirstNonEmptyGuid(request?.ActorPersonId),
                TargetStatusForEscalated: targetStatus);

            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Reassigned request '{RequestId}' to new owner '{OwnerPersonId}' in status '{Status}'.",
                enriched.Id,
                enriched.OwnerPersonId,
                enriched.Status);

            return Ok(enriched);
        }
        catch (InvalidOperationException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    private ObjectResult CreateBadRequestProblem(string detail)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        };
        problem.Extensions["errorCode"] = "BAD_REQUEST";
        problem.Extensions["traceId"] = traceId;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    }

    private ObjectResult CreateForbiddenProblem(string detail)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3"
        };
        problem.Extensions["errorCode"] = "FORBIDDEN";
        problem.Extensions["traceId"] = traceId;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status403Forbidden,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static Guid? FirstNonEmptyGuid(params Guid?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.HasValue && candidate.Value != Guid.Empty)
            {
                return candidate.Value;
            }
        }

        return null;
    }

    private static string? FirstNonWhiteSpace(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/requests</c> (<c>RecordRequest</c>).
    /// </summary>
    public sealed class RecordRequestBody
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? ProductId { get; set; }
        public string? RequestType { get; set; }
        public string? Type { get; set; }
        public string? Priority { get; set; }
        public Guid? WorkPackageId { get; set; }
        public Guid? ActorPersonId { get; set; }
        public int? Complexity { get; set; }

        public string ResolvedRequestType =>
            FirstNonWhiteSpace(RequestType, Type) ?? "GENERAL";

        public string ResolvedPriority =>
            FirstNonWhiteSpace(Priority) ?? "NORMAL";
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/requests/{id}/assign</c> (<c>AssignRequestOwner</c>).
    /// </summary>
    public sealed class AssignRequestOwnerBody
    {
        public Guid? OwnerPersonId { get; set; }
        public Guid? AssigneePersonId { get; set; }
        public Guid? AssigneeId { get; set; }
        public Guid? PersonId { get; set; }
        public string? Notes { get; set; }
        public Guid? ActorPersonId { get; set; }

        public Guid ResolvedOwnerPersonId =>
            FirstNonEmptyGuid(OwnerPersonId, AssigneePersonId, AssigneeId, PersonId) ?? Guid.Empty;
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/requests/{id}/evaluate</c> (<c>EvaluateRequest</c>).
    /// </summary>
    public sealed class EvaluateRequestBody
    {
        public string? EvaluationNotes { get; set; }
        public string? Notes { get; set; }
        public string? Evaluation { get; set; }
        public Guid? ActorPersonId { get; set; }
        public int? Complexity { get; set; }

        public string ResolvedEvaluationNotes =>
            FirstNonWhiteSpace(EvaluationNotes, Notes, Evaluation) ?? string.Empty;
    }

    /// <summary>
    /// Request payload for <c>PATCH /api/v1/requests/{id}/complexity</c> (<c>UpdateRequestComplexity</c>).
    /// </summary>
    public sealed class UpdateRequestComplexityBody
    {
        public int Complexity { get; set; }
        public string? Reason { get; set; }
        public Guid? ActorPersonId { get; set; }
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/requests/{id}/accept</c> (<c>AcceptRequestResponsibility</c>).
    /// </summary>
    public sealed class AcceptRequestBody
    {
        public string? Notes { get; set; }
        public Guid? ActorPersonId { get; set; }
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/requests/{id}/reject</c> (<c>RejectRequest</c>).
    /// </summary>
    public sealed class RejectRequestBody
    {
        public string? Reason { get; set; }
        public string? RejectionReason { get; set; }
        public string? Notes { get; set; }
        public Guid? ActorPersonId { get; set; }

        public string ResolvedReason =>
            FirstNonWhiteSpace(Reason, RejectionReason, Notes) ?? string.Empty;
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/requests/{id}/escalate</c> (<c>EscalateRequest</c>).
    /// </summary>
    public sealed class EscalateRequestBody
    {
        public string? Reason { get; set; }
        public string? EscalationReason { get; set; }
        public string? Notes { get; set; }
        public Guid? ActorPersonId { get; set; }

        public string ResolvedReason =>
            FirstNonWhiteSpace(Reason, EscalationReason, Notes) ?? string.Empty;
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/requests/{id}/management-decision</c> (<c>RequestManagementDecision</c>).
    /// </summary>
    public sealed class RequestManagementDecisionBody
    {
        public string? DecisionDetails { get; set; }
        public string? DecisionNotes { get; set; }
        public string? Decision { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public string? TargetStatus { get; set; }
        public Guid? ActorPersonId { get; set; }

        public string ResolvedDecisionDetails =>
            FirstNonWhiteSpace(DecisionDetails, DecisionNotes, Decision, Reason, Notes) ?? string.Empty;
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/requests/{id}/complete</c> (<c>ReviewRequestCompletion</c>).
    /// </summary>
    public sealed class CompleteRequestBody
    {
        public string? ResolutionDescription { get; set; }
        public string? CompletionDetails { get; set; }
        public string? Summary { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public Guid? ActorPersonId { get; set; }

        public string ResolvedResolutionDescription =>
            FirstNonWhiteSpace(ResolutionDescription, CompletionDetails, Summary, Description, Notes) ?? string.Empty;
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/requests/{id}/reassign</c> (<c>ReassignRequestOwnership</c>).
    /// </summary>
    public sealed class ReassignRequestOwnershipBody
    {
        public Guid? NewOwnerPersonId { get; set; }
        public Guid? OwnerPersonId { get; set; }
        public Guid? AssigneePersonId { get; set; }
        public Guid? AssigneeId { get; set; }
        public string? Notes { get; set; }
        public string? Reason { get; set; }
        public string? TargetStatusForEscalated { get; set; }
        public string? TargetStatus { get; set; }
        public Guid? ActorPersonId { get; set; }

        public Guid ResolvedNewOwnerPersonId =>
            FirstNonEmptyGuid(NewOwnerPersonId, OwnerPersonId, AssigneePersonId, AssigneeId) ?? Guid.Empty;

        public string? ResolvedNotes =>
            FirstNonWhiteSpace(Notes, Reason);

        public string? ResolvedTargetStatus =>
            FirstNonWhiteSpace(TargetStatusForEscalated, TargetStatus);
    }
}
