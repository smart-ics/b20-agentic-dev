using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when active work on a Request is suspended (Architecture CR-016 TD-002).
/// </summary>
public sealed record RequestWorkPaused : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid? OwnerPersonId { get; init; }
    public Guid ActorPersonId { get; init; }
    public string? Notes { get; init; }
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestWorkPaused(
        Guid requestId,
        Guid? ownerPersonId,
        Guid actorPersonId,
        string? notes = null,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        OwnerPersonId = ownerPersonId;
        ActorPersonId = actorPersonId;
        Notes = notes;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}
