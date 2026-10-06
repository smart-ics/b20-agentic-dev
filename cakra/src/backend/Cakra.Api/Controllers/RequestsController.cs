using System.Diagnostics;
using System.Security.Claims;
using Cakra.Core;
using Cakra.Modules.Request;
using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Domain.Exceptions;
using Cakra.Modules.Request.Services;
using FluentValidation;
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
    private readonly ICurrentContextProvider? _currentContextProvider;

    public RequestsController(
        IMediator mediator,
        IRequestQueryService requestQueryService,
        ILogger<RequestsController> logger,
        ICurrentContextProvider? currentContextProvider = null)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _requestQueryService = requestQueryService ?? throw new ArgumentNullException(nameof(requestQueryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _currentContextProvider = currentContextProvider;
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
    /// Retrieves active requests containing unfinished sub-tasks assigned to the specified or current authenticated person
    /// (CR-006 Architecture TD-009).
    /// </summary>
    [HttpGet("assigned-subtasks")]
    [ProducesResponseType(typeof(IReadOnlyList<RequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<RequestDto>>> GetRequestsWithAssignedSubTasks(
        [FromQuery] Guid? personId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedPersonId = FirstNonEmptyGuid(personId) ?? _currentContextProvider?.CurrentPersonId;
        if (!resolvedPersonId.HasValue || resolvedPersonId.Value == Guid.Empty)
        {
            return Ok(Array.Empty<RequestDto>());
        }

        var items = await _mediator.Send(new GetRequestsWithAssignedSubTasksQuery(resolvedPersonId.Value), cancellationToken);
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
    /// Updates the core attributes (Title, Description, RequestType, Priority) of an active request
    /// (<c>RequestService.UpdateRequestCoreAttributes</c>; CR-018 Architecture TD-001, TD-004).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRequestCoreAttributes(
        [FromRoute] Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] UpdateRequestCoreAttributesBody? request = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return CreateBadRequestProblem("Request ID is required.");
        }

        var actorPersonId = FirstNonEmptyGuid(request?.ActorPersonId)
            ?? _currentContextProvider?.CurrentPersonId
            ?? ResolvePersonIdFromUserClaims(HttpContext?.User);

        var command = new UpdateRequestCoreAttributesCommand(
            RequestId: id,
            Title: request?.Title ?? string.Empty,
            Description: request?.Description ?? string.Empty,
            Priority: request?.ResolvedPriority ?? request?.Priority ?? string.Empty,
            RequestType: request?.ResolvedRequestType ?? request?.RequestType ?? string.Empty,
            ActorPersonId: actorPersonId);

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Updated core attributes for request '{RequestId}' ({Title}).",
                enriched.Id,
                enriched.Title);

            return Ok(enriched);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateForbiddenProblem(ex.Message);
        }
        catch (RequestNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
        }
        catch (RequestDomainValidationException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
        catch (ValidationException ex)
        {
            return CreateValidationProblem(ex);
        }
        catch (InvalidRequestStateTransitionException ex)
        {
            return CreateBadRequestProblem(ex.Message);
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
            Complexity: request?.Complexity,
            InitialSubTasks: request?.ResolvedInitialSubTasks);

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
        catch (KeyNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
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
    /// Starts active work on a request, transitioning <c>ASSIGNED</c> or <c>PAUSED</c> to <c>IN_PROGRESS</c>
    /// (<c>RequestService.StartWork</c>; CR-016 Architecture TD-002). Strictly executable by the assigned owner.
    /// </summary>
    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> StartWork(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] StartRequestBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new StartWorkCommand(
            RequestId: id,
            Notes: request?.Notes,
            ActorPersonId: FirstNonEmptyGuid(request?.ActorPersonId));

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Started work on request '{RequestId}', transitioning to '{Status}'.",
                enriched.Id,
                enriched.Status);

            return Ok(enriched);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateForbiddenProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
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
    /// Suspends active work on a request, transitioning <c>IN_PROGRESS</c> to <c>PAUSED</c>
    /// (<c>RequestService.PauseWork</c>; CR-016 Architecture TD-002).
    /// </summary>
    [HttpPost("{id:guid}/pause")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PauseWork(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PauseRequestBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new PauseWorkCommand(
            RequestId: id,
            Note: request?.ResolvedNote,
            ActorPersonId: FirstNonEmptyGuid(request?.ActorPersonId));

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Paused work on request '{RequestId}', transitioning to '{Status}'.",
                enriched.Id,
                enriched.Status);

            return Ok(enriched);
        }
        catch (KeyNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
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
    /// Cancels a request in any active state, transitioning it to terminal <c>CANCELLED</c> state
    /// (<c>RequestService.CancelRequest</c>; CR-016 Architecture TD-002).
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CancelRequest(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelRequestBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new CancelRequestCommand(
            RequestId: id,
            Reason: request?.ResolvedReason ?? string.Empty,
            ActorPersonId: FirstNonEmptyGuid(request?.ActorPersonId));

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Cancelled request '{RequestId}', transitioning to '{Status}'.",
                enriched.Id,
                enriched.Status);

            return Ok(enriched);
        }
        catch (KeyNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
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
    /// Reviews and completes work on a request in <c>IN_PROGRESS</c> state, transitioning it to <c>COMPLETED</c>
    /// (<c>RequestService.ReviewRequestCompletion</c>; Architecture §7, §8 — <c>UC-REQ-008</c>, <c>SCR-REQ-003</c>).
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
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
        catch (UnauthorizedAccessException ex)
        {
            return CreateForbiddenProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
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

    /// <summary>
    /// Adds a sub-task checklist item to an active request
    /// (<c>RequestService.AddRequestSubTask</c>; CR-006 Architecture §4 TD-001, TD-003, TD-006).
    /// </summary>
    [HttpPost("{id:guid}/subtasks")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddSubTask(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AddSubTaskBody? request = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return CreateBadRequestProblem("Request ID is required.");
        }

        var command = new AddRequestSubTaskCommand(
            RequestId: id,
            Title: request?.Title ?? string.Empty,
            AssigneePersonId: request?.ResolvedAssigneePersonId,
            ActorPersonId: FirstNonEmptyGuid(request?.ActorPersonId));

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Added sub-task '{Title}' to request '{RequestId}'. Sub-tasks count: {Count}, Progress: {Progress}%.",
                request?.Title,
                id,
                enriched.TotalSubTasksCount,
                enriched.CompletionPercentage);

            return Ok(enriched);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateForbiddenProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
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
    /// Marks a sub-task checklist item completed on a request
    /// (<c>RequestService.CompleteRequestSubTask</c>; CR-006 Architecture §4 TD-001, TD-003, TD-006).
    /// </summary>
    [HttpPost("{id:guid}/subtasks/{subTaskId:guid}/complete")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteSubTask(
        Guid id,
        Guid subTaskId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SubTaskActionBody? request = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return CreateBadRequestProblem("Request ID is required.");
        }

        if (subTaskId == Guid.Empty)
        {
            return CreateBadRequestProblem("Sub-task ID is required.");
        }

        var command = new CompleteRequestSubTaskCommand(
            RequestId: id,
            SubTaskId: subTaskId,
            ActorPersonId: FirstNonEmptyGuid(request?.ActorPersonId));

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Completed sub-task '{SubTaskId}' on request '{RequestId}'. Progress: {Progress}%.",
                subTaskId,
                id,
                enriched.CompletionPercentage);

            return Ok(enriched);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateForbiddenProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
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
    /// Reopens a completed sub-task checklist item back to pending on a request
    /// (<c>RequestService.ReopenRequestSubTask</c>; CR-006 Architecture §4 TD-001, TD-003, TD-006).
    /// </summary>
    [HttpPost("{id:guid}/subtasks/{subTaskId:guid}/reopen")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReopenSubTask(
        Guid id,
        Guid subTaskId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SubTaskActionBody? request = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return CreateBadRequestProblem("Request ID is required.");
        }

        if (subTaskId == Guid.Empty)
        {
            return CreateBadRequestProblem("Sub-task ID is required.");
        }

        var command = new ReopenRequestSubTaskCommand(
            RequestId: id,
            SubTaskId: subTaskId,
            ActorPersonId: FirstNonEmptyGuid(request?.ActorPersonId));

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Reopened sub-task '{SubTaskId}' on request '{RequestId}'. Progress: {Progress}%.",
                subTaskId,
                id,
                enriched.CompletionPercentage);

            return Ok(enriched);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateForbiddenProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
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
    /// Removes a sub-task checklist item from a request
    /// (<c>RequestService.RemoveRequestSubTask</c>; CR-006 Architecture §4 TD-001, TD-006).
    /// </summary>
    [HttpDelete("{id:guid}/subtasks/{subTaskId:guid}")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveSubTask(
        Guid id,
        Guid subTaskId,
        [FromQuery] Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return CreateBadRequestProblem("Request ID is required.");
        }

        if (subTaskId == Guid.Empty)
        {
            return CreateBadRequestProblem("Sub-task ID is required.");
        }

        var command = new RemoveRequestSubTaskCommand(
            RequestId: id,
            SubTaskId: subTaskId,
            ActorPersonId: FirstNonEmptyGuid(actorPersonId));

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _requestQueryService.GetRequestByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Removed sub-task '{SubTaskId}' from request '{RequestId}'. Sub-tasks count: {Count}, Progress: {Progress}%.",
                subTaskId,
                id,
                enriched.TotalSubTasksCount,
                enriched.CompletionPercentage);

            return Ok(enriched);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateForbiddenProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
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

    private ObjectResult CreateNotFoundProblem(string detail)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not Found",
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4"
        };
        problem.Extensions["errorCode"] = "RESOURCE_NOT_FOUND";
        problem.Extensions["traceId"] = traceId;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status404NotFound,
            ContentTypes = { "application/problem+json" }
        };
    }

    private ObjectResult CreateValidationProblem(ValidationException ex)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var errors = ex.Errors
            .GroupBy(e => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
            );

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Error",
            Detail = ex.Message,
            Instance = HttpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        };
        problem.Extensions["errorCode"] = "VALIDATION_FAILED";
        problem.Extensions["traceId"] = traceId;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static Guid? ResolvePersonIdFromUserClaims(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var personIdClaim = user.FindFirst("personId")?.Value
            ?? user.FindFirst("person_id")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        return Guid.TryParse(personIdClaim, out var parsed) && parsed != Guid.Empty
            ? parsed
            : null;
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
        public IReadOnlyList<InitialSubTaskInput>? InitialSubTasks { get; set; }
        public IReadOnlyList<InitialSubTaskInput>? SubTasks { get; set; }

        public IReadOnlyList<InitialSubTaskDto>? ResolvedInitialSubTasks =>
            (InitialSubTasks ?? SubTasks)?
                .Where(s => s is not null)
                .Select(s => new InitialSubTaskDto(
                    s.Title ?? string.Empty,
                    FirstNonEmptyGuid(s.AssigneePersonId, s.AssigneeId)))
                .ToList();

        public string ResolvedRequestType =>
            FirstNonWhiteSpace(RequestType, Type) ?? "GENERAL";

        public string ResolvedPriority =>
            FirstNonWhiteSpace(Priority) ?? "NORMAL";
    }

    /// <summary>
    /// Sub-task input item used during request recording intake.
    /// </summary>
    public sealed class InitialSubTaskInput
    {
        public string? Title { get; set; }
        public Guid? AssigneePersonId { get; set; }
        public Guid? AssigneeId { get; set; }
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
    /// Request payload for <c>POST /api/v1/requests/{id}/start</c> (<c>StartWork</c>).
    /// </summary>
    public sealed class StartRequestBody
    {
        public string? Notes { get; set; }
        public Guid? ActorPersonId { get; set; }
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/requests/{id}/pause</c> (<c>PauseWork</c>).
    /// </summary>
    public sealed class PauseRequestBody
    {
        public string? Note { get; set; }
        public string? Notes { get; set; }
        public Guid? ActorPersonId { get; set; }

        public string? ResolvedNote => FirstNonWhiteSpace(Note, Notes);
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
    /// Request payload for <c>POST /api/v1/requests/{id}/cancel</c> (<c>CancelRequest</c>).
    /// </summary>
    public sealed class CancelRequestBody
    {
        public string? Reason { get; set; }
        public string? CancellationReason { get; set; }
        public string? Notes { get; set; }
        public Guid? ActorPersonId { get; set; }

        public string ResolvedReason =>
            FirstNonWhiteSpace(Reason, CancellationReason, Notes) ?? string.Empty;
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

    /// <summary>
    /// Request payload for <c>POST /api/v1/requests/{id}/subtasks</c> (<c>AddSubTask</c>).
    /// </summary>
    public sealed class AddSubTaskBody
    {
        public string? Title { get; set; }
        public Guid? AssigneePersonId { get; set; }
        public Guid? AssigneeId { get; set; }
        public Guid? ActorPersonId { get; set; }

        public Guid? ResolvedAssigneePersonId =>
            FirstNonEmptyGuid(AssigneePersonId, AssigneeId);
    }

    /// <summary>
    /// Optional request payload for sub-task actions (<c>CompleteSubTask</c>, <c>ReopenSubTask</c>).
    /// </summary>
    public sealed class SubTaskActionBody
    {
        public Guid? ActorPersonId { get; set; }
    }

    /// <summary>
    /// Request payload for <c>PUT /api/v1/requests/{id}</c> (<c>UpdateRequestCoreAttributes</c>; CR-018 Architecture TD-001).
    /// </summary>
    public sealed class UpdateRequestCoreAttributesBody
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? RequestType { get; set; }
        public string? Type { get; set; }
        public string? Priority { get; set; }
        public Guid? ActorPersonId { get; set; }

        public string? ResolvedRequestType =>
            FirstNonWhiteSpace(RequestType, Type);

        public string? ResolvedPriority =>
            FirstNonWhiteSpace(Priority);
    }
}

