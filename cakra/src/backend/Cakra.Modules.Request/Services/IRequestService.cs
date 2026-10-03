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
        string description,
        Guid? customerId = null,
        Guid? productId = null,
        string requestType = "GENERAL",
        string priority = "NORMAL",
        Guid? actorPersonId = null,
        Guid? workPackageId = null,
        int? complexity = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="RecordRequestAsync"/>.
    /// </summary>
    Task<RequestDto> RecordRequest(
        string title,
        string description,
        Guid? customerId = null,
        Guid? productId = null,
        string requestType = "GENERAL",
        string priority = "NORMAL",
        Guid? actorPersonId = null,
        Guid? workPackageId = null,
        int? complexity = null,
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
    /// Records triage evaluation notes on a Request in <c>EVALUATING</c> state and emits
    /// <c>RequestEvaluated</c> (UC-REQ-003).
    /// </summary>
    Task<RequestDto> EvaluateRequestAsync(
        Guid requestId,
        string evaluationNotes,
        Guid? actorPersonId = null,
        int? complexity = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="EvaluateRequestAsync"/>.
    /// </summary>
    Task<RequestDto> EvaluateRequest(
        Guid requestId,
        string evaluationNotes,
        Guid? actorPersonId = null,
        int? complexity = null,
        CancellationToken cancellationToken = default)
        => EvaluateRequestAsync(requestId, evaluationNotes, actorPersonId, complexity, cancellationToken);

    /// <summary>
    /// Accepts operational responsibility for a Request in <c>EVALUATING</c> (or <c>ACCEPTED</c>) state,
    /// transitioning it to <c>IN_PROGRESS</c>, recording state change audit entries in
    /// <c>request.RequestAssignments</c>, and emitting <c>RequestAccepted</c> (UC-REQ-004).
    /// </summary>
    Task<RequestDto> AcceptRequestResponsibilityAsync(
        Guid requestId,
        string? notes = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="AcceptRequestResponsibilityAsync"/>.
    /// </summary>
    Task<RequestDto> AcceptRequestResponsibility(
        Guid requestId,
        string? notes = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => AcceptRequestResponsibilityAsync(requestId, notes, actorPersonId, cancellationToken);

    /// <summary>
    /// Convenience alias for <see cref="AcceptRequestResponsibilityAsync"/>.
    /// </summary>
    Task<RequestDto> AcceptRequestAsync(
        Guid requestId,
        string? notes = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => AcceptRequestResponsibilityAsync(requestId, notes, actorPersonId, cancellationToken);

    /// <summary>
    /// Convenience alias for <see cref="AcceptRequestResponsibilityAsync"/>.
    /// </summary>
    Task<RequestDto> AcceptRequest(
        Guid requestId,
        string? notes = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => AcceptRequestResponsibilityAsync(requestId, notes, actorPersonId, cancellationToken);

    /// <summary>
    /// Rejects a Request in <c>EVALUATING</c> state, transitioning it to <c>REJECTED</c>,
    /// recording the rejection resolution in <c>request.RequestResolutions</c> and audit entry in
    /// <c>request.RequestAssignments</c>, and emitting <c>RequestRejected</c> (UC-REQ-005).
    /// </summary>
    Task<RequestDto> RejectRequestAsync(
        Guid requestId,
        string reason,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="RejectRequestAsync"/>.
    /// </summary>
    Task<RequestDto> RejectRequest(
        Guid requestId,
        string reason,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => RejectRequestAsync(requestId, reason, actorPersonId, cancellationToken);

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
}
