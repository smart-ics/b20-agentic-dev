using Cakra.Modules.Organization.Domain;

namespace Cakra.Modules.Organization.Persistence;

/// <summary>
/// Persistence contract for <see cref="RoleAssignment"/> junction records (Architecture §6, §14, §19.3).
/// </summary>
internal interface IRoleAssignmentRepository
{
    /// <summary>Assigns a role to a person, reactivating if previously revoked.</summary>
    Task AssignAsync(RoleAssignment assignment, CancellationToken cancellationToken = default);

    /// <summary>Revokes an active role assignment from a person.</summary>
    Task RevokeAsync(Guid personId, Guid roleId, DateTime revokedAt, CancellationToken cancellationToken = default);

    /// <summary>Retrieves all role assignments (active and revoked) for a person.</summary>
    Task<IReadOnlyList<RoleAssignment>> GetByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>Retrieves only active (non-revoked) role assignments for a person.</summary>
    Task<IReadOnlyList<RoleAssignment>> GetActiveByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>Retrieves all assignments for a specific role.</summary>
    Task<IReadOnlyList<RoleAssignment>> GetByRoleIdAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>Retrieves the specific role assignment between a person and role, or null if none.</summary>
    Task<RoleAssignment?> GetAsync(Guid personId, Guid roleId, CancellationToken cancellationToken = default);
}
