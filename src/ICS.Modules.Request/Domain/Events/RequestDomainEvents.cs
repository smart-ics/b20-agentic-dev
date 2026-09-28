namespace ICS.Modules.Request.Domain.Events;

using ICS.Core.Domain;

/// <summary>
/// Domain event published when a new operational customer Request is captured and recorded.
/// Architecture §7, §8 (UC-REQ-001); request-domain.md §10.
/// </summary>
public sealed record RequestRecorded(
    Guid RequestId,
    string Title,
    string Type,
    Guid? RequesterPersonId,
    Guid? RequesterContactId,
    string? RequesterName,
    Guid? CustomerId,
    Guid? ProductId,
    Guid? WorkPackageId) : DomainEvent;

/// <summary>
/// Domain event published when a Request is assigned or reassigned to an operational owner.
/// Architecture §7, §8 (UC-REQ-002, UC-MGT-001); request-domain.md §10.
/// </summary>
public sealed record RequestAssigned(
    Guid RequestId,
    Guid? PreviousOwnerPersonId,
    Guid NewOwnerPersonId,
    Guid AssignedByPersonId,
    string? Note) : DomainEvent;

/// <summary>
/// Domain event published when an assigned Request is evaluated during triage.
/// Architecture §7, §8 (UC-REQ-003); request-domain.md §10.
/// </summary>
public sealed record RequestEvaluated(
    Guid RequestId,
    Guid EvaluatedByPersonId,
    string? Notes) : DomainEvent;

/// <summary>
/// Domain event published when operational responsibility for a Request is accepted.
/// Architecture §7, §8 (UC-REQ-004); request-domain.md §10.
/// </summary>
public sealed record RequestAccepted(
    Guid RequestId,
    Guid AcceptedByPersonId,
    string? Notes) : DomainEvent;

/// <summary>
/// Domain event published when a Request is rejected and closed with documented justification.
/// Architecture §7, §8 (UC-REQ-005); request-domain.md §10.
/// </summary>
public sealed record RequestRejected(
    Guid RequestId,
    Guid RejectedByPersonId,
    string Reason) : DomainEvent;

/// <summary>
/// Domain event published when an active Request is escalated for higher-level attention.
/// Architecture §7, §8 (UC-REQ-006); request-domain.md §10.
/// </summary>
public sealed record RequestEscalated(
    Guid RequestId,
    Guid EscalatedByPersonId,
    string Reason,
    string? RequiredAssistance) : DomainEvent;

/// <summary>
/// Domain event published when a Request is elevated to Management for a policy/resource decision.
/// Architecture §7, §8 (UC-REQ-007); request-domain.md §10.
/// </summary>
public sealed record ManagementDecisionRequested(
    Guid RequestId,
    Guid RequestedByPersonId,
    string Question,
    string? Options,
    string? Impact) : DomainEvent;

/// <summary>
/// Domain event published when work on a Request is completed and accepted as resolved.
/// Architecture §7, §8 (UC-REQ-008); request-domain.md §10.
/// </summary>
public sealed record RequestCompleted(
    Guid RequestId,
    Guid CompletedByPersonId,
    string Summary,
    string Outcome) : DomainEvent;
