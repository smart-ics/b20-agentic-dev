using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when an operational impediment/blocker is cleared on a Request (CR-032 Architecture §4 TD-003).
/// </summary>
public sealed record RequestBlockerCleared : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid BlockerId { get; init; }
    public string? ClearanceReason { get; init; }
    public Guid ClearedByPersonId { get; init; }
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestBlockerCleared(
        Guid requestId,
        Guid blockerId,
        string? clearanceReason,
        Guid clearedByPersonId,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        BlockerId = blockerId;
        ClearanceReason = clearanceReason;
        ClearedByPersonId = clearedByPersonId;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}
