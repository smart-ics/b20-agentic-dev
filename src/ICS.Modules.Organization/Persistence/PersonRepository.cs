namespace ICS.Modules.Organization.Persistence;

using Dapper;
using ICS.Core.Data;
using ICS.Modules.Organization.Domain;

/// <summary>
/// Dapper-based repository implementation for Person entity.
/// Uses explicit parameterized SQL against organization.Persons table.
/// Architecture §17, §19.3, §20.
/// </summary>
internal class PersonRepository : IPersonRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PersonRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Person?> GetByIdAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                PersonId AS Id,
                Name,
                Email,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [organization].[Persons]
            WHERE PersonId = @PersonId;";

        return await connection.QuerySingleOrDefaultAsync<Person>(sql, new { PersonId = personId });
    }

    public async Task<Person?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                PersonId AS Id,
                Name,
                Email,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [organization].[Persons]
            WHERE LOWER(Email) = LOWER(@Email);";

        return await connection.QuerySingleOrDefaultAsync<Person>(sql, new { Email = email.Trim() });
    }

    public async Task<IReadOnlyList<Person>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                PersonId AS Id,
                Name,
                Email,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [organization].[Persons]
            WHERE Status = @Status
            ORDER BY Name ASC;";

        var results = await connection.QueryAsync<Person>(sql, new { Status = Person.StatusActive });
        return results.ToList();
    }

    public async Task AddAsync(Person person, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(person);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO [organization].[Persons] (
                PersonId,
                Name,
                Email,
                Status,
                CreatedAt,
                UpdatedAt
            ) VALUES (
                @PersonId,
                @Name,
                @Email,
                @Status,
                @CreatedAt,
                @UpdatedAt
            );";

        await connection.ExecuteAsync(sql, new
        {
            PersonId = person.Id,
            person.Name,
            person.Email,
            person.Status,
            person.CreatedAt,
            person.UpdatedAt
        });
    }

    public async Task UpdateAsync(Person person, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(person);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            UPDATE [organization].[Persons]
            SET 
                Name = @Name,
                Email = @Email,
                Status = @Status,
                UpdatedAt = @UpdatedAt
            WHERE PersonId = @PersonId;";

        await connection.ExecuteAsync(sql, new
        {
            PersonId = person.Id,
            person.Name,
            person.Email,
            person.Status,
            person.UpdatedAt
        });
    }
}
