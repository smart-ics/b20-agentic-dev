namespace ICS.Modules.Customer.Persistence;

using Dapper;
using ICS.Core.Data;
using ICS.Modules.Customer.Domain;

/// <summary>
/// Dapper-based repository implementation for Customer entity.
/// Uses explicit parameterized SQL against customer.Customers table.
/// Architecture §17, §19.3, §20.
/// </summary>
internal class CustomerRepository : ICustomerRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CustomerRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                CustomerId AS Id,
                CustomerCode,
                CustomerName,
                Status,
                HasActiveMaintenanceContract,
                CreatedAt,
                UpdatedAt
            FROM [customer].[Customers]
            WHERE CustomerId = @CustomerId;";

        return await connection.QuerySingleOrDefaultAsync<Customer>(sql, new { CustomerId = customerId });
    }

    public async Task<Customer?> GetByCodeAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                CustomerId AS Id,
                CustomerCode,
                CustomerName,
                Status,
                HasActiveMaintenanceContract,
                CreatedAt,
                UpdatedAt
            FROM [customer].[Customers]
            WHERE UPPER(CustomerCode) = UPPER(@CustomerCode);";

        return await connection.QuerySingleOrDefaultAsync<Customer>(sql, new { CustomerCode = customerCode.Trim() });
    }

    public async Task<IReadOnlyList<Customer>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                CustomerId AS Id,
                CustomerCode,
                CustomerName,
                Status,
                HasActiveMaintenanceContract,
                CreatedAt,
                UpdatedAt
            FROM [customer].[Customers]
            WHERE Status = @Status
            ORDER BY CustomerName ASC;";

        var results = await connection.QueryAsync<Customer>(sql, new { Status = Customer.StatusActive });
        return results.ToList();
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO [customer].[Customers] (
                CustomerId,
                CustomerCode,
                CustomerName,
                Status,
                HasActiveMaintenanceContract,
                CreatedAt,
                UpdatedAt
            ) VALUES (
                @CustomerId,
                @CustomerCode,
                @CustomerName,
                @Status,
                @HasActiveMaintenanceContract,
                @CreatedAt,
                @UpdatedAt
            );";

        await connection.ExecuteAsync(sql, new
        {
            CustomerId = customer.Id,
            customer.CustomerCode,
            customer.CustomerName,
            customer.Status,
            customer.HasActiveMaintenanceContract,
            customer.CreatedAt,
            customer.UpdatedAt
        });
    }

    public async Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            UPDATE [customer].[Customers]
            SET 
                CustomerCode = @CustomerCode,
                CustomerName = @CustomerName,
                Status = @Status,
                HasActiveMaintenanceContract = @HasActiveMaintenanceContract,
                UpdatedAt = @UpdatedAt
            WHERE CustomerId = @CustomerId;";

        await connection.ExecuteAsync(sql, new
        {
            CustomerId = customer.Id,
            customer.CustomerCode,
            customer.CustomerName,
            customer.Status,
            customer.HasActiveMaintenanceContract,
            customer.UpdatedAt
        });
    }
}
