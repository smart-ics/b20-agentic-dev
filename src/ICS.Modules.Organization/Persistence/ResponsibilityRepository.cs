namespace ICS.Modules.Organization.Persistence;

using Dapper;
using ICS.Core.Data;
using ICS.Modules.Organization.Application.DTOs;
using ICS.Modules.Organization.Domain;

/// <summary>
/// Dapper-based repository implementation for Responsibility and ResponsibilityAssignment.
/// Uses explicit parameterized SQL against organization schema tables.
/// Architecture §7, §17, §19.3, §20.
/// </summary>
internal class ResponsibilityRepository : IResponsibilityRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ResponsibilityRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Responsibility?> GetByIdAsync(Guid responsibilityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                ResponsibilityId AS Id,
                Name,
                Description,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [organization].[Responsibilities]
            WHERE ResponsibilityId = @ResponsibilityId;";

        return await connection.QuerySingleOrDefaultAsync<Responsibility>(sql, new { ResponsibilityId = responsibilityId });
    }

    public async Task<Responsibility?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                ResponsibilityId AS Id,
                Name,
                Description,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [organization].[Responsibilities]
            WHERE LOWER(Name) = LOWER(@Name);";

        return await connection.QuerySingleOrDefaultAsync<Responsibility>(sql, new { Name = name.Trim() });
    }

    public async Task AddAsync(Responsibility responsibility, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responsibility);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO [organization].[Responsibilities] (
                ResponsibilityId,
                Name,
                Description,
                Status,
                CreatedAt,
                UpdatedAt
            ) VALUES (
                @ResponsibilityId,
                @Name,
                @Description,
                @Status,
                @CreatedAt,
                @UpdatedAt
            );";

        await connection.ExecuteAsync(sql, new
        {
            ResponsibilityId = responsibility.Id,
            responsibility.Name,
            responsibility.Description,
            responsibility.Status,
            responsibility.CreatedAt,
            responsibility.UpdatedAt
        });
    }

    public async Task UpdateAsync(Responsibility responsibility, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responsibility);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            UPDATE [organization].[Responsibilities]
            SET 
                Name = @Name,
                Description = @Description,
                Status = @Status,
                UpdatedAt = @UpdatedAt
            WHERE ResponsibilityId = @ResponsibilityId;";

        await connection.ExecuteAsync(sql, new
        {
            ResponsibilityId = responsibility.Id,
            responsibility.Name,
            responsibility.Description,
            responsibility.Status,
            responsibility.UpdatedAt
        });
    }

    public async Task AddAssignmentAsync(ResponsibilityAssignment assignment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO [organization].[ResponsibilityAssignments] (
                AssignmentId,
                PersonId,
                ResponsibilityId,
                AssignedAt,
                RevokedAt,
                IsActive
            ) VALUES (
                @AssignmentId,
                @PersonId,
                @ResponsibilityId,
                @AssignedAt,
                @RevokedAt,
                @IsActive
            );";

        await connection.ExecuteAsync(sql, new
        {
            AssignmentId = assignment.Id,
            assignment.PersonId,
            assignment.ResponsibilityId,
            assignment.AssignedAt,
            assignment.RevokedAt,
            assignment.IsActive
        });
    }

    public async Task<ResponsibilityAssignment?> GetActiveAssignmentAsync(Guid personId, Guid responsibilityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                AssignmentId AS Id,
                PersonId,
                ResponsibilityId,
                AssignedAt,
                RevokedAt,
                IsActive
            FROM [organization].[ResponsibilityAssignments]
            WHERE PersonId = @PersonId AND ResponsibilityId = @ResponsibilityId AND IsActive = 1;";

        return await connection.QuerySingleOrDefaultAsync<ResponsibilityAssignment>(sql, new { PersonId = personId, ResponsibilityId = responsibilityId });
    }

    public async Task RevokeAssignmentAsync(Guid assignmentId, DateTime revokedAt, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            UPDATE [organization].[ResponsibilityAssignments]
            SET 
                IsActive = 0,
                RevokedAt = @RevokedAt
            WHERE AssignmentId = @AssignmentId;";

        await connection.ExecuteAsync(sql, new { AssignmentId = assignmentId, RevokedAt = revokedAt });
    }

    public async Task<IReadOnlyList<string>> GetResponsibilitiesByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT resp.Name
            FROM [organization].[ResponsibilityAssignments] ra
            JOIN [organization].[Responsibilities] resp ON ra.ResponsibilityId = resp.ResponsibilityId
            WHERE ra.PersonId = @PersonId 
              AND ra.IsActive = 1 
              AND resp.Status = 'ACTIVE'
            ORDER BY resp.Name ASC;";

        var results = await connection.QueryAsync<string>(sql, new { PersonId = personId });
        return results.ToList();
    }

    public async Task<IReadOnlyList<ResponsibilityAssignmentDto>> GetAssignmentsByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                ra.AssignmentId,
                ra.PersonId,
                ra.ResponsibilityId,
                resp.Name AS ResponsibilityName,
                ra.AssignedAt,
                ra.RevokedAt,
                ra.IsActive
            FROM [organization].[ResponsibilityAssignments] ra
            JOIN [organization].[Responsibilities] resp ON ra.ResponsibilityId = resp.ResponsibilityId
            WHERE ra.PersonId = @PersonId
            ORDER BY ra.AssignedAt DESC;";

        var results = await connection.QueryAsync<ResponsibilityAssignmentDto>(sql, new { PersonId = personId });
        return results.ToList();
    }
}
