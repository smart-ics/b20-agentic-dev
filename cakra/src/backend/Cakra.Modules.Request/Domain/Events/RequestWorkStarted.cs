using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when active work on a Request is initiated by the assigned owner (Architecture CR-016 TD-002).
/// </summary>
public sealed record RequestWorkStarted : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid OwnerPersonId { get; init; }
    public Guid ActorPersonId { get; init; }
    public RequestStatus PreviousStatus { get; init; }
    public string? Notes { get; init; }
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestWorkStarted(
        Guid requestId,
        Guid ownerPersonId,
        Guid actorPersonId,
        RequestStatus previousStatus,
        string? notes = null,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        OwnerPersonId = ownerPersonId;
        ActorPersonId = actorPersonId;
        PreviousStatus = previousStatus;
        Notes = notes;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}
