using Cakra.Core.Infrastructure.Persistence;
using Dapper;

namespace Cakra.Modules.Product.Persistence;

/// <summary>
/// Dapper implementation of <see cref="IProductRepository"/> using explicit parameterized SQL
/// against the <c>product.Products</c> table (Architecture §10, §17, §19.3, §20, §21).
/// </summary>
internal sealed class ProductRepository : IProductRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProductRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Domain.Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [Code], [Name], [Description], [OwnerPersonId], [Status], [CreatedAt], [UpdatedAt]
            FROM [product].[Products]
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Domain.Product>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Domain.Product?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [Code], [Name], [Description], [OwnerPersonId], [Status], [CreatedAt], [UpdatedAt]
            FROM [product].[Products]
            WHERE [Code] = @Code;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Domain.Product>(
            new CommandDefinition(sql, new { Code = code }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<Domain.Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [Code], [Name], [Description], [OwnerPersonId], [Status], [CreatedAt], [UpdatedAt]
            FROM [product].[Products]
            ORDER BY [Name], [Code];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Domain.Product>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IReadOnlyList<Domain.Product>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [Code], [Name], [Description], [OwnerPersonId], [Status], [CreatedAt], [UpdatedAt]
            FROM [product].[Products]
            WHERE [Status] = @Status
            ORDER BY [Name], [Code];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Domain.Product>(
            new CommandDefinition(sql, new { Status = Domain.Product.StatusActive }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IReadOnlyList<Domain.Product>> GetByOwnerPersonIdAsync(Guid ownerPersonId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [Code], [Name], [Description], [OwnerPersonId], [Status], [CreatedAt], [UpdatedAt]
            FROM [product].[Products]
            WHERE [OwnerPersonId] = @OwnerPersonId
            ORDER BY [Name], [Code];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Domain.Product>(
            new CommandDefinition(sql, new { OwnerPersonId = ownerPersonId }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task AddAsync(Domain.Product entity, CancellationToken cancellationToken = default)
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

        const string sql = """
            INSERT INTO [product].[Products] (
                [Id], [Code], [Name], [Description], [OwnerPersonId], [Status], [CreatedAt], [UpdatedAt]
            ) VALUES (
                @Id, @Code, @Name, @Description, @OwnerPersonId, @Status, @CreatedAt, @UpdatedAt
            );
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Description,
            entity.OwnerPersonId,
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Domain.Product entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt ??= DateTime.UtcNow;

        const string sql = """
            UPDATE [product].[Products]
            SET [Code] = @Code,
                [Name] = @Name,
                [Description] = @Description,
                [OwnerPersonId] = @OwnerPersonId,
                [Status] = @Status,
                [UpdatedAt] = @UpdatedAt
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Description,
            entity.OwnerPersonId,
            entity.Status,
            entity.UpdatedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE [product].[Products]
            SET [Status] = @Status,
                [UpdatedAt] = @UpdatedAt
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            Status = status,
            UpdatedAt = DateTime.UtcNow
        }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM [product].[Products]
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
