namespace ICS.Modules.Organization.Persistence;

using ICS.Modules.Organization.Application.DTOs;
using ICS.Modules.Organization.Domain;

/// <summary>
/// Internal repository contract for Role and RoleAssignment persistence.
/// </summary>
internal interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task AddAsync(Role role, CancellationToken cancellationToken = default);
    Task UpdateAsync(Role role, CancellationToken cancellationToken = default);
    Task AddAssignmentAsync(RoleAssignment assignment, CancellationToken cancellationToken = default);
    Task<RoleAssignment?> GetActiveAssignmentAsync(Guid personId, Guid roleId, CancellationToken cancellationToken = default);
    Task RevokeAssignmentAsync(Guid assignmentId, DateTime revokedAt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetRolesByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleAssignmentDto>> GetAssignmentsByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default);
}
