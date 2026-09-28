using Cakra.Modules.Organization.Domain;

namespace Cakra.Modules.Organization.Services;

/// <summary>
/// Application service contract for Organization write commands (Architecture §7, §14, §16).
/// Manages Persons, Teams, Roles, Responsibilities, Memberships, and Assignments.
/// </summary>
public interface IOrganizationService
{
    /// <summary>Creates a new person in ACTIVE status and emits <c>PersonCreated</c>.</summary>
    Task<Person> CreatePersonAsync(
        string firstName,
        string lastName,
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>Synchronous convenience overload for <see cref="CreatePersonAsync"/>.</summary>
    Person CreatePerson(string firstName, string lastName, string email) =>
        CreatePersonAsync(firstName, lastName, email, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>Updates an existing person's attributes.</summary>
    Task<Person> UpdatePersonAsync(
        Guid personId,
        string firstName,
        string lastName,
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>Synchronous convenience overload for <see cref="UpdatePersonAsync"/>.</summary>
    Person UpdatePerson(Guid personId, string firstName, string lastName, string email) =>
        UpdatePersonAsync(personId, firstName, lastName, email, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>Creates a new organizational team.</summary>
    Task<Team> CreateTeamAsync(
        string name,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>Synchronous convenience overload for <see cref="CreateTeamAsync"/>.</summary>
    Team CreateTeam(string name, string? description = null) =>
        CreateTeamAsync(name, description, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>Assigns a person to an organizational team.</summary>
    Task<TeamMembership> AssignPersonToTeamAsync(
        Guid personId,
        Guid teamId,
        CancellationToken cancellationToken = default);

    /// <summary>Synchronous convenience overload for <see cref="AssignPersonToTeamAsync"/>.</summary>
    TeamMembership AssignPersonToTeam(Guid personId, Guid teamId) =>
        AssignPersonToTeamAsync(personId, teamId, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>Creates a new organizational role.</summary>
    Task<Role> CreateRoleAsync(
        string name,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>Synchronous convenience overload for <see cref="CreateRoleAsync"/>.</summary>
    Role CreateRole(string name, string? description = null) =>
        CreateRoleAsync(name, description, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>Assigns an organizational role to a person and emits <c>RoleAssigned</c>.</summary>
    Task<RoleAssignment> AssignRoleToPersonAsync(
        Guid personId,
        Guid roleId,
        CancellationToken cancellationToken = default);

    /// <summary>Synchronous convenience overload for <see cref="AssignRoleToPersonAsync"/>.</summary>
    RoleAssignment AssignRoleToPerson(Guid personId, Guid roleId) =>
        AssignRoleToPersonAsync(personId, roleId, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>Revokes an active organizational role from a person and emits <c>RoleRevoked</c>.</summary>
    Task RevokeRoleFromPersonAsync(
        Guid personId,
        Guid roleId,
        CancellationToken cancellationToken = default);

    /// <summary>Synchronous convenience overload for <see cref="RevokeRoleFromPersonAsync"/>.</summary>
    void RevokeRoleFromPerson(Guid personId, Guid roleId) =>
        RevokeRoleFromPersonAsync(personId, roleId, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>Creates a new organizational responsibility.</summary>
    Task<Responsibility> CreateResponsibilityAsync(
        string name,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>Synchronous convenience overload for <see cref="CreateResponsibilityAsync"/>.</summary>
    Responsibility CreateResponsibility(string name, string? description = null) =>
        CreateResponsibilityAsync(name, description, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>Assigns an organizational responsibility to a person.</summary>
    Task<ResponsibilityAssignment> AssignResponsibilityToPersonAsync(
        Guid personId,
        Guid responsibilityId,
        CancellationToken cancellationToken = default);

    /// <summary>Synchronous convenience overload for <see cref="AssignResponsibilityToPersonAsync"/>.</summary>
    ResponsibilityAssignment AssignResponsibilityToPerson(Guid personId, Guid responsibilityId) =>
        AssignResponsibilityToPersonAsync(personId, responsibilityId, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>Deactivates a person (sets Status = INACTIVE) and emits <c>PersonDeactivated</c>.</summary>
    Task<Person> DeactivatePersonAsync(
        Guid personId,
        CancellationToken cancellationToken = default);

    /// <summary>Synchronous convenience overload for <see cref="DeactivatePersonAsync"/>.</summary>
    Person DeactivatePerson(Guid personId) =>
        DeactivatePersonAsync(personId, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>Reactivates a person (sets Status = ACTIVE).</summary>
    Task<Person> ActivatePersonAsync(
        Guid personId,
        CancellationToken cancellationToken = default);

    /// <summary>Synchronous convenience overload for <see cref="ActivatePersonAsync"/>.</summary>
    Person ActivatePerson(Guid personId) =>
        ActivatePersonAsync(personId, CancellationToken.None).GetAwaiter().GetResult();
}
