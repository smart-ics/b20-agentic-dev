using System.Data;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Organization.Domain;
using Dapper;

namespace Cakra.Modules.Organization.Persistence;

/// <summary>
/// Dapper implementation of <see cref="IRoleAssignmentRepository"/> using explicit parameterized SQL
/// against the <c>organization.RoleAssignments</c> table (Architecture §17, §19.3).
/// </summary>
internal sealed class RoleAssignmentRepository : IRoleAssignmentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RoleAssignmentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task AssignAsync(RoleAssignment assignment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        if (assignment.AssignedAt == default)
        {
            assignment.AssignedAt = DateTime.UtcNow;
        }

        const string sql = """
            IF EXISTS (
                SELECT 1 FROM [organization].[RoleAssignments]
                WHERE [PersonId] = @PersonId AND [RoleId] = @RoleId
            )
            BEGIN
                UPDATE [organization].[RoleAssignments]
                SET [AssignedAt] = @AssignedAt,
                    [RevokedAt] = NULL
                WHERE [PersonId] = @PersonId AND [RoleId] = @RoleId;
            END
            ELSE
            BEGIN
                INSERT INTO [organization].[RoleAssignments] ([PersonId], [RoleId], [AssignedAt], [RevokedAt])
                VALUES (@PersonId, @RoleId, @AssignedAt, NULL);
            END;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            assignment.PersonId,
            assignment.RoleId,
            assignment.AssignedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task RevokeAsync(Guid personId, Guid roleId, DateTime revokedAt, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE [organization].[RoleAssignments]
            SET [RevokedAt] = @RevokedAt
            WHERE [PersonId] = @PersonId AND [RoleId] = @RoleId AND [RevokedAt] IS NULL;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            PersonId = personId,
            RoleId = roleId,
            RevokedAt = revokedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<RoleAssignment>> GetByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [PersonId], [RoleId], [AssignedAt], [RevokedAt]
            FROM [organization].[RoleAssignments]
            WHERE [PersonId] = @PersonId
            ORDER BY [AssignedAt] DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<RoleAssignment>(
            new CommandDefinition(sql, new { PersonId = personId }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IReadOnlyList<RoleAssignment>> GetActiveByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [PersonId], [RoleId], [AssignedAt], [RevokedAt]
            FROM [organization].[RoleAssignments]
            WHERE [PersonId] = @PersonId AND [RevokedAt] IS NULL
            ORDER BY [AssignedAt] DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<RoleAssignment>(
            new CommandDefinition(sql, new { PersonId = personId }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IReadOnlyList<RoleAssignment>> GetByRoleIdAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [PersonId], [RoleId], [AssignedAt], [RevokedAt]
            FROM [organization].[RoleAssignments]
            WHERE [RoleId] = @RoleId AND [RevokedAt] IS NULL
            ORDER BY [AssignedAt] DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<RoleAssignment>(
            new CommandDefinition(sql, new { RoleId = roleId }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<RoleAssignment?> GetAsync(Guid personId, Guid roleId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [PersonId], [RoleId], [AssignedAt], [RevokedAt]
            FROM [organization].[RoleAssignments]
            WHERE [PersonId] = @PersonId AND [RoleId] = @RoleId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<RoleAssignment>(
            new CommandDefinition(sql, new { PersonId = personId, RoleId = roleId }, cancellationToken: cancellationToken));
    }
}
