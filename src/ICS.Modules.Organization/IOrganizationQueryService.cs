namespace ICS.Modules.Organization;

using ICS.Modules.Organization.Application.DTOs;

/// <summary>
/// In-process query contract exposed by the Organization module for cross-module integration and UI queries.
/// Architecture §7, §14, §15, and §20.
/// </summary>
public interface IOrganizationQueryService
{
    /// <summary>
    /// Gets a Person by identifier.
    /// </summary>
    Task<PersonDto?> GetPersonByIdAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all active Persons in the organization.
    /// </summary>
    Task<IReadOnlyList<PersonDto>> ListActivePersonsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active member roster for a specified Team.
    /// </summary>
    Task<IReadOnlyList<TeamMemberDto>> GetTeamRosterAsync(Guid teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves active role names assigned to the specified Person.
    /// </summary>
    Task<IReadOnlyList<string>> GetPersonRolesAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves active responsibility names assigned to the specified Person.
    /// </summary>
    Task<IReadOnlyList<string>> GetPersonResponsibilitiesAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether the specified Person is active in the organization.
    /// Supports pre-population bootstrap fallback when Organization tables are not yet provisioned.
    /// </summary>
    Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves active organizational roles assigned to the specified Person (alias for GetPersonRolesAsync).
    /// </summary>
    Task<IReadOnlyList<string>> GetRolesByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers bootstrap roles for a Person in-memory for testing and pre-provisioning scenarios.
    /// </summary>
    void RegisterBootstrapRoles(Guid personId, IEnumerable<string> roles);
}
