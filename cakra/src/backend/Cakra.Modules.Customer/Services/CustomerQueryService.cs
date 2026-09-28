using Cakra.Core.Infrastructure.Persistence;
using Dapper;
using MediatR;

namespace Cakra.Modules.Customer.Services;

/// <summary>
/// Dapper implementation of <see cref="ICustomerQueryService"/> executing explicit parameterized SQL
/// against the <c>customer</c> schema (Architecture §7, §15, §19.3, §20).
/// </summary>
public sealed class CustomerQueryService :
    ICustomerQueryService,
    IRequestHandler<GetCustomerByIdQuery, CustomerDto?>,
    IRequestHandler<ListActiveCustomersQuery, IReadOnlyList<CustomerDto>>,
    IRequestHandler<GetCustomerContactsQuery, IReadOnlyList<CustomerContactDto>>,
    IRequestHandler<GetCustomerWithContractStatusQuery, CustomerWithContractStatusDto?>
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CustomerQueryService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<CustomerDto?> GetCustomerByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [CustomerCode], [CustomerName], [Status], [HasActiveMaintenanceContract], [CreatedAt], [UpdatedAt]
            FROM [customer].[Customers]
            WHERE [Id] = @CustomerId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CustomerDto>(
            new CommandDefinition(sql, new { CustomerId = customerId }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public Task<CustomerDto?> GetCustomerById(Guid customerId, CancellationToken cancellationToken = default)
        => GetCustomerByIdAsync(customerId, cancellationToken);

    /// <inheritdoc />
    public async Task<CustomerDto?> GetCustomerByCodeAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerCode))
        {
            return null;
        }

        const string sql = """
            SELECT [Id], [CustomerCode], [CustomerName], [Status], [HasActiveMaintenanceContract], [CreatedAt], [UpdatedAt]
            FROM [customer].[Customers]
            WHERE [CustomerCode] = @CustomerCode;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CustomerDto>(
            new CommandDefinition(sql, new { CustomerCode = customerCode.Trim() }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public Task<CustomerDto?> GetCustomerByCode(string customerCode, CancellationToken cancellationToken = default)
        => GetCustomerByCodeAsync(customerCode, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomerDto>> ListActiveCustomersAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [CustomerCode], [CustomerName], [Status], [HasActiveMaintenanceContract], [CreatedAt], [UpdatedAt]
            FROM [customer].[Customers]
            WHERE [Status] = @Status
            ORDER BY [CustomerName], [CustomerCode];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<CustomerDto>(
            new CommandDefinition(sql, new { Status = Domain.Customer.StatusActive }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CustomerDto>> ListActiveCustomers(CancellationToken cancellationToken = default)
        => ListActiveCustomersAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomerDto>> ListAllCustomersAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [CustomerCode], [CustomerName], [Status], [HasActiveMaintenanceContract], [CreatedAt], [UpdatedAt]
            FROM [customer].[Customers]
            ORDER BY [CustomerName], [CustomerCode];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<CustomerDto>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.AsList();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CustomerDto>> ListAllCustomers(CancellationToken cancellationToken = default)
        => ListAllCustomersAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomerContactDto>> GetCustomerContactsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [CustomerId], [Name], [Position], [PhoneNumber], [Email], [Status], [CreatedAt], [UpdatedAt]
            FROM [customer].[CustomerContacts]
            WHERE [CustomerId] = @CustomerId
            ORDER BY [Name];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<CustomerContactDto>(
            new CommandDefinition(sql, new { CustomerId = customerId }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CustomerContactDto>> GetCustomerContacts(Guid customerId, CancellationToken cancellationToken = default)
        => GetCustomerContactsAsync(customerId, cancellationToken);

    /// <inheritdoc />
    public async Task<CustomerWithContractStatusDto?> GetCustomerWithContractStatusAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [CustomerCode], [CustomerName], [Status], [HasActiveMaintenanceContract], [CreatedAt], [UpdatedAt]
            FROM [customer].[Customers]
            WHERE [Id] = @CustomerId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CustomerWithContractStatusDto>(
            new CommandDefinition(sql, new { CustomerId = customerId }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public Task<CustomerWithContractStatusDto?> GetCustomerWithContractStatus(Guid customerId, CancellationToken cancellationToken = default)
        => GetCustomerWithContractStatusAsync(customerId, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> IsCustomerActiveAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM [customer].[Customers]
                WHERE [Id] = @CustomerId AND [Status] = 'ACTIVE'
            ) THEN 1 ELSE 0 END;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { CustomerId = customerId }, cancellationToken: cancellationToken));
        return result == 1;
    }

    // MediatR query handler entry points
    Task<CustomerDto?> IRequestHandler<GetCustomerByIdQuery, CustomerDto?>.Handle(
        GetCustomerByIdQuery request,
        CancellationToken cancellationToken)
        => GetCustomerByIdAsync(request.CustomerId, cancellationToken);

    Task<IReadOnlyList<CustomerDto>> IRequestHandler<ListActiveCustomersQuery, IReadOnlyList<CustomerDto>>.Handle(
        ListActiveCustomersQuery request,
        CancellationToken cancellationToken)
        => ListActiveCustomersAsync(cancellationToken);

    Task<IReadOnlyList<CustomerContactDto>> IRequestHandler<GetCustomerContactsQuery, IReadOnlyList<CustomerContactDto>>.Handle(
        GetCustomerContactsQuery request,
        CancellationToken cancellationToken)
        => GetCustomerContactsAsync(request.CustomerId, cancellationToken);

    Task<CustomerWithContractStatusDto?> IRequestHandler<GetCustomerWithContractStatusQuery, CustomerWithContractStatusDto?>.Handle(
        GetCustomerWithContractStatusQuery request,
        CancellationToken cancellationToken)
        => GetCustomerWithContractStatusAsync(request.CustomerId, cancellationToken);
}
