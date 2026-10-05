using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Cakra.Modules.Product;
using Cakra.Modules.Request;
using Cakra.Modules.WorkPackage.Domain;
using Dapper;
using MediatR;

namespace Cakra.Modules.WorkPackage.Services;

/// <summary>
/// Dapper implementation of <see cref="IWorkPackageQueryService"/> executing explicit parameterized SQL
/// exclusively against the <c>workpackage</c> schema (<c>workpackage.WorkPackages</c> and
/// <c>workpackage.WorkPackageRequests</c>) and enriching cross-module display names and linked Request
/// details via published query interfaces (Architecture §7, §8, §11, §15, §19.3, §20, §21).
/// </summary>
public sealed class WorkPackageQueryService :
    IWorkPackageQueryService,
    IRequestHandler<GetWorkPackageByIdQuery, WorkPackageDto?>,
    IRequestHandler<ListWorkPackagesQuery, IReadOnlyList<WorkPackageDto>>,
    IRequestHandler<GetWorkPackageScopeQuery, IReadOnlyList<WorkPackageScopeItemDto>>,
    IRequestHandler<GetRequestWorkPackageQuery, WorkPackageDto?>
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IOrganizationQueryService? _organizationQueryService;
    private readonly ICustomerQueryService? _customerQueryService;
    private readonly IProductQueryService? _productQueryService;
    private readonly IRequestQueryService? _requestQueryService;

    public WorkPackageQueryService(
        IDbConnectionFactory connectionFactory,
        IOrganizationQueryService? organizationQueryService = null,
        ICustomerQueryService? customerQueryService = null,
        IProductQueryService? productQueryService = null,
        IRequestQueryService? requestQueryService = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _organizationQueryService = organizationQueryService;
        _customerQueryService = customerQueryService;
        _productQueryService = productQueryService;
        _requestQueryService = requestQueryService;
    }

    /// <inheritdoc />
    public async Task<WorkPackageDto?> GetWorkPackageByIdAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default)
    {
        if (workPackageId == Guid.Empty)
        {
            return null;
        }

        const string sql = """
            SELECT
                wp.[Id],
                wp.[Name],
                wp.[Objective],
                wp.[Status],
                wp.[OwnerPersonId],
                wp.[CustomerId],
                wp.[ProductId],
                wp.[ClosedReason],
                wp.[ClosedAt],
                wp.[CreatedAt],
                wp.[UpdatedAt]
            FROM [workpackage].[WorkPackages] wp
            WHERE wp.[Id] = @WorkPackageId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<WorkPackageQueryRow>(
            new CommandDefinition(sql, new { WorkPackageId = workPackageId }, cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var scopeItems = await GetWorkPackageScopeAsync(workPackageId, cancellationToken);
        var dto = row.ToDto(scopeItems);
        var enriched = await EnrichWorkPackagesAsync(new[] { dto }, cancellationToken);
        return enriched[0];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkPackageDto>> ListWorkPackagesAsync(
        string? status = null,
        Guid? ownerPersonId = null,
        Guid? customerId = null,
        Guid? productId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedStatus = NormalizeStatusFilter(status);
        var effectiveOwnerPersonId = ownerPersonId.HasValue && ownerPersonId.Value != Guid.Empty
            ? ownerPersonId
            : null;
        var effectiveCustomerId = customerId.HasValue && customerId.Value != Guid.Empty
            ? customerId
            : null;
        var effectiveProductId = productId.HasValue && productId.Value != Guid.Empty
            ? productId
            : null;

        const string packagesSql = """
            SELECT
                wp.[Id],
                wp.[Name],
                wp.[Objective],
                wp.[Status],
                wp.[OwnerPersonId],
                wp.[CustomerId],
                wp.[ProductId],
                wp.[ClosedReason],
                wp.[ClosedAt],
                wp.[CreatedAt],
                wp.[UpdatedAt]
            FROM [workpackage].[WorkPackages] wp
            WHERE (@Status IS NULL OR wp.[Status] = @Status)
              AND (@OwnerPersonId IS NULL OR wp.[OwnerPersonId] = @OwnerPersonId)
              AND (@CustomerId IS NULL OR wp.[CustomerId] = @CustomerId)
              AND (@ProductId IS NULL OR wp.[ProductId] = @ProductId)
            ORDER BY wp.[CreatedAt] DESC, wp.[Id] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<WorkPackageQueryRow>(
            new CommandDefinition(
                packagesSql,
                new
                {
                    Status = normalizedStatus,
                    OwnerPersonId = effectiveOwnerPersonId,
                    CustomerId = effectiveCustomerId,
                    ProductId = effectiveProductId
                },
                cancellationToken: cancellationToken))).AsList();

        if (rows.Count == 0)
        {
            return Array.Empty<WorkPackageDto>();
        }

        var packageIds = rows.Select(r => r.Id).ToArray();

        const string membershipsSql = """
            SELECT
                wpr.[Id],
                wpr.[WorkPackageId],
                wpr.[RequestId],
                wpr.[SortOrder],
                wpr.[AddedAt],
                wpr.[RemovedAt],
                wpr.[CreatedAt],
                wpr.[UpdatedAt]
            FROM [workpackage].[WorkPackageRequests] wpr
            WHERE wpr.[WorkPackageId] IN @WorkPackageIds
            ORDER BY wpr.[SortOrder] ASC, wpr.[AddedAt] ASC, wpr.[CreatedAt] ASC, wpr.[Id] ASC;
            """;

        var membershipRows = (await connection.QueryAsync<WorkPackageMembershipQueryRow>(
            new CommandDefinition(
                membershipsSql,
                new { WorkPackageIds = packageIds },
                cancellationToken: cancellationToken))).AsList();

        var enrichedMemberships = await EnrichScopeItemsAsync(
            membershipRows.Select(m => m.ToDto()).ToList(),
            cancellationToken);

        var membershipsByPackage = enrichedMemberships
            .GroupBy(m => m.WorkPackageId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<WorkPackageScopeItemDto>)g.ToList());

        var dtos = rows
            .Select(r => r.ToDto(
                membershipsByPackage.TryGetValue(r.Id, out var scope)
                    ? scope
                    : Array.Empty<WorkPackageScopeItemDto>()))
            .ToList();

        return await EnrichWorkPackagesAsync(dtos, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkPackageScopeItemDto>> GetWorkPackageScopeAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default)
    {
        if (workPackageId == Guid.Empty)
        {
            return Array.Empty<WorkPackageScopeItemDto>();
        }

        const string sql = """
            SELECT
                wpr.[Id],
                wpr.[WorkPackageId],
                wpr.[RequestId],
                wpr.[SortOrder],
                wpr.[AddedAt],
                wpr.[RemovedAt],
                wpr.[CreatedAt],
                wpr.[UpdatedAt]
            FROM [workpackage].[WorkPackageRequests] wpr
            WHERE wpr.[WorkPackageId] = @WorkPackageId
            ORDER BY wpr.[SortOrder] ASC, wpr.[AddedAt] ASC, wpr.[CreatedAt] ASC, wpr.[Id] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<WorkPackageMembershipQueryRow>(
            new CommandDefinition(sql, new { WorkPackageId = workPackageId }, cancellationToken: cancellationToken))).AsList();

        if (rows.Count == 0)
        {
            return Array.Empty<WorkPackageScopeItemDto>();
        }

        var dtos = rows.Select(r => r.ToDto()).ToList();
        return await EnrichScopeItemsAsync(dtos, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkPackageDto?> GetRequestWorkPackageAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            return null;
        }

        const string sql = """
            SELECT TOP (1)
                wp.[Id],
                wp.[Name],
                wp.[Objective],
                wp.[Status],
                wp.[OwnerPersonId],
                wp.[CustomerId],
                wp.[ProductId],
                wp.[ClosedReason],
                wp.[ClosedAt],
                wp.[CreatedAt],
                wp.[UpdatedAt]
            FROM [workpackage].[WorkPackages] wp
            INNER JOIN [workpackage].[WorkPackageRequests] wpr ON wpr.[WorkPackageId] = wp.[Id]
            WHERE wpr.[RequestId] = @RequestId
              AND wpr.[RemovedAt] IS NULL
              AND wp.[Status] IN ('ACTIVE', 'DRAFT')
            ORDER BY
                CASE WHEN wp.[Status] = 'ACTIVE' THEN 0 ELSE 1 END ASC,
                wpr.[AddedAt] DESC,
                wp.[CreatedAt] DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<WorkPackageQueryRow>(
            new CommandDefinition(sql, new { RequestId = requestId }, cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var scopeItems = await GetWorkPackageScopeAsync(row.Id, cancellationToken);
        var dto = row.ToDto(scopeItems);
        var enriched = await EnrichWorkPackagesAsync(new[] { dto }, cancellationToken);
        return enriched[0];
    }

    /// <inheritdoc />
    public async Task<bool> WorkPackageExistsAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default)
    {
        if (workPackageId == Guid.Empty)
        {
            return false;
        }

        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM [workpackage].[WorkPackages]
                WHERE [Id] = @WorkPackageId
            ) THEN 1 ELSE 0 END;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { WorkPackageId = workPackageId }, cancellationToken: cancellationToken));

        return result == 1;
    }

    // MediatR query handler entry points
    Task<WorkPackageDto?> IRequestHandler<GetWorkPackageByIdQuery, WorkPackageDto?>.Handle(
        GetWorkPackageByIdQuery request,
        CancellationToken cancellationToken)
        => GetWorkPackageByIdAsync(request.WorkPackageId, cancellationToken);

    Task<IReadOnlyList<WorkPackageDto>> IRequestHandler<ListWorkPackagesQuery, IReadOnlyList<WorkPackageDto>>.Handle(
        ListWorkPackagesQuery request,
        CancellationToken cancellationToken)
        => ListWorkPackagesAsync(
            request.Status,
            request.OwnerPersonId,
            request.CustomerId,
            request.ProductId,
            cancellationToken);

    Task<IReadOnlyList<WorkPackageScopeItemDto>> IRequestHandler<GetWorkPackageScopeQuery, IReadOnlyList<WorkPackageScopeItemDto>>.Handle(
        GetWorkPackageScopeQuery request,
        CancellationToken cancellationToken)
        => GetWorkPackageScopeAsync(request.WorkPackageId, cancellationToken);

    Task<WorkPackageDto?> IRequestHandler<GetRequestWorkPackageQuery, WorkPackageDto?>.Handle(
        GetRequestWorkPackageQuery request,
        CancellationToken cancellationToken)
        => GetRequestWorkPackageAsync(request.RequestId, cancellationToken);

    private static string? NormalizeStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        try
        {
            return WorkPackageStatusNames.FromName(status).ToName();
        }
        catch
        {
            return status.Trim().ToUpperInvariant();
        }
    }

    private async Task<IReadOnlyList<WorkPackageScopeItemDto>> EnrichScopeItemsAsync(
        IReadOnlyList<WorkPackageScopeItemDto> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0 || _requestQueryService is null)
        {
            return items;
        }

        var distinctRequestIds = items
            .Select(i => i.RequestId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (distinctRequestIds.Length == 0)
        {
            return items;
        }

        var requests = await _requestQueryService.GetRequestsByIdsAsync(distinctRequestIds, cancellationToken);
        var requestMap = requests.ToDictionary(r => r.Id);

        if (requestMap.Count < distinctRequestIds.Length)
        {
            foreach (var requestId in distinctRequestIds)
            {
                if (!requestMap.ContainsKey(requestId))
                {
                    var single = await _requestQueryService.GetRequestByIdAsync(requestId, cancellationToken);
                    if (single is not null)
                    {
                        requestMap[requestId] = single;
                    }
                }
            }
        }

        var enriched = new List<WorkPackageScopeItemDto>(items.Count);
        foreach (var item in items)
        {
            requestMap.TryGetValue(item.RequestId, out var requestDto);
            enriched.Add(item.EnrichWith(requestDto));
        }

        return enriched;
    }

    private async Task<IReadOnlyList<WorkPackageDto>> EnrichWorkPackagesAsync(
        IReadOnlyList<WorkPackageDto> items,
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
        var enriched = new List<WorkPackageDto>(items.Count);

        foreach (var item in items)
        {
            string? ownerName = null;
            if (item.OwnerPersonId != Guid.Empty && _organizationQueryService is not null)
            {
                if (!personCache.TryGetValue(item.OwnerPersonId, out ownerName))
                {
                    var person = await _organizationQueryService.GetPersonByIdAsync(item.OwnerPersonId, cancellationToken);
                    ownerName = person?.FullName;
                    personCache[item.OwnerPersonId] = ownerName;
                }
            }

            CustomerDto? customer = null;
            if (item.CustomerId.HasValue && item.CustomerId.Value != Guid.Empty && _customerQueryService is not null)
            {
                if (!customerCache.TryGetValue(item.CustomerId.Value, out customer))
                {
                    customer = await _customerQueryService.GetCustomerByIdAsync(item.CustomerId.Value, cancellationToken);
                    customerCache[item.CustomerId.Value] = customer;
                }
            }

            ProductDto? product = null;
            if (item.ProductId.HasValue && item.ProductId.Value != Guid.Empty && _productQueryService is not null)
            {
                if (!productCache.TryGetValue(item.ProductId.Value, out product))
                {
                    product = await _productQueryService.GetProductByIdAsync(item.ProductId.Value, cancellationToken);
                    productCache[item.ProductId.Value] = product;
                }
            }

            enriched.Add(item with
            {
                OwnerName = ownerName,
                CustomerName = customer?.CustomerName,
                CustomerCode = customer?.CustomerCode,
                ProductName = product?.Name,
                ProductCode = product?.Code
            });
        }

        return enriched;
    }

    private sealed class WorkPackageQueryRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Objective { get; init; } = string.Empty;
        public string Status { get; init; } = WorkPackageStatusNames.Draft;
        public Guid OwnerPersonId { get; init; }
        public Guid? CustomerId { get; init; }
        public Guid? ProductId { get; init; }
        public string? ClosedReason { get; init; }
        public DateTime? ClosedAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }

        public WorkPackageDto ToDto(IReadOnlyList<WorkPackageScopeItemDto>? requests = null)
        {
            var scope = requests ?? Array.Empty<WorkPackageScopeItemDto>();
            return new WorkPackageDto
            {
                Id = Id,
                Name = Name,
                Objective = Objective,
                Status = Status,
                OwnerPersonId = OwnerPersonId,
                CustomerId = CustomerId,
                ProductId = ProductId,
                ClosedReason = ClosedReason,
                ClosedAt = ClosedAt,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt,
                Requests = scope
            };
        }
    }

    private sealed class WorkPackageMembershipQueryRow
    {
        public Guid Id { get; init; }
        public Guid WorkPackageId { get; init; }
        public Guid RequestId { get; init; }
        public int SortOrder { get; init; }
        public DateTime AddedAt { get; init; }
        public DateTime? RemovedAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }

        public WorkPackageScopeItemDto ToDto()
        {
            return new WorkPackageScopeItemDto
            {
                Id = Id,
                WorkPackageId = WorkPackageId,
                RequestId = RequestId,
                SortOrder = SortOrder,
                AddedAt = AddedAt,
                RemovedAt = RemovedAt,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt
            };
        }
    }
}
