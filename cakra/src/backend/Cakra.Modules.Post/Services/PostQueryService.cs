using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Cakra.Modules.Post.Domain;
using Cakra.Modules.Product;
using Cakra.Modules.Request;
using Cakra.Modules.WorkPackage;
using Dapper;
using MediatR;

namespace Cakra.Modules.Post.Services;

/// <summary>
/// Dapper implementation of <see cref="IPostQueryService"/> executing explicit parameterized SQL
/// exclusively against the <c>post</c> schema (<c>post.Posts</c>, <c>post.Comments</c>,
/// <c>post.Reactions</c>, and <c>post.PostReferences</c>) and enriching cross-module display names
/// via published query interfaces (Architecture §7, §8, §15, §19.3, §20, §21 — SCR-POST-001).
/// </summary>
public sealed class PostQueryService :
    IPostQueryService,
    IRequestHandler<GetPostThreadDetailsQuery, PostThreadDetailsDto?>,
    IRequestHandler<GetFullCommentsQuery, IReadOnlyList<CommentDto>>,
    IRequestHandler<GetReactionListQuery, IReadOnlyList<ReactionDto>>
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IOrganizationQueryService? _organizationQueryService;
    private readonly ICustomerQueryService? _customerQueryService;
    private readonly IProductQueryService? _productQueryService;
    private readonly IRequestQueryService? _requestQueryService;
    private readonly IWorkPackageQueryService? _workPackageQueryService;

    public PostQueryService(
        IDbConnectionFactory connectionFactory,
        IOrganizationQueryService? organizationQueryService = null,
        ICustomerQueryService? customerQueryService = null,
        IProductQueryService? productQueryService = null,
        IRequestQueryService? requestQueryService = null,
        IWorkPackageQueryService? workPackageQueryService = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _organizationQueryService = organizationQueryService;
        _customerQueryService = customerQueryService;
        _productQueryService = productQueryService;
        _requestQueryService = requestQueryService;
        _workPackageQueryService = workPackageQueryService;
    }

    /// <inheritdoc />
    public async Task<PostThreadDetailsDto?> GetPostThreadDetailsAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        if (postId == Guid.Empty)
        {
            return null;
        }

        const string postSql = """
            SELECT
                p.[Id],
                p.[Title],
                p.[Content],
                p.[AuthorPersonId],
                p.[Source],
                p.[SourceEventType],
                p.[Status],
                p.[Visibility],
                p.[IsException],
                p.[ExceptionType],
                p.[CustomerId],
                p.[ProductId],
                p.[RequestId],
                p.[WorkPackageId],
                p.[ArchivedAt],
                p.[ArchivedByPersonId],
                p.[CreatedAt],
                p.[UpdatedAt]
            FROM [post].[Posts] p
            WHERE p.[Id] = @PostId;
            """;

        const string referencesSql = """
            SELECT
                pr.[Id],
                pr.[PostId],
                pr.[ReferenceType],
                pr.[ReferenceId],
                pr.[ReferenceDisplay],
                pr.[RemovedAt],
                pr.[CreatedAt],
                pr.[UpdatedAt]
            FROM [post].[PostReferences] pr
            WHERE pr.[PostId] = @PostId
              AND pr.[RemovedAt] IS NULL
            ORDER BY pr.[CreatedAt] ASC, pr.[Id] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();

        var postRow = await connection.QuerySingleOrDefaultAsync<PostQueryRow>(
            new CommandDefinition(postSql, new { PostId = postId }, cancellationToken: cancellationToken));

        if (postRow is null)
        {
            return null;
        }

        var referenceRows = (await connection.QueryAsync<PostReferenceQueryRow>(
            new CommandDefinition(referencesSql, new { PostId = postId }, cancellationToken: cancellationToken))).AsList();

        var comments = await GetFullCommentsAsync(postId, cancellationToken);
        var reactions = await GetReactionListAsync(postId, cancellationToken);

        return await BuildAndEnrichThreadDetailsAsync(
            postRow,
            referenceRows.Select(r => r.ToDto()).ToList(),
            comments,
            reactions,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CommentDto>> GetFullCommentsAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        if (postId == Guid.Empty)
        {
            return Array.Empty<CommentDto>();
        }

        const string commentsSql = """
            SELECT
                c.[Id],
                c.[PostId],
                c.[AuthorPersonId],
                c.[Content],
                c.[Status],
                c.[CreatedAt],
                c.[UpdatedAt]
            FROM [post].[Comments] c
            WHERE c.[PostId] = @PostId
              AND c.[Status] = @ActiveStatus
            ORDER BY c.[CreatedAt] ASC, c.[Id] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<CommentQueryRow>(
            new CommandDefinition(
                commentsSql,
                new
                {
                    PostId = postId,
                    ActiveStatus = Comment.StatusActive
                },
                cancellationToken: cancellationToken))).AsList();

        if (rows.Count == 0)
        {
            return Array.Empty<CommentDto>();
        }

        var authorNames = await ResolvePersonNamesAsync(
            rows.Select(r => r.AuthorPersonId),
            cancellationToken);

        return rows
            .Select(r => r.ToDto(authorNames.TryGetValue(r.AuthorPersonId, out var name) ? name : null))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReactionDto>> GetReactionListAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        if (postId == Guid.Empty)
        {
            return Array.Empty<ReactionDto>();
        }

        const string reactionsSql = """
            SELECT
                r.[Id],
                r.[PostId],
                r.[CommentId],
                r.[PersonId],
                r.[ReactionType],
                r.[IsActive],
                r.[RemovedAt],
                r.[CreatedAt],
                r.[UpdatedAt]
            FROM [post].[Reactions] r
            WHERE r.[PostId] = @PostId
              AND r.[IsActive] = 1
              AND r.[RemovedAt] IS NULL
            ORDER BY r.[CreatedAt] ASC, r.[Id] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<ReactionQueryRow>(
            new CommandDefinition(
                reactionsSql,
                new { PostId = postId },
                cancellationToken: cancellationToken))).AsList();

        if (rows.Count == 0)
        {
            return Array.Empty<ReactionDto>();
        }

        var personNames = await ResolvePersonNamesAsync(
            rows.Select(r => r.PersonId),
            cancellationToken);

        return rows
            .Select(r => r.ToDto(personNames.TryGetValue(r.PersonId, out var name) ? name : null))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<bool> PostExistsAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        if (postId == Guid.Empty)
        {
            return false;
        }

        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM [post].[Posts] WHERE [Id] = @PostId
            ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { PostId = postId }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PostThreadDetailsDto>> GetPostsByReferenceAsync(
        string referenceType,
        Guid referenceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(referenceType) || referenceId == Guid.Empty)
        {
            return Array.Empty<PostThreadDetailsDto>();
        }

        var normalizedType = PostReferenceTypes.NormalizeAndValidate(referenceType);

        const string postIdsSql = """
            SELECT DISTINCT p.[Id], p.[CreatedAt]
            FROM [post].[Posts] p
            LEFT JOIN [post].[PostReferences] pr
                ON pr.[PostId] = p.[Id] AND pr.[RemovedAt] IS NULL
            WHERE p.[Status] = @ActiveStatus
              AND p.[Visibility] = @VisibleVisibility
              AND (
                  (pr.[ReferenceType] = @ReferenceType AND pr.[ReferenceId] = @ReferenceId)
                  OR (@ReferenceType = 'REQUEST' AND p.[RequestId] = @ReferenceId)
                  OR (@ReferenceType = 'WORK_PACKAGE' AND p.[WorkPackageId] = @ReferenceId)
                  OR (@ReferenceType = 'CUSTOMER' AND p.[CustomerId] = @ReferenceId)
                  OR (@ReferenceType = 'PRODUCT' AND p.[ProductId] = @ReferenceId)
              )
            ORDER BY p.[CreatedAt] DESC, p.[Id] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var postIdRows = (await connection.QueryAsync<(Guid Id, DateTime CreatedAt)>(
            new CommandDefinition(
                postIdsSql,
                new
                {
                    ActiveStatus = PostStatusNames.Active,
                    VisibleVisibility = PostVisibilityNames.Visible,
                    ReferenceType = normalizedType,
                    ReferenceId = referenceId
                },
                cancellationToken: cancellationToken))).AsList();

        if (postIdRows.Count == 0)
        {
            return Array.Empty<PostThreadDetailsDto>();
        }

        var results = new List<PostThreadDetailsDto>(postIdRows.Count);
        foreach (var (id, _) in postIdRows)
        {
            var details = await GetPostThreadDetailsAsync(id, cancellationToken);
            if (details is not null)
            {
                results.Add(details);
            }
        }

        return results;
    }

    // MediatR query handler entry points
    public Task<PostThreadDetailsDto?> Handle(
        GetPostThreadDetailsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetPostThreadDetailsAsync(request.PostId, cancellationToken);
    }

    public Task<IReadOnlyList<CommentDto>> Handle(
        GetFullCommentsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetFullCommentsAsync(request.PostId, cancellationToken);
    }

    public Task<IReadOnlyList<ReactionDto>> Handle(
        GetReactionListQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetReactionListAsync(request.PostId, cancellationToken);
    }

    private async Task<PostThreadDetailsDto> BuildAndEnrichThreadDetailsAsync(
        PostQueryRow postRow,
        IReadOnlyList<PostReferenceDto> references,
        IReadOnlyList<CommentDto> comments,
        IReadOnlyList<ReactionDto> reactions,
        CancellationToken cancellationToken)
    {
        string? authorName = null;
        if (postRow.AuthorPersonId.HasValue && _organizationQueryService is not null)
        {
            var person = await _organizationQueryService.GetPersonByIdAsync(postRow.AuthorPersonId.Value, cancellationToken);
            authorName = person?.FullName;
        }

        if (string.IsNullOrWhiteSpace(authorName) &&
            string.Equals(postRow.Source, PostSourceNames.SystemGenerated, StringComparison.OrdinalIgnoreCase))
        {
            authorName = "SYSTEM";
        }

        string? customerName = null;
        string? customerCode = null;
        if (postRow.CustomerId.HasValue && _customerQueryService is not null)
        {
            var customer = await _customerQueryService.GetCustomerByIdAsync(postRow.CustomerId.Value, cancellationToken);
            customerName = customer?.CustomerName;
            customerCode = customer?.CustomerCode;
        }

        string? productName = null;
        string? productCode = null;
        if (postRow.ProductId.HasValue && _productQueryService is not null)
        {
            var product = await _productQueryService.GetProductByIdAsync(postRow.ProductId.Value, cancellationToken);
            productName = product?.Name;
            productCode = product?.Code;
        }

        string? requestTitle = null;
        if (postRow.RequestId.HasValue && _requestQueryService is not null)
        {
            var req = await _requestQueryService.GetRequestByIdAsync(postRow.RequestId.Value, cancellationToken);
            requestTitle = req?.Title;
        }

        string? workPackageName = null;
        if (postRow.WorkPackageId.HasValue && _workPackageQueryService is not null)
        {
            var wp = await _workPackageQueryService.GetWorkPackageByIdAsync(postRow.WorkPackageId.Value, cancellationToken);
            workPackageName = wp?.Name;
        }

        var primaryRef = ResolvePrimaryReference(references);
        var reactionCounts = reactions
            .Where(r => r.IsActive && r.RemovedAt is null)
            .GroupBy(r => r.ReactionType, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key.ToUpperInvariant(), g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return new PostThreadDetailsDto
        {
            Id = postRow.Id,
            Title = postRow.Title,
            Content = postRow.Content,
            AuthorPersonId = postRow.AuthorPersonId,
            AuthorName = authorName,
            Source = postRow.Source,
            SourceEventType = postRow.SourceEventType,
            Status = postRow.Status,
            Visibility = postRow.Visibility,
            IsException = postRow.IsException,
            ExceptionType = postRow.ExceptionType,
            CustomerId = postRow.CustomerId,
            CustomerName = customerName,
            CustomerCode = customerCode,
            ProductId = postRow.ProductId,
            ProductName = productName,
            ProductCode = productCode,
            RequestId = postRow.RequestId,
            RequestTitle = requestTitle,
            WorkPackageId = postRow.WorkPackageId,
            WorkPackageName = workPackageName,
            ReferenceType = primaryRef?.ReferenceType,
            ReferenceId = primaryRef?.ReferenceId,
            ReferenceDisplay = primaryRef?.ReferenceDisplay ?? requestTitle ?? workPackageName ?? customerName ?? productName,
            ArchivedAt = postRow.ArchivedAt,
            ArchivedByPersonId = postRow.ArchivedByPersonId,
            CreatedAt = postRow.CreatedAt,
            UpdatedAt = postRow.UpdatedAt,
            References = references,
            Comments = comments,
            Reactions = reactions,
            ReactionCounts = reactionCounts
        };
    }

    private async Task<Dictionary<Guid, string>> ResolvePersonNamesAsync(
        IEnumerable<Guid> personIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, string>();
        if (_organizationQueryService is null)
        {
            return result;
        }

        foreach (var personId in personIds.Where(id => id != Guid.Empty).Distinct())
        {
            var person = await _organizationQueryService.GetPersonByIdAsync(personId, cancellationToken);
            if (person is not null && !string.IsNullOrWhiteSpace(person.FullName))
            {
                result[personId] = person.FullName;
            }
        }

        return result;
    }

    private static PostReferenceDto? ResolvePrimaryReference(IReadOnlyList<PostReferenceDto> references)
    {
        if (references.Count == 0)
        {
            return null;
        }

        return references.FirstOrDefault(r => string.Equals(r.ReferenceType, PostReferenceTypes.Request, StringComparison.OrdinalIgnoreCase))
            ?? references.FirstOrDefault(r => string.Equals(r.ReferenceType, PostReferenceTypes.WorkPackage, StringComparison.OrdinalIgnoreCase))
            ?? references.FirstOrDefault(r => string.Equals(r.ReferenceType, PostReferenceTypes.Customer, StringComparison.OrdinalIgnoreCase))
            ?? references.FirstOrDefault(r => string.Equals(r.ReferenceType, PostReferenceTypes.Product, StringComparison.OrdinalIgnoreCase))
            ?? references[0];
    }

    private sealed class PostQueryRow
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
    }

    private sealed class PostReferenceQueryRow
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public string ReferenceType { get; set; } = string.Empty;
        public Guid ReferenceId { get; set; }
        public string? ReferenceDisplay { get; set; }
        public DateTime? RemovedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public PostReferenceDto ToDto() => new()
        {
            Id = Id,
            PostId = PostId,
            ReferenceType = ReferenceType,
            ReferenceId = ReferenceId,
            ReferenceDisplay = ReferenceDisplay,
            RemovedAt = RemovedAt,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }

    private sealed class CommentQueryRow
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public Guid AuthorPersonId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Status { get; set; } = Comment.StatusActive;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public CommentDto ToDto(string? authorName) => new()
        {
            Id = Id,
            PostId = PostId,
            AuthorPersonId = AuthorPersonId,
            AuthorName = authorName,
            Content = Content,
            Status = Status,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }

    private sealed class ReactionQueryRow
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

        public ReactionDto ToDto(string? personName) => new()
        {
            Id = Id,
            PostId = PostId,
            CommentId = CommentId,
            PersonId = PersonId,
            PersonName = personName,
            ReactionType = ReactionType,
            IsActive = IsActive && RemovedAt is null,
            RemovedAt = RemovedAt,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }
}
