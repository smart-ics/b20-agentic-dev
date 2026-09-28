namespace Cakra.Modules.Post;

/// <summary>
/// Published cross-module query contract for Post thread details, full discussion comments,
/// and active reaction lists (Architecture §7, §8, §15, §20, §21 — SCR-POST-001).
/// Internal repositories are not exposed outside the Post module boundary.
/// </summary>
public interface IPostQueryService
{
    /// <summary>
    /// Retrieves full thread details for a Post, including active references, ordered comments,
    /// active reactions, and aggregated reaction counts, or <c>null</c> if not found (Architecture §7).
    /// </summary>
    Task<PostThreadDetailsDto?> GetPostThreadDetailsAsync(
        Guid postId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="GetPostThreadDetailsAsync"/> (Architecture §7).
    /// </summary>
    Task<PostThreadDetailsDto?> GetPostThreadDetails(
        Guid postId,
        CancellationToken cancellationToken = default)
        => GetPostThreadDetailsAsync(postId, cancellationToken);

    /// <summary>
    /// Retrieves all active comments for the specified Post in chronological order (Architecture §7).
    /// </summary>
    Task<IReadOnlyList<CommentDto>> GetFullCommentsAsync(
        Guid postId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="GetFullCommentsAsync"/> (Architecture §7).
    /// </summary>
    Task<IReadOnlyList<CommentDto>> GetFullComments(
        Guid postId,
        CancellationToken cancellationToken = default)
        => GetFullCommentsAsync(postId, cancellationToken);

    /// <summary>
    /// Retrieves all active (non-removed) reactions for the specified Post in chronological order (Architecture §7).
    /// </summary>
    Task<IReadOnlyList<ReactionDto>> GetReactionListAsync(
        Guid postId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="GetReactionListAsync"/> (Architecture §7).
    /// </summary>
    Task<IReadOnlyList<ReactionDto>> GetReactionList(
        Guid postId,
        CancellationToken cancellationToken = default)
        => GetReactionListAsync(postId, cancellationToken);

    /// <summary>
    /// Checks whether a Post with the specified identifier exists in <c>post.Posts</c>.
    /// </summary>
    Task<bool> PostExistsAsync(
        Guid postId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous convenience overload for <see cref="PostExistsAsync"/>.
    /// </summary>
    bool PostExists(Guid postId)
        => PostExistsAsync(postId, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Retrieves active posts associated with a given contextual reference (<c>REQUEST</c>, <c>WORK_PACKAGE</c>,
    /// <c>CUSTOMER</c>, or <c>PRODUCT</c>) ordered by creation timestamp descending (UC-COL-001).
    /// </summary>
    Task<IReadOnlyList<PostThreadDetailsDto>> GetPostsByReferenceAsync(
        string referenceType,
        Guid referenceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="GetPostsByReferenceAsync"/>.
    /// </summary>
    Task<IReadOnlyList<PostThreadDetailsDto>> GetPostsByReference(
        string referenceType,
        Guid referenceId,
        CancellationToken cancellationToken = default)
        => GetPostsByReferenceAsync(referenceType, referenceId, cancellationToken);
}
