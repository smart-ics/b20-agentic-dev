using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when a sub-task item is removed from a Request (CR-006 Architecture TD-007).
/// </summary>
public sealed record RequestSubTaskRemoved : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid SubTaskId { get; init; }
    public int TotalSubTasksCount { get; init; }
    public int CompletionPercentage { get; init; }
    public Guid ActorPersonId { get; init; }
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestSubTaskRemoved(
        Guid requestId,
        Guid subTaskId,
        int totalSubTasksCount,
        int completionPercentage,
        Guid actorPersonId,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        SubTaskId = subTaskId;
        TotalSubTasksCount = totalSubTasksCount;
        CompletionPercentage = completionPercentage;
        ActorPersonId = actorPersonId;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}
