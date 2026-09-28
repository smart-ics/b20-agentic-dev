using Cakra.Core.Infrastructure.Persistence;
using Dapper;

namespace Cakra.Modules.Customer.Persistence;

/// <summary>
/// Dapper implementation of <see cref="ICustomerRepository"/> using explicit parameterized SQL
/// against the <c>customer.Customers</c> table (Architecture §17, §19.3, §20, §21).
/// </summary>
internal sealed class CustomerRepository : ICustomerRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CustomerRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Domain.Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [CustomerCode], [CustomerName], [Status], [HasActiveMaintenanceContract], [CreatedAt], [UpdatedAt]
            FROM [customer].[Customers]
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Domain.Customer>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Domain.Customer?> GetByCodeAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [CustomerCode], [CustomerName], [Status], [HasActiveMaintenanceContract], [CreatedAt], [UpdatedAt]
            FROM [customer].[Customers]
            WHERE [CustomerCode] = @CustomerCode;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Domain.Customer>(
            new CommandDefinition(sql, new { CustomerCode = customerCode }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<Domain.Customer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [CustomerCode], [CustomerName], [Status], [HasActiveMaintenanceContract], [CreatedAt], [UpdatedAt]
            FROM [customer].[Customers]
            ORDER BY [CustomerName], [CustomerCode];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Domain.Customer>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IReadOnlyList<Domain.Customer>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [CustomerCode], [CustomerName], [Status], [HasActiveMaintenanceContract], [CreatedAt], [UpdatedAt]
            FROM [customer].[Customers]
            WHERE [Status] = @Status
            ORDER BY [CustomerName], [CustomerCode];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Domain.Customer>(
            new CommandDefinition(sql, new { Status = Domain.Customer.StatusActive }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task AddAsync(Domain.Customer entity, CancellationToken cancellationToken = default)
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
            INSERT INTO [customer].[Customers] (
                [Id], [CustomerCode], [CustomerName], [Status], [HasActiveMaintenanceContract], [CreatedAt], [UpdatedAt]
            ) VALUES (
                @Id, @CustomerCode, @CustomerName, @Status, @HasActiveMaintenanceContract, @CreatedAt, @UpdatedAt
            );
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            entity.Id,
            entity.CustomerCode,
            entity.CustomerName,
            entity.Status,
            entity.HasActiveMaintenanceContract,
            entity.CreatedAt,
            entity.UpdatedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Domain.Customer entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt ??= DateTime.UtcNow;

        const string sql = """
            UPDATE [customer].[Customers]
            SET [CustomerCode] = @CustomerCode,
                [CustomerName] = @CustomerName,
                [Status] = @Status,
                [HasActiveMaintenanceContract] = @HasActiveMaintenanceContract,
                [UpdatedAt] = @UpdatedAt
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            entity.Id,
            entity.CustomerCode,
            entity.CustomerName,
            entity.Status,
            entity.HasActiveMaintenanceContract,
            entity.UpdatedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE [customer].[Customers]
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
            DELETE FROM [customer].[Customers]
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
