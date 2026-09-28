namespace ICS.Modules.Product.Persistence;

using Dapper;
using ICS.Core.Data;
using ICS.Modules.Product.Domain;

/// <summary>
/// Dapper-based repository implementation for Product entity.
/// Uses explicit parameterized SQL against product.Products table.
/// Architecture §10, §17, §19.3, §20.
/// </summary>
internal class ProductRepository : IProductRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProductRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Product?> GetByIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                ProductId AS Id,
                Code,
                Name,
                Description,
                OwnerPersonId,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [product].[Products]
            WHERE ProductId = @ProductId;";

        return await connection.QuerySingleOrDefaultAsync<Product>(sql, new { ProductId = productId });
    }

    public async Task<Product?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                ProductId AS Id,
                Code,
                Name,
                Description,
                OwnerPersonId,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [product].[Products]
            WHERE UPPER(Code) = UPPER(@Code);";

        return await connection.QuerySingleOrDefaultAsync<Product>(sql, new { Code = code.Trim() });
    }

    public async Task<IReadOnlyList<Product>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                ProductId AS Id,
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

        var results = await connection.QueryAsync<Product>(sql, new { Status = Product.StatusActive });
        return results.ToList();
    }

    public async Task<IReadOnlyList<Product>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                ProductId AS Id,
                Code,
                Name,
                Description,
                OwnerPersonId,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [product].[Products]
            ORDER BY Name ASC;";

        var results = await connection.QueryAsync<Product>(sql);
        return results.ToList();
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO [product].[Products] (
                ProductId,
                Code,
                Name,
                Description,
                OwnerPersonId,
                Status,
                CreatedAt,
                UpdatedAt
            ) VALUES (
                @ProductId,
                @Code,
                @Name,
                @Description,
                @OwnerPersonId,
                @Status,
                @CreatedAt,
                @UpdatedAt
            );";

        await connection.ExecuteAsync(sql, new
        {
            ProductId = product.Id,
            product.Code,
            product.Name,
            product.Description,
            product.OwnerPersonId,
            product.Status,
            product.CreatedAt,
            product.UpdatedAt
        });
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            UPDATE [product].[Products]
            SET 
                Name = @Name,
                Description = @Description,
                OwnerPersonId = @OwnerPersonId,
                Status = @Status,
                UpdatedAt = @UpdatedAt
            WHERE ProductId = @ProductId;";

        await connection.ExecuteAsync(sql, new
        {
            ProductId = product.Id,
            product.Name,
            product.Description,
            product.OwnerPersonId,
            product.Status,
            product.UpdatedAt
        });
    }
}
