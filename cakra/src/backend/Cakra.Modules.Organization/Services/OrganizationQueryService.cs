using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Organization.Domain;
using Dapper;

namespace Cakra.Modules.Organization.Services;

/// <summary>
/// Dapper implementation of <see cref="IOrganizationQueryService"/> using explicit parameterized SQL
/// (Architecture §7, §14, §15, §19.3, §20).
/// </summary>
public sealed class OrganizationQueryService : IOrganizationQueryService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public OrganizationQueryService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM [organization].[Persons]
                WHERE [Id] = @PersonId AND [Status] = @Status
            ) THEN 1 ELSE 0 END;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new { PersonId = personId, Status = Person.StatusActive },
                cancellationToken: cancellationToken));
        return result == 1;
    }

    /// <inheritdoc />
    public bool IsPersonActive(Guid personId) =>
        IsPersonActiveAsync(personId, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<PersonDto?> GetPersonByIdAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                [Id],
                [FirstName],
                [LastName],
                [Email],
                [Status],
                [CreatedAt],
                [UpdatedAt]
            FROM [organization].[Persons]
            WHERE [Id] = @PersonId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PersonDto>(
            new CommandDefinition(sql, new { PersonId = personId }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public PersonDto? GetPersonById(Guid personId) =>
        GetPersonByIdAsync(personId, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<IReadOnlyList<PersonDto>> ListActivePersonsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                [Id],
                [FirstName],
                [LastName],
                [Email],
                [Status],
                [CreatedAt],
                [UpdatedAt]
            FROM [organization].[Persons]
            WHERE [Status] = @Status
            ORDER BY [LastName], [FirstName];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<PersonDto>(
            new CommandDefinition(sql, new { Status = Person.StatusActive }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    /// <inheritdoc />
    public IReadOnlyList<PersonDto> ListActivePersons() =>
        ListActivePersonsAsync(CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeamRosterMemberDto>> GetTeamRosterAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                tm.[TeamId],
                t.[Name] AS [TeamName],
                p.[Id] AS [PersonId],
                p.[FirstName],
                p.[LastName],
                p.[Email],
                p.[Status],
                tm.[AssignedAt]
            FROM [organization].[TeamMemberships] tm
            INNER JOIN [organization].[Teams] t ON tm.[TeamId] = t.[Id]
            INNER JOIN [organization].[Persons] p ON tm.[PersonId] = p.[Id]
            WHERE tm.[TeamId] = @TeamId
            ORDER BY p.[LastName], p.[FirstName];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<TeamRosterMemberDto>(
            new CommandDefinition(sql, new { TeamId = teamId }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TeamRosterMemberDto> GetTeamRoster(Guid teamId) =>
        GetTeamRosterAsync(teamId, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetPersonRolesAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT r.[Name]
            FROM [organization].[RoleAssignments] ra
            INNER JOIN [organization].[Roles] r ON ra.[RoleId] = r.[Id]
            WHERE ra.[PersonId] = @PersonId AND ra.[RevokedAt] IS NULL
            ORDER BY r.[Name];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<string>(
            new CommandDefinition(sql, new { PersonId = personId }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetPersonRoles(Guid personId) =>
        GetPersonRolesAsync(personId, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<IReadOnlyList<PersonResponsibilityDto>> GetPersonResponsibilitiesAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                r.[Id] AS [ResponsibilityId],
                r.[Name],
                r.[Description],
                ra.[AssignedAt]
            FROM [organization].[ResponsibilityAssignments] ra
            INNER JOIN [organization].[Responsibilities] r ON ra.[ResponsibilityId] = r.[Id]
            WHERE ra.[PersonId] = @PersonId
            ORDER BY r.[Name];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<PersonResponsibilityDto>(
            new CommandDefinition(sql, new { PersonId = personId }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    /// <inheritdoc />
    public IReadOnlyList<PersonResponsibilityDto> GetPersonResponsibilities(Guid personId) =>
        GetPersonResponsibilitiesAsync(personId, CancellationToken.None).GetAwaiter().GetResult();
}
