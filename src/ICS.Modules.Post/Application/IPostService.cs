namespace ICS.Modules.Post.Application;

/// <summary>
/// Application service contract for executing Post lifecycle commands.
/// Each command is dispatched through the MediatR pipeline and handled by a dedicated handler.
/// Architecture §7, §12, §19.2.
/// </summary>
public interface IPostService
{
    /// <summary>
    /// Creates a new human-authored operational Post with optional cross-domain references.
    /// Validates author and all references against their owning modules.
    /// Emits <see cref="PostCreated"/> domain event.
    /// </summary>
    Task<PostThreadDto> CreateOperationalPostAsync(
        string title,
        string content,
        Guid authorPersonId,
        IReadOnlyList<(string ReferenceType, Guid TargetId)>? references = null,
        Guid? postId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a system-generated Post in response to an operational event or state change.
    /// Emits <see cref="PostCreated"/> domain event.
    /// </summary>
    Task<PostThreadDto> RecordSystemPostAsync(
        string title,
        string content,
        string sourceEventOrObject,
        Guid? postId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a Comment to a Post discussion.
    /// Emits <see cref="CommentAdded"/> domain event.
    /// </summary>
    Task<PostThreadDto> PostCommentAsync(
        Guid postId,
        string content,
        Guid authorPersonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a Reaction to a Post.
    /// Emits <see cref="ReactionAdded"/> domain event.
    /// </summary>
    Task<PostThreadDto> AddReactionAsync(
        Guid postId,
        Guid personId,
        string reactionType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a Reaction from a Post.
    /// Emits <see cref="ReactionRemoved"/> domain event.
    /// </summary>
    Task<PostThreadDto> RemoveReactionAsync(
        Guid postId,
        Guid personId,
        string reactionType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a Post's visibility (VISIBLE ↔ HIDDEN).
    /// Emits <see cref="PostVisibilityChanged"/> domain event.
    /// </summary>
    Task<PostThreadDto> TogglePostVisibilityAsync(
        Guid postId,
        string newVisibility,
        Guid changedByPersonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives a Post (soft-delete, preserves discussion history).
    /// Emits <see cref="PostArchived"/> domain event.
    /// post-domain.md Business Rule 28 (Archiving does not delete the Post).
    /// </summary>
    Task<PostThreadDto> ArchivePostAsync(
        Guid postId,
        Guid archivedByPersonId,
        CancellationToken cancellationToken = default);
}