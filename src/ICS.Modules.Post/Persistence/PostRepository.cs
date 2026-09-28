namespace ICS.Modules.Post.Persistence;

using System.Data;
using Dapper;
using ICS.Core.Data;
using ICS.Modules.Post.Application;
using ICS.Modules.Post.Domain;

/// <summary>
/// Dapper-based repository implementation for Post aggregate and associated entities.
/// Uses explicit parameterized SQL exclusively against post.* schema tables.
/// Architecture §12, §16, §17, §19.3, §20.
/// </summary>
internal sealed class PostRepository : IPostRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PostRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Post?> GetByIdAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT
                PostId,
                Title,
                Content,
                Source,
                Status,
                Visibility,
                AuthorPersonId,
                CreatedAt,
                UpdatedAt,
                ArchivedAt
            FROM [post].[Posts]
            WHERE PostId = @PostId;";

        var row = await connection.QuerySingleOrDefaultAsync<PostRow>(sql, new { PostId = postId });
        if (row is null)
        {
            return null;
        }

        // Load references
        const string refSql = @"
            SELECT
                ReferenceId,
                PostId,
                ReferenceType,
                TargetId,
                CreatedAt
            FROM [post].[PostReferences]
            WHERE PostId = @PostId;";

        var refRows = await connection.QueryAsync<PostReferenceRow>(refSql, new { PostId = postId });
        var references = refRows.Select(r => new PostReference(
            r.ReferenceId,
            r.PostId,
            r.ReferenceType,
            r.TargetId,
            r.CreatedAt)).ToList();

        return MapToAggregate(row, references);
    }

    public async Task AddAsync(Post post, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(post);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string insertPostSql = @"
            INSERT INTO [post].[Posts] (
                PostId,
                Title,
                Content,
                Source,
                Status,
                Visibility,
                AuthorPersonId,
                CreatedAt,
                UpdatedAt,
                ArchivedAt
            ) VALUES (
                @PostId,
                @Title,
                @Content,
                @Source,
                @Status,
                @Visibility,
                @AuthorPersonId,
                @CreatedAt,
                @UpdatedAt,
                @ArchivedAt
            );";

        await connection.ExecuteAsync(insertPostSql, new
        {
            PostId = post.Id,
            post.Title,
            post.Content,
            post.Source,
            post.Status,
            post.Visibility,
            AuthorPersonId = post.AuthorPersonId,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt,
            post.ArchivedAt
        }, transaction);

        if (post.References.Count > 0)
        {
            const string insertRefSql = @"
                INSERT INTO [post].[PostReferences] (
                    ReferenceId,
                    PostId,
                    ReferenceType,
                    TargetId,
                    CreatedAt
                ) VALUES (
                    @ReferenceId,
                    @PostId,
                    @ReferenceType,
                    @TargetId,
                    @CreatedAt
                );";

            foreach (var reference in post.References)
            {
                await connection.ExecuteAsync(insertRefSql, new
                {
                    ReferenceId = reference.Id,
                    ReferenceType = reference.ReferenceType,
                    TargetId = reference.TargetId,
                    PostId = post.Id,
                    CreatedAt = reference.CreatedAt
                }, transaction);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateAsync(Post post, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(post);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string updatePostSql = @"
            UPDATE [post].[Posts]
            SET
                Title = @Title,
                Content = @Content,
                Status = @Status,
                Visibility = @Visibility,
                UpdatedAt = @UpdatedAt,
                ArchivedAt = @ArchivedAt
            WHERE PostId = @PostId;";

        await connection.ExecuteAsync(updatePostSql, new
        {
            PostId = post.Id,
            post.Title,
            post.Content,
            post.Status,
            post.Visibility,
            UpdatedAt = post.UpdatedAt,
            post.ArchivedAt
        });
    }

    public async Task AddCommentAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comment);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO [post].[Comments] (
                CommentId,
                PostId,
                AuthorPersonId,
                Content,
                Status,
                CreatedAt,
                UpdatedAt
            ) VALUES (
                @CommentId,
                @PostId,
                @AuthorPersonId,
                @Content,
                @Status,
                @CreatedAt,
                @UpdatedAt
            );";

        await connection.ExecuteAsync(sql, new
        {
            CommentId = comment.Id,
            PostId = comment.PostId,
            AuthorPersonId = comment.AuthorPersonId,
            comment.Content,
            comment.Status,
            CreatedAt = comment.CreatedAt,
            comment.UpdatedAt
        });
    }

    public async Task UpdateCommentAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comment);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            UPDATE [post].[Comments]
            SET
                Content = @Content,
                Status = @Status,
                UpdatedAt = @UpdatedAt
            WHERE CommentId = @CommentId;";

        await connection.ExecuteAsync(sql, new
        {
            CommentId = comment.Id,
            comment.Content,
            comment.Status,
            comment.UpdatedAt
        });
    }

    public async Task AddReactionAsync(Reaction reaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reaction);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO [post].[Reactions] (
                ReactionId,
                PostId,
                CommentId,
                PersonId,
                Type,
                Status,
                CreatedAt
            ) VALUES (
                @ReactionId,
                @PostId,
                @CommentId,
                @PersonId,
                @Type,
                @Status,
                @CreatedAt
            );";

        await connection.ExecuteAsync(sql, new
        {
            ReactionId = reaction.Id,
            PostId = reaction.PostId,
            CommentId = reaction.CommentId,
            PersonId = reaction.PersonId,
            reaction.Type,
            reaction.Status,
            CreatedAt = reaction.CreatedAt
        });
    }

    public async Task RemoveReactionAsync(Reaction reaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reaction);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            UPDATE [post].[Reactions]
            SET Status = 'DELETED',
                UpdatedAt = @UpdatedAt
            WHERE ReactionId = @ReactionId;";

        await connection.ExecuteAsync(sql, new
        {
            ReactionId = reaction.Id,
            UpdatedAt = reaction.UpdatedAt
        });
    }

    public async Task<IReadOnlyList<PostCommentDto>> GetFullCommentsAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT
                c.CommentId,
                c.PostId,
                c.AuthorPersonId,
                c.Content,
                c.Status,
                c.CreatedAt,
                c.UpdatedAt
            FROM [post].[Comments] c
            WHERE c.PostId = @PostId
            ORDER BY c.CreatedAt ASC;";

        var commentRows = await connection.QueryAsync<CommentRow>(sql, new { PostId = postId });

        if (commentRows.Count() == 0)
        {
            return [];
        }

        var commentIds = commentRows.Select(r => r.CommentId).ToList();

        const string reactionSql = @"
            SELECT
                r.ReactionId,
                r.PostId,
                r.CommentId,
                r.PersonId,
                r.Type,
                r.Status,
                r.CreatedAt
            FROM [post].[Reactions] r
            WHERE r.CommentId IN @CommentIds
              AND r.Status = 'ACTIVE'
            ORDER BY r.CreatedAt ASC;";

        var reactionRows = await connection.QueryAsync<ReactionRow>(
            reactionSql,
            new { CommentIds = commentIds },
            commandTimeout: 30);

        var reactionsByComment = reactionRows
            .GroupBy(r => r.CommentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var comments = commentRows.Select(row => new PostCommentDto(
            row.CommentId,
            row.PostId,
            row.AuthorPersonId,
            row.Content,
            row.Status,
            row.CreatedAt,
            row.UpdatedAt,
            reactionsByComment.TryGetValue(row.CommentId, out var reactions)
                ? reactions.Select(r => new PostReactionDto(
                    r.ReactionId!.Value,
                    r.PostId!.Value,
                    r.CommentId,
                    r.PersonId,
                    r.Type,
                    r.CreatedAt)).ToList()
                : [])).ToList();

        return comments;
    }

    public async Task<IReadOnlyList<PostReactionDto>> GetReactionListAsync(
        Guid postId,
        Guid? commentId = null,
        string? reactionType = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("PostId", postId);
        parameters.Add("CommentId", commentId);
        parameters.Add("Type", reactionType);

        var sql = @"
            SELECT
                ReactionId,
                PostId,
                CommentId,
                PersonId,
                Type,
                CreatedAt
            FROM [post].[Reactions]
            WHERE PostId = @PostId";

        if (commentId.HasValue)
        {
            sql += " AND CommentId = @CommentId";
        }
        else
        {
            sql += " AND PostId = @PostId AND CommentId IS NULL";
        }

        if (!string.IsNullOrWhiteSpace(reactionType))
        {
            sql += " AND Type = @Type";
        }

        sql += " AND Status = 'ACTIVE' ORDER BY CreatedAt ASC;";

        var rows = await connection.QueryAsync<ReactionRow>(sql, parameters);

        return rows.Select(r => new PostReactionDto(
            r.ReactionId!.Value,
            r.PostId!.Value,
            r.CommentId,
            r.PersonId,
            r.Type,
            r.CreatedAt)).ToList();
    }

    private static Post MapToAggregate(PostRow row, List<PostReference> references)
    {
        return new Post(
            row.PostId,
            row.Title,
            row.Content,
            row.Source,
            row.AuthorPersonId,
            row.Status,
            row.Visibility,
            row.CreatedAt,
            row.UpdatedAt,
            row.ArchivedAt);
    }

    private sealed class PostRow
    {
        public Guid PostId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Visibility { get; set; } = string.Empty;
        public Guid? AuthorPersonId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ArchivedAt { get; set; }
    }

    private sealed class PostReferenceRow
    {
        public Guid ReferenceId { get; set; }
        public Guid PostId { get; set; }
        public string ReferenceType { get; set; } = string.Empty;
        public Guid TargetId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class CommentRow
    {
        public Guid CommentId { get; set; }
        public Guid PostId { get; set; }
        public Guid AuthorPersonId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    private sealed class ReactionRow
    {
        public Guid? ReactionId { get; set; }
        public Guid? PostId { get; set; }
        public Guid? CommentId { get; set; }
        public Guid PersonId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}