using System.Data;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Organization.Domain;
using Dapper;

namespace Cakra.Modules.Organization.Persistence;

/// <summary>
/// Dapper implementation of <see cref="ITeamMembershipRepository"/> using explicit parameterized SQL
/// against the <c>organization.TeamMemberships</c> table (Architecture §17, §19.3).
/// </summary>
internal sealed class TeamMembershipRepository : ITeamMembershipRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TeamMembershipRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task AddAsync(TeamMembership membership, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(membership);

        if (membership.AssignedAt == default)
        {
            membership.AssignedAt = DateTime.UtcNow;
        }

        const string sql = """
            IF NOT EXISTS (
                SELECT 1 FROM [organization].[TeamMemberships]
                WHERE [PersonId] = @PersonId AND [TeamId] = @TeamId
            )
            BEGIN
                INSERT INTO [organization].[TeamMemberships] ([PersonId], [TeamId], [AssignedAt])
                VALUES (@PersonId, @TeamId, @AssignedAt);
            END;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            membership.PersonId,
            membership.TeamId,
            membership.AssignedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task RemoveAsync(Guid personId, Guid teamId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM [organization].[TeamMemberships]
            WHERE [PersonId] = @PersonId AND [TeamId] = @TeamId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            PersonId = personId,
            TeamId = teamId
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TeamMembership>> GetByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [PersonId], [TeamId], [AssignedAt]
            FROM [organization].[TeamMemberships]
            WHERE [PersonId] = @PersonId
            ORDER BY [AssignedAt] DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<TeamMembership>(
            new CommandDefinition(sql, new { PersonId = personId }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IReadOnlyList<TeamMembership>> GetByTeamIdAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [PersonId], [TeamId], [AssignedAt]
            FROM [organization].[TeamMemberships]
            WHERE [TeamId] = @TeamId
            ORDER BY [AssignedAt] DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<TeamMembership>(
            new CommandDefinition(sql, new { TeamId = teamId }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<bool> ExistsAsync(Guid personId, Guid teamId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM [organization].[TeamMemberships]
                WHERE [PersonId] = @PersonId AND [TeamId] = @TeamId
            ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { PersonId = personId, TeamId = teamId }, cancellationToken: cancellationToken));
    }
}
