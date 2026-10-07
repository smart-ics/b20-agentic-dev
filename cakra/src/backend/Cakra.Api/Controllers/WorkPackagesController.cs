using System.Diagnostics;
using Cakra.Modules.WorkPackage;
using Cakra.Modules.WorkPackage.Domain.Exceptions;
using Cakra.Modules.WorkPackage.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;

namespace Cakra.Api.Controllers;

/// <summary>
/// REST API controller for the Work Package module (<c>SCR-WP-001</c>, <c>UC-WP-001..003</c>,
/// <c>FEAT-WP-001</c>; Architecture §7, §8, §9, §11, §18, §19.5, §19.6).
/// Exposes <c>/api/v1/work-packages/*</c> endpoints protected by <see cref="AuthorizeAttribute"/>,
/// wiring all <see cref="WorkPackageService"/> MediatR command handlers and
/// <see cref="IWorkPackageQueryService"/> query methods to HTTP endpoints.
/// </summary>
[Authorize]
[Route("api/v1/work-packages")]
public sealed class WorkPackagesController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWorkPackageQueryService _workPackageQueryService;
    private readonly ILogger<WorkPackagesController> _logger;

    public WorkPackagesController(
        IMediator mediator,
        IWorkPackageQueryService workPackageQueryService,
        ILogger<WorkPackagesController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _workPackageQueryService = workPackageQueryService ?? throw new ArgumentNullException(nameof(workPackageQueryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves a filtered list of work packages (<c>WorkPackageQueryService.ListWorkPackages</c>;
    /// Architecture §7, §8, §11 — <c>UC-WP-003</c>, <c>SCR-WP-001</c>).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WorkPackageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<WorkPackageDto>>> ListWorkPackages(
        [FromQuery] string? status = null,
        [FromQuery] string? statusFilter = null,
        [FromQuery] Guid? ownerPersonId = null,
        [FromQuery] Guid? ownerId = null,
        [FromQuery] Guid? owner = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] Guid? customer = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] Guid? product = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedStatus = FirstNonWhiteSpace(status, statusFilter);
        var resolvedOwnerId = FirstNonEmptyGuid(ownerPersonId, ownerId, owner);
        var resolvedCustomerId = FirstNonEmptyGuid(customerId, customer);
        var resolvedProductId = FirstNonEmptyGuid(productId, product);

        var query = new ListWorkPackagesQuery(
            Status: resolvedStatus,
            OwnerPersonId: resolvedOwnerId,
            CustomerId: resolvedCustomerId,
            ProductId: resolvedProductId);

        var items = await _mediator.Send(query, cancellationToken);
        return Ok(items);
    }

    /// <summary>
    /// Retrieves a single work package's details by unique identifier
    /// (<c>WorkPackageQueryService.GetWorkPackageById</c>; Architecture §7, §8, §11 — <c>UC-WP-003</c>, <c>SCR-WP-001</c>).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WorkPackageDto>> GetWorkPackageById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var workPackage = await _mediator.Send(new GetWorkPackageByIdQuery(id), cancellationToken)
            ?? throw new KeyNotFoundException($"WorkPackage '{id}' was not found.");

        return Ok(workPackage);
    }

    /// <summary>
    /// Retrieves current and historical requests included in the work package scope, enriched with
    /// authoritative request details (<c>WorkPackageQueryService.GetWorkPackageScope</c>;
    /// Architecture §7, §8, §11 — <c>UC-WP-003</c>, <c>SCR-WP-001</c>).
    /// </summary>
    [HttpGet("{id:guid}/scope")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkPackageScopeItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<WorkPackageScopeItemDto>>> GetWorkPackageScope(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var scope = await _mediator.Send(new GetWorkPackageScopeQuery(id), cancellationToken);
        if (scope.Count == 0 && !await _workPackageQueryService.WorkPackageExistsAsync(id, cancellationToken))
        {
            throw new KeyNotFoundException($"WorkPackage '{id}' was not found.");
        }

        return Ok(scope);
    }

    /// <summary>
    /// Resolves the active work package containing the specified request
    /// (<c>WorkPackageQueryService.GetRequestWorkPackage</c>; Architecture §11).
    /// </summary>
    [HttpGet("by-request/{requestId:guid}")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WorkPackageDto>> GetRequestWorkPackage(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var workPackage = await _mediator.Send(new GetRequestWorkPackageQuery(requestId), cancellationToken)
            ?? throw new KeyNotFoundException($"No active WorkPackage found for Request '{requestId}'.");

        return Ok(workPackage);
    }

    /// <summary>
    /// Creates a new work package in <c>DRAFT</c> state
    /// (<c>WorkPackageService.CreateWorkPackage</c>; Architecture §7, §8, §11 — <c>UC-WP-001</c>, <c>SCR-WP-001</c>).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateWorkPackage(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateWorkPackageBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateWorkPackageCommand(
            Name: request?.ResolvedName ?? string.Empty,
            Objective: request?.ResolvedObjective ?? string.Empty,
            OwnerPersonId: request?.ResolvedOwnerPersonId ?? Guid.Empty,
            CustomerId: FirstNonEmptyGuid(request?.CustomerId),
            ProductId: FirstNonEmptyGuid(request?.ProductId),
            Deadline: request?.Deadline);

        try
        {
            var created = await _mediator.Send(command, cancellationToken);
            var enriched = await _workPackageQueryService.GetWorkPackageByIdAsync(created.Id, cancellationToken) ?? created;

            _logger.LogInformation(
                "Created work package '{WorkPackageId}' ({Name}) in status '{Status}' with owner '{OwnerPersonId}'.",
                enriched.Id,
                enriched.Name,
                enriched.Status,
                enriched.OwnerPersonId);

            return CreatedAtAction(nameof(GetWorkPackageById), new { id = enriched.Id }, enriched);
        }
        catch (Exception ex) when (ex is InvalidOperationException or WorkPackageDomainException)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Updates the title and objective of an existing work package
    /// (<c>WorkPackageService.UpdateObjective</c>; Architecture §7, §8, §11 — <c>UC-WP-001</c>, <c>SCR-WP-001</c>).
    /// </summary>
    [HttpPut("{id:guid}/objective")]
    [HttpPost("{id:guid}/objective")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateObjective(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] UpdateWorkPackageObjectiveBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedObjective = request?.ResolvedObjective ?? string.Empty;
        var resolvedName = request?.ExplicitName;

        if (resolvedName is null && !string.IsNullOrWhiteSpace(resolvedObjective))
        {
            var existing = await _workPackageQueryService.GetWorkPackageByIdAsync(id, cancellationToken);
            resolvedName = !string.IsNullOrWhiteSpace(existing?.Name)
                ? existing.Name
                : TruncateToNameLength(resolvedObjective);
        }

        var command = new UpdateObjectiveCommand(
            WorkPackageId: id,
            Name: resolvedName ?? string.Empty,
            Objective: resolvedObjective);

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _workPackageQueryService.GetWorkPackageByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Updated objective for work package '{WorkPackageId}' ({Name}).",
                enriched.Id,
                enriched.Name);

            return Ok(enriched);
        }
        catch (Exception ex) when (ex is InvalidOperationException or WorkPackageDomainException)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Updates or clears the target deadline date of an existing work package
    /// (<c>WorkPackageService.UpdateDeadline</c>; Architecture CR-023 §4 TD-004).
    /// </summary>
    [HttpPut("{id:guid}/deadline")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateDeadline(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] UpdateWorkPackageDeadlineBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateWorkPackageDeadlineCommand(
            WorkPackageId: id,
            Deadline: request?.Deadline);

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _workPackageQueryService.GetWorkPackageByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Updated deadline for work package '{WorkPackageId}' to '{Deadline}'.",
                enriched.Id,
                enriched.Deadline);

            return Ok(enriched);
        }
        catch (Exception ex) when (ex is InvalidOperationException or WorkPackageDomainException)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Updates or clears the Customer and Product context associations of an existing work package (CR-024).
    /// </summary>
    [HttpPut("{id:guid}/context")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateContext(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] UpdateWorkPackageContextBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateWorkPackageContextCommand(
            WorkPackageId: id,
            CustomerId: FirstNonEmptyGuid(request?.CustomerId),
            ProductId: FirstNonEmptyGuid(request?.ProductId));

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _workPackageQueryService.GetWorkPackageByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Updated context for work package '{WorkPackageId}' (Customer: '{CustomerId}', Product: '{ProductId}').",
                enriched.Id,
                enriched.CustomerId,
                enriched.ProductId);

            return Ok(enriched);
        }
        catch (Exception ex) when (ex is InvalidOperationException or WorkPackageDomainException or ArgumentException)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Reassigns ownership of a work package to an active organizational person
    /// (<c>WorkPackageService.AssignOwner</c>; Architecture §7, §8, §11 — <c>UC-WP-001</c>, <c>SCR-WP-001</c>).
    /// </summary>
    [HttpPost("{id:guid}/assign-owner")]
    [HttpPut("{id:guid}/owner")]
    [HttpPost("{id:guid}/owner")]
    [HttpPut("{id:guid}/assign-owner")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AssignOwner(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AssignWorkPackageOwnerBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new AssignOwnerCommand(
            WorkPackageId: id,
            NewOwnerPersonId: request?.ResolvedOwnerPersonId ?? Guid.Empty);

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _workPackageQueryService.GetWorkPackageByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Assigned work package '{WorkPackageId}' to owner '{OwnerPersonId}'.",
                enriched.Id,
                enriched.OwnerPersonId);

            return Ok(enriched);
        }
        catch (Exception ex) when (ex is InvalidOperationException or WorkPackageDomainException)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Transitions a work package from <c>DRAFT</c> to <c>ACTIVE</c> state
    /// (<c>WorkPackageService.ActivateWorkPackage</c>; Architecture §7, §8, §11 — <c>UC-WP-001</c>, <c>SCR-WP-001</c>).
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [HttpPut("{id:guid}/activate")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ActivateWorkPackage(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await _mediator.Send(new ActivateWorkPackageCommand(id), cancellationToken);
            var enriched = await _workPackageQueryService.GetWorkPackageByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Activated work package '{WorkPackageId}', transitioning to '{Status}'.",
                enriched.Id,
                enriched.Status);

            return Ok(enriched);
        }
        catch (Exception ex) when (ex is InvalidOperationException or WorkPackageDomainException)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Transitions a work package from <c>ACTIVE</c> or <c>DRAFT</c> to <c>CLOSED</c> state
    /// (<c>WorkPackageService.CloseWorkPackage</c>; Architecture §7, §8, §11 — <c>UC-WP-001</c>, <c>SCR-WP-001</c>).
    /// </summary>
    [HttpPost("{id:guid}/close")]
    [HttpPut("{id:guid}/close")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CloseWorkPackage(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CloseWorkPackageBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new CloseWorkPackageCommand(
            WorkPackageId: id,
            Reason: request?.ResolvedReason ?? string.Empty);

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _workPackageQueryService.GetWorkPackageByIdAsync(updated.Id, cancellationToken) ?? createdOrFallback(updated);

            _logger.LogInformation(
                "Closed work package '{WorkPackageId}' with reason '{ClosedReason}', transitioning to '{Status}'.",
                enriched.Id,
                enriched.ClosedReason,
                enriched.Status);

            return Ok(enriched);
        }
        catch (Exception ex) when (ex is InvalidOperationException or WorkPackageDomainException)
        {
            return CreateBadRequestProblem(ex.Message);
        }

        static WorkPackageDto createdOrFallback(WorkPackageDto dto) => dto;
    }

    /// <summary>
    /// Associates an existing operational request with a work package, enforcing Business Rule 9
    /// (<c>WorkPackageService.AddRequestToWorkPackage</c>; Architecture §7, §8, §11 — <c>UC-WP-002</c>, <c>SCR-WP-001</c>).
    /// </summary>
    [HttpPost("{id:guid}/requests")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddRequestToWorkPackage(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AddRequestToWorkPackageBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new AddRequestToWorkPackageCommand(
            WorkPackageId: id,
            RequestId: request?.ResolvedRequestId ?? Guid.Empty);

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _workPackageQueryService.GetWorkPackageByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Added request '{RequestId}' to work package '{WorkPackageId}'.",
                command.RequestId,
                enriched.Id);

            return Ok(enriched);
        }
        catch (Exception ex) when (ex is InvalidOperationException or WorkPackageDomainException)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Removes a request from a work package by deactivating its membership link
    /// (<c>WorkPackageService.RemoveRequestFromWorkPackage</c>; Architecture §7, §8, §11 — <c>UC-WP-002</c>, <c>SCR-WP-001</c>).
    /// </summary>
    [HttpDelete("{id:guid}/requests/{requestId:guid}")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveRequestFromWorkPackage(
        Guid id,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var command = new RemoveRequestFromWorkPackageCommand(
            WorkPackageId: id,
            RequestId: requestId);

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _workPackageQueryService.GetWorkPackageByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Removed request '{RequestId}' from work package '{WorkPackageId}'.",
                requestId,
                enriched.Id);

            return Ok(enriched);
        }
        catch (Exception ex) when (ex is InvalidOperationException or WorkPackageDomainException)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Reorders the active requests belonging to a work package (CR-015; Architecture §4 TD-002, TD-003).
    /// </summary>
    [HttpPut("{id:guid}/requests/reorder")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReorderRequests(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReorderWorkPackageRequestsBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var orderedIds = request?.ResolvedOrderedRequestIds ?? Array.Empty<Guid>();
        var command = new ReorderWorkPackageRequestsCommand(id, orderedIds);

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _workPackageQueryService.GetWorkPackageByIdAsync(updated.Id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Reordered {Count} requests in work package '{WorkPackageId}'.",
                orderedIds.Count,
                enriched.Id);

            return Ok(enriched);
        }
        catch (InvalidWorkPackageStateTransitionException ex)
        {
            return CreateConflictProblem(ex.Message);
        }
        catch (Exception ex) when (ex is InvalidOperationException or WorkPackageDomainException or ArgumentException)
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

    private ObjectResult CreateConflictProblem(string detail)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict",
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8"
        };
        problem.Extensions["errorCode"] = "CONFLICT";
        problem.Extensions["traceId"] = traceId;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status409Conflict,
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

    private static string TruncateToNameLength(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= 255 ? trimmed : trimmed[..255];
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/work-packages</c> (<c>CreateWorkPackage</c>).
    /// </summary>
    public sealed class CreateWorkPackageBody
    {
        public string? Name { get; set; }
        public string? Title { get; set; }
        public string? Objective { get; set; }
        public Guid? OwnerPersonId { get; set; }
        public Guid? OwnerId { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? ProductId { get; set; }
        public DateTime? Deadline { get; set; }

        public string ResolvedObjective => Objective ?? string.Empty;

        public string ResolvedName
        {
            get
            {
                if (Name is not null)
                {
                    return Name;
                }

                if (Title is not null)
                {
                    return Title;
                }

                if (!string.IsNullOrWhiteSpace(Objective))
                {
                    return TruncateToNameLength(Objective);
                }

                return string.Empty;
            }
        }

        public Guid ResolvedOwnerPersonId =>
            FirstNonEmptyGuid(OwnerPersonId, OwnerId) ?? Guid.Empty;
    }

    /// <summary>
    /// Request payload for <c>PUT /api/v1/work-packages/{id}/deadline</c> (<c>UpdateDeadline</c>).
    /// </summary>
    public sealed class UpdateWorkPackageDeadlineBody
    {
        public DateTime? Deadline { get; set; }
    }

    /// <summary>
    /// Request payload for <c>PUT /api/v1/work-packages/{id}/context</c> (<c>UpdateContext</c>).
    /// </summary>
    public sealed class UpdateWorkPackageContextBody
    {
        public Guid? CustomerId { get; set; }
        public Guid? ProductId { get; set; }
    }

    /// <summary>
    /// Request payload for <c>PUT /api/v1/work-packages/{id}/objective</c> (<c>UpdateObjective</c>).
    /// </summary>
    public sealed class UpdateWorkPackageObjectiveBody
    {
        public string? Name { get; set; }
        public string? Title { get; set; }
        public string? Objective { get; set; }

        public string? ExplicitName => Name ?? Title;

        public string ResolvedObjective => Objective ?? string.Empty;
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/work-packages/{id}/assign-owner</c> (<c>AssignOwner</c>).
    /// </summary>
    public sealed class AssignWorkPackageOwnerBody
    {
        public Guid? NewOwnerPersonId { get; set; }
        public Guid? OwnerPersonId { get; set; }
        public Guid? OwnerId { get; set; }
        public Guid? PersonId { get; set; }

        public Guid ResolvedOwnerPersonId =>
            FirstNonEmptyGuid(NewOwnerPersonId, OwnerPersonId, OwnerId, PersonId) ?? Guid.Empty;
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/work-packages/{id}/close</c> (<c>CloseWorkPackage</c>).
    /// </summary>
    public sealed class CloseWorkPackageBody
    {
        public string? Reason { get; set; }
        public string? CloseReason { get; set; }
        public string? ClosedReason { get; set; }
        public string? Notes { get; set; }

        public string ResolvedReason =>
            FirstNonWhiteSpace(Reason, CloseReason, ClosedReason, Notes) ?? string.Empty;
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/work-packages/{id}/requests</c> (<c>AddRequestToWorkPackage</c>).
    /// </summary>
    public sealed class AddRequestToWorkPackageBody
    {
        public Guid? RequestId { get; set; }
        public Guid? Id { get; set; }

        public Guid ResolvedRequestId =>
            FirstNonEmptyGuid(RequestId, Id) ?? Guid.Empty;
    }

    /// <summary>
    /// Request payload for <c>PUT /api/v1/work-packages/{id}/requests/reorder</c> (<c>ReorderRequests</c>).
    /// </summary>
    public class ReorderWorkPackageRequestsBody
    {
        public List<Guid>? OrderedRequestIds { get; set; }
        public List<Guid>? RequestIds { get; set; }

        public IReadOnlyList<Guid> ResolvedOrderedRequestIds =>
            OrderedRequestIds ?? RequestIds ?? (IReadOnlyList<Guid>)Array.Empty<Guid>();
    }

    /// <summary>
    /// Request payload for <c>PUT /api/v1/work-packages/{id}/requests/reorder</c> (<c>ReorderRequests</c>).
    /// </summary>
    public sealed class ReorderWorkPackageRequestsRequest : ReorderWorkPackageRequestsBody
    {
    }
}
