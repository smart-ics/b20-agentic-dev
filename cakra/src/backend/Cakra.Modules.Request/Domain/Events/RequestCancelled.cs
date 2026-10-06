using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when a Request is cancelled (Architecture CR-016 TD-002).
/// </summary>
public sealed record RequestCancelled : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid CancelledByPersonId { get; init; }
    public string CancellationReason { get; init; } = string.Empty;
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestCancelled(
        Guid requestId,
        Guid cancelledByPersonId,
        string cancellationReason,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        CancelledByPersonId = cancelledByPersonId;
        CancellationReason = cancellationReason;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}
