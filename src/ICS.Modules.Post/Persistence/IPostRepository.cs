namespace ICS.Modules.Post.Persistence;

using ICS.Modules.Post.Domain;
using ICS.Modules.Post.Application;

/// <summary>
/// Repository contract for the Post aggregate using explicit parameterized Dapper SQL.
/// Public only so the module's public <see cref="IPostQueryService"/> can consume it;
/// it is a module implementation detail and is not a cross-module contract — other modules
/// must use <see cref="IPostQueryService"/> instead.
/// Architecture §12, §16, §17, §19.3, §20.
/// </summary>
public interface IPostRepository
{
    Task<Post?> GetByIdAsync(Guid postId, CancellationToken cancellationToken = default);

    Task AddAsync(Post post, CancellationToken cancellationToken = default);

    Task UpdateAsync(Post post, CancellationToken cancellationToken = default);

    Task AddCommentAsync(Comment comment, CancellationToken cancellationToken = default);

    Task UpdateCommentAsync(Comment comment, CancellationToken cancellationToken = default);

    Task AddReactionAsync(Reaction reaction, CancellationToken cancellationToken = default);

    Task RemoveReactionAsync(Reaction reaction, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PostCommentDto>> GetFullCommentsAsync(
        Guid postId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PostReactionDto>> GetReactionListAsync(
        Guid postId,
        Guid? commentId = null,
        string? reactionType = null,
        CancellationToken cancellationToken = default);
}