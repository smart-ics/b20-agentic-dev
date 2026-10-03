using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when a sub-task item is marked completed on a Request (CR-006 Architecture TD-007).
/// </summary>
public sealed record RequestSubTaskCompleted : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid SubTaskId { get; init; }
    public Guid CompletedByPersonId { get; init; }
    public int CompletedSubTasksCount { get; init; }
    public int CompletionPercentage { get; init; }
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestSubTaskCompleted(
        Guid requestId,
        Guid subTaskId,
        Guid completedByPersonId,
        int completedSubTasksCount,
        int completionPercentage,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        SubTaskId = subTaskId;
        CompletedByPersonId = completedByPersonId;
        CompletedSubTasksCount = completedSubTasksCount;
        CompletionPercentage = completionPercentage;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}
