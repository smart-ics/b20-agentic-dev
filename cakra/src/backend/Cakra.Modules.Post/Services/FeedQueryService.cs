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
/// <c>post.FeedItems</c> materialized read model (Architecture §6, §7, §8, §9, §12, §19.2, §19.3, §20, §21).
/// Supports <c>UC-FCOL-004</c> (Navigate from Post to Request), <c>UC-FCOL-005</c> (Filter Operational Feed),
/// <c>UC-AWR-001</c> (Observe Operational Feed), <c>UC-AWR-002</c> (Discover Request via Feed), and
/// <c>UC-AWR-003</c> (Monitor Operational Exceptions via Feed) with sub-50ms target latency.
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
        var searchTerm = NormalizeOptionalString(filter?.SearchTerm);
        var searchPattern = searchTerm is null ? null : $"%{EscapeLikeWildcards(searchTerm)}%";

        const string getFeedSql = """
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
              AND (
                  @SearchPattern IS NULL
                  OR [Title] LIKE @SearchPattern ESCAPE N'\'
                  OR [ContentExcerpt] LIKE @SearchPattern ESCAPE N'\'
                  OR [CustomerName] LIKE @SearchPattern ESCAPE N'\'
                  OR [ProductName] LIKE @SearchPattern ESCAPE N'\'
                  OR [ReferenceDisplay] LIKE @SearchPattern ESCAPE N'\'
              );

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
                [ProductId],
                [ProductName],
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
              AND (
                  @SearchPattern IS NULL
                  OR [Title] LIKE @SearchPattern ESCAPE N'\'
                  OR [ContentExcerpt] LIKE @SearchPattern ESCAPE N'\'
                  OR [CustomerName] LIKE @SearchPattern ESCAPE N'\'
                  OR [ProductName] LIKE @SearchPattern ESCAPE N'\'
                  OR [ReferenceDisplay] LIKE @SearchPattern ESCAPE N'\'
              )
            ORDER BY [CreatedAt] DESC, [FeedItemId] DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        var parameters = new
        {
            CustomerId = customerId,
            ProductId = productId,
            ExceptionsOnly = exceptionsOnly,
            ExceptionType = exceptionType,
            RequestId = requestId,
            WorkPackageId = workPackageId,
            AuthorPersonId = authorPersonId,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            SearchPattern = searchPattern,
            Offset = offset,
            PageSize = pageSize
        };

        using var connection = _connectionFactory.CreateConnection();
        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(getFeedSql, parameters, cancellationToken: cancellationToken));

        var totalCount = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<FeedItemDto>()).AsList();

        stopwatch.Stop();

        _logger.LogInformation(
            "FeedQueryService.GetFeedAsync returned {ItemCount}/{TotalCount} items in {ElapsedMs}ms (CustomerId={CustomerId}, ProductId={ProductId}, ExceptionsOnly={ExceptionsOnly}, ExceptionType={ExceptionType}, PageSize={PageSize}, Offset={Offset})",
            items.Count,
            totalCount,
            stopwatch.ElapsedMilliseconds,
            customerId,
            productId,
            exceptionsOnly,
            exceptionType,
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
                [ProductId],
                [ProductName],
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
                [ProductId],
                [ProductName],
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
