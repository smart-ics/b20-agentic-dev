using System.Data;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Models;
using Dapper;

namespace Cakra.Modules.Organization.Persistence;

/// <summary>
/// Dapper implementation of <see cref="IPersonRepository"/> using explicit parameterized SQL
/// against the <c>organization.Persons</c> table (Architecture §17, §19.3).
/// </summary>
internal sealed class PersonRepository : IPersonRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PersonRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [FirstName], [LastName], [Email], [Status], [CreatedAt], [UpdatedAt]
            FROM [organization].[Persons]
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Person>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<PersonDto?> GetPersonDtoByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                p.[Id],
                p.[FirstName],
                p.[LastName],
                p.[Email],
                p.[Status],
                p.[CreatedAt],
                p.[UpdatedAt],
                STRING_AGG(r.[Name], ',') WITHIN GROUP (ORDER BY r.[Name]) AS [RolesRaw]
            FROM [organization].[Persons] p
            LEFT JOIN [organization].[RoleAssignments] ra ON p.[Id] = ra.[PersonId] AND ra.[RevokedAt] IS NULL
            LEFT JOIN [organization].[Roles] r ON ra.[RoleId] = r.[Id]
            WHERE p.[Id] = @Id
            GROUP BY p.[Id], p.[FirstName], p.[LastName], p.[Email], p.[Status], p.[CreatedAt], p.[UpdatedAt];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<PersonDtoRow>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return row?.ToDto();
    }

    public async Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [FirstName], [LastName], [Email], [Status], [CreatedAt], [UpdatedAt]
            FROM [organization].[Persons]
            ORDER BY [LastName], [FirstName];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Person>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.AsList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PersonDto>> GetAllPersonDtosAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                p.[Id],
                p.[FirstName],
                p.[LastName],
                p.[Email],
                p.[Status],
                p.[CreatedAt],
                p.[UpdatedAt],
                STRING_AGG(r.[Name], ',') WITHIN GROUP (ORDER BY r.[Name]) AS [RolesRaw]
            FROM [organization].[Persons] p
            LEFT JOIN [organization].[RoleAssignments] ra ON p.[Id] = ra.[PersonId] AND ra.[RevokedAt] IS NULL
            LEFT JOIN [organization].[Roles] r ON ra.[RoleId] = r.[Id]
            GROUP BY p.[Id], p.[FirstName], p.[LastName], p.[Email], p.[Status], p.[CreatedAt], p.[UpdatedAt]
            ORDER BY p.[LastName], p.[FirstName];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<PersonDtoRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<Person>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [FirstName], [LastName], [Email], [Status], [CreatedAt], [UpdatedAt]
            FROM [organization].[Persons]
            WHERE [Status] = @Status
            ORDER BY [LastName], [FirstName];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Person>(
            new CommandDefinition(sql, new { Status = Person.StatusActive }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<Person?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [FirstName], [LastName], [Email], [Status], [CreatedAt], [UpdatedAt]
            FROM [organization].[Persons]
            WHERE [Email] = @Email;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Person>(
            new CommandDefinition(sql, new { Email = email }, cancellationToken: cancellationToken));
    }

    public async Task AddAsync(Person entity, CancellationToken cancellationToken = default)
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
            INSERT INTO [organization].[Persons] (
                [Id], [FirstName], [LastName], [Email], [Status], [CreatedAt], [UpdatedAt]
            ) VALUES (
                @Id, @FirstName, @LastName, @Email, @Status, @CreatedAt, @UpdatedAt
            );
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            entity.Id,
            entity.FirstName,
            entity.LastName,
            entity.Email,
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Person entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt = DateTime.UtcNow;

        const string sql = """
            UPDATE [organization].[Persons]
            SET [FirstName] = @FirstName,
                [LastName] = @LastName,
                [Email] = @Email,
                [Status] = @Status,
                [UpdatedAt] = @UpdatedAt
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            entity.Id,
            entity.FirstName,
            entity.LastName,
            entity.Email,
            entity.Status,
            entity.UpdatedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE [organization].[Persons]
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
            DELETE FROM [organization].[Persons]
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
