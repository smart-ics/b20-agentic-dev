using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when an owner is assigned or reassigned to a Request (UC-REQ-002, UC-MGT-001).
/// </summary>
public sealed record RequestAssigned : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid OwnerPersonId { get; init; }
    public Guid? PreviousOwnerPersonId { get; init; }
    public Guid ActorPersonId { get; init; }
    public RequestStatus PreviousStatus { get; init; }
    public RequestStatus NewStatus { get; init; }
    public string? Notes { get; init; }
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestAssigned(
        Guid requestId,
        Guid ownerPersonId,
        Guid? previousOwnerPersonId,
        Guid actorPersonId,
        RequestStatus previousStatus,
        RequestStatus newStatus,
        string? notes = null,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        OwnerPersonId = ownerPersonId;
        PreviousOwnerPersonId = previousOwnerPersonId;
        ActorPersonId = actorPersonId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        Notes = notes;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}
