using Cakra.Core;

namespace Cakra.Modules.Post.Domain.Events;

/// <summary>
/// Domain event emitted when a <see cref="Post"/> transitions from <c>ACTIVE</c> to <c>ARCHIVED</c>
/// (Architecture §12 Update Triggers; Post Domain §8, §9).
/// </summary>
public sealed record PostArchived(
    Guid EventId,
    Guid PostId,
    string PreviousStatus,
    string NewStatus,
    Guid? ActorPersonId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    /// <summary>Convenience alias for <see cref="NewStatus"/> matching <c>FeedItems.Status</c>.</summary>
    public string Status => NewStatus;
}
