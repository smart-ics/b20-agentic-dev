using Cakra.Core;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Cakra.Modules.Product;
using Cakra.Modules.Request.Domain;
using Dapper;
using MediatR;

namespace Cakra.Modules.Request.Services;

/// <summary>
/// Dapper implementation of <see cref="IRequestQueryService"/> executing explicit parameterized SQL
/// exclusively against the <c>request</c> schema (<c>request.Requests</c>, <c>request.RequestResolutions</c>,
/// and <c>request.RequestAssignments</c>) and enriching cross-module display names via published query
/// interfaces (Architecture §7, §8, §15, §19.3, §20, §21).
/// </summary>
public sealed class RequestQueryService :
    IRequestQueryService,
    IRequestHandler<GetRequestByIdQuery, RequestDto?>,
    IRequestHandler<GetRequestStateHistoryQuery, IReadOnlyList<RequestAssignmentDto>>,
    IRequestHandler<ListMyAssignedRequestsQuery, IReadOnlyList<RequestDto>>,
    IRequestHandler<GetFilteredRequestGridQuery, PagedRequestGridResult>
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ICurrentContextProvider? _currentContextProvider;
    private readonly IOrganizationQueryService? _organizationQueryService;
    private readonly ICustomerQueryService? _customerQueryService;
    private readonly IProductQueryService? _productQueryService;

    public RequestQueryService(
        IDbConnectionFactory connectionFactory,
        ICurrentContextProvider? currentContextProvider = null,
        IOrganizationQueryService? organizationQueryService = null,
        ICustomerQueryService? customerQueryService = null,
        IProductQueryService? productQueryService = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _currentContextProvider = currentContextProvider;
        _organizationQueryService = organizationQueryService;
        _customerQueryService = customerQueryService;
        _productQueryService = productQueryService;
    }

    /// <inheritdoc />
    public async Task<RequestDto?> GetRequestByIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            return null;
        }

        const string requestSql = """
            SELECT
                r.[Id],
                r.[Title],
                r.[Description],
                r.[RequestType],
                r.[Status],
                r.[Priority],
                r.[OwnerPersonId],
                r.[CustomerId],
                r.[ProductId],
                r.[WorkPackageId],
                r.[EvaluationNotes],
                r.[EscalationReason],
                r.[ManagementDecisionNotes],
                r.[CreatedAt],
                r.[UpdatedAt],
                res.[Id] AS [ResolutionId],
                res.[Outcome] AS [ResolutionOutcome],
                res.[Description] AS [ResolutionDescription],
                res.[ResolvedBy] AS [ResolutionResolvedBy],
                res.[ResolvedAt] AS [ResolutionResolvedAt],
                res.[CreatedAt] AS [ResolutionCreatedAt],
                res.[UpdatedAt] AS [ResolutionUpdatedAt]
            FROM [request].[Requests] r
            LEFT JOIN [request].[RequestResolutions] res ON res.[RequestId] = r.[Id]
            WHERE r.[Id] = @RequestId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<RequestWithResolutionRow>(
            new CommandDefinition(requestSql, new { RequestId = requestId }, cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var assignments = await GetRequestStateHistoryAsync(requestId, cancellationToken);
        var dto = row.ToDto(assignments);
        var enriched = await EnrichRequestsAsync(new[] { dto }, cancellationToken);
        return enriched[0];
    }

    /// <inheritdoc />
    public Task<RequestDto?> GetRequestById(
        Guid requestId,
        CancellationToken cancellationToken = default)
        => GetRequestByIdAsync(requestId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RequestAssignmentDto>> GetRequestStateHistoryAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            return Array.Empty<RequestAssignmentDto>();
        }

        const string sql = """
            SELECT
                [Id],
                [RequestId],
                [PreviousOwnerPersonId],
                [AssignedOwnerPersonId],
                [ActorPersonId],
                [PreviousStatus],
                [NewStatus],
                [AssignedAtUtc],
                [Notes],
                [CreatedAt],
                [UpdatedAt]
            FROM [request].[RequestAssignments]
            WHERE [RequestId] = @RequestId
            ORDER BY [AssignedAtUtc] ASC, [CreatedAt] ASC, [Id] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<RequestAssignmentDto>(
            new CommandDefinition(sql, new { RequestId = requestId }, cancellationToken: cancellationToken))).AsList();

        if (rows.Count == 0 || _organizationQueryService is null)
        {
            return rows;
        }

        var personCache = new Dictionary<Guid, string?>();
        var enriched = new List<RequestAssignmentDto>(rows.Count);

        foreach (var item in rows)
        {
            var actorName = await ResolvePersonNameAsync(item.ActorPersonId, personCache, cancellationToken);
            var prevOwnerName = item.PreviousOwnerPersonId.HasValue
                ? await ResolvePersonNameAsync(item.PreviousOwnerPersonId.Value, personCache, cancellationToken)
                : null;
            var assignedOwnerName = item.AssignedOwnerPersonId.HasValue
                ? await ResolvePersonNameAsync(item.AssignedOwnerPersonId.Value, personCache, cancellationToken)
                : null;

            enriched.Add(item with
            {
                ActorName = actorName,
                PreviousOwnerName = prevOwnerName,
                AssignedOwnerName = assignedOwnerName
            });
        }

        return enriched;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RequestAssignmentDto>> GetRequestStateHistory(
        Guid requestId,
        CancellationToken cancellationToken = default)
        => GetRequestStateHistoryAsync(requestId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RequestDto>> ListMyAssignedRequestsAsync(
        Guid? personId = null,
        CancellationToken cancellationToken = default)
    {
        var effectivePersonId = personId ?? _currentContextProvider?.CurrentPersonId;
        if (!effectivePersonId.HasValue || effectivePersonId.Value == Guid.Empty)
        {
            return Array.Empty<RequestDto>();
        }

        const string sql = """
            SELECT
                r.[Id],
                r.[Title],
                r.[Description],
                r.[RequestType],
                r.[Status],
                r.[Priority],
                r.[OwnerPersonId],
                r.[CustomerId],
                r.[ProductId],
                r.[WorkPackageId],
                r.[EvaluationNotes],
                r.[EscalationReason],
                r.[ManagementDecisionNotes],
                r.[CreatedAt],
                r.[UpdatedAt],
                res.[Id] AS [ResolutionId],
                res.[Outcome] AS [ResolutionOutcome],
                res.[Description] AS [ResolutionDescription],
                res.[ResolvedBy] AS [ResolutionResolvedBy],
                res.[ResolvedAt] AS [ResolutionResolvedAt],
                res.[CreatedAt] AS [ResolutionCreatedAt],
                res.[UpdatedAt] AS [ResolutionUpdatedAt]
            FROM [request].[Requests] r
            LEFT JOIN [request].[RequestResolutions] res ON res.[RequestId] = r.[Id]
            WHERE r.[OwnerPersonId] = @OwnerPersonId
            ORDER BY r.[CreatedAt] DESC, r.[Id] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<RequestWithResolutionRow>(
            new CommandDefinition(
                sql,
                new { OwnerPersonId = effectivePersonId.Value },
                cancellationToken: cancellationToken));

        var dtos = rows.Select(r => r.ToDto()).ToList();
        return await EnrichRequestsAsync(dtos, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RequestDto>> ListMyAssignedRequests(
        Guid? personId = null,
        CancellationToken cancellationToken = default)
        => ListMyAssignedRequestsAsync(personId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<RequestDto>> ListMyAssignedRequests(
        CancellationToken cancellationToken)
        => ListMyAssignedRequestsAsync(null, cancellationToken);

    /// <inheritdoc />
    public async Task<PagedRequestGridResult> GetFilteredRequestGridAsync(
        RequestGridFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveFilter = filter ?? new RequestGridFilter();
        var pageSize = effectiveFilter.PageSize <= 0 ? 20 : Math.Min(effectiveFilter.PageSize, 200);
        var page = effectiveFilter.Page <= 0 ? 1 : effectiveFilter.Page;
        var offset = effectiveFilter.Offset.HasValue && effectiveFilter.Offset.Value >= 0
            ? effectiveFilter.Offset.Value
            : (page - 1) * pageSize;
        var effectivePage = effectiveFilter.Offset.HasValue && effectiveFilter.Offset.Value >= 0
            ? (offset / pageSize) + 1
            : page;

        var normalizedStatus = NormalizeStatusFilter(effectiveFilter.Status);
        var ownerPersonId = effectiveFilter.OwnerPersonId.HasValue && effectiveFilter.OwnerPersonId.Value != Guid.Empty
            ? effectiveFilter.OwnerPersonId
            : null;
        var customerId = effectiveFilter.CustomerId.HasValue && effectiveFilter.CustomerId.Value != Guid.Empty
            ? effectiveFilter.CustomerId
            : null;
        var productId = effectiveFilter.ProductId.HasValue && effectiveFilter.ProductId.Value != Guid.Empty
            ? effectiveFilter.ProductId
            : null;
        var workPackageId = effectiveFilter.WorkPackageId.HasValue && effectiveFilter.WorkPackageId.Value != Guid.Empty
            ? effectiveFilter.WorkPackageId
            : null;
        var searchPattern = string.IsNullOrWhiteSpace(effectiveFilter.SearchTerm)
            ? null
            : $"%{effectiveFilter.SearchTerm.Trim()}%";

        const string countSql = """
            SELECT COUNT(1)
            FROM [request].[Requests] r
            WHERE (@Status IS NULL OR r.[Status] = @Status)
              AND (@OwnerPersonId IS NULL OR r.[OwnerPersonId] = @OwnerPersonId)
              AND (@CustomerId IS NULL OR r.[CustomerId] = @CustomerId)
              AND (@ProductId IS NULL OR r.[ProductId] = @ProductId)
              AND (@WorkPackageId IS NULL OR r.[WorkPackageId] = @WorkPackageId)
              AND (@SearchPattern IS NULL OR r.[Title] LIKE @SearchPattern OR r.[Description] LIKE @SearchPattern);
            """;

        const string itemsSql = """
            SELECT
                r.[Id],
                r.[Title],
                r.[Description],
                r.[RequestType],
                r.[Status],
                r.[Priority],
                r.[OwnerPersonId],
                r.[CustomerId],
                r.[ProductId],
                r.[WorkPackageId],
                r.[EvaluationNotes],
                r.[EscalationReason],
                r.[ManagementDecisionNotes],
                r.[CreatedAt],
                r.[UpdatedAt],
                res.[Id] AS [ResolutionId],
                res.[Outcome] AS [ResolutionOutcome],
                res.[Description] AS [ResolutionDescription],
                res.[ResolvedBy] AS [ResolutionResolvedBy],
                res.[ResolvedAt] AS [ResolutionResolvedAt],
                res.[CreatedAt] AS [ResolutionCreatedAt],
                res.[UpdatedAt] AS [ResolutionUpdatedAt]
            FROM [request].[Requests] r
            LEFT JOIN [request].[RequestResolutions] res ON res.[RequestId] = r.[Id]
            WHERE (@Status IS NULL OR r.[Status] = @Status)
              AND (@OwnerPersonId IS NULL OR r.[OwnerPersonId] = @OwnerPersonId)
              AND (@CustomerId IS NULL OR r.[CustomerId] = @CustomerId)
              AND (@ProductId IS NULL OR r.[ProductId] = @ProductId)
              AND (@WorkPackageId IS NULL OR r.[WorkPackageId] = @WorkPackageId)
              AND (@SearchPattern IS NULL OR r.[Title] LIKE @SearchPattern OR r.[Description] LIKE @SearchPattern)
            ORDER BY r.[CreatedAt] DESC, r.[Id] ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        var parameters = new
        {
            Status = normalizedStatus,
            OwnerPersonId = ownerPersonId,
            CustomerId = customerId,
            ProductId = productId,
            WorkPackageId = workPackageId,
            SearchPattern = searchPattern,
            Offset = offset,
            PageSize = pageSize
        };

        using var connection = _connectionFactory.CreateConnection();

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync<RequestWithResolutionRow>(
            new CommandDefinition(itemsSql, parameters, cancellationToken: cancellationToken));

        var dtos = rows.Select(r => r.ToDto()).ToList();
        var enrichedItems = await EnrichRequestsAsync(dtos, cancellationToken);

        return new PagedRequestGridResult
        {
            Items = enrichedItems,
            TotalCount = totalCount,
            Page = effectivePage,
            PageSize = pageSize,
            Offset = offset
        };
    }

    /// <inheritdoc />
    public Task<PagedRequestGridResult> GetFilteredRequestGrid(
        RequestGridFilter? filter = null,
        CancellationToken cancellationToken = default)
        => GetFilteredRequestGridAsync(filter, cancellationToken);

    /// <inheritdoc />
    public Task<PagedRequestGridResult> GetFilteredRequestGridAsync(
        string? status,
        Guid? assigneePersonId = null,
        Guid? customerId = null,
        Guid? productId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
        => GetFilteredRequestGridAsync(
            new RequestGridFilter
            {
                Status = status,
                AssigneePersonId = assigneePersonId,
                CustomerId = customerId,
                ProductId = productId,
                Page = page,
                PageSize = pageSize
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<PagedRequestGridResult> GetFilteredRequestGrid(
        string? status,
        Guid? assigneePersonId = null,
        Guid? customerId = null,
        Guid? productId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
        => GetFilteredRequestGridAsync(
            status,
            assigneePersonId,
            customerId,
            productId,
            page,
            pageSize,
            cancellationToken);

    /// <inheritdoc />
    public async Task<bool> RequestExistsAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            return false;
        }

        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM [request].[Requests]
                WHERE [Id] = @RequestId
            ) THEN 1 ELSE 0 END;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { RequestId = requestId }, cancellationToken: cancellationToken));
        return result == 1;
    }

    /// <inheritdoc />
    public bool RequestExists(Guid requestId)
        => RequestExistsAsync(requestId, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<IReadOnlyList<RequestDto>> GetRequestsByIdsAsync(
        IEnumerable<Guid> requestIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestIds);

        var ids = requestIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return Array.Empty<RequestDto>();
        }

        const string sql = """
            SELECT
                r.[Id],
                r.[Title],
                r.[Description],
                r.[RequestType],
                r.[Status],
                r.[Priority],
                r.[OwnerPersonId],
                r.[CustomerId],
                r.[ProductId],
                r.[WorkPackageId],
                r.[EvaluationNotes],
                r.[EscalationReason],
                r.[ManagementDecisionNotes],
                r.[CreatedAt],
                r.[UpdatedAt],
                res.[Id] AS [ResolutionId],
                res.[Outcome] AS [ResolutionOutcome],
                res.[Description] AS [ResolutionDescription],
                res.[ResolvedBy] AS [ResolutionResolvedBy],
                res.[ResolvedAt] AS [ResolutionResolvedAt],
                res.[CreatedAt] AS [ResolutionCreatedAt],
                res.[UpdatedAt] AS [ResolutionUpdatedAt]
            FROM [request].[Requests] r
            LEFT JOIN [request].[RequestResolutions] res ON res.[RequestId] = r.[Id]
            WHERE r.[Id] IN @Ids
            ORDER BY r.[CreatedAt] DESC, r.[Id] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<RequestWithResolutionRow>(
            new CommandDefinition(sql, new { Ids = ids }, cancellationToken: cancellationToken));

        var dtos = rows.Select(r => r.ToDto()).ToList();
        return await EnrichRequestsAsync(dtos, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RequestDto>> GetRequestsByIds(
        IEnumerable<Guid> requestIds,
        CancellationToken cancellationToken = default)
        => GetRequestsByIdsAsync(requestIds, cancellationToken);

    // MediatR query handler entry points
    Task<RequestDto?> IRequestHandler<GetRequestByIdQuery, RequestDto?>.Handle(
        GetRequestByIdQuery request,
        CancellationToken cancellationToken)
        => GetRequestByIdAsync(request.RequestId, cancellationToken);

    Task<IReadOnlyList<RequestAssignmentDto>> IRequestHandler<GetRequestStateHistoryQuery, IReadOnlyList<RequestAssignmentDto>>.Handle(
        GetRequestStateHistoryQuery request,
        CancellationToken cancellationToken)
        => GetRequestStateHistoryAsync(request.RequestId, cancellationToken);

    Task<IReadOnlyList<RequestDto>> IRequestHandler<ListMyAssignedRequestsQuery, IReadOnlyList<RequestDto>>.Handle(
        ListMyAssignedRequestsQuery request,
        CancellationToken cancellationToken)
        => ListMyAssignedRequestsAsync(request.PersonId, cancellationToken);

    Task<PagedRequestGridResult> IRequestHandler<GetFilteredRequestGridQuery, PagedRequestGridResult>.Handle(
        GetFilteredRequestGridQuery request,
        CancellationToken cancellationToken)
        => GetFilteredRequestGridAsync(request.ToFilter(), cancellationToken);

    private static string? NormalizeStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        try
        {
            return RequestStatusNames.FromName(status).ToName();
        }
        catch
        {
            return status.Trim().ToUpperInvariant();
        }
    }

    private async Task<IReadOnlyList<RequestDto>> EnrichRequestsAsync(
        IReadOnlyList<RequestDto> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0 ||
            (_organizationQueryService is null && _customerQueryService is null && _productQueryService is null))
        {
            return items;
        }

        var personCache = new Dictionary<Guid, string?>();
        var customerCache = new Dictionary<Guid, CustomerDto?>();
        var productCache = new Dictionary<Guid, ProductDto?>();
        var enriched = new List<RequestDto>(items.Count);

        foreach (var item in items)
        {
            var ownerName = item.OwnerPersonId.HasValue
                ? await ResolvePersonNameAsync(item.OwnerPersonId.Value, personCache, cancellationToken)
                : null;

            CustomerDto? customer = null;
            if (item.CustomerId.HasValue && _customerQueryService is not null)
            {
                if (!customerCache.TryGetValue(item.CustomerId.Value, out customer))
                {
                    customer = await _customerQueryService.GetCustomerByIdAsync(item.CustomerId.Value, cancellationToken);
                    customerCache[item.CustomerId.Value] = customer;
                }
            }

            ProductDto? product = null;
            if (item.ProductId.HasValue && _productQueryService is not null)
            {
                if (!productCache.TryGetValue(item.ProductId.Value, out product))
                {
                    product = await _productQueryService.GetProductByIdAsync(item.ProductId.Value, cancellationToken);
                    productCache[item.ProductId.Value] = product;
                }
            }

            var resolution = item.Resolution;
            if (resolution is not null && resolution.ResolvedBy != Guid.Empty && _organizationQueryService is not null)
            {
                var resolvedByName = await ResolvePersonNameAsync(resolution.ResolvedBy, personCache, cancellationToken);
                resolution = resolution with { ResolvedByName = resolvedByName };
            }

            enriched.Add(item with
            {
                OwnerName = ownerName,
                CustomerName = customer?.CustomerName,
                CustomerCode = customer?.CustomerCode,
                ProductName = product?.Name,
                ProductCode = product?.Code,
                Resolution = resolution
            });
        }

        return enriched;
    }

    private async Task<string?> ResolvePersonNameAsync(
        Guid personId,
        Dictionary<Guid, string?> cache,
        CancellationToken cancellationToken)
    {
        if (personId == Guid.Empty || _organizationQueryService is null)
        {
            return null;
        }

        if (cache.TryGetValue(personId, out var cachedName))
        {
            return cachedName;
        }

        var person = await _organizationQueryService.GetPersonByIdAsync(personId, cancellationToken);
        var fullName = person?.FullName;
        cache[personId] = fullName;
        return fullName;
    }

    private sealed class RequestWithResolutionRow
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string RequestType { get; init; } = "GENERAL";
        public string Status { get; init; } = RequestStatusNames.Captured;
        public string Priority { get; init; } = "NORMAL";
        public Guid? OwnerPersonId { get; init; }
        public Guid? CustomerId { get; init; }
        public Guid? ProductId { get; init; }
        public Guid? WorkPackageId { get; init; }
        public string? EvaluationNotes { get; init; }
        public string? EscalationReason { get; init; }
        public string? ManagementDecisionNotes { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }

        public Guid? ResolutionId { get; init; }
        public string? ResolutionOutcome { get; init; }
        public string? ResolutionDescription { get; init; }
        public Guid? ResolutionResolvedBy { get; init; }
        public DateTime? ResolutionResolvedAt { get; init; }
        public DateTime? ResolutionCreatedAt { get; init; }
        public DateTime? ResolutionUpdatedAt { get; init; }

        public RequestDto ToDto(IReadOnlyList<RequestAssignmentDto>? assignments = null)
        {
            RequestResolutionDto? resolution = null;
            if (ResolutionId.HasValue && ResolutionId.Value != Guid.Empty)
            {
                resolution = new RequestResolutionDto
                {
                    Id = ResolutionId.Value,
                    RequestId = Id,
                    Outcome = ResolutionOutcome ?? string.Empty,
                    Description = ResolutionDescription ?? string.Empty,
                    ResolvedBy = ResolutionResolvedBy ?? Guid.Empty,
                    ResolvedAt = ResolutionResolvedAt ?? CreatedAt,
                    CreatedAt = ResolutionCreatedAt ?? CreatedAt,
                    UpdatedAt = ResolutionUpdatedAt
                };
            }

            return new RequestDto
            {
                Id = Id,
                Title = Title,
                Description = Description,
                RequestType = RequestType,
                Status = Status,
                Priority = Priority,
                OwnerPersonId = OwnerPersonId,
                CustomerId = CustomerId,
                ProductId = ProductId,
                WorkPackageId = WorkPackageId,
                EvaluationNotes = EvaluationNotes,
                EscalationReason = EscalationReason,
                ManagementDecisionNotes = ManagementDecisionNotes,
                Resolution = resolution,
                Assignments = assignments ?? Array.Empty<RequestAssignmentDto>(),
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt
            };
        }
    }
}
