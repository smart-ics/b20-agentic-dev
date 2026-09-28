using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when a Request is escalated due to authority or capability limits (UC-REQ-006).
/// </summary>
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
