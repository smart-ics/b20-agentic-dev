using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Obsolete event records preserved for backward compatibility with external event handlers
/// during CR-016 migration until dependent modules and test suites are refactored.
/// </summary>
[Obsolete("RequestEscalated is obsolete in CR-016. Use RequestWorkPaused instead.")]
public sealed record RequestEscalated : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid EscalatedByPersonId { get; init; }
    public string EscalationReason { get; init; } = string.Empty;
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestEscalated(
        Guid requestId,
        Guid escalatedByPersonId,
        string escalationReason,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        EscalatedByPersonId = escalatedByPersonId;
        EscalationReason = escalationReason;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}

[Obsolete("RequestRejected is obsolete in CR-016. Use RequestCancelled instead.")]
public sealed record RequestRejected : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid RejectedByPersonId { get; init; }
    public string RejectionReason { get; init; } = string.Empty;
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestRejected(
        Guid requestId,
        Guid rejectedByPersonId,
        string rejectionReason,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        RejectedByPersonId = rejectedByPersonId;
        RejectionReason = rejectionReason;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}

[Obsolete("RequestAccepted is obsolete in CR-016. Use RequestWorkStarted instead.")]
public sealed record RequestAccepted : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid OwnerPersonId { get; init; }
    public string? Notes { get; init; }
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestAccepted(
        Guid requestId,
        Guid ownerPersonId,
        string? notes = null,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        OwnerPersonId = ownerPersonId;
        Notes = notes;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}

[Obsolete("RequestEvaluated is obsolete in CR-016. Evaluation phase has been eliminated.")]
public sealed record RequestEvaluated : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid EvaluatedByPersonId { get; init; }
    public string EvaluationNotes { get; init; } = string.Empty;
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestEvaluated(
        Guid requestId,
        Guid evaluatedByPersonId,
        string evaluationNotes,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        EvaluatedByPersonId = evaluatedByPersonId;
        EvaluationNotes = evaluationNotes;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}

[Obsolete("ManagementDecisionRequested is obsolete in CR-016. Management decisions are replaced by post discussions.")]
public sealed record ManagementDecisionRequested : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid RequestedByPersonId { get; init; }
    public string DecisionDetails { get; init; } = string.Empty;
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public ManagementDecisionRequested(
        Guid requestId,
        Guid requestedByPersonId,
        string decisionDetails,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        RequestedByPersonId = requestedByPersonId;
        DecisionDetails = decisionDetails;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}
