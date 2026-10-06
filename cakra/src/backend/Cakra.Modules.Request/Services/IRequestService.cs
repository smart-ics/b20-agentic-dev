namespace Cakra.Modules.Request.Services;

/// <summary>
/// Published application command service contract for Request lifecycle operations
/// (Architecture §7, §8, §15, §18).
/// </summary>
public partial interface IRequestService
{
    /// <summary>
    /// Records a new operational Request in <c>CAPTURED</c> state, validating customer via
    /// <c>ICustomerQueryService</c> and product via <c>IProductQueryService</c>, persisting the
    /// initial state audit record in <c>request.RequestAssignments</c>, and emitting <c>RequestRecorded</c>
    /// (UC-REQ-001).
    /// </summary>
    Task<RequestDto> RecordRequestAsync(
        string title,
        string? description = null,
        Guid? customerId = null,
        Guid? productId = null,
        string requestType = "GENERAL",
        string priority = "NORMAL",
        Guid? actorPersonId = null,
        Guid? workPackageId = null,
        int? complexity = null,
        IReadOnlyList<InitialSubTaskDto>? initialSubTasks = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="RecordRequestAsync"/>.
    /// </summary>
    Task<RequestDto> RecordRequest(
        string title,
        string? description = null,
        Guid? customerId = null,
        Guid? productId = null,
        string requestType = "GENERAL",
        string priority = "NORMAL",
        Guid? actorPersonId = null,
        Guid? workPackageId = null,
        int? complexity = null,
        IReadOnlyList<InitialSubTaskDto>? initialSubTasks = null,
        CancellationToken cancellationToken = default)
        => RecordRequestAsync(
            title,
            description,
            customerId,
            productId,
            requestType,
            priority,
            actorPersonId,
            workPackageId,
            complexity,
            initialSubTasks,
            cancellationToken);

    /// <summary>
    /// Assigns an organizational owner to a Request, validating the assignee via
    /// <c>IOrganizationQueryService</c>, transitioning <c>CAPTURED -&gt; EVALUATING</c>,
    /// recording the state change audit in <c>request.RequestAssignments</c>, and emitting
    /// <c>RequestAssigned</c> (UC-REQ-002).
    /// </summary>
    Task<RequestDto> AssignRequestOwnerAsync(
        Guid requestId,
        Guid ownerPersonId,
        string? notes = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="AssignRequestOwnerAsync"/>.
    /// </summary>
    Task<RequestDto> AssignRequestOwner(
        Guid requestId,
        Guid ownerPersonId,
        string? notes = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => AssignRequestOwnerAsync(requestId, ownerPersonId, notes, actorPersonId, cancellationToken);

    /// <summary>
    /// Starts active work on a Request, transitioning <c>ASSIGNED</c> or <c>PAUSED -&gt; IN_PROGRESS</c>,
    /// enforcing that only the assigned owner can start work, recording state change audit entries in
    /// <c>request.RequestAssignments</c>, and emitting <c>RequestWorkStarted</c> (Architecture CR-016 TD-002).
    /// </summary>
    Task<RequestDto> StartWorkAsync(
        Guid requestId,
        string? notes = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="StartWorkAsync"/>.
    /// </summary>
    Task<RequestDto> StartWork(
        Guid requestId,
        string? notes = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => StartWorkAsync(requestId, notes, actorPersonId, cancellationToken);

    /// <summary>
    /// Suspends active work on a Request, transitioning <c>IN_PROGRESS -&gt; PAUSED</c>,
    /// recording state change audit entries in <c>request.RequestAssignments</c>, and emitting
    /// <c>RequestWorkPaused</c> (Architecture CR-016 TD-002).
    /// </summary>
    Task<RequestDto> PauseWorkAsync(
        Guid requestId,
        string? note = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="PauseWorkAsync"/>.
    /// </summary>
    Task<RequestDto> PauseWork(
        Guid requestId,
        string? note = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => PauseWorkAsync(requestId, note, actorPersonId, cancellationToken);

    /// <summary>
    /// Cancels a Request in any active state, transitioning it to <c>CANCELLED</c>,
    /// recording the cancellation resolution in <c>request.RequestResolutions</c> and audit entry in
    /// <c>request.RequestAssignments</c>, and emitting <c>RequestCancelled</c> (Architecture CR-016 TD-002).
    /// </summary>
    Task<RequestDto> CancelRequestAsync(
        Guid requestId,
        string reason,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="CancelRequestAsync"/>.
    /// </summary>
    Task<RequestDto> CancelRequest(
        Guid requestId,
        string reason,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => CancelRequestAsync(requestId, reason, actorPersonId, cancellationToken);

    /// <summary>
    /// Convenience alias for <see cref="CancelRequestAsync"/>.
    /// </summary>
    Task<RequestDto> Cancel(
        Guid requestId,
        string reason,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => CancelRequestAsync(requestId, reason, actorPersonId, cancellationToken);

    /// <summary>
    /// Reassigns request ownership to a new person (Architecture CR-016 TD-003).
    /// </summary>
    Task<RequestDto> ReassignRequestOwnershipAsync(
        Guid requestId,
        Guid newOwnerPersonId,
        string? notes = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => AssignRequestOwnerAsync(requestId, newOwnerPersonId, notes, actorPersonId, cancellationToken);

    /// <summary>
    /// Convenience alias for <see cref="ReassignRequestOwnershipAsync"/>.
    /// </summary>
    Task<RequestDto> ReassignRequestOwnership(
        Guid requestId,
        Guid newOwnerPersonId,
        string? notes = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => AssignRequestOwnerAsync(requestId, newOwnerPersonId, notes, actorPersonId, cancellationToken);

    /// <summary>
    /// Reviews and completes work on a Request in <c>IN_PROGRESS</c> state, transitioning it to
    /// <c>COMPLETED</c>, recording completion details in <c>request.RequestResolutions</c> and
    /// audit entry in <c>request.RequestAssignments</c>, and emitting <c>RequestCompleted</c> (UC-REQ-008).
    /// </summary>
    Task<RequestDto> ReviewRequestCompletionAsync(
        Guid requestId,
        string resolutionDescription,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="ReviewRequestCompletionAsync"/>.
    /// </summary>
    Task<RequestDto> ReviewRequestCompletion(
        Guid requestId,
        string resolutionDescription,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => ReviewRequestCompletionAsync(requestId, resolutionDescription, actorPersonId, cancellationToken);

    /// <summary>
    /// Convenience alias for <see cref="ReviewRequestCompletionAsync"/>.
    /// </summary>
    Task<RequestDto> CompleteRequestAsync(
        Guid requestId,
        string resolutionDescription,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => ReviewRequestCompletionAsync(requestId, resolutionDescription, actorPersonId, cancellationToken);

    /// <summary>
    /// Convenience alias for <see cref="ReviewRequestCompletionAsync"/>.
    /// </summary>
    Task<RequestDto> CompleteRequest(
        Guid requestId,
        string resolutionDescription,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => ReviewRequestCompletionAsync(requestId, resolutionDescription, actorPersonId, cancellationToken);

    /// <summary>
    /// Updates the complexity rating (1 to 5) of a Request, enforcing role authorization and emitting
    /// <c>RequestComplexityUpdated</c> (Architecture §4 TD-002, TD-004, TD-005).
    /// </summary>
    Task<RequestDto> UpdateRequestComplexityAsync(
        Guid requestId,
        int complexity,
        string? reason = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="UpdateRequestComplexityAsync"/>.
    /// </summary>
    Task<RequestDto> UpdateRequestComplexity(
        Guid requestId,
        int complexity,
        string? reason = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => UpdateRequestComplexityAsync(requestId, complexity, reason, actorPersonId, cancellationToken);

    /// <summary>
    /// Adds a new operational sub-task checklist item to a Request (Architecture CR-006 §4 TD-001, TD-006).
    /// </summary>
    Task<RequestDto> AddRequestSubTaskAsync(
        Guid requestId,
        string title,
        Guid? assigneePersonId = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="AddRequestSubTaskAsync"/>.
    /// </summary>
    Task<RequestDto> AddRequestSubTask(
        Guid requestId,
        string title,
        Guid? assigneePersonId = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => AddRequestSubTaskAsync(requestId, title, assigneePersonId, actorPersonId, cancellationToken);

    /// <summary>
    /// Marks a sub-task checklist item completed on a Request (Architecture CR-006 §4 TD-001, TD-006).
    /// </summary>
    Task<RequestDto> CompleteRequestSubTaskAsync(
        Guid requestId,
        Guid subTaskId,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="CompleteRequestSubTaskAsync"/>.
    /// </summary>
    Task<RequestDto> CompleteRequestSubTask(
        Guid requestId,
        Guid subTaskId,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => CompleteRequestSubTaskAsync(requestId, subTaskId, actorPersonId, cancellationToken);

    /// <summary>
    /// Reopens a completed sub-task checklist item back to pending on a Request (Architecture CR-006 §4 TD-001, TD-003, TD-006).
    /// </summary>
    Task<RequestDto> ReopenRequestSubTaskAsync(
        Guid requestId,
        Guid subTaskId,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="ReopenRequestSubTaskAsync"/>.
    /// </summary>
    Task<RequestDto> ReopenRequestSubTask(
        Guid requestId,
        Guid subTaskId,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => ReopenRequestSubTaskAsync(requestId, subTaskId, actorPersonId, cancellationToken);

    /// <summary>
    /// Removes a sub-task checklist item from a Request (Architecture CR-006 §4 TD-001, TD-006).
    /// </summary>
    Task<RequestDto> RemoveRequestSubTaskAsync(
        Guid requestId,
        Guid subTaskId,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="RemoveRequestSubTaskAsync"/>.
    /// </summary>
    Task<RequestDto> RemoveRequestSubTask(
        Guid requestId,
        Guid subTaskId,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => RemoveRequestSubTaskAsync(requestId, subTaskId, actorPersonId, cancellationToken);

    /// <summary>
    /// Updates core attributes (Title, Description, Priority, RequestType) on an active Request (Architecture CR-018 §4 TD-001, TD-004).
    /// </summary>
    Task<RequestDto> UpdateRequestCoreAttributesAsync(
        Guid requestId,
        string title,
        string description,
        string priority,
        string requestType,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="UpdateRequestCoreAttributesAsync"/>.
    /// </summary>
    Task<RequestDto> UpdateRequestCoreAttributes(
        Guid requestId,
        string title,
        string description,
        string priority,
        string requestType,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => UpdateRequestCoreAttributesAsync(requestId, title, description, priority, requestType, actorPersonId, cancellationToken);
}

