using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when the operational complexity rating of a Request is updated (Architecture CR-005).
/// </summary>
public sealed record RequestComplexityUpdated : IDomainEvent
{
    public Guid RequestId { get; init; }
    public int PreviousComplexity { get; init; }
    public int NewComplexity { get; init; }
    public Guid ActorPersonId { get; init; }
    public string? Reason { get; init; }
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestComplexityUpdated(
        Guid requestId,
        int previousComplexity,
        int newComplexity,
        Guid actorPersonId,
        string? reason = null,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        PreviousComplexity = previousComplexity;
        NewComplexity = newComplexity;
        ActorPersonId = actorPersonId;
        Reason = reason;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}
