namespace Cakra.Modules.Post.Services;

/// <summary>
/// Application command service contract for Post authoring, system post recording,
/// flat discussion commenting, structured operational reactions, visibility toggling,
/// and archiving (Architecture §6, §7, §8, §12, §15, §20, §21 — UC-FCOL-001..003, UC-COL-001).
/// </summary>
public interface IPostService
{

    /// <summary>
    /// Records a system-generated operational Post (e.g. triggered by an operational event or state transition)
    /// (Post Domain §5, §7 Rules 6-7; Architecture §7, §12).
    /// </summary>
    Task<PostThreadDetailsDto> RecordSystemPostAsync(
        string title,
        string content,
        Guid? authorPersonId = null,
        string? sourceEventType = null,
        Guid? customerId = null,
        Guid? productId = null,
        Guid? requestId = null,
        Guid? workPackageId = null,
        bool isException = false,
        string? exceptionType = null,
        IReadOnlyList<PostReferenceInput>? references = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="RecordSystemPostAsync"/> (Architecture §7).
    /// </summary>
    Task<PostThreadDetailsDto> RecordSystemPost(
        string title,
        string content,
        Guid? authorPersonId = null,
        string? sourceEventType = null,
        Guid? customerId = null,
        Guid? productId = null,
        Guid? requestId = null,
        Guid? workPackageId = null,
        bool isException = false,
        string? exceptionType = null,
        IReadOnlyList<PostReferenceInput>? references = null,
        CancellationToken cancellationToken = default)
        => RecordSystemPostAsync(
            title,
            content,
            authorPersonId,
            sourceEventType,
            customerId,
            productId,
            requestId,
            workPackageId,
            isException,
            exceptionType,
            references,
            cancellationToken);

    /// <summary>
    /// Adds a comment to an active, visible Post (UC-FCOL-001, FEAT-FCOL-001; Architecture §7, §8).
    /// </summary>
    Task<CommentDto> PostCommentAsync(
        Guid postId,
        string content,
        Guid? authorPersonId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="PostCommentAsync"/> (Architecture §7).
    /// </summary>
    Task<CommentDto> PostComment(
        Guid postId,
        string content,
        Guid? authorPersonId = null,
        CancellationToken cancellationToken = default)
        => PostCommentAsync(postId, content, authorPersonId, cancellationToken);

    /// <summary>
    /// Adds (or idempotently retains / reactivates) a structured operational reaction on an active, visible Post
    /// (UC-FCOL-002, FEAT-FCOL-002; Architecture §7, §8).
    /// </summary>
    Task<ReactionDto> AddReactionAsync(
        Guid postId,
        string reactionType,
        Guid? personId = null,
        Guid? commentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="AddReactionAsync"/> (Architecture §7).
    /// </summary>
    Task<ReactionDto> AddReaction(
        Guid postId,
        string reactionType,
        Guid? personId = null,
        Guid? commentId = null,
        CancellationToken cancellationToken = default)
        => AddReactionAsync(postId, reactionType, personId, commentId, cancellationToken);

    /// <summary>
    /// Soft-removes an active reaction from a Post without physical deletion
    /// (UC-FCOL-002, FEAT-FCOL-002; Architecture §7, §8, §20, §21 Permanent Data Retention).
    /// </summary>
    Task<PostThreadDetailsDto> RemoveReactionAsync(
        Guid postId,
        string reactionType,
        Guid? personId = null,
        Guid? commentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="RemoveReactionAsync"/> (Architecture §7).
    /// </summary>
    Task<PostThreadDetailsDto> RemoveReaction(
        Guid postId,
        string reactionType,
        Guid? personId = null,
        Guid? commentId = null,
        CancellationToken cancellationToken = default)
        => RemoveReactionAsync(postId, reactionType, personId, commentId, cancellationToken);

    /// <summary>
    /// Toggles a Post's visibility between <c>VISIBLE</c> and <c>HIDDEN</c> (or sets it to the specified
    /// <paramref name="visibility"/>) without altering its lifecycle status (Architecture §7, §12).
    /// </summary>
    Task<PostThreadDetailsDto> TogglePostVisibilityAsync(
        Guid postId,
        string? visibility = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="TogglePostVisibilityAsync"/> (Architecture §7).
    /// </summary>
    Task<PostThreadDetailsDto> TogglePostVisibility(
        Guid postId,
        string? visibility = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => TogglePostVisibilityAsync(postId, visibility, actorPersonId, cancellationToken);

    /// <summary>
    /// Archives an active Post (<c>ACTIVE -&gt; ARCHIVED</c>) while preserving its discussion history
    /// (Architecture §7, §12, §20, §21).
    /// </summary>
    Task<PostThreadDetailsDto> ArchivePostAsync(
        Guid postId,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="ArchivePostAsync"/> (Architecture §7).
    /// </summary>
    Task<PostThreadDetailsDto> ArchivePost(
        Guid postId,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => ArchivePostAsync(postId, actorPersonId, cancellationToken);
}
