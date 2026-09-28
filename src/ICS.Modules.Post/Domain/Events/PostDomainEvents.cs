namespace ICS.Modules.Post.Domain.Events;

using ICS.Core.Domain;

/// <summary>
/// Domain event published when a Post is created (human-authored or system-generated).
/// Architecture §12 (Update Triggers), §19.2 (MediatR notifications); post-domain.md §9.
/// Subscribed to by FeedProjectionHandler (P6-S24) to insert a FeedItem row.
/// </summary>
public sealed record PostCreated(
    Guid PostId,
    string Title,
    string Content,
    string Source,
    Guid? AuthorPersonId,
    string Status,
    string Visibility,
    IReadOnlyList<PostReferencePayload>? References,
    DateTime CreatedAt) : DomainEvent;

/// <summary>
/// Lightweight payload describing a Post Reference carried in the <see cref="PostCreated"/> event.
/// </summary>
public sealed record PostReferencePayload(
    string ReferenceType,
    Guid ReferenceId);

/// <summary>
/// Domain event published when a Comment is added to a Post.
/// Architecture §12 (Update Triggers). Subscribed to by FeedProjectionHandler to increment CommentCount.
/// </summary>
public sealed record CommentAdded(
    Guid CommentId,
    Guid PostId,
    Guid AuthorPersonId,
    string Content,
    DateTime CreatedAt) : DomainEvent;

/// <summary>
/// Domain event published when a Comment's content is changed.
/// </summary>
public sealed record CommentChanged(
    Guid CommentId,
    Guid PostId,
    Guid AuthorPersonId,
    DateTime ChangedAt) : DomainEvent;

/// <summary>
/// Domain event published when a Comment is hidden from normal discussion display.
/// </summary>
public sealed record CommentHidden(
    Guid CommentId,
    Guid PostId,
    Guid AuthorPersonId,
    DateTime HiddenAt) : DomainEvent;

/// <summary>
/// Domain event published when a Comment is restored to normal discussion display.
/// </summary>
public sealed record CommentShown(
    Guid CommentId,
    Guid PostId,
    Guid AuthorPersonId,
    DateTime ShownAt) : DomainEvent;

/// <summary>
/// Domain event published when a Reaction is added to a Post or Comment.
/// Architecture §12 (Update Triggers). Subscribed to by FeedProjectionHandler to update ReactionCountsJson.
/// </summary>
public sealed record ReactionAdded(
    Guid ReactionId,
    Guid? PostId,
    Guid? CommentId,
    Guid PersonId,
    string Type,
    DateTime CreatedAt) : DomainEvent;

/// <summary>
/// Domain event published when a Reaction is removed from a Post or Comment.
/// Architecture §12 (Update Triggers). Subscribed to by FeedProjectionHandler to update ReactionCountsJson.
/// </summary>
public sealed record ReactionRemoved(
    Guid ReactionId,
    Guid? PostId,
    Guid? CommentId,
    Guid PersonId,
    string Type,
    DateTime RemovedAt) : DomainEvent;

/// <summary>
/// Domain event published when a Post's visibility changes (VISIBLE ↔ HIDDEN).
/// Architecture §12 (Update Triggers). Subscribed to by FeedProjectionHandler to update Visibility.
/// </summary>
public sealed record PostVisibilityChanged(
    Guid PostId,
    string PreviousVisibility,
    string NewVisibility,
    Guid ChangedByPersonId,
    DateTime ChangedAt) : DomainEvent;

/// <summary>
/// Domain event published when a Post is archived.
/// post-domain.md Business Rule 28 (archiving preserves discussion history).
/// Subscribed to by FeedProjectionHandler to set Status = 'ARCHIVED'.
/// </summary>
public sealed record PostArchived(
    Guid PostId,
    Guid ArchivedByPersonId,
    DateTime ArchivedAt) : DomainEvent;