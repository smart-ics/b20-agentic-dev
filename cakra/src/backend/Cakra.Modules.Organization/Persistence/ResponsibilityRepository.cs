using System.Data;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Organization.Domain;
using Dapper;

namespace Cakra.Modules.Organization.Persistence;

/// <summary>
/// Dapper implementation of <see cref="IResponsibilityRepository"/> using explicit parameterized SQL
/// against the <c>organization.Responsibilities</c> table (Architecture §17, §19.3).
/// </summary>
internal sealed class ResponsibilityRepository : IResponsibilityRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ResponsibilityRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Responsibility?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [Name], [Description], [CreatedAt], [UpdatedAt]
            FROM [organization].[Responsibilities]
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Responsibility>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<Responsibility>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [Name], [Description], [CreatedAt], [UpdatedAt]
            FROM [organization].[Responsibilities]
            ORDER BY [Name];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<Responsibility>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<Responsibility?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [Id], [Name], [Description], [CreatedAt], [UpdatedAt]
            FROM [organization].[Responsibilities]
            WHERE [Name] = @Name;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Responsibility>(
            new CommandDefinition(sql, new { Name = name }, cancellationToken: cancellationToken));
    }

    public async Task AddAsync(Responsibility entity, CancellationToken cancellationToken = default)
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
            INSERT INTO [organization].[Responsibilities] (
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

    public async Task UpdateAsync(Responsibility entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt = DateTime.UtcNow;

        const string sql = """
            UPDATE [organization].[Responsibilities]
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
            DELETE FROM [organization].[Responsibilities]
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
