namespace ICS.Modules.Organization.Persistence;

using Dapper;
using ICS.Core.Data;
using ICS.Modules.Organization.Application.DTOs;
using ICS.Modules.Organization.Domain;

/// <summary>
/// Dapper-based repository implementation for Team and TeamMembership.
/// Uses explicit parameterized SQL against organization schema tables.
/// Architecture §17, §19.3, §20.
/// </summary>
internal class TeamRepository : ITeamRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TeamRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Team?> GetByIdAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                TeamId AS Id,
                Name,
                Description,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [organization].[Teams]
            WHERE TeamId = @TeamId;";

        return await connection.QuerySingleOrDefaultAsync<Team>(sql, new { TeamId = teamId });
    }

    public async Task<Team?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                TeamId AS Id,
                Name,
                Description,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [organization].[Teams]
            WHERE LOWER(Name) = LOWER(@Name);";

        return await connection.QuerySingleOrDefaultAsync<Team>(sql, new { Name = name.Trim() });
    }

    public async Task AddAsync(Team team, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(team);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO [organization].[Teams] (
                TeamId,
                Name,
                Description,
                Status,
                CreatedAt,
                UpdatedAt
            ) VALUES (
                @TeamId,
                @Name,
                @Description,
                @Status,
                @CreatedAt,
                @UpdatedAt
            );";

        await connection.ExecuteAsync(sql, new
        {
            TeamId = team.Id,
            team.Name,
            team.Description,
            team.Status,
            team.CreatedAt,
            team.UpdatedAt
        });
    }

    public async Task UpdateAsync(Team team, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(team);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            UPDATE [organization].[Teams]
            SET 
                Name = @Name,
                Description = @Description,
                Status = @Status,
                UpdatedAt = @UpdatedAt
            WHERE TeamId = @TeamId;";

        await connection.ExecuteAsync(sql, new
        {
            TeamId = team.Id,
            team.Name,
            team.Description,
            team.Status,
            team.UpdatedAt
        });
    }

    public async Task AddMembershipAsync(TeamMembership membership, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(membership);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO [organization].[TeamMemberships] (
                MembershipId,
                PersonId,
                TeamId,
                JoinedAt,
                LeftAt,
                IsActive
            ) VALUES (
                @MembershipId,
                @PersonId,
                @TeamId,
                @JoinedAt,
                @LeftAt,
                @IsActive
            );";

        await connection.ExecuteAsync(sql, new
        {
            MembershipId = membership.Id,
            membership.PersonId,
            membership.TeamId,
            membership.JoinedAt,
            membership.LeftAt,
            membership.IsActive
        });
    }

    public async Task<TeamMembership?> GetActiveMembershipAsync(Guid personId, Guid teamId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                MembershipId AS Id,
                PersonId,
                TeamId,
                JoinedAt,
                LeftAt,
                IsActive
            FROM [organization].[TeamMemberships]
            WHERE PersonId = @PersonId AND TeamId = @TeamId AND IsActive = 1;";

        return await connection.QuerySingleOrDefaultAsync<TeamMembership>(sql, new { PersonId = personId, TeamId = teamId });
    }

    public async Task EndMembershipAsync(Guid membershipId, DateTime leftAt, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            UPDATE [organization].[TeamMemberships]
            SET 
                IsActive = 0,
                LeftAt = @LeftAt
            WHERE MembershipId = @MembershipId;";

        await connection.ExecuteAsync(sql, new { MembershipId = membershipId, LeftAt = leftAt });
    }

    public async Task<IReadOnlyList<TeamMemberDto>> GetRosterByTeamIdAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                tm.MembershipId,
                tm.PersonId,
                p.Name AS PersonName,
                p.Email AS PersonEmail,
                tm.TeamId,
                tm.JoinedAt,
                tm.LeftAt,
                tm.IsActive
            FROM [organization].[TeamMemberships] tm
            JOIN [organization].[Persons] p ON tm.PersonId = p.PersonId
            WHERE tm.TeamId = @TeamId AND tm.IsActive = 1
            ORDER BY p.Name ASC;";

        var results = await connection.QueryAsync<TeamMemberDto>(sql, new { TeamId = teamId });
        return results.ToList();
    }
}
