namespace ICS.Modules.Organization.Application;

using ICS.Modules.Organization.Application.DTOs;

/// <summary>
/// Application service contract for Organization module state-mutating operations.
/// Architecture §6, §7, §16, §20.
/// </summary>
public interface IOrganizationService
{
    Task<PersonDto> CreatePersonAsync(string name, string email, Guid? personId = null, CancellationToken cancellationToken = default);
    Task<PersonDto> UpdatePersonAsync(Guid personId, string name, string email, CancellationToken cancellationToken = default);
    Task<bool> DeactivatePersonAsync(Guid personId, CancellationToken cancellationToken = default);

    Task<TeamDto> CreateTeamAsync(string name, string? description = null, Guid? teamId = null, CancellationToken cancellationToken = default);
    Task<Guid> AssignPersonToTeamAsync(Guid personId, Guid teamId, CancellationToken cancellationToken = default);

    Task<RoleDto> CreateRoleAsync(string name, string? description = null, Guid? roleId = null, CancellationToken cancellationToken = default);
    Task<Guid> AssignRoleToPersonAsync(Guid personId, Guid roleId, CancellationToken cancellationToken = default);
    Task<bool> RevokeRoleFromPersonAsync(Guid personId, Guid roleId, CancellationToken cancellationToken = default);

    Task<ResponsibilityDto> CreateResponsibilityAsync(string name, string? description = null, Guid? responsibilityId = null, CancellationToken cancellationToken = default);
    Task<Guid> AssignResponsibilityToPersonAsync(Guid personId, Guid responsibilityId, CancellationToken cancellationToken = default);
    Task<bool> RevokeResponsibilityFromPersonAsync(Guid personId, Guid responsibilityId, CancellationToken cancellationToken = default);
}
