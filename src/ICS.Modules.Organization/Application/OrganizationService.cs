namespace ICS.Modules.Organization.Application;

using ICS.Modules.Organization.Application.Commands;
using ICS.Modules.Organization.Application.DTOs;
using MediatR;

/// <summary>
/// Concrete implementation of <see cref="IOrganizationService"/> executing commands through MediatR.
/// Architecture §6, §7, §16, §19.2, §20.
/// </summary>
public class OrganizationService : IOrganizationService
{
    private readonly ISender _sender;

    public OrganizationService(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public Task<PersonDto> CreatePersonAsync(string name, string email, Guid? personId = null, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new CreatePersonCommand(name, email, personId), cancellationToken);
    }

    public Task<PersonDto> UpdatePersonAsync(Guid personId, string name, string email, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new UpdatePersonCommand(personId, name, email), cancellationToken);
    }

    public Task<bool> DeactivatePersonAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new DeactivatePersonCommand(personId), cancellationToken);
    }

    public Task<TeamDto> CreateTeamAsync(string name, string? description = null, Guid? teamId = null, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new CreateTeamCommand(name, description, teamId), cancellationToken);
    }

    public Task<Guid> AssignPersonToTeamAsync(Guid personId, Guid teamId, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new AssignPersonToTeamCommand(personId, teamId), cancellationToken);
    }

    public Task<RoleDto> CreateRoleAsync(string name, string? description = null, Guid? roleId = null, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new CreateRoleCommand(name, description, roleId), cancellationToken);
    }

    public Task<Guid> AssignRoleToPersonAsync(Guid personId, Guid roleId, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new AssignRoleToPersonCommand(personId, roleId), cancellationToken);
    }

    public Task<bool> RevokeRoleFromPersonAsync(Guid personId, Guid roleId, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new RevokeRoleFromPersonCommand(personId, roleId), cancellationToken);
    }

    public Task<ResponsibilityDto> CreateResponsibilityAsync(string name, string? description = null, Guid? responsibilityId = null, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new CreateResponsibilityCommand(name, description, responsibilityId), cancellationToken);
    }

    public Task<Guid> AssignResponsibilityToPersonAsync(Guid personId, Guid responsibilityId, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new AssignResponsibilityToPersonCommand(personId, responsibilityId), cancellationToken);
    }

    public Task<bool> RevokeResponsibilityFromPersonAsync(Guid personId, Guid responsibilityId, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new RevokeResponsibilityFromPersonCommand(personId, responsibilityId), cancellationToken);
    }
}
