using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when a completed sub-task item is reopened on a Request (CR-006 Architecture TD-007).
/// </summary>
public sealed record RequestSubTaskReopened : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid SubTaskId { get; init; }
    public Guid ActorPersonId { get; init; }
    public int CompletedSubTasksCount { get; init; }
    public int CompletionPercentage { get; init; }
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestSubTaskReopened(
        Guid requestId,
        Guid subTaskId,
        Guid actorPersonId,
        int completedSubTasksCount,
        int completionPercentage,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        SubTaskId = subTaskId;
        ActorPersonId = actorPersonId;
        CompletedSubTasksCount = completedSubTasksCount;
        CompletionPercentage = completionPercentage;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}
