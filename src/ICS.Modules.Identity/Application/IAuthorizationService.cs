namespace ICS.Modules.Identity.Application;

/// <summary>
/// Service contract for resolving active roles and evaluating role-based permissions per Architecture §14.
/// Integrates with OrganizationQueryService to query assigned roles.
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Dynamically resolves the active organizational roles assigned to the specified Person.
    /// Delegates to OrganizationQueryService.GetPersonRoles per Architecture §14.
    /// </summary>
    /// <param name="personId">The authoritative Person identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active role names.</returns>
    Task<IReadOnlyList<string>> ResolveRolesAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronously resolves the active organizational roles assigned to the specified Person.
    /// </summary>
    /// <param name="personId">The authoritative Person identifier.</param>
    /// <returns>List of active role names.</returns>
    IReadOnlyList<string> ResolveRoles(Guid personId);

    /// <summary>
    /// Resolves the active organizational roles assigned to the specified Person (alias for ResolveRolesAsync).
    /// </summary>
    /// <param name="personId">The authoritative Person identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active role names.</returns>
    Task<IReadOnlyList<string>> GetRolesForPersonAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the specified Person holds a particular active role.
    /// </summary>
    /// <param name="personId">The authoritative Person identifier.</param>
    /// <param name="role">The role name to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the person holds the role, otherwise false.</returns>
    Task<bool> HasRoleAsync(Guid personId, string role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the specified Person holds at least one of the specified roles.
    /// </summary>
    /// <param name="personId">The authoritative Person identifier.</param>
    /// <param name="roles">The roles to verify against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the person holds at least one of the roles, otherwise false.</returns>
    Task<bool> HasAnyRoleAsync(Guid personId, IEnumerable<string> roles, CancellationToken cancellationToken = default);
}
