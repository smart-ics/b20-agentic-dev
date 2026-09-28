using Cakra.Core;

namespace Cakra.Modules.Post.Domain.Events;

/// <summary>
/// Domain event notification indicating that an operational Request has stalled due to prolonged
/// inactivity or blocked progress (Architecture §12 — <c>IsException = TRUE</c>,
/// <c>ExceptionType = 'STALLED'</c>; <c>UC-AWR-003</c>).
/// Handled by <see cref="Services.FeedProjectionHandler"/> to flag associated <c>post.FeedItems</c>
/// (or record a system-generated stalled exception entry when no feed item exists yet).
/// </summary>
public sealed record RequestStalled : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid? ActorPersonId { get; init; }
    public string StalledReason { get; init; } = string.Empty;
    public int? StalledHours { get; init; }
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestStalled(
        Guid requestId,
        string? stalledReason = null,
        Guid? actorPersonId = null,
        int? stalledHours = null,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        StalledReason = string.IsNullOrWhiteSpace(stalledReason)
            ? "Request has exceeded the expected operational activity threshold and is marked as stalled."
            : stalledReason.Trim();
        ActorPersonId = actorPersonId;
        StalledHours = stalledHours;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}
