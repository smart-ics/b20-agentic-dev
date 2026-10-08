using Cakra.Core;
using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Domain.Events;
using Cakra.Modules.Organization.Persistence;

namespace Cakra.Modules.Organization.Services;

/// <summary>
/// Application service orchestrating Organization domain mutations and domain event dispatching
/// (Architecture §7, §14, §16, §18).
/// </summary>
public sealed class OrganizationService : IOrganizationService
{
    private readonly IPersonRepository _personRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IResponsibilityRepository _responsibilityRepository;
    private readonly ITeamMembershipRepository _teamMembershipRepository;
    private readonly IRoleAssignmentRepository _roleAssignmentRepository;
    private readonly IResponsibilityAssignmentRepository _responsibilityAssignmentRepository;
    private readonly IDomainEventDispatcher? _eventDispatcher;
    private readonly ISystemClock? _clock;

    internal OrganizationService(
        IPersonRepository personRepository,
        ITeamRepository teamRepository,
        IRoleRepository roleRepository,
        IResponsibilityRepository responsibilityRepository,
        ITeamMembershipRepository teamMembershipRepository,
        IRoleAssignmentRepository roleAssignmentRepository,
        IResponsibilityAssignmentRepository responsibilityAssignmentRepository,
        IDomainEventDispatcher? eventDispatcher = null,
        ISystemClock? clock = null)
    {
        _personRepository = personRepository ?? throw new ArgumentNullException(nameof(personRepository));
        _teamRepository = teamRepository ?? throw new ArgumentNullException(nameof(teamRepository));
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _responsibilityRepository = responsibilityRepository ?? throw new ArgumentNullException(nameof(responsibilityRepository));
        _teamMembershipRepository = teamMembershipRepository ?? throw new ArgumentNullException(nameof(teamMembershipRepository));
        _roleAssignmentRepository = roleAssignmentRepository ?? throw new ArgumentNullException(nameof(roleAssignmentRepository));
        _responsibilityAssignmentRepository = responsibilityAssignmentRepository ?? throw new ArgumentNullException(nameof(responsibilityAssignmentRepository));
        _eventDispatcher = eventDispatcher;
        _clock = clock;
    }

    private DateTime UtcNow => _clock?.UtcNow ?? DateTime.UtcNow;

    /// <inheritdoc />
    public async Task<Person> CreatePersonAsync(
        string firstName,
        string lastName,
        string email,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalizedEmail = email.Trim();
        var existing = await _personRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"A person with email '{normalizedEmail}' already exists.");
        }

        var now = UtcNow;
        var person = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = normalizedEmail,
            Status = Person.StatusActive,
            CreatedAt = now
        };

        await _personRepository.AddAsync(person, cancellationToken);

        if (_eventDispatcher is not null)
        {
            await _eventDispatcher.DispatchAsync(
                new PersonCreated(person.Id, person.FirstName, person.LastName, person.Email, Guid.NewGuid(), now),
                cancellationToken);
        }

        if (roleIds is not null)
        {
            foreach (var roleId in roleIds.Distinct())
            {
                _ = await _roleRepository.GetByIdAsync(roleId, cancellationToken)
                    ?? throw new KeyNotFoundException($"Role '{roleId}' was not found.");

                var assignment = new RoleAssignment
                {
                    PersonId = person.Id,
                    RoleId = roleId,
                    AssignedAt = now,
                    RevokedAt = null
                };

                await _roleAssignmentRepository.AssignAsync(assignment, cancellationToken);

                if (_eventDispatcher is not null)
                {
                    await _eventDispatcher.DispatchAsync(
                        new RoleAssigned(person.Id, roleId, now, Guid.NewGuid(), now),
                        cancellationToken);
                }
            }
        }

        return person;
    }

    /// <inheritdoc />
    public Task<Person> CreatePersonAsync(
        string firstName,
        string lastName,
        string email,
        CancellationToken cancellationToken = default) =>
        CreatePersonAsync(firstName, lastName, email, Array.Empty<Guid>(), cancellationToken);

    /// <inheritdoc />
    public async Task<Person> UpdatePersonAsync(
        Guid personId,
        string firstName,
        string lastName,
        string email,
        IReadOnlyList<Guid> roleIds,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var person = await _personRepository.GetByIdAsync(personId, cancellationToken)
            ?? throw new KeyNotFoundException($"Person '{personId}' was not found.");

        var normalizedEmail = email.Trim();
        if (!string.Equals(person.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            var existingByEmail = await _personRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
            if (existingByEmail is not null && existingByEmail.Id != personId)
            {
                throw new InvalidOperationException($"A person with email '{normalizedEmail}' already exists.");
            }
        }

        var now = UtcNow;

        // Self-demotion check (TD-004):
        // If ActorPersonId == TargetPersonId, verify that the Administrator role is not removed.
        if (actorPersonId.HasValue && actorPersonId.Value == personId)
        {
            var adminRole = await _roleRepository.GetByNameAsync("Administrator", cancellationToken);
            if (adminRole is not null)
            {
                var activeAssignments = await _roleAssignmentRepository.GetActiveByPersonIdAsync(personId, cancellationToken);
                var isCurrentlyAdmin = activeAssignments.Any(a => a.RoleId == adminRole.Id);
                if (isCurrentlyAdmin && (roleIds is null || !roleIds.Contains(adminRole.Id)))
                {
                    throw new InvalidOperationException("Cannot remove the Administrator role from your own person record.");
                }
            }
        }

        person.FirstName = firstName.Trim();
        person.LastName = lastName.Trim();
        person.Email = normalizedEmail;
        person.UpdatedAt = now;

        await _personRepository.UpdateAsync(person, cancellationToken);

        // Role set reconciliation (TD-002):
        if (roleIds is not null)
        {
            var currentActiveAssignments = await _roleAssignmentRepository.GetActiveByPersonIdAsync(personId, cancellationToken);
            var currentActiveRoleIds = currentActiveAssignments.Select(a => a.RoleId).ToHashSet();
            var desiredRoleIds = roleIds.Distinct().ToHashSet();

            var toRevoke = currentActiveRoleIds.Except(desiredRoleIds).ToList();
            var toAdd = desiredRoleIds.Except(currentActiveRoleIds).ToList();

            foreach (var roleId in toRevoke)
            {
                await _roleAssignmentRepository.RevokeAsync(personId, roleId, now, cancellationToken);

                if (_eventDispatcher is not null)
                {
                    await _eventDispatcher.DispatchAsync(
                        new RoleRevoked(personId, roleId, now, Guid.NewGuid(), now),
                        cancellationToken);
                }
            }

            foreach (var roleId in toAdd)
            {
                _ = await _roleRepository.GetByIdAsync(roleId, cancellationToken)
                    ?? throw new KeyNotFoundException($"Role '{roleId}' was not found.");

                var assignment = new RoleAssignment
                {
                    PersonId = personId,
                    RoleId = roleId,
                    AssignedAt = now,
                    RevokedAt = null
                };

                await _roleAssignmentRepository.AssignAsync(assignment, cancellationToken);

                if (_eventDispatcher is not null)
                {
                    await _eventDispatcher.DispatchAsync(
                        new RoleAssigned(personId, roleId, now, Guid.NewGuid(), now),
                        cancellationToken);
                }
            }
        }

        return person;
    }

    /// <inheritdoc />
    public Task<Person> UpdatePersonAsync(
        Guid personId,
        string firstName,
        string lastName,
        string email,
        CancellationToken cancellationToken = default) =>
        UpdatePersonAsync(personId, firstName, lastName, email, null!, null, cancellationToken);

    /// <inheritdoc />
    public async Task<Team> CreateTeamAsync(
        string name,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedName = name.Trim();
        var existing = await _teamRepository.GetByNameAsync(normalizedName, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"A team with name '{normalizedName}' already exists.");
        }

        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedAt = UtcNow
        };

        await _teamRepository.AddAsync(team, cancellationToken);
        return team;
    }

    /// <inheritdoc />
    public async Task<TeamMembership> AssignPersonToTeamAsync(
        Guid personId,
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        _ = await _personRepository.GetByIdAsync(personId, cancellationToken)
            ?? throw new KeyNotFoundException($"Person '{personId}' was not found.");

        _ = await _teamRepository.GetByIdAsync(teamId, cancellationToken)
            ?? throw new KeyNotFoundException($"Team '{teamId}' was not found.");

        var membership = new TeamMembership
        {
            PersonId = personId,
            TeamId = teamId,
            AssignedAt = UtcNow
        };

        await _teamMembershipRepository.AddAsync(membership, cancellationToken);
        return membership;
    }

    /// <inheritdoc />
    public async Task<Role> CreateRoleAsync(
        string name,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedName = name.Trim();
        var existing = await _roleRepository.GetByNameAsync(normalizedName, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"A role with name '{normalizedName}' already exists.");
        }

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedAt = UtcNow
        };

        await _roleRepository.AddAsync(role, cancellationToken);
        return role;
    }

    /// <inheritdoc />
    public async Task<RoleAssignment> AssignRoleToPersonAsync(
        Guid personId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        _ = await _personRepository.GetByIdAsync(personId, cancellationToken)
            ?? throw new KeyNotFoundException($"Person '{personId}' was not found.");

        _ = await _roleRepository.GetByIdAsync(roleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Role '{roleId}' was not found.");

        var now = UtcNow;
        var assignment = new RoleAssignment
        {
            PersonId = personId,
            RoleId = roleId,
            AssignedAt = now,
            RevokedAt = null
        };

        await _roleAssignmentRepository.AssignAsync(assignment, cancellationToken);

        if (_eventDispatcher is not null)
        {
            await _eventDispatcher.DispatchAsync(
                new RoleAssigned(personId, roleId, now, Guid.NewGuid(), now),
                cancellationToken);
        }

        return assignment;
    }

    /// <inheritdoc />
    public async Task RevokeRoleFromPersonAsync(
        Guid personId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        _ = await _personRepository.GetByIdAsync(personId, cancellationToken)
            ?? throw new KeyNotFoundException($"Person '{personId}' was not found.");

        _ = await _roleRepository.GetByIdAsync(roleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Role '{roleId}' was not found.");

        var now = UtcNow;
        await _roleAssignmentRepository.RevokeAsync(personId, roleId, now, cancellationToken);

        if (_eventDispatcher is not null)
        {
            await _eventDispatcher.DispatchAsync(
                new RoleRevoked(personId, roleId, now, Guid.NewGuid(), now),
                cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<Responsibility> CreateResponsibilityAsync(
        string name,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedName = name.Trim();
        var existing = await _responsibilityRepository.GetByNameAsync(normalizedName, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"A responsibility with name '{normalizedName}' already exists.");
        }

        var responsibility = new Responsibility
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedAt = UtcNow
        };

        await _responsibilityRepository.AddAsync(responsibility, cancellationToken);
        return responsibility;
    }

    /// <inheritdoc />
    public async Task<ResponsibilityAssignment> AssignResponsibilityToPersonAsync(
        Guid personId,
        Guid responsibilityId,
        CancellationToken cancellationToken = default)
    {
        _ = await _personRepository.GetByIdAsync(personId, cancellationToken)
            ?? throw new KeyNotFoundException($"Person '{personId}' was not found.");

        _ = await _responsibilityRepository.GetByIdAsync(responsibilityId, cancellationToken)
            ?? throw new KeyNotFoundException($"Responsibility '{responsibilityId}' was not found.");

        var assignment = new ResponsibilityAssignment
        {
            PersonId = personId,
            ResponsibilityId = responsibilityId,
            AssignedAt = UtcNow
        };

        await _responsibilityAssignmentRepository.AssignAsync(assignment, cancellationToken);
        return assignment;
    }

    /// <inheritdoc />
    public async Task<Person> DeactivatePersonAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        var person = await _personRepository.GetByIdAsync(personId, cancellationToken)
            ?? throw new KeyNotFoundException($"Person '{personId}' was not found.");

        var now = UtcNow;
        person.Deactivate(now);
        await _personRepository.UpdateStatusAsync(personId, Person.StatusInactive, cancellationToken);

        if (_eventDispatcher is not null)
        {
            await _eventDispatcher.DispatchAsync(
                new PersonDeactivated(personId, Guid.NewGuid(), now),
                cancellationToken);
        }

        return person;
    }

    /// <inheritdoc />
    public async Task<Person> ActivatePersonAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        var person = await _personRepository.GetByIdAsync(personId, cancellationToken)
            ?? throw new KeyNotFoundException($"Person '{personId}' was not found.");

        var now = UtcNow;
        person.Activate(now);
        await _personRepository.UpdateStatusAsync(personId, Person.StatusActive, cancellationToken);

        if (_eventDispatcher is not null)
        {
            await _eventDispatcher.DispatchAsync(
                new PersonActivated(personId, Guid.NewGuid(), now),
                cancellationToken);
        }

        return person;
    }
}
