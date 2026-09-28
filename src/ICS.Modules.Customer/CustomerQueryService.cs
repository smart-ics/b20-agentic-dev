namespace ICS.Modules.Customer;

using Dapper;
using ICS.Core.Data;
using ICS.Modules.Customer.Application.DTOs;

/// <summary>
/// Concrete implementation of <see cref="ICustomerQueryService"/> querying the customer schema via Dapper.
/// Uses explicit parameterized SQL against customer.Customers and customer.CustomerContacts tables.
/// Architecture §7, §15, §16, §17, §19.3, §20.
/// </summary>
public class CustomerQueryService : ICustomerQueryService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CustomerQueryService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<CustomerDto?> GetCustomerByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

            const string sql = @"
                SELECT 
                    CustomerId,
                    CustomerCode,
                    CustomerName,
                    Status,
                    HasActiveMaintenanceContract,
                    CreatedAt,
                    UpdatedAt
                FROM [customer].[Customers]
                WHERE CustomerId = @CustomerId;";

            return await connection.QuerySingleOrDefaultAsync<CustomerDto>(sql, new { CustomerId = customerId });
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomerDto>> ListActiveCustomersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

            const string sql = @"
                SELECT 
                    CustomerId,
                    CustomerCode,
                    CustomerName,
                    Status,
                    HasActiveMaintenanceContract,
                    CreatedAt,
                    UpdatedAt
                FROM [customer].[Customers]
                WHERE Status = 'ACTIVE'
                ORDER BY CustomerName ASC;";

            var results = await connection.QueryAsync<CustomerDto>(sql);
            return results.ToList();
        }
        catch
        {
            return Array.Empty<CustomerDto>();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomerContactDto>> GetCustomerContactsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

            const string sql = @"
                SELECT 
                    ContactId,
                    CustomerId,
                    Name,
                    Position,
                    PhoneNumber,
                    Email,
                    Status,
                    CreatedAt,
                    UpdatedAt
                FROM [customer].[CustomerContacts]
                WHERE CustomerId = @CustomerId
                ORDER BY Name ASC;";

            var results = await connection.QueryAsync<CustomerContactDto>(sql, new { CustomerId = customerId });
            return results.ToList();
        }
        catch
        {
            return Array.Empty<CustomerContactDto>();
        }
    }

    /// <inheritdoc />
    public async Task<CustomerWithContractStatusDto?> GetCustomerWithContractStatusAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

            const string sql = @"
                SELECT 
                    CustomerId,
                    CustomerCode,
                    CustomerName,
                    Status,
                    HasActiveMaintenanceContract
                FROM [customer].[Customers]
                WHERE CustomerId = @CustomerId;";

            return await connection.QuerySingleOrDefaultAsync<CustomerWithContractStatusDto>(sql, new { CustomerId = customerId });
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public CustomerDto? GetCustomerById(Guid customerId)
    {
        return GetCustomerByIdAsync(customerId).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public IReadOnlyList<CustomerDto> ListActiveCustomers()
    {
        return ListActiveCustomersAsync().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public IReadOnlyList<CustomerContactDto> GetCustomerContacts(Guid customerId)
    {
        return GetCustomerContactsAsync(customerId).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public CustomerWithContractStatusDto? GetCustomerWithContractStatus(Guid customerId)
    {
        return GetCustomerWithContractStatusAsync(customerId).GetAwaiter().GetResult();
    }
}
