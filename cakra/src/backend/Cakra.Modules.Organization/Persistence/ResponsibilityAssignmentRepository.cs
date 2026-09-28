using System.Data;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Organization.Domain;
using Dapper;

namespace Cakra.Modules.Organization.Persistence;

/// <summary>
/// Dapper implementation of <see cref="IResponsibilityAssignmentRepository"/> using explicit parameterized SQL
/// against the <c>organization.ResponsibilityAssignments</c> table (Architecture §17, §19.3).
/// </summary>
internal sealed class ResponsibilityAssignmentRepository : IResponsibilityAssignmentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ResponsibilityAssignmentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task AssignAsync(ResponsibilityAssignment assignment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        if (assignment.AssignedAt == default)
        {
            assignment.AssignedAt = DateTime.UtcNow;
        }

        const string sql = """
            IF NOT EXISTS (
                SELECT 1 FROM [organization].[ResponsibilityAssignments]
                WHERE [PersonId] = @PersonId AND [ResponsibilityId] = @ResponsibilityId
            )
            BEGIN
                INSERT INTO [organization].[ResponsibilityAssignments] ([PersonId], [ResponsibilityId], [AssignedAt])
                VALUES (@PersonId, @ResponsibilityId, @AssignedAt);
            END;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            assignment.PersonId,
            assignment.ResponsibilityId,
            assignment.AssignedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task RemoveAsync(Guid personId, Guid responsibilityId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM [organization].[ResponsibilityAssignments]
            WHERE [PersonId] = @PersonId AND [ResponsibilityId] = @ResponsibilityId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            PersonId = personId,
            ResponsibilityId = responsibilityId
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ResponsibilityAssignment>> GetByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [PersonId], [ResponsibilityId], [AssignedAt]
            FROM [organization].[ResponsibilityAssignments]
            WHERE [PersonId] = @PersonId
            ORDER BY [AssignedAt] DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<ResponsibilityAssignment>(
            new CommandDefinition(sql, new { PersonId = personId }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<IReadOnlyList<ResponsibilityAssignment>> GetByResponsibilityIdAsync(Guid responsibilityId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [PersonId], [ResponsibilityId], [AssignedAt]
            FROM [organization].[ResponsibilityAssignments]
            WHERE [ResponsibilityId] = @ResponsibilityId
            ORDER BY [AssignedAt] DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<ResponsibilityAssignment>(
            new CommandDefinition(sql, new { ResponsibilityId = responsibilityId }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<bool> ExistsAsync(Guid personId, Guid responsibilityId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM [organization].[ResponsibilityAssignments]
                WHERE [PersonId] = @PersonId AND [ResponsibilityId] = @ResponsibilityId
            ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { PersonId = personId, ResponsibilityId = responsibilityId }, cancellationToken: cancellationToken));
    }
}
