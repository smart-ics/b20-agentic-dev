using Cakra.Core;

namespace Cakra.Modules.Post.Domain.Events;

/// <summary>
/// Domain event emitted when a <see cref="Post"/>'s visibility condition changes between
/// <c>VISIBLE</c> and <c>HIDDEN</c> (Architecture §12 Update Triggers; Post Domain §8, §9).
/// </summary>
public sealed record PostVisibilityChanged(
    Guid EventId,
    Guid PostId,
    string PreviousVisibility,
    string NewVisibility,
    Guid? ActorPersonId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    /// <summary>Convenience alias for <see cref="NewVisibility"/> matching <c>FeedItems.Visibility</c>.</summary>
    public string Visibility => NewVisibility;
}
