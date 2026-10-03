using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when a new sub-task item is added to a Request (CR-006 Architecture TD-007).
/// </summary>
public sealed record RequestSubTaskAdded : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid SubTaskId { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid? AssigneePersonId { get; init; }
    public int TotalSubTasksCount { get; init; }
    public int CompletionPercentage { get; init; }
    public Guid ActorPersonId { get; init; }
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestSubTaskAdded(
        Guid requestId,
        Guid subTaskId,
        string title,
        Guid? assigneePersonId,
        int totalSubTasksCount,
        int completionPercentage,
        Guid actorPersonId,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        SubTaskId = subTaskId;
        Title = title;
        AssigneePersonId = assigneePersonId;
        TotalSubTasksCount = totalSubTasksCount;
        CompletionPercentage = completionPercentage;
        ActorPersonId = actorPersonId;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}
