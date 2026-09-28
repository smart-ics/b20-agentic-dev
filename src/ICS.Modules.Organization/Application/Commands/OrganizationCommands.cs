namespace ICS.Modules.Organization.Application.Commands;

using FluentValidation;
using ICS.Core.Domain;
using ICS.Core.Time;
using ICS.Modules.Organization.Application.DTOs;
using ICS.Modules.Organization.Domain;
using ICS.Modules.Organization.Domain.Events;
using ICS.Modules.Organization.Persistence;
using MediatR;

#region CreatePerson

public sealed record CreatePersonCommand(
    string Name,
    string Email,
    Guid? PersonId = null) : IRequest<PersonDto>;

public sealed class CreatePersonCommandValidator : AbstractValidator<CreatePersonCommand>
{
    public CreatePersonCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Person name is required.")
            .MaximumLength(100).WithMessage("Person name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Person email is required.")
            .EmailAddress().WithMessage("Person email must be a valid email address.")
            .MaximumLength(150).WithMessage("Person email must not exceed 150 characters.");
    }
}

internal sealed class CreatePersonCommandHandler : IRequestHandler<CreatePersonCommand, PersonDto>
{
    private readonly IPersonRepository _personRepository;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public CreatePersonCommandHandler(
        IPersonRepository personRepository,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _personRepository = personRepository;
        _clock = clock;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<PersonDto> Handle(CreatePersonCommand request, CancellationToken cancellationToken)
    {
        var existing = await _personRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"A person with email '{request.Email}' already exists.");
        }

        var personId = request.PersonId ?? Guid.NewGuid();
        var person = Person.Create(personId, request.Name, request.Email, _clock.UtcNow);

        await _personRepository.AddAsync(person, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(person, cancellationToken);

        return new PersonDto(person.Id, person.Name, person.Email, person.Status, person.CreatedAt, person.UpdatedAt);
    }
}

#endregion

#region UpdatePerson

public sealed record UpdatePersonCommand(
    Guid PersonId,
    string Name,
    string Email) : IRequest<PersonDto>;

public sealed class UpdatePersonCommandValidator : AbstractValidator<UpdatePersonCommand>
{
    public UpdatePersonCommandValidator()
    {
        RuleFor(x => x.PersonId)
            .NotEmpty().WithMessage("PersonId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Person name is required.")
            .MaximumLength(100).WithMessage("Person name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Person email is required.")
            .EmailAddress().WithMessage("Person email must be a valid email address.")
            .MaximumLength(150).WithMessage("Person email must not exceed 150 characters.");
    }
}

internal sealed class UpdatePersonCommandHandler : IRequestHandler<UpdatePersonCommand, PersonDto>
{
    private readonly IPersonRepository _personRepository;
    private readonly ISystemClock _clock;

    public UpdatePersonCommandHandler(IPersonRepository personRepository, ISystemClock clock)
    {
        _personRepository = personRepository;
        _clock = clock;
    }

    public async Task<PersonDto> Handle(UpdatePersonCommand request, CancellationToken cancellationToken)
    {
        var person = await _personRepository.GetByIdAsync(request.PersonId, cancellationToken);
        if (person == null)
        {
            throw new KeyNotFoundException($"Person with ID '{request.PersonId}' was not found.");
        }

        var existingWithEmail = await _personRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existingWithEmail != null && existingWithEmail.Id != person.Id)
        {
            throw new InvalidOperationException($"Another person with email '{request.Email}' already exists.");
        }

        person.Update(request.Name, request.Email, _clock.UtcNow);
        await _personRepository.UpdateAsync(person, cancellationToken);

        return new PersonDto(person.Id, person.Name, person.Email, person.Status, person.CreatedAt, person.UpdatedAt);
    }
}

#endregion

#region DeactivatePerson

public sealed record DeactivatePersonCommand(Guid PersonId) : IRequest<bool>;

public sealed class DeactivatePersonCommandValidator : AbstractValidator<DeactivatePersonCommand>
{
    public DeactivatePersonCommandValidator()
    {
        RuleFor(x => x.PersonId)
            .NotEmpty().WithMessage("PersonId is required.");
    }
}

internal sealed class DeactivatePersonCommandHandler : IRequestHandler<DeactivatePersonCommand, bool>
{
    private readonly IPersonRepository _personRepository;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public DeactivatePersonCommandHandler(
        IPersonRepository personRepository,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _personRepository = personRepository;
        _clock = clock;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<bool> Handle(DeactivatePersonCommand request, CancellationToken cancellationToken)
    {
        var person = await _personRepository.GetByIdAsync(request.PersonId, cancellationToken);
        if (person == null)
        {
            throw new KeyNotFoundException($"Person with ID '{request.PersonId}' was not found.");
        }

        person.Deactivate(_clock.UtcNow);
        await _personRepository.UpdateAsync(person, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(person, cancellationToken);

        return true;
    }
}

#endregion

#region CreateTeam

public sealed record CreateTeamCommand(
    string Name,
    string? Description = null,
    Guid? TeamId = null) : IRequest<TeamDto>;

public sealed class CreateTeamCommandValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Team name is required.")
            .MaximumLength(100).WithMessage("Team name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");
    }
}

internal sealed class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, TeamDto>
{
    private readonly ITeamRepository _teamRepository;
    private readonly ISystemClock _clock;

    public CreateTeamCommandHandler(ITeamRepository teamRepository, ISystemClock clock)
    {
        _teamRepository = teamRepository;
        _clock = clock;
    }

    public async Task<TeamDto> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var existing = await _teamRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"A team with name '{request.Name}' already exists.");
        }

        var teamId = request.TeamId ?? Guid.NewGuid();
        var team = Team.Create(teamId, request.Name, request.Description, _clock.UtcNow);

        await _teamRepository.AddAsync(team, cancellationToken);

        return new TeamDto(team.Id, team.Name, team.Description, team.Status, team.CreatedAt, team.UpdatedAt);
    }
}

#endregion

#region AssignPersonToTeam

public sealed record AssignPersonToTeamCommand(
    Guid PersonId,
    Guid TeamId) : IRequest<Guid>;

public sealed class AssignPersonToTeamCommandValidator : AbstractValidator<AssignPersonToTeamCommand>
{
    public AssignPersonToTeamCommandValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty().WithMessage("PersonId is required.");
        RuleFor(x => x.TeamId).NotEmpty().WithMessage("TeamId is required.");
    }
}

internal sealed class AssignPersonToTeamCommandHandler : IRequestHandler<AssignPersonToTeamCommand, Guid>
{
    private readonly IPersonRepository _personRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public AssignPersonToTeamCommandHandler(
        IPersonRepository personRepository,
        ITeamRepository teamRepository,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _personRepository = personRepository;
        _teamRepository = teamRepository;
        _clock = clock;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Guid> Handle(AssignPersonToTeamCommand request, CancellationToken cancellationToken)
    {
        var person = await _personRepository.GetByIdAsync(request.PersonId, cancellationToken);
        if (person == null)
        {
            throw new KeyNotFoundException($"Person with ID '{request.PersonId}' was not found.");
        }

        var team = await _teamRepository.GetByIdAsync(request.TeamId, cancellationToken);
        if (team == null)
        {
            throw new KeyNotFoundException($"Team with ID '{request.TeamId}' was not found.");
        }

        var existingMembership = await _teamRepository.GetActiveMembershipAsync(request.PersonId, request.TeamId, cancellationToken);
        if (existingMembership != null)
        {
            return existingMembership.Id;
        }

        var membership = TeamMembership.Create(Guid.NewGuid(), request.PersonId, request.TeamId, _clock.UtcNow);
        await _teamRepository.AddMembershipAsync(membership, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(membership, cancellationToken);

        return membership.Id;
    }
}

#endregion

#region CreateRole

public sealed record CreateRoleCommand(
    string Name,
    string? Description = null,
    Guid? RoleId = null) : IRequest<RoleDto>;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Role name is required.")
            .MaximumLength(50).WithMessage("Role name must not exceed 50 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("Description must not exceed 255 characters.");
    }
}

internal sealed class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, RoleDto>
{
    private readonly IRoleRepository _roleRepository;
    private readonly ISystemClock _clock;

    public CreateRoleCommandHandler(IRoleRepository roleRepository, ISystemClock clock)
    {
        _roleRepository = roleRepository;
        _clock = clock;
    }

    public async Task<RoleDto> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var existing = await _roleRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"A role with name '{request.Name}' already exists.");
        }

        var roleId = request.RoleId ?? Guid.NewGuid();
        var role = Role.Create(roleId, request.Name, request.Description, _clock.UtcNow);

        await _roleRepository.AddAsync(role, cancellationToken);

        return new RoleDto(role.Id, role.Name, role.Description, role.Status, role.CreatedAt, role.UpdatedAt);
    }
}

#endregion

#region AssignRoleToPerson

public sealed record AssignRoleToPersonCommand(
    Guid PersonId,
    Guid RoleId) : IRequest<Guid>;

public sealed class AssignRoleToPersonCommandValidator : AbstractValidator<AssignRoleToPersonCommand>
{
    public AssignRoleToPersonCommandValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty().WithMessage("PersonId is required.");
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("RoleId is required.");
    }
}

internal sealed class AssignRoleToPersonCommandHandler : IRequestHandler<AssignRoleToPersonCommand, Guid>
{
    private readonly IPersonRepository _personRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public AssignRoleToPersonCommandHandler(
        IPersonRepository personRepository,
        IRoleRepository roleRepository,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _personRepository = personRepository;
        _roleRepository = roleRepository;
        _clock = clock;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Guid> Handle(AssignRoleToPersonCommand request, CancellationToken cancellationToken)
    {
        var person = await _personRepository.GetByIdAsync(request.PersonId, cancellationToken);
        if (person == null)
        {
            throw new KeyNotFoundException($"Person with ID '{request.PersonId}' was not found.");
        }

        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
        if (role == null)
        {
            throw new KeyNotFoundException($"Role with ID '{request.RoleId}' was not found.");
        }

        var existingAssignment = await _roleRepository.GetActiveAssignmentAsync(request.PersonId, request.RoleId, cancellationToken);
        if (existingAssignment != null)
        {
            return existingAssignment.Id;
        }

        var assignment = RoleAssignment.Create(Guid.NewGuid(), request.PersonId, request.RoleId, role.Name, _clock.UtcNow);
        await _roleRepository.AddAssignmentAsync(assignment, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(assignment, cancellationToken);

        return assignment.Id;
    }
}

#endregion

#region RevokeRoleFromPerson

public sealed record RevokeRoleFromPersonCommand(
    Guid PersonId,
    Guid RoleId) : IRequest<bool>;

public sealed class RevokeRoleFromPersonCommandValidator : AbstractValidator<RevokeRoleFromPersonCommand>
{
    public RevokeRoleFromPersonCommandValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty().WithMessage("PersonId is required.");
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("RoleId is required.");
    }
}

internal sealed class RevokeRoleFromPersonCommandHandler : IRequestHandler<RevokeRoleFromPersonCommand, bool>
{
    private readonly IRoleRepository _roleRepository;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public RevokeRoleFromPersonCommandHandler(
        IRoleRepository roleRepository,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _roleRepository = roleRepository;
        _clock = clock;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<bool> Handle(RevokeRoleFromPersonCommand request, CancellationToken cancellationToken)
    {
        var assignment = await _roleRepository.GetActiveAssignmentAsync(request.PersonId, request.RoleId, cancellationToken);
        if (assignment == null)
        {
            return false;
        }

        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
        var roleName = role?.Name ?? "Unknown";

        assignment.Revoke(roleName, _clock.UtcNow);
        await _roleRepository.RevokeAssignmentAsync(assignment.Id, _clock.UtcNow, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(assignment, cancellationToken);

        return true;
    }
}

#endregion

#region CreateResponsibility

public sealed record CreateResponsibilityCommand(
    string Name,
    string? Description = null,
    Guid? ResponsibilityId = null) : IRequest<ResponsibilityDto>;

public sealed class CreateResponsibilityCommandValidator : AbstractValidator<CreateResponsibilityCommand>
{
    public CreateResponsibilityCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Responsibility name is required.")
            .MaximumLength(100).WithMessage("Responsibility name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");
    }
}

internal sealed class CreateResponsibilityCommandHandler : IRequestHandler<CreateResponsibilityCommand, ResponsibilityDto>
{
    private readonly IResponsibilityRepository _responsibilityRepository;
    private readonly ISystemClock _clock;

    public CreateResponsibilityCommandHandler(IResponsibilityRepository responsibilityRepository, ISystemClock clock)
    {
        _responsibilityRepository = responsibilityRepository;
        _clock = clock;
    }

    public async Task<ResponsibilityDto> Handle(CreateResponsibilityCommand request, CancellationToken cancellationToken)
    {
        var existing = await _responsibilityRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"A responsibility with name '{request.Name}' already exists.");
        }

        var respId = request.ResponsibilityId ?? Guid.NewGuid();
        var resp = Responsibility.Create(respId, request.Name, request.Description, _clock.UtcNow);

        await _responsibilityRepository.AddAsync(resp, cancellationToken);

        return new ResponsibilityDto(resp.Id, resp.Name, resp.Description, resp.Status, resp.CreatedAt, resp.UpdatedAt);
    }
}

#endregion

#region AssignResponsibilityToPerson

public sealed record AssignResponsibilityToPersonCommand(
    Guid PersonId,
    Guid ResponsibilityId) : IRequest<Guid>;

public sealed class AssignResponsibilityToPersonCommandValidator : AbstractValidator<AssignResponsibilityToPersonCommand>
{
    public AssignResponsibilityToPersonCommandValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty().WithMessage("PersonId is required.");
        RuleFor(x => x.ResponsibilityId).NotEmpty().WithMessage("ResponsibilityId is required.");
    }
}

internal sealed class AssignResponsibilityToPersonCommandHandler : IRequestHandler<AssignResponsibilityToPersonCommand, Guid>
{
    private readonly IPersonRepository _personRepository;
    private readonly IResponsibilityRepository _responsibilityRepository;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public AssignResponsibilityToPersonCommandHandler(
        IPersonRepository personRepository,
        IResponsibilityRepository responsibilityRepository,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _personRepository = personRepository;
        _responsibilityRepository = responsibilityRepository;
        _clock = clock;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Guid> Handle(AssignResponsibilityToPersonCommand request, CancellationToken cancellationToken)
    {
        var person = await _personRepository.GetByIdAsync(request.PersonId, cancellationToken);
        if (person == null)
        {
            throw new KeyNotFoundException($"Person with ID '{request.PersonId}' was not found.");
        }

        var resp = await _responsibilityRepository.GetByIdAsync(request.ResponsibilityId, cancellationToken);
        if (resp == null)
        {
            throw new KeyNotFoundException($"Responsibility with ID '{request.ResponsibilityId}' was not found.");
        }

        var existing = await _responsibilityRepository.GetActiveAssignmentAsync(request.PersonId, request.ResponsibilityId, cancellationToken);
        if (existing != null)
        {
            return existing.Id;
        }

        var assignment = ResponsibilityAssignment.Create(Guid.NewGuid(), request.PersonId, request.ResponsibilityId, resp.Name, _clock.UtcNow);
        await _responsibilityRepository.AddAssignmentAsync(assignment, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(assignment, cancellationToken);

        return assignment.Id;
    }
}

#endregion

#region RevokeResponsibilityFromPerson

public sealed record RevokeResponsibilityFromPersonCommand(
    Guid PersonId,
    Guid ResponsibilityId) : IRequest<bool>;

public sealed class RevokeResponsibilityFromPersonCommandValidator : AbstractValidator<RevokeResponsibilityFromPersonCommand>
{
    public RevokeResponsibilityFromPersonCommandValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty().WithMessage("PersonId is required.");
        RuleFor(x => x.ResponsibilityId).NotEmpty().WithMessage("ResponsibilityId is required.");
    }
}

internal sealed class RevokeResponsibilityFromPersonCommandHandler : IRequestHandler<RevokeResponsibilityFromPersonCommand, bool>
{
    private readonly IResponsibilityRepository _responsibilityRepository;
    private readonly ISystemClock _clock;

    public RevokeResponsibilityFromPersonCommandHandler(
        IResponsibilityRepository responsibilityRepository,
        ISystemClock clock)
    {
        _responsibilityRepository = responsibilityRepository;
        _clock = clock;
    }

    public async Task<bool> Handle(RevokeResponsibilityFromPersonCommand request, CancellationToken cancellationToken)
    {
        var assignment = await _responsibilityRepository.GetActiveAssignmentAsync(request.PersonId, request.ResponsibilityId, cancellationToken);
        if (assignment == null)
        {
            return false;
        }

        assignment.Revoke(_clock.UtcNow);
        await _responsibilityRepository.RevokeAssignmentAsync(assignment.Id, _clock.UtcNow, cancellationToken);

        return true;
    }
}

#endregion
