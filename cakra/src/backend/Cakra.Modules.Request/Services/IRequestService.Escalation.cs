using Cakra.Modules.Request.Domain;

namespace Cakra.Modules.Request.Services;

/// <summary>
/// Escalation, management decision, and ownership reassignment command contracts for
/// <see cref="IRequestService"/> (Architecture §7, §8 — UC-REQ-006, UC-REQ-007, UC-MGT-001).
/// </summary>
public partial interface IRequestService
{
    /// <summary>
    /// Escalates a Request in <c>EVALUATING</c> or <c>IN_PROGRESS</c> state to <c>ESCALATED</c>,
    /// recording the escalation actor and reason in <c>request.Requests</c> and audit entry in
    /// <c>request.RequestAssignments</c>, and emitting <c>RequestEscalated</c> (UC-REQ-006).
    /// </summary>
    Task<RequestDto> EscalateRequestAsync(
        Guid requestId,
        string reason,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="EscalateRequestAsync"/>.
    /// </summary>
    Task<RequestDto> EscalateRequest(
        Guid requestId,
        string reason,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => EscalateRequestAsync(requestId, reason, actorPersonId, cancellationToken);

    /// <summary>
    /// Records a management decision request or determination on an active or escalated Request,
    /// optionally transitioning an <c>ESCALATED</c> Request back to <c>EVALUATING</c> or <c>IN_PROGRESS</c>,
    /// recording an audit entry in <c>request.RequestAssignments</c>, and emitting
    /// <c>ManagementDecisionRequested</c> (UC-REQ-007).
    /// </summary>
    Task<RequestDto> RequestManagementDecisionAsync(
        Guid requestId,
        string decisionDetails,
        Guid? actorPersonId = null,
        RequestStatus? targetStatus = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="RequestManagementDecisionAsync"/>.
    /// </summary>
    Task<RequestDto> RequestManagementDecision(
        Guid requestId,
        string decisionDetails,
        Guid? actorPersonId = null,
        RequestStatus? targetStatus = null,
        CancellationToken cancellationToken = default)
        => RequestManagementDecisionAsync(requestId, decisionDetails, actorPersonId, targetStatus, cancellationToken);

    /// <summary>
    /// Reassigns Request ownership to a new active Person in Organization, validating the new assignee
    /// via <c>IOrganizationQueryService</c>, updating <c>OwnerPersonId</c> (and transitioning
    /// <c>ESCALATED -&gt; EVALUATING/IN_PROGRESS</c> or <c>CAPTURED -&gt; EVALUATING</c>), recording
    /// the state/ownership audit entry in <c>request.RequestAssignments</c>, and emitting
    /// <c>RequestAssigned</c> (UC-MGT-001).
    /// </summary>
    Task<RequestDto> ReassignRequestOwnershipAsync(
        Guid requestId,
        Guid newOwnerPersonId,
        string? notes = null,
        Guid? actorPersonId = null,
        RequestStatus? targetStatusForEscalated = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="ReassignRequestOwnershipAsync"/>.
    /// </summary>
    Task<RequestDto> ReassignRequestOwnership(
        Guid requestId,
        Guid newOwnerPersonId,
        string? notes = null,
        Guid? actorPersonId = null,
        RequestStatus? targetStatusForEscalated = null,
        CancellationToken cancellationToken = default)
        => ReassignRequestOwnershipAsync(
            requestId,
            newOwnerPersonId,
            notes,
            actorPersonId,
            targetStatusForEscalated,
            cancellationToken);
}
