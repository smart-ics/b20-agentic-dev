using System.Data;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Post.Domain;
using Dapper;

namespace Cakra.Modules.Post.Persistence;

/// <summary>
/// Dapper implementation of <see cref="IPostRepository"/> executing explicit parameterized SQL
/// exclusively against <c>post.Posts</c>, <c>post.Comments</c>, <c>post.Reactions</c>, and
/// <c>post.PostReferences</c> (Architecture §6, §7, §17, §19.3, §20, §21).
/// Strictly enforces Permanent Data Retention (§20, §21): zero physical <c>DELETE</c> statements.
/// </summary>
internal sealed class PostRepository : IPostRepository
{
    static PostRepository()
    {
        SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2);
        SqlMapper.AddTypeMap(typeof(DateTime?), DbType.DateTime2);
    }

    private readonly IDbConnectionFactory _connectionFactory;

    public PostRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Domain.Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        const string postSql = """
            SELECT
                [Id],
                [Title],
                [Content],
                [AuthorPersonId],
                [Source],
                [SourceEventType],
                [Status],
                [Visibility],
                [IsException],
                [ExceptionType],
                [CustomerId],
                [ProductId],
                [RequestId],
                [WorkPackageId],
                [ArchivedAt],
                [ArchivedByPersonId],
                [CreatedAt],
                [UpdatedAt]
            FROM [post].[Posts]
            WHERE [Id] = @Id;
            """;

        const string referencesSql = """
            SELECT
                [Id],
                [PostId],
                [ReferenceType],
                [ReferenceId],
                [ReferenceDisplay],
                [RemovedAt],
                [CreatedAt],
                [UpdatedAt]
            FROM [post].[PostReferences]
            WHERE [PostId] = @PostId
            ORDER BY [CreatedAt] ASC, [Id] ASC;
            """;

        const string commentsSql = """
            SELECT
                [Id],
                [PostId],
                [AuthorPersonId],
                [Content],
                [Status],
                [CreatedAt],
                [UpdatedAt]
            FROM [post].[Comments]
            WHERE [PostId] = @PostId
            ORDER BY [CreatedAt] ASC, [Id] ASC;
            """;

        const string reactionsSql = """
            SELECT
                [Id],
                [PostId],
                [CommentId],
                [PersonId],
                [ReactionType],
                [IsActive],
                [RemovedAt],
                [CreatedAt],
                [UpdatedAt]
            FROM [post].[Reactions]
            WHERE [PostId] = @PostId
            ORDER BY [CreatedAt] ASC, [Id] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();

        var postRow = await connection.QuerySingleOrDefaultAsync<PostRow>(
            new CommandDefinition(postSql, new { Id = id }, cancellationToken: cancellationToken));

        if (postRow is null)
        {
            return null;
        }

        var referenceRows = await connection.QueryAsync<PostReferenceRow>(
            new CommandDefinition(referencesSql, new { PostId = id }, cancellationToken: cancellationToken));

        var commentRows = await connection.QueryAsync<CommentRow>(
            new CommandDefinition(commentsSql, new { PostId = id }, cancellationToken: cancellationToken));

        var reactionRows = await connection.QueryAsync<ReactionRow>(
            new CommandDefinition(reactionsSql, new { PostId = id }, cancellationToken: cancellationToken));

        return postRow.ToDomain(
            referenceRows.Select(r => r.ToDomain()),
            commentRows.Select(c => c.ToDomain()),
            reactionRows.Select(r => r.ToDomain()));
    }

    public async Task<IReadOnlyList<Domain.Post>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string postsSql = """
            SELECT
                [Id],
                [Title],
                [Content],
                [AuthorPersonId],
                [Source],
                [SourceEventType],
                [Status],
                [Visibility],
                [IsException],
                [ExceptionType],
                [CustomerId],
                [ProductId],
                [RequestId],
                [WorkPackageId],
                [ArchivedAt],
                [ArchivedByPersonId],
                [CreatedAt],
                [UpdatedAt]
            FROM [post].[Posts]
            ORDER BY [CreatedAt] DESC, [Id] ASC;
            """;

        const string referencesSql = """
            SELECT
                [Id],
                [PostId],
                [ReferenceType],
                [ReferenceId],
                [ReferenceDisplay],
                [RemovedAt],
                [CreatedAt],
                [UpdatedAt]
            FROM [post].[PostReferences]
            ORDER BY [CreatedAt] ASC, [Id] ASC;
            """;

        const string commentsSql = """
            SELECT
                [Id],
                [PostId],
                [AuthorPersonId],
                [Content],
                [Status],
                [CreatedAt],
                [UpdatedAt]
            FROM [post].[Comments]
            ORDER BY [CreatedAt] ASC, [Id] ASC;
            """;

        const string reactionsSql = """
            SELECT
                [Id],
                [PostId],
                [CommentId],
                [PersonId],
                [ReactionType],
                [IsActive],
                [RemovedAt],
                [CreatedAt],
                [UpdatedAt]
            FROM [post].[Reactions]
            ORDER BY [CreatedAt] ASC, [Id] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();

        var postRows = (await connection.QueryAsync<PostRow>(
            new CommandDefinition(postsSql, cancellationToken: cancellationToken))).AsList();

        if (postRows.Count == 0)
        {
            return Array.Empty<Domain.Post>();
        }

        var referenceRows = await connection.QueryAsync<PostReferenceRow>(
            new CommandDefinition(referencesSql, cancellationToken: cancellationToken));

        var commentRows = await connection.QueryAsync<CommentRow>(
            new CommandDefinition(commentsSql, cancellationToken: cancellationToken));

        var reactionRows = await connection.QueryAsync<ReactionRow>(
            new CommandDefinition(reactionsSql, cancellationToken: cancellationToken));

        var referencesByPost = referenceRows
            .GroupBy(r => r.PostId)
            .ToDictionary(g => g.Key, g => (IEnumerable<PostReference>)g.Select(r => r.ToDomain()).ToList());

        var commentsByPost = commentRows
            .GroupBy(c => c.PostId)
            .ToDictionary(g => g.Key, g => (IEnumerable<Comment>)g.Select(c => c.ToDomain()).ToList());

        var reactionsByPost = reactionRows
            .GroupBy(r => r.PostId)
            .ToDictionary(g => g.Key, g => (IEnumerable<Reaction>)g.Select(r => r.ToDomain()).ToList());

        return postRows
            .Select(p => p.ToDomain(
                referencesByPost.TryGetValue(p.Id, out var refs) ? refs : null,
                commentsByPost.TryGetValue(p.Id, out var comments) ? comments : null,
                reactionsByPost.TryGetValue(p.Id, out var reactions) ? reactions : null))
            .ToList();
    }

    public async Task<IReadOnlyList<Domain.Post>> GetByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            return Array.Empty<Domain.Post>();
        }

        const string postsSql = """
            SELECT
                [Id],
                [Title],
                [Content],
                [AuthorPersonId],
                [Source],
                [SourceEventType],
                [Status],
                [Visibility],
                [IsException],
                [ExceptionType],
                [CustomerId],
                [ProductId],
                [RequestId],
                [WorkPackageId],
                [ArchivedAt],
                [ArchivedByPersonId],
                [CreatedAt],
                [UpdatedAt]
            FROM [post].[Posts]
            WHERE [RequestId] = @RequestId
            ORDER BY [CreatedAt] ASC, [Id] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();

        var postRows = (await connection.QueryAsync<PostRow>(
            new CommandDefinition(postsSql, new { RequestId = requestId }, cancellationToken: cancellationToken))).AsList();

        if (postRows.Count == 0)
        {
            return Array.Empty<Domain.Post>();
        }

        return postRows
            .Select(p => p.ToDomain())
            .ToList();
    }

    public async Task AddAsync(Domain.Post entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        if (entity.CreatedAt == default)
        {
            entity.CreatedAt = DateTime.UtcNow;
        }

        const string insertPostSql = """
            INSERT INTO [post].[Posts] (
                [Id],
                [Title],
                [Content],
                [AuthorPersonId],
                [Source],
                [SourceEventType],
                [Status],
                [Visibility],
                [IsException],
                [ExceptionType],
                [CustomerId],
                [ProductId],
                [RequestId],
                [WorkPackageId],
                [ArchivedAt],
                [ArchivedByPersonId],
                [CreatedAt],
                [UpdatedAt]
            ) VALUES (
                @Id,
                @Title,
                @Content,
                @AuthorPersonId,
                @Source,
                @SourceEventType,
                @Status,
                @Visibility,
                @IsException,
                @ExceptionType,
                @CustomerId,
                @ProductId,
                @RequestId,
                @WorkPackageId,
                @ArchivedAt,
                @ArchivedByPersonId,
                @CreatedAt,
                @UpdatedAt
            );
            """;

        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition(
            insertPostSql,
            new
            {
                entity.Id,
                entity.Title,
                entity.Content,
                entity.AuthorPersonId,
                Source = entity.SourceName,
                entity.SourceEventType,
                Status = entity.StatusName,
                Visibility = entity.VisibilityName,
                entity.IsException,
                entity.ExceptionType,
                entity.CustomerId,
                entity.ProductId,
                entity.RequestId,
                entity.WorkPackageId,
                entity.ArchivedAt,
                entity.ArchivedByPersonId,
                entity.CreatedAt,
                entity.UpdatedAt
            },
            cancellationToken: cancellationToken));

        foreach (var reference in entity.References)
        {
            await AddReferenceAsync(reference, cancellationToken);
        }

        foreach (var comment in entity.Comments)
        {
            await AddCommentAsync(comment, entity.UpdatedAt ?? entity.CreatedAt, cancellationToken);
        }

        foreach (var reaction in entity.Reactions)
        {
            await AddReactionAsync(reaction, entity.UpdatedAt ?? entity.CreatedAt, cancellationToken);
        }
    }

    public async Task UpdateAsync(Domain.Post entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt ??= DateTime.UtcNow;

        const string updatePostSql = """
            UPDATE [post].[Posts]
            SET
                [Title] = @Title,
                [Content] = @Content,
                [Status] = @Status,
                [Visibility] = @Visibility,
                [IsException] = @IsException,
                [ExceptionType] = @ExceptionType,
                [CustomerId] = @CustomerId,
                [ProductId] = @ProductId,
                [RequestId] = @RequestId,
                [WorkPackageId] = @WorkPackageId,
                [ArchivedAt] = @ArchivedAt,
                [ArchivedByPersonId] = @ArchivedByPersonId,
                [UpdatedAt] = @UpdatedAt
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            updatePostSql,
            new
            {
                entity.Id,
                entity.Title,
                entity.Content,
                Status = entity.StatusName,
                Visibility = entity.VisibilityName,
                entity.IsException,
                entity.ExceptionType,
                entity.CustomerId,
                entity.ProductId,
                entity.RequestId,
                entity.WorkPackageId,
                entity.ArchivedAt,
                entity.ArchivedByPersonId,
                entity.UpdatedAt
            },
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Soft-archives the Post to comply with Architecture §20 and §21 (Permanent Data Retention — no physical purge).
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return;
        }

        var now = DateTime.UtcNow;
        const string archiveSql = """
            UPDATE [post].[Posts]
            SET
                [Status] = @Status,
                [ArchivedAt] = COALESCE([ArchivedAt], @Now),
                [UpdatedAt] = @Now
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            archiveSql,
            new
            {
                Id = id,
                Status = PostStatusNames.Archived,
                Now = now
            },
            cancellationToken: cancellationToken));
    }

    public async Task AddCommentAsync(
        Comment comment,
        DateTime postUpdatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comment);

        if (comment.Id == Guid.Empty)
        {
            comment.Id = Guid.NewGuid();
        }

        if (comment.CreatedAt == default)
        {
            comment.CreatedAt = postUpdatedAtUtc;
        }

        const string insertCommentSql = """
            INSERT INTO [post].[Comments] (
                [Id],
                [PostId],
                [AuthorPersonId],
                [Content],
                [Status],
                [CreatedAt],
                [UpdatedAt]
            ) VALUES (
                @Id,
                @PostId,
                @AuthorPersonId,
                @Content,
                @Status,
                @CreatedAt,
                @UpdatedAt
            );

            UPDATE [post].[Posts]
            SET [UpdatedAt] = @PostUpdatedAt
            WHERE [Id] = @PostId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            insertCommentSql,
            new
            {
                comment.Id,
                comment.PostId,
                comment.AuthorPersonId,
                comment.Content,
                comment.Status,
                comment.CreatedAt,
                comment.UpdatedAt,
                PostUpdatedAt = postUpdatedAtUtc
            },
            cancellationToken: cancellationToken));
    }

    public async Task AddReactionAsync(
        Reaction reaction,
        DateTime postUpdatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reaction);

        if (reaction.Id == Guid.Empty)
        {
            reaction.Id = Guid.NewGuid();
        }

        if (reaction.CreatedAt == default)
        {
            reaction.CreatedAt = postUpdatedAtUtc;
        }

        const string insertReactionSql = """
            INSERT INTO [post].[Reactions] (
                [Id],
                [PostId],
                [CommentId],
                [PersonId],
                [ReactionType],
                [IsActive],
                [RemovedAt],
                [CreatedAt],
                [UpdatedAt]
            ) VALUES (
                @Id,
                @PostId,
                @CommentId,
                @PersonId,
                @ReactionType,
                @IsActive,
                @RemovedAt,
                @CreatedAt,
                @UpdatedAt
            );

            UPDATE [post].[Posts]
            SET [UpdatedAt] = @PostUpdatedAt
            WHERE [Id] = @PostId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            insertReactionSql,
            new
            {
                reaction.Id,
                reaction.PostId,
                reaction.CommentId,
                reaction.PersonId,
                reaction.ReactionType,
                reaction.IsActive,
                reaction.RemovedAt,
                reaction.CreatedAt,
                reaction.UpdatedAt,
                PostUpdatedAt = postUpdatedAtUtc
            },
            cancellationToken: cancellationToken));
    }

    public async Task UpdateReactionAsync(
        Reaction reaction,
        DateTime postUpdatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reaction);

        reaction.UpdatedAt ??= postUpdatedAtUtc;

        const string updateReactionSql = """
            UPDATE [post].[Reactions]
            SET
                [IsActive] = @IsActive,
                [RemovedAt] = @RemovedAt,
                [UpdatedAt] = @UpdatedAt
            WHERE [Id] = @Id;

            UPDATE [post].[Posts]
            SET [UpdatedAt] = @PostUpdatedAt
            WHERE [Id] = @PostId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            updateReactionSql,
            new
            {
                reaction.Id,
                reaction.PostId,
                reaction.IsActive,
                reaction.RemovedAt,
                reaction.UpdatedAt,
                PostUpdatedAt = postUpdatedAtUtc
            },
            cancellationToken: cancellationToken));
    }

    public async Task AddReferenceAsync(
        PostReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (reference.Id == Guid.Empty)
        {
            reference.Id = Guid.NewGuid();
        }

        if (reference.CreatedAt == default)
        {
            reference.CreatedAt = DateTime.UtcNow;
        }

        const string insertRefSql = """
            INSERT INTO [post].[PostReferences] (
                [Id],
                [PostId],
                [ReferenceType],
                [ReferenceId],
                [ReferenceDisplay],
                [RemovedAt],
                [CreatedAt],
                [UpdatedAt]
            ) VALUES (
                @Id,
                @PostId,
                @ReferenceType,
                @ReferenceId,
                @ReferenceDisplay,
                @RemovedAt,
                @CreatedAt,
                @UpdatedAt
            );
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            insertRefSql,
            new
            {
                reference.Id,
                reference.PostId,
                reference.ReferenceType,
                reference.ReferenceId,
                reference.ReferenceDisplay,
                reference.RemovedAt,
                reference.CreatedAt,
                reference.UpdatedAt
            },
            cancellationToken: cancellationToken));
    }

    private sealed class PostRow
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public Guid? AuthorPersonId { get; set; }
        public string Source { get; set; } = PostSourceNames.HumanAuthored;
        public string? SourceEventType { get; set; }
        public string Status { get; set; } = PostStatusNames.Active;
        public string Visibility { get; set; } = PostVisibilityNames.Visible;
        public bool IsException { get; set; }
        public string? ExceptionType { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? ProductId { get; set; }
        public Guid? RequestId { get; set; }
        public Guid? WorkPackageId { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public Guid? ArchivedByPersonId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Domain.Post ToDomain(
            IEnumerable<PostReference>? references = null,
            IEnumerable<Comment>? comments = null,
            IEnumerable<Reaction>? reactions = null)
        {
            return Domain.Post.Rehydrate(
                id: Id,
                title: Title,
                content: Content,
                authorPersonId: AuthorPersonId,
                source: Source,
                sourceEventType: SourceEventType,
                status: Status,
                visibility: Visibility,
                isException: IsException,
                exceptionType: ExceptionType,
                customerId: CustomerId,
                productId: ProductId,
                requestId: RequestId,
                workPackageId: WorkPackageId,
                archivedAt: ArchivedAt,
                archivedByPersonId: ArchivedByPersonId,
                createdAt: CreatedAt,
                updatedAt: UpdatedAt,
                references: references,
                comments: comments,
                reactions: reactions);
        }
    }

    private sealed class PostReferenceRow
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public string ReferenceType { get; set; } = string.Empty;
        public Guid ReferenceId { get; set; }
        public string? ReferenceDisplay { get; set; }
        public DateTime? RemovedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public PostReference ToDomain() => PostReference.Rehydrate(
            id: Id,
            postId: PostId,
            referenceType: ReferenceType,
            referenceId: ReferenceId,
            referenceDisplay: ReferenceDisplay,
            removedAt: RemovedAt,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt);
    }

    private sealed class CommentRow
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public Guid AuthorPersonId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Status { get; set; } = Comment.StatusActive;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Comment ToDomain() => Comment.Rehydrate(
            id: Id,
            postId: PostId,
            authorPersonId: AuthorPersonId,
            content: Content,
            status: Status,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt);
    }

    private sealed class ReactionRow
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public Guid? CommentId { get; set; }
        public Guid PersonId { get; set; }
        public string ReactionType { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime? RemovedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Reaction ToDomain() => Reaction.Rehydrate(
            id: Id,
            postId: PostId,
            commentId: CommentId,
            personId: PersonId,
            reactionType: ReactionType,
            isActive: IsActive,
            removedAt: RemovedAt,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt);
    }
}
