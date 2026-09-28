using System.Data;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Organization.Domain;
using Dapper;

namespace Cakra.Modules.Organization.Persistence;

/// <summary>
/// Dapper implementation of <see cref="ITeamRepository"/> using explicit parameterized SQL
/// against the <c>organization.Teams</c> table (Architecture §17, §19.3).
/// </summary>
internal sealed class TeamRepository : ITeamRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TeamRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [Name], [Description], [CreatedAt], [UpdatedAt]
            FROM [organization].[Teams]
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Team>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [Name], [Description], [CreatedAt], [UpdatedAt]
            FROM [organization].[Teams]
            ORDER BY [Name];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Team>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<Team?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [Name], [Description], [CreatedAt], [UpdatedAt]
            FROM [organization].[Teams]
            WHERE [Name] = @Name;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Team>(
            new CommandDefinition(sql, new { Name = name }, cancellationToken: cancellationToken));
    }

    public async Task AddAsync(Team entity, CancellationToken cancellationToken = default)
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
            INSERT INTO [organization].[Teams] (
                [Id], [Name], [Description], [CreatedAt], [UpdatedAt]
            ) VALUES (
                @Id, @Name, @Description, @CreatedAt, @UpdatedAt
            );
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            entity.Id,
            entity.Name,
            entity.Description,
            entity.CreatedAt,
            entity.UpdatedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Team entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt = DateTime.UtcNow;

        const string sql = """
            UPDATE [organization].[Teams]
            SET [Name] = @Name,
                [Description] = @Description,
                [UpdatedAt] = @UpdatedAt
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            entity.Id,
            entity.Name,
            entity.Description,
            entity.UpdatedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM [organization].[Teams]
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
