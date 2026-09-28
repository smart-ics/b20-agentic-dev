using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when the Request Owner accepts operational responsibility (UC-REQ-004).
/// </summary>
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
