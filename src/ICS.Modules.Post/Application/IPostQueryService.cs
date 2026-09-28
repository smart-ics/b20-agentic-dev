namespace ICS.Modules.Post.Application;

/// <summary>
/// In-process query contract exposed by the Post module for cross-module integration and UI queries.
/// Architecture §7, §15; post-domain.md §5.
/// </summary>
public interface IPostQueryService
{
    /// <summary>
    /// Gets a Post thread by identifier including its comments and reactions.
    /// </summary>
    Task<PostThreadDto?> GetPostThreadDetailsAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all comments for a Post with their reaction counts.
    /// </summary>
    Task<IReadOnlyList<PostCommentDto>> GetFullCommentsAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all reactions for a Post or a specific Comment, filtered by reaction type.
    /// </summary>
    Task<IReadOnlyList<PostReactionDto>> GetReactionListAsync(
        Guid postId,
        Guid? commentId = null,
        string? reactionType = null,
        CancellationToken cancellationToken = default);
}