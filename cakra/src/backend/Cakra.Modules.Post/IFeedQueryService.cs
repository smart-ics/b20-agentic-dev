namespace Cakra.Modules.Post;

/// <summary>
/// Filter criteria for <see cref="IFeedQueryService.GetFeedAsync(FeedFilter?, FeedPagination?, CancellationToken)"/>
/// and <see cref="IFeedQueryService.GetFeed(FeedFilter?, FeedPagination?)"/>
/// (Architecture §7, §8, §9, §12, §20 — <c>UC-FCOL-005</c>, <c>UC-AWR-001..003</c>, <c>SCR-FEED-001</c>).
/// </summary>
public sealed record FeedFilter
{
    /// <summary>Optional Customer identifier filter (<c>post.FeedItems.CustomerId</c>).</summary>
    public Guid? CustomerId { get; init; }

    /// <summary>Optional Product identifier filter (<c>post.FeedItems.ProductId</c>).</summary>
    public Guid? ProductId { get; init; }

    /// <summary>
    /// Optional exception badge filter (<c>post.FeedItems.IsException</c>).
    /// When <c>true</c>, restricts results to rows where <c>IsException = 1</c> (<c>ESCALATION</c>,
    /// <c>REJECTION</c>, <c>STALLED</c>) per Architecture §12 (<c>AND (:ExceptionsOnly IS FALSE OR IsException = TRUE)</c>).
    /// </summary>
    public bool? IsException { get; init; }

    /// <summary>
    /// Convenience alias for <see cref="IsException"/> matching Architecture §12 <c>:ExceptionsOnly</c>.
    /// </summary>
    public bool ExceptionsOnly
    {
        get => IsException == true;
        init => IsException = value ? true : null;
    }

    /// <summary>
    /// Optional specific exception type filter (<c>ESCALATION</c>, <c>REJECTION</c>, or <c>STALLED</c>).
    /// When specified, also implies <c>IsException = true</c>.
    /// </summary>
    public string? ExceptionType { get; init; }

    /// <summary>Optional Request identifier filter (<c>post.FeedItems.RequestId</c>).</summary>
    public Guid? RequestId { get; init; }

    /// <summary>Optional Work Package identifier filter (<c>post.FeedItems.WorkPackageId</c>).</summary>
    public Guid? WorkPackageId { get; init; }

    /// <summary>Optional Author Person identifier filter (<c>post.FeedItems.AuthorPersonId</c>).</summary>
    public Guid? AuthorPersonId { get; init; }

    /// <summary>Optional primary contextual reference type filter (<c>REQUEST</c>, <c>WORK_PACKAGE</c>, <c>CUSTOMER</c>, <c>PRODUCT</c>).</summary>
    public string? ReferenceType { get; init; }

    /// <summary>Optional primary contextual reference identifier filter.</summary>
    public Guid? ReferenceId { get; init; }

    /// <summary>Optional keyword search matched against Title, ContentExcerpt, CustomerName, ProductName, or ReferenceDisplay.</summary>
    public string? SearchTerm { get; init; }

    /// <summary>Optional 1-based page number (default 1) when pagination is supplied directly on the filter.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Optional page size (default 20, clamped to 1..200) when pagination is supplied directly on the filter.</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>Optional 0-based row offset when pagination is supplied directly on the filter.</summary>
    public int? Offset { get; init; }
}

/// <summary>
/// Pagination parameters (<c>PageSize</c> and <c>Offset</c> / <c>Page</c>) for
/// <see cref="IFeedQueryService.GetFeedAsync(FeedFilter?, FeedPagination?, CancellationToken)"/>
/// and <see cref="IFeedQueryService.GetFeed(FeedFilter?, FeedPagination?)"/> (Architecture §12, §20).
/// </summary>
public sealed record FeedPagination
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 200;

    /// <summary>Maximum number of rows to return (default 20, clamped to 1..200).</summary>
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>0-based row offset (default 0).</summary>
    public int Offset { get; init; }

    /// <summary>Optional 1-based page index. Used when <see cref="Offset"/> is 0 and <c>Page &gt; 1</c>.</summary>
    public int? Page { get; init; }

    public FeedPagination()
    {
    }

    public FeedPagination(int pageSize, int offset = 0, int? page = null)
    {
        PageSize = pageSize;
        Offset = offset;
        Page = page;
    }
}

/// <summary>
/// Paginated feed result set returned by <see cref="IFeedQueryService"/>
/// (Architecture §7, §8, §9, §12, §20 — <c>SCR-FEED-001</c>).
/// </summary>
public sealed record FeedPageResultDto
{
    /// <summary>Current page of visible, active <see cref="FeedItemDto"/> rows ordered by <c>CreatedAt DESC</c>.</summary>
    public IReadOnlyList<FeedItemDto> Items { get; init; } = Array.Empty<FeedItemDto>();

    /// <summary>Convenience alias for <see cref="Items"/>.</summary>
    public IReadOnlyList<FeedItemDto> FeedItems => Items;

    /// <summary>Total count of matching visible, active feed items across all pages.</summary>
    public int TotalCount { get; init; }

    /// <summary>1-based page number corresponding to <see cref="Offset"/> and <see cref="PageSize"/>.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Page size used for the query.</summary>
    public int PageSize { get; init; } = FeedPagination.DefaultPageSize;

    /// <summary>0-based row offset used for the query.</summary>
    public int Offset { get; init; }

    /// <summary>Total number of pages available.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>Indicates whether additional rows exist beyond the current page.</summary>
    public bool HasMore => Offset + Items.Count < TotalCount;
}

/// <summary>
/// Published query contract for high-performance single-table indexed queries over <c>post.FeedItems</c>
/// with filtering by Customer, Product, or Exception badge and pagination
/// (Architecture §7, §8, §9, §12, §20 — <c>UC-FCOL-004</c>, <c>UC-FCOL-005</c>, <c>UC-AWR-001..003</c>, <c>SCR-FEED-001</c>).
/// </summary>
public interface IFeedQueryService
{
    /// <summary>
    /// Executes the single-table indexed Dapper query over <c>post.FeedItems</c> filtering on
    /// <c>Visibility = 'VISIBLE'</c>, <c>Status = 'ACTIVE'</c>, and optional <c>CustomerId</c>,
    /// <c>ProductId</c>, and <c>IsException</c> criteria, ordered by <c>CreatedAt DESC</c> with
    /// <c>OFFSET / FETCH NEXT</c> pagination (Architecture §12 Query Semantics, §20 Sub-50ms target).
    /// </summary>
    Task<FeedPageResultDto> GetFeedAsync(
        FeedFilter? filter = null,
        FeedPagination? pagination = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience overload for <see cref="GetFeedAsync(FeedFilter?, FeedPagination?, CancellationToken)"/>
    /// when pagination is carried on <paramref name="filter"/>.
    /// </summary>
    Task<FeedPageResultDto> GetFeedAsync(
        FeedFilter? filter,
        CancellationToken cancellationToken)
        => GetFeedAsync(filter, pagination: null, cancellationToken);

    /// <summary>
    /// Convenience overload for <see cref="GetFeedAsync(FeedFilter?, FeedPagination?, CancellationToken)"/>
    /// accepting individual filter and pagination parameters.
    /// </summary>
    Task<FeedPageResultDto> GetFeedAsync(
        Guid? customerId,
        Guid? productId = null,
        bool? isException = null,
        int pageSize = FeedPagination.DefaultPageSize,
        int offset = 0,
        string? exceptionType = null,
        CancellationToken cancellationToken = default)
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
            cancellationToken);

    /// <summary>
    /// Synchronous convenience method executing the single-table indexed Dapper query over <c>post.FeedItems</c>
    /// (Architecture §12 — <c>FeedQueryService.GetFeed(filter, pagination)</c>).
    /// </summary>
    FeedPageResultDto GetFeed(
        FeedFilter? filter = null,
        FeedPagination? pagination = null)
        => GetFeedAsync(filter, pagination, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Synchronous convenience overload accepting individual filter and pagination parameters.
    /// </summary>
    FeedPageResultDto GetFeed(
        Guid? customerId,
        Guid? productId = null,
        bool? isException = null,
        int pageSize = FeedPagination.DefaultPageSize,
        int offset = 0,
        string? exceptionType = null)
        => GetFeedAsync(customerId, productId, isException, pageSize, offset, exceptionType, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

    /// <summary>
    /// Retrieves a single projected <see cref="FeedItemDto"/> by its associated <c>PostId</c>,
    /// or <c>null</c> if not found.
    /// </summary>
    Task<FeedItemDto?> GetFeedItemByPostIdAsync(
        Guid postId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous convenience overload for <see cref="GetFeedItemByPostIdAsync"/>.
    /// </summary>
    FeedItemDto? GetFeedItemByPostId(Guid postId)
        => GetFeedItemByPostIdAsync(postId, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Retrieves a single projected <see cref="FeedItemDto"/> by its primary key <c>FeedItemId</c>,
    /// or <c>null</c> if not found.
    /// </summary>
    Task<FeedItemDto?> GetFeedItemByIdAsync(
        Guid feedItemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous convenience overload for <see cref="GetFeedItemByIdAsync"/>.
    /// </summary>
    FeedItemDto? GetFeedItemById(Guid feedItemId)
        => GetFeedItemByIdAsync(feedItemId, CancellationToken.None).GetAwaiter().GetResult();
}
