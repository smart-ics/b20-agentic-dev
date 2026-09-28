using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when a Request is rejected during evaluation and closed (UC-REQ-005).
/// </summary>
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
