using System.Data;
using System.Diagnostics;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Post.Domain;
using Dapper;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cakra.Modules.Post.Services;

/// <summary>
/// High-performance Dapper query service executing single-table indexed queries over the
/// <c>post.FeedItems</c> materialized read model (Architecture §6, §7, §8, §9, §12, §19.2, §19.3, §20, §21; CR-014 §4, §5).
/// Supports <c>UC-FCOL-004</c> (Navigate from Post to Request), <c>UC-FCOL-005</c> (Filter Operational Feed),
/// <c>UC-AWR-001</c> (Observe Operational Feed), <c>UC-AWR-002</c> (Discover Request via Feed), and
/// <c>UC-AWR-003</c> (Monitor Operational Exceptions via Feed) with sub-50ms target latency and zero cross-schema JOINs.
/// Supports universal search compilation with Prefix-AND syntax and dual-mode FTS execution with graceful parameterized LIKE fallback.
/// </summary>
public sealed class FeedQueryService :
    IFeedQueryService,
    IRequestHandler<GetFeedQuery, FeedPageResultDto>,
    IRequestHandler<GetFeedItemByPostIdQuery, FeedItemDto?>,
    IRequestHandler<GetFeedItemByIdQuery, FeedItemDto?>
{
    static FeedQueryService()
    {
        SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2);
        SqlMapper.AddTypeMap(typeof(DateTime?), DbType.DateTime2);
    }

    private static readonly HashSet<string> FtsReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "AND", "OR", "NOT", "NEAR", "FORMSOF", "INFLECTIONAL", "THESAURUS", "ISABOUT", "WEIGHT"
    };

    private static bool? _isFtsAvailable;
    private static readonly SemaphoreSlim _ftsCheckLock = new(1, 1);

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<FeedQueryService> _logger;

    public FeedQueryService(
        IDbConnectionFactory connectionFactory,
        ILogger<FeedQueryService>? logger = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? NullLogger<FeedQueryService>.Instance;
    }

    /// <inheritdoc />
    public async Task<FeedPageResultDto> GetFeedAsync(
        FeedFilter? filter = null,
        FeedPagination? pagination = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var (pageSize, offset, page) = ResolvePagination(filter, pagination);

        var customerId = NormalizeOptionalGuid(filter?.CustomerId);
        var productId = NormalizeOptionalGuid(filter?.ProductId);
        var requestId = NormalizeOptionalGuid(filter?.RequestId);
        var workPackageId = NormalizeOptionalGuid(filter?.WorkPackageId);
        var authorPersonId = NormalizeOptionalGuid(filter?.AuthorPersonId);
        var referenceId = NormalizeOptionalGuid(filter?.ReferenceId);
        var referenceType = NormalizeOptionalString(filter?.ReferenceType)?.ToUpperInvariant();
        var exceptionType = PostExceptionTypes.NormalizeAndValidate(filter?.ExceptionType);
        var exceptionsOnly = (filter?.IsException == true) || exceptionType is not null;
        var rawSearchTerm = NormalizeOptionalString(filter?.SearchTerm);
        var tokens = ExtractSearchTokens(rawSearchTerm);
        var (ftsQuery, _) = ParseSearchTokens(rawSearchTerm);

        using var connection = _connectionFactory.CreateConnection();
        var isFtsActive = await CheckFtsAvailabilityAsync(connection, cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("CustomerId", customerId);
        parameters.Add("ProductId", productId);
        parameters.Add("ExceptionsOnly", exceptionsOnly);
        parameters.Add("ExceptionType", exceptionType);
        parameters.Add("RequestId", requestId);
        parameters.Add("WorkPackageId", workPackageId);
        parameters.Add("AuthorPersonId", authorPersonId);
        parameters.Add("ReferenceType", referenceType);
        parameters.Add("ReferenceId", referenceId);
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        string searchPredicate;
        if (isFtsActive && ftsQuery is not null)
        {
            parameters.Add("FtsQuery", ftsQuery);
            searchPredicate = "AND CONTAINS([SearchContent], @FtsQuery)";
        }
        else if (tokens.Count > 0)
        {
            var tokenPredicates = new List<string>(tokens.Count);
            for (var i = 0; i < tokens.Count; i++)
            {
                var paramName = $"SearchToken_{i}";
                parameters.Add(paramName, $"%{EscapeLikeWildcards(tokens[i])}%");
                tokenPredicates.Add($"""
                    (
                        [SearchContent] LIKE @{paramName} ESCAPE N'\'
                        OR [Title] LIKE @{paramName} ESCAPE N'\'
                        OR [ContentExcerpt] LIKE @{paramName} ESCAPE N'\'
                        OR [CustomerName] LIKE @{paramName} ESCAPE N'\'
                        OR [ProductName] LIKE @{paramName} ESCAPE N'\'
                        OR [ReferenceDisplay] LIKE @{paramName} ESCAPE N'\'
                    )
                    """);
            }
            searchPredicate = "AND " + string.Join(" AND ", tokenPredicates);
        }
        else
        {
            searchPredicate = string.Empty;
        }

        var getFeedSql = $"""
            SELECT COUNT(1)
            FROM [post].[FeedItems]
            WHERE [Visibility] = N'VISIBLE'
              AND [Status] = N'ACTIVE'
              AND (@CustomerId IS NULL OR [CustomerId] = @CustomerId)
              AND (@ProductId IS NULL OR [ProductId] = @ProductId)
              AND (@ExceptionsOnly = 0 OR [IsException] = 1)
              AND (@ExceptionType IS NULL OR [ExceptionType] = @ExceptionType)
              AND (@RequestId IS NULL OR [RequestId] = @RequestId)
              AND (@WorkPackageId IS NULL OR [WorkPackageId] = @WorkPackageId)
              AND (@AuthorPersonId IS NULL OR [AuthorPersonId] = @AuthorPersonId)
              AND (@ReferenceType IS NULL OR [ReferenceType] = @ReferenceType)
              AND (@ReferenceId IS NULL OR [ReferenceId] = @ReferenceId)
              {searchPredicate};

            SELECT
                [FeedItemId],
                [PostId],
                [AuthorPersonId],
                [AuthorName],
                [PostType],
                [Source],
                [Title],
                [ContentExcerpt],
                [Summary],
                [Status],
                [Visibility],
                [IsException],
                [ExceptionType],
                [ReferenceType],
                [ReferenceId],
                [ReferenceDisplay],
                [CustomerId],
                [CustomerName],
                (SELECT [CustomerCode] FROM [customer].[Customers] WHERE [Id] = [post].[FeedItems].[CustomerId]) AS [CustomerCode],
                [ProductId],
                [ProductName],
                (SELECT [Code] FROM [product].[Products] WHERE [Id] = [post].[FeedItems].[ProductId]) AS [ProductCode],
                [RequestId],
                [WorkPackageId],
                [CommentCount],
                [LatestCommentExcerpt],
                [ReactionCountsJson],
                [CreatedAt],
                [UpdatedAt],
                [LastActivityAt]
            FROM [post].[FeedItems]
            WHERE [Visibility] = N'VISIBLE'
              AND [Status] = N'ACTIVE'
              AND (@CustomerId IS NULL OR [CustomerId] = @CustomerId)
              AND (@ProductId IS NULL OR [ProductId] = @ProductId)
              AND (@ExceptionsOnly = 0 OR [IsException] = 1)
              AND (@ExceptionType IS NULL OR [ExceptionType] = @ExceptionType)
              AND (@RequestId IS NULL OR [RequestId] = @RequestId)
              AND (@WorkPackageId IS NULL OR [WorkPackageId] = @WorkPackageId)
              AND (@AuthorPersonId IS NULL OR [AuthorPersonId] = @AuthorPersonId)
              AND (@ReferenceType IS NULL OR [ReferenceType] = @ReferenceType)
              AND (@ReferenceId IS NULL OR [ReferenceId] = @ReferenceId)
              {searchPredicate}
            ORDER BY [CreatedAt] DESC, [FeedItemId] DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(getFeedSql, parameters, cancellationToken: cancellationToken));

        var totalCount = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<FeedItemDto>()).AsList();

        stopwatch.Stop();

        _logger.LogInformation(
            "FeedQueryService.GetFeedAsync returned {ItemCount}/{TotalCount} items in {ElapsedMs}ms (CustomerId={CustomerId}, ProductId={ProductId}, ExceptionsOnly={ExceptionsOnly}, ExceptionType={ExceptionType}, FtsActive={FtsActive}, PageSize={PageSize}, Offset={Offset})",
            items.Count,
            totalCount,
            stopwatch.ElapsedMilliseconds,
            customerId,
            productId,
            exceptionsOnly,
            exceptionType,
            isFtsActive,
            pageSize,
            offset);

        return new FeedPageResultDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Offset = offset
        };
    }

    /// <inheritdoc />
    public FeedPageResultDto GetFeed(
        FeedFilter? filter = null,
        FeedPagination? pagination = null)
        => GetFeedAsync(filter, pagination, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public FeedPageResultDto GetFeed(
        Guid? customerId,
        Guid? productId = null,
        bool? isException = null,
        int pageSize = FeedPagination.DefaultPageSize,
        int offset = 0,
        string? exceptionType = null)
        => GetFeedAsync(
                new FeedFilter
                {
                    CustomerId = customerId,
                    ProductId = productId,
                    IsException = isException,
                    ExceptionType = exceptionType,
                    PageSize = pageSize,
                    Offset = offset
                },
                new FeedPagination(pageSize, offset),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

    /// <inheritdoc />
    public async Task<FeedItemDto?> GetFeedItemByPostIdAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        if (postId == Guid.Empty)
        {
            return null;
        }

        const string byPostIdSql = """
            SELECT
                [FeedItemId],
                [PostId],
                [AuthorPersonId],
                [AuthorName],
                [PostType],
                [Source],
                [Title],
                [ContentExcerpt],
                [Summary],
                [Status],
                [Visibility],
                [IsException],
                [ExceptionType],
                [ReferenceType],
                [ReferenceId],
                [ReferenceDisplay],
                [CustomerId],
                [CustomerName],
                (SELECT [CustomerCode] FROM [customer].[Customers] WHERE [Id] = [post].[FeedItems].[CustomerId]) AS [CustomerCode],
                [ProductId],
                [ProductName],
                (SELECT [Code] FROM [product].[Products] WHERE [Id] = [post].[FeedItems].[ProductId]) AS [ProductCode],
                [RequestId],
                [WorkPackageId],
                [CommentCount],
                [LatestCommentExcerpt],
                [ReactionCountsJson],
                [CreatedAt],
                [UpdatedAt],
                [LastActivityAt]
            FROM [post].[FeedItems]
            WHERE [PostId] = @PostId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<FeedItemDto>(
            new CommandDefinition(byPostIdSql, new { PostId = postId }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<FeedItemDto?> GetFeedItemByIdAsync(
        Guid feedItemId,
        CancellationToken cancellationToken = default)
    {
        if (feedItemId == Guid.Empty)
        {
            return null;
        }

        const string byFeedItemIdSql = """
            SELECT
                [FeedItemId],
                [PostId],
                [AuthorPersonId],
                [AuthorName],
                [PostType],
                [Source],
                [Title],
                [ContentExcerpt],
                [Summary],
                [Status],
                [Visibility],
                [IsException],
                [ExceptionType],
                [ReferenceType],
                [ReferenceId],
                [ReferenceDisplay],
                [CustomerId],
                [CustomerName],
                (SELECT [CustomerCode] FROM [customer].[Customers] WHERE [Id] = [post].[FeedItems].[CustomerId]) AS [CustomerCode],
                [ProductId],
                [ProductName],
                (SELECT [Code] FROM [product].[Products] WHERE [Id] = [post].[FeedItems].[ProductId]) AS [ProductCode],
                [RequestId],
                [WorkPackageId],
                [CommentCount],
                [LatestCommentExcerpt],
                [ReactionCountsJson],
                [CreatedAt],
                [UpdatedAt],
                [LastActivityAt]
            FROM [post].[FeedItems]
            WHERE [FeedItemId] = @FeedItemId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<FeedItemDto>(
            new CommandDefinition(byFeedItemIdSql, new { FeedItemId = feedItemId }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public Task<FeedPageResultDto> Handle(GetFeedQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var filter = request.Filter ?? new FeedFilter
        {
            CustomerId = request.CustomerId,
            ProductId = request.ProductId,
            IsException = request.IsException,
            ExceptionType = request.ExceptionType,
            RequestId = request.RequestId,
            WorkPackageId = request.WorkPackageId,
            AuthorPersonId = request.AuthorPersonId,
            SearchTerm = request.SearchTerm,
            Page = request.Page ?? 1,
            PageSize = request.PageSize,
            Offset = request.Page.HasValue && request.Offset == 0 ? null : request.Offset
        };

        var pagination = request.Pagination ?? new FeedPagination(
            request.PageSize,
            request.Offset,
            request.Page);

        return GetFeedAsync(filter, pagination, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FeedItemDto?> Handle(GetFeedItemByPostIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetFeedItemByPostIdAsync(request.PostId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FeedItemDto?> Handle(GetFeedItemByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetFeedItemByIdAsync(request.FeedItemId, cancellationToken);
    }

    /// <summary>
    /// Extracts and sanitizes search tokens by stripping punctuation and reserved FTS words (Architecture CR-014 §4 TD-003).
    /// </summary>
    public static IReadOnlyList<string> ExtractSearchTokens(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Array.Empty<string>();
        }

        var cleaned = new string(input
            .Select(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ')
            .ToArray());

        return cleaned
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !IsFtsReservedWord(t))
            .ToList();
    }

    /// <summary>
    /// Parses and sanitizes a raw search input string into a Prefix-AND full-text query
    /// and a parameterized LIKE search pattern (Architecture CR-014 §4 TD-003).
    /// </summary>
    public static (string? FtsQuery, string? LikePattern) ParseSearchTokens(string? input)
    {
        var tokens = ExtractSearchTokens(input);
        if (tokens.Count == 0)
        {
            return (null, null);
        }

        var ftsQuery = string.Join(" AND ", tokens.Select(t => $"\"{t}*\""));
        var likePattern = $"%{string.Join("%", tokens.Select(EscapeLikeWildcards))}%";

        return (ftsQuery, likePattern);
    }

    /// <summary>
    /// Determines whether the specified token matches a SQL Server Full-Text Search reserved keyword.
    /// </summary>
    public static bool IsFtsReservedWord(string token) =>
        !string.IsNullOrEmpty(token) && FtsReservedWords.Contains(token);

    private async Task<bool> CheckFtsAvailabilityAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (_isFtsAvailable.HasValue)
        {
            return _isFtsAvailable.Value;
        }

        await _ftsCheckLock.WaitAsync(cancellationToken);
        try
        {
            if (_isFtsAvailable.HasValue)
            {
                return _isFtsAvailable.Value;
            }

            const string ftsCheckSql = """
                SELECT CAST(CASE WHEN SERVERPROPERTY('IsFullTextInstalled') = 1
                                 AND EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(N'[post].[FeedItems]'))
                            THEN 1 ELSE 0 END AS BIT);
                """;

            var isInstalled = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(ftsCheckSql, cancellationToken: cancellationToken));

            _isFtsAvailable = isInstalled;
            return isInstalled;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to determine FTS availability on [post].[FeedItems]. Falling back to LIKE search.");
            _isFtsAvailable = false;
            return false;
        }
        finally
        {
            _ftsCheckLock.Release();
        }
    }

    internal static void ResetFtsAvailabilityCache()
    {
        _isFtsAvailable = null;
    }

    private static (int PageSize, int Offset, int Page) ResolvePagination(
        FeedFilter? filter,
        FeedPagination? pagination)
    {
        if (pagination is not null)
        {
            var pageSize = Math.Clamp(
                pagination.PageSize <= 0 ? FeedPagination.DefaultPageSize : pagination.PageSize,
                1,
                FeedPagination.MaxPageSize);

            int offset;
            int page;

            if (pagination.Offset > 0)
            {
                offset = pagination.Offset;
                page = pagination.Page ?? ((offset / pageSize) + 1);
            }
            else if (pagination.Page.HasValue && pagination.Page.Value > 1)
            {
                page = pagination.Page.Value;
                offset = (page - 1) * pageSize;
            }
            else
            {
                offset = Math.Max(0, pagination.Offset);
                page = Math.Max(1, pagination.Page ?? 1);
            }

            return (pageSize, offset, page);
        }

        if (filter is not null)
        {
            var pageSize = Math.Clamp(
                filter.PageSize <= 0 ? FeedPagination.DefaultPageSize : filter.PageSize,
                1,
                FeedPagination.MaxPageSize);

            if (filter.Offset.HasValue)
            {
                var offset = Math.Max(0, filter.Offset.Value);
                var page = (offset / pageSize) + 1;
                return (pageSize, offset, page);
            }

            var resolvedPage = Math.Max(1, filter.Page);
            var resolvedOffset = (resolvedPage - 1) * pageSize;
            return (pageSize, resolvedOffset, resolvedPage);
        }

        return (FeedPagination.DefaultPageSize, 0, 1);
    }

    private static Guid? NormalizeOptionalGuid(Guid? value)
        => value.HasValue && value.Value != Guid.Empty ? value.Value : null;

    private static string? NormalizeOptionalString(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string EscapeLikeWildcards(string value)
        => value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal)
            .Replace("[", @"\[", StringComparison.Ordinal);
}
