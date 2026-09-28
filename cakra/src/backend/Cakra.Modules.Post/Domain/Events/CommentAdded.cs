using Cakra.Core;

namespace Cakra.Modules.Post.Domain.Events;

/// <summary>
/// Domain event emitted when a <see cref="Comment"/> is added to an active <see cref="Post"/>
/// (Architecture §12 Update Triggers; Post Domain §9; UC-FCOL-001).
/// </summary>
public sealed record CommentAdded(
    Guid EventId,
    Guid CommentId,
    Guid PostId,
    Guid AuthorPersonId,
    string? AuthorName,
    string Content,
    int CommentCount,
    DateTime OccurredAtUtc) : IDomainEvent
{
    /// <summary>Bounded excerpt (up to 300 chars) matching <c>FeedItems.LatestCommentExcerpt</c> (Architecture §12).</summary>
    public string LatestCommentExcerpt => Content.Length <= 300 ? Content : Content[..300];
}
