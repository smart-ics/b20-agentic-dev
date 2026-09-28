using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when a Request resolution is accepted and the request is marked completed (UC-REQ-008).
/// </summary>
public sealed record RequestCompleted : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid CompletedByPersonId { get; init; }
    public string ResolutionDescription { get; init; } = string.Empty;
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestCompleted(
        Guid requestId,
        Guid completedByPersonId,
        string resolutionDescription,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        CompletedByPersonId = completedByPersonId;
        ResolutionDescription = resolutionDescription;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}
