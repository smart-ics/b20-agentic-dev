namespace ICS.Modules.Customer.Persistence;

using Dapper;
using ICS.Core.Data;
using ICS.Modules.Customer.Domain;

/// <summary>
/// Dapper-based repository implementation for CustomerContact entity.
/// Uses explicit parameterized SQL against customer.CustomerContacts table.
/// Architecture §17, §19.3, §20.
/// </summary>
internal class CustomerContactRepository : ICustomerContactRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CustomerContactRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<CustomerContact?> GetByIdAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                ContactId AS Id,
                CustomerId,
                Name,
                Position,
                PhoneNumber,
                Email,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [customer].[CustomerContacts]
            WHERE ContactId = @ContactId;";

        return await connection.QuerySingleOrDefaultAsync<CustomerContact>(sql, new { ContactId = contactId });
    }

    public async Task<IReadOnlyList<CustomerContact>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                ContactId AS Id,
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

        var results = await connection.QueryAsync<CustomerContact>(sql, new { CustomerId = customerId });
        return results.ToList();
    }

    public async Task AddAsync(CustomerContact contact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contact);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO [customer].[CustomerContacts] (
                ContactId,
                CustomerId,
                Name,
                Position,
                PhoneNumber,
                Email,
                Status,
                CreatedAt,
                UpdatedAt
            ) VALUES (
                @ContactId,
                @CustomerId,
                @Name,
                @Position,
                @PhoneNumber,
                @Email,
                @Status,
                @CreatedAt,
                @UpdatedAt
            );";

        await connection.ExecuteAsync(sql, new
        {
            ContactId = contact.Id,
            contact.CustomerId,
            contact.Name,
            contact.Position,
            contact.PhoneNumber,
            contact.Email,
            contact.Status,
            contact.CreatedAt,
            contact.UpdatedAt
        });
    }

    public async Task UpdateAsync(CustomerContact contact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contact);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            UPDATE [customer].[CustomerContacts]
            SET 
                Name = @Name,
                Position = @Position,
                PhoneNumber = @PhoneNumber,
                Email = @Email,
                Status = @Status,
                UpdatedAt = @UpdatedAt
            WHERE ContactId = @ContactId;";

        await connection.ExecuteAsync(sql, new
        {
            ContactId = contact.Id,
            contact.Name,
            contact.Position,
            contact.PhoneNumber,
            contact.Email,
            contact.Status,
            contact.UpdatedAt
        });
    }
}
