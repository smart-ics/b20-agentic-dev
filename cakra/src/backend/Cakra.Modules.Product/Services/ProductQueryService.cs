using Cakra.Core.Infrastructure.Persistence;
using Dapper;
using MediatR;

namespace Cakra.Modules.Product.Services;

/// <summary>
/// Dapper implementation of <see cref="IProductQueryService"/> executing explicit parameterized SQL
/// against the <c>product</c> schema (Architecture §7, §10, §15, §19.3, §20).
/// </summary>
public sealed class ProductQueryService :
    IProductQueryService,
    IRequestHandler<GetProductByIdQuery, ProductDto?>,
    IRequestHandler<GetProductByCodeQuery, ProductDto?>,
    IRequestHandler<ListActiveProductsQuery, IReadOnlyList<ProductDto>>,
    IRequestHandler<ListAllProductsQuery, IReadOnlyList<ProductDto>>
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProductQueryService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<ProductDto?> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [Code], [Name], [Description], [OwnerPersonId], [Status], [CreatedAt], [UpdatedAt]
            FROM [product].[Products]
            WHERE [Id] = @ProductId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ProductDto>(
            new CommandDefinition(sql, new { ProductId = productId }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public Task<ProductDto?> GetProductById(Guid productId, CancellationToken cancellationToken = default)
        => GetProductByIdAsync(productId, cancellationToken);

    /// <inheritdoc />
    public async Task<ProductDto?> GetProductByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        const string sql = """
            SELECT [Id], [Code], [Name], [Description], [OwnerPersonId], [Status], [CreatedAt], [UpdatedAt]
            FROM [product].[Products]
            WHERE [Code] = @Code;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ProductDto>(
            new CommandDefinition(sql, new { Code = code.Trim() }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public Task<ProductDto?> GetProductByCode(string code, CancellationToken cancellationToken = default)
        => GetProductByCodeAsync(code, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductDto>> ListActiveProductsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [Code], [Name], [Description], [OwnerPersonId], [Status], [CreatedAt], [UpdatedAt]
            FROM [product].[Products]
            WHERE [Status] = @Status
            ORDER BY [Name], [Code];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<ProductDto>(
            new CommandDefinition(sql, new { Status = Domain.Product.StatusActive }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ProductDto>> ListActiveProducts(CancellationToken cancellationToken = default)
        => ListActiveProductsAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductDto>> ListAllProductsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [Code], [Name], [Description], [OwnerPersonId], [Status], [CreatedAt], [UpdatedAt]
            FROM [product].[Products]
            ORDER BY [Name], [Code];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<ProductDto>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.AsList();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ProductDto>> ListAllProducts(CancellationToken cancellationToken = default)
        => ListAllProductsAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> IsProductActiveAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM [product].[Products]
                WHERE [Id] = @ProductId AND [Status] = @Status
            ) THEN 1 ELSE 0 END;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new { ProductId = productId, Status = Domain.Product.StatusActive },
                cancellationToken: cancellationToken));
        return result == 1;
    }

    // MediatR query handler entry points
    Task<ProductDto?> IRequestHandler<GetProductByIdQuery, ProductDto?>.Handle(
        GetProductByIdQuery request,
        CancellationToken cancellationToken)
        => GetProductByIdAsync(request.ProductId, cancellationToken);

    Task<ProductDto?> IRequestHandler<GetProductByCodeQuery, ProductDto?>.Handle(
        GetProductByCodeQuery request,
        CancellationToken cancellationToken)
        => GetProductByCodeAsync(request.Code, cancellationToken);

    Task<IReadOnlyList<ProductDto>> IRequestHandler<ListActiveProductsQuery, IReadOnlyList<ProductDto>>.Handle(
        ListActiveProductsQuery request,
        CancellationToken cancellationToken)
        => ListActiveProductsAsync(cancellationToken);

    Task<IReadOnlyList<ProductDto>> IRequestHandler<ListAllProductsQuery, IReadOnlyList<ProductDto>>.Handle(
        ListAllProductsQuery request,
        CancellationToken cancellationToken)
        => ListAllProductsAsync(cancellationToken);
}
