using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when an operational impediment/blocker is added to a Request (CR-032 Architecture §4 TD-003).
/// </summary>
public sealed record RequestBlockerAdded : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid BlockerId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public Guid CreatedByPersonId { get; init; }
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestBlockerAdded(
        Guid requestId,
        Guid blockerId,
        string reason,
        Guid createdByPersonId,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        BlockerId = blockerId;
        Reason = reason;
        CreatedByPersonId = createdByPersonId;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}
