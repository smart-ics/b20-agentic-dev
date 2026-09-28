namespace ICS.Modules.Product;

using Dapper;
using ICS.Core.Data;
using ICS.Modules.Organization;
using ICS.Modules.Product.Application.DTOs;
using ICS.Modules.Product.Domain;

/// <summary>
/// Concrete implementation of <see cref="IProductQueryService"/> querying the product schema via Dapper.
/// Uses explicit parameterized SQL against product.Products table.
/// Architecture §10, §15, §16, §17, §19.3, §20.
/// </summary>
public class ProductQueryService : IProductQueryService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IOrganizationQueryService _organizationQueryService;

    public ProductQueryService(
        IDbConnectionFactory connectionFactory,
        IOrganizationQueryService organizationQueryService)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
    }

    /// <inheritdoc />
    public async Task<ProductDto?> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

            const string sql = @"
                SELECT 
                    ProductId,
                    Code,
                    Name,
                    Description,
                    OwnerPersonId,
                    Status,
                    CreatedAt,
                    UpdatedAt
                FROM [product].[Products]
                WHERE ProductId = @ProductId;";

            var product = await connection.QuerySingleOrDefaultAsync<ProductDto>(sql, new { ProductId = productId });
            if (product == null)
            {
                return null;
            }

            var owner = await _organizationQueryService.GetPersonByIdAsync(product.OwnerPersonId, cancellationToken);
            return product with { OwnerName = owner?.Name };
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ProductDto?> GetProductByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

            const string sql = @"
                SELECT 
                    ProductId,
                    Code,
                    Name,
                    Description,
                    OwnerPersonId,
                    Status,
                    CreatedAt,
                    UpdatedAt
                FROM [product].[Products]
                WHERE UPPER(Code) = UPPER(@Code);";

            var product = await connection.QuerySingleOrDefaultAsync<ProductDto>(sql, new { Code = code.Trim() });
            if (product == null)
            {
                return null;
            }

            var owner = await _organizationQueryService.GetPersonByIdAsync(product.OwnerPersonId, cancellationToken);
            return product with { OwnerName = owner?.Name };
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductDto>> ListActiveProductsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

            const string sql = @"
                SELECT 
                    ProductId,
                    Code,
                    Name,
                    Description,
                    OwnerPersonId,
                    Status,
                    CreatedAt,
                    UpdatedAt
                FROM [product].[Products]
                WHERE Status = @Status
                ORDER BY Name ASC;";

            var products = (await connection.QueryAsync<ProductDto>(sql, new { Status = Product.StatusActive })).ToList();
            if (products.Count == 0)
            {
                return products;
            }

            try
            {
                var activePersons = await _organizationQueryService.ListActivePersonsAsync(cancellationToken);
                var personMap = activePersons.ToDictionary(p => p.PersonId, p => p.Name);

                return products.Select(p => personMap.TryGetValue(p.OwnerPersonId, out var ownerName)
                    ? p with { OwnerName = ownerName }
                    : p).ToList();
            }
            catch
            {
                return products;
            }
        }
        catch
        {
            return Array.Empty<ProductDto>();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductDto>> ListAllProductsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

            const string sql = @"
                SELECT 
                    ProductId,
                    Code,
                    Name,
                    Description,
                    OwnerPersonId,
                    Status,
                    CreatedAt,
                    UpdatedAt
                FROM [product].[Products]
                ORDER BY Name ASC;";

            var products = (await connection.QueryAsync<ProductDto>(sql)).ToList();
            if (products.Count == 0)
            {
                return products;
            }

            try
            {
                var activePersons = await _organizationQueryService.ListActivePersonsAsync(cancellationToken);
                var personMap = activePersons.ToDictionary(p => p.PersonId, p => p.Name);

                return products.Select(p => personMap.TryGetValue(p.OwnerPersonId, out var ownerName)
                    ? p with { OwnerName = ownerName }
                    : p).ToList();
            }
            catch
            {
                return products;
            }
        }
        catch
        {
            return Array.Empty<ProductDto>();
        }
    }

    /// <inheritdoc />
    public ProductDto? GetProductById(Guid productId)
    {
        return GetProductByIdAsync(productId).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public ProductDto? GetProductByCode(string code)
    {
        return GetProductByCodeAsync(code).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public IReadOnlyList<ProductDto> ListActiveProducts()
    {
        return ListActiveProductsAsync().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public IReadOnlyList<ProductDto> ListAllProducts()
    {
        return ListAllProductsAsync().GetAwaiter().GetResult();
    }
}
