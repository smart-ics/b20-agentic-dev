using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Customer.Domain;
using Dapper;

namespace Cakra.Modules.Customer.Persistence;

/// <summary>
/// Dapper implementation of <see cref="ICustomerContactRepository"/> using explicit parameterized SQL
/// against the <c>customer.CustomerContacts</c> table (Architecture §17, §19.3, §20, §21).
/// </summary>
internal sealed class CustomerContactRepository : ICustomerContactRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CustomerContactRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<CustomerContact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [CustomerId], [Name], [Position], [PhoneNumber], [Email], [Status], [CreatedAt], [UpdatedAt]
            FROM [customer].[CustomerContacts]
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CustomerContact>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CustomerContact>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [CustomerId], [Name], [Position], [PhoneNumber], [Email], [Status], [CreatedAt], [UpdatedAt]
            FROM [customer].[CustomerContacts]
            ORDER BY [Name];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<CustomerContact>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IReadOnlyList<CustomerContact>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [CustomerId], [Name], [Position], [PhoneNumber], [Email], [Status], [CreatedAt], [UpdatedAt]
            FROM [customer].[CustomerContacts]
            WHERE [CustomerId] = @CustomerId
            ORDER BY [Name];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<CustomerContact>(
            new CommandDefinition(sql, new { CustomerId = customerId }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IReadOnlyList<CustomerContact>> GetActiveByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [CustomerId], [Name], [Position], [PhoneNumber], [Email], [Status], [CreatedAt], [UpdatedAt]
            FROM [customer].[CustomerContacts]
            WHERE [CustomerId] = @CustomerId AND [Status] = @Status
            ORDER BY [Name];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<CustomerContact>(
            new CommandDefinition(sql, new { CustomerId = customerId, Status = CustomerContact.StatusActive }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task AddAsync(CustomerContact entity, CancellationToken cancellationToken = default)
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
            INSERT INTO [customer].[CustomerContacts] (
                [Id], [CustomerId], [Name], [Position], [PhoneNumber], [Email], [Status], [CreatedAt], [UpdatedAt]
            ) VALUES (
                @Id, @CustomerId, @Name, @Position, @PhoneNumber, @Email, @Status, @CreatedAt, @UpdatedAt
            );
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            entity.Id,
            entity.CustomerId,
            entity.Name,
            entity.Position,
            entity.PhoneNumber,
            entity.Email,
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(CustomerContact entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt ??= DateTime.UtcNow;

        const string sql = """
            UPDATE [customer].[CustomerContacts]
            SET [CustomerId] = @CustomerId,
                [Name] = @Name,
                [Position] = @Position,
                [PhoneNumber] = @PhoneNumber,
                [Email] = @Email,
                [Status] = @Status,
                [UpdatedAt] = @UpdatedAt
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            entity.Id,
            entity.CustomerId,
            entity.Name,
            entity.Position,
            entity.PhoneNumber,
            entity.Email,
            entity.Status,
            entity.UpdatedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE [customer].[CustomerContacts]
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
            DELETE FROM [customer].[CustomerContacts]
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
