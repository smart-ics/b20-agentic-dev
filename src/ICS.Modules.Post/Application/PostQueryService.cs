namespace ICS.Modules.Post.Application;

using Dapper;
using ICS.Modules.Post.Domain;
using ICS.Modules.Post.Domain.Events;
using ICS.Modules.Post.Persistence;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Dapper-backed query service for the Post module.
/// Reads exclusively from post.* schema tables and resolves reference names
/// through the owning modules' published query services.
/// Architecture §7, §15, §19.3, §20.
/// </summary>
public sealed class PostQueryService : IPostQueryService
{
    private readonly IPostRepository _repository;
    private readonly ILogger<PostQueryService> _logger;

    public PostQueryService(IPostRepository repository, ILogger<PostQueryService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PostThreadDto?> GetPostThreadDetailsAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var post = await _repository.GetByIdAsync(postId, cancellationToken);
        if (post is null)
        {
            return null;
        }

        var comments = await _repository.GetFullCommentsAsync(postId, cancellationToken);
        var reactions = await _repository.GetReactionListAsync(postId, cancellationToken: cancellationToken);

        var dto = new PostThreadDto(
            post.Id,
            post.Title,
            post.Content,
            post.Source,
            post.Status,
            post.Visibility,
            post.AuthorPersonId,
            post.CreatedAt,
            post.UpdatedAt,
            post.ArchivedAt,
            comments,
            reactions);

        return dto;
    }

    public async Task<IReadOnlyList<PostCommentDto>> GetFullCommentsAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetFullCommentsAsync(postId, cancellationToken);
    }

    public async Task<IReadOnlyList<PostReactionDto>> GetReactionListAsync(
        Guid postId,
        Guid? commentId = null,
        string? reactionType = null,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetReactionListAsync(postId, commentId, reactionType, cancellationToken);
    }
}