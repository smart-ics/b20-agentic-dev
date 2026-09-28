namespace ICS.Modules.Organization.Persistence;

using Dapper;
using ICS.Core.Data;
using ICS.Modules.Organization.Application.DTOs;
using ICS.Modules.Organization.Domain;

/// <summary>
/// Dapper-based repository implementation for Role and RoleAssignment.
/// Uses explicit parameterized SQL against organization schema tables.
/// Architecture §14, §17, §19.3, §20.
/// </summary>
internal class RoleRepository : IRoleRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RoleRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Role?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                RoleId AS Id,
                Name,
                Description,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [organization].[Roles]
            WHERE RoleId = @RoleId;";

        return await connection.QuerySingleOrDefaultAsync<Role>(sql, new { RoleId = roleId });
    }

    public async Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                RoleId AS Id,
                Name,
                Description,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [organization].[Roles]
            WHERE LOWER(Name) = LOWER(@Name);";

        return await connection.QuerySingleOrDefaultAsync<Role>(sql, new { Name = name.Trim() });
    }

    public async Task AddAsync(Role role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO [organization].[Roles] (
                RoleId,
                Name,
                Description,
                Status,
                CreatedAt,
                UpdatedAt
            ) VALUES (
                @RoleId,
                @Name,
                @Description,
                @Status,
                @CreatedAt,
                @UpdatedAt
            );";

        await connection.ExecuteAsync(sql, new
        {
            RoleId = role.Id,
            role.Name,
            role.Description,
            role.Status,
            role.CreatedAt,
            role.UpdatedAt
        });
    }

    public async Task UpdateAsync(Role role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            UPDATE [organization].[Roles]
            SET 
                Name = @Name,
                Description = @Description,
                Status = @Status,
                UpdatedAt = @UpdatedAt
            WHERE RoleId = @RoleId;";

        await connection.ExecuteAsync(sql, new
        {
            RoleId = role.Id,
            role.Name,
            role.Description,
            role.Status,
            role.UpdatedAt
        });
    }

    public async Task AddAssignmentAsync(RoleAssignment assignment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO [organization].[RoleAssignments] (
                AssignmentId,
                PersonId,
                RoleId,
                AssignedAt,
                RevokedAt,
                IsActive
            ) VALUES (
                @AssignmentId,
                @PersonId,
                @RoleId,
                @AssignedAt,
                @RevokedAt,
                @IsActive
            );";

        await connection.ExecuteAsync(sql, new
        {
            AssignmentId = assignment.Id,
            assignment.PersonId,
            assignment.RoleId,
            assignment.AssignedAt,
            assignment.RevokedAt,
            assignment.IsActive
        });
    }

    public async Task<RoleAssignment?> GetActiveAssignmentAsync(Guid personId, Guid roleId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                AssignmentId AS Id,
                PersonId,
                RoleId,
                AssignedAt,
                RevokedAt,
                IsActive
            FROM [organization].[RoleAssignments]
            WHERE PersonId = @PersonId AND RoleId = @RoleId AND IsActive = 1;";

        return await connection.QuerySingleOrDefaultAsync<RoleAssignment>(sql, new { PersonId = personId, RoleId = roleId });
    }

    public async Task RevokeAssignmentAsync(Guid assignmentId, DateTime revokedAt, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            UPDATE [organization].[RoleAssignments]
            SET 
                IsActive = 0,
                RevokedAt = @RevokedAt
            WHERE AssignmentId = @AssignmentId;";

        await connection.ExecuteAsync(sql, new { AssignmentId = assignmentId, RevokedAt = revokedAt });
    }

    public async Task<IReadOnlyList<string>> GetRolesByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT r.Name
            FROM [organization].[RoleAssignments] ra
            JOIN [organization].[Roles] r ON ra.RoleId = r.RoleId
            WHERE ra.PersonId = @PersonId 
              AND ra.IsActive = 1 
              AND r.Status = 'ACTIVE'
            ORDER BY r.Name ASC;";

        var results = await connection.QueryAsync<string>(sql, new { PersonId = personId });
        return results.ToList();
    }

    public async Task<IReadOnlyList<RoleAssignmentDto>> GetAssignmentsByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                ra.AssignmentId,
                ra.PersonId,
                ra.RoleId,
                r.Name AS RoleName,
                ra.AssignedAt,
                ra.RevokedAt,
                ra.IsActive
            FROM [organization].[RoleAssignments] ra
            JOIN [organization].[Roles] r ON ra.RoleId = r.RoleId
            WHERE ra.PersonId = @PersonId
            ORDER BY ra.AssignedAt DESC;";

        var results = await connection.QueryAsync<RoleAssignmentDto>(sql, new { PersonId = personId });
        return results.ToList();
    }
}
