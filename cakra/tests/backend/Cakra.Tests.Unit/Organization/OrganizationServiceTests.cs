using Cakra.Core;
using Cakra.Modules.Organization;
using Cakra.Modules.Organization.Commands;
using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Domain.Events;
using Cakra.Modules.Organization.Persistence;
using Cakra.Modules.Organization.Services;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Organization;

public class OrganizationServiceTests
{
    private readonly InMemoryPersonRepository _personRepository = new();
    private readonly InMemoryTeamRepository _teamRepository = new();
    private readonly InMemoryRoleRepository _roleRepository = new();
    private readonly InMemoryResponsibilityRepository _responsibilityRepository = new();
    private readonly InMemoryTeamMembershipRepository _teamMembershipRepository = new();
    private readonly InMemoryRoleAssignmentRepository _roleAssignmentRepository = new();
    private readonly InMemoryResponsibilityAssignmentRepository _responsibilityAssignmentRepository = new();
    private readonly RecordingEventDispatcher _eventDispatcher = new();
    private readonly FixedClock _clock = new(new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc));
    private readonly OrganizationService _service;

    public OrganizationServiceTests()
    {
        _service = new OrganizationService(
            _personRepository,
            _teamRepository,
            _roleRepository,
            _responsibilityRepository,
            _teamMembershipRepository,
            _roleAssignmentRepository,
            _responsibilityAssignmentRepository,
            _eventDispatcher,
            _clock);
    }

    [Fact]
    public async Task CreatePersonCommand_creates_active_person_and_dispatches_PersonCreated_and_RoleAssigned_events()
    {
        var role = await _service.CreateRoleAsync("Developer");
        _eventDispatcher.DispatchedEvents.Clear();

        var handler = new CreatePersonCommandHandler(_service);
        var command = new CreatePersonCommand("Alice", "Pratama", "alice.pratama@cakra.id", new[] { role.Id });

        var person = await handler.Handle(command, CancellationToken.None);

        person.Id.Should().NotBeEmpty();
        person.FirstName.Should().Be("Alice");
        person.LastName.Should().Be("Pratama");
        person.Email.Should().Be("alice.pratama@cakra.id");
        person.Status.Should().Be(Person.StatusActive);
        person.IsActive.Should().BeTrue();
        person.CreatedAt.Should().Be(_clock.UtcNow);

        var persisted = await _personRepository.GetByIdAsync(person.Id);
        persisted.Should().NotBeNull();

        var assignments = await _roleAssignmentRepository.GetActiveByPersonIdAsync(person.Id);
        assignments.Should().ContainSingle(a => a.RoleId == role.Id);

        _eventDispatcher.DispatchedEvents.Should().HaveCount(2);
        _eventDispatcher.DispatchedEvents.Should().ContainSingle(e => e is PersonCreated)
            .Which.Should().BeOfType<PersonCreated>()
            .Which.PersonId.Should().Be(person.Id);
        _eventDispatcher.DispatchedEvents.Should().ContainSingle(e => e is RoleAssigned)
            .Which.Should().BeOfType<RoleAssigned>()
            .Which.RoleId.Should().Be(role.Id);
    }

    [Fact]
    public async Task CreatePersonCommand_rejects_duplicate_email()
    {
        var role = await _service.CreateRoleAsync("Tester");
        var handler = new CreatePersonCommandHandler(_service);
        await handler.Handle(new CreatePersonCommand("Alice", "One", "dup@cakra.id", new[] { role.Id }), CancellationToken.None);

        var act = async () => await handler.Handle(
            new CreatePersonCommand("Bob", "Two", "dup@cakra.id", new[] { role.Id }),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*dup@cakra.id*");
    }

    [Fact]
    public async Task UpdatePersonCommand_updates_person_attributes_and_timestamp()
    {
        var role = await _service.CreateRoleAsync("Architect");
        var created = await _service.CreatePersonAsync("Budi", "Santoso", "budi@cakra.id", new[] { role.Id });
        _clock.UtcNow = _clock.UtcNow.AddMinutes(15);

        var handler = new UpdatePersonCommandHandler(_service);
        var updated = await handler.Handle(
            new UpdatePersonCommand(created.Id, "Budi", "Wijaya", "budi.wijaya@cakra.id", new[] { role.Id }),
            CancellationToken.None);

        updated.FirstName.Should().Be("Budi");
        updated.LastName.Should().Be("Wijaya");
        updated.Email.Should().Be("budi.wijaya@cakra.id");
        updated.UpdatedAt.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public async Task CreateTeamCommand_and_AssignPersonToTeamCommand_assign_person_to_team()
    {
        var person = await _service.CreatePersonAsync("Citra", "Dewi", "citra@cakra.id");
        var createTeamHandler = new CreateTeamCommandHandler(_service);
        var assignTeamHandler = new AssignPersonToTeamCommandHandler(_service);

        var team = await createTeamHandler.Handle(
            new CreateTeamCommand("Platform Engineering", "Core backend team"),
            CancellationToken.None);

        team.Id.Should().NotBeEmpty();
        team.Name.Should().Be("Platform Engineering");
        team.Description.Should().Be("Core backend team");

        var membership = await assignTeamHandler.Handle(
            new AssignPersonToTeamCommand(person.Id, team.Id),
            CancellationToken.None);

        membership.PersonId.Should().Be(person.Id);
        membership.TeamId.Should().Be(team.Id);
        membership.AssignedAt.Should().Be(_clock.UtcNow);

        var exists = await _teamMembershipRepository.ExistsAsync(person.Id, team.Id);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task CreateRoleCommand_and_AssignRoleToPersonCommand_assign_role_and_dispatch_RoleAssigned()
    {
        var person = await _service.CreatePersonAsync("Dedi", "Kurniawan", "dedi@cakra.id");
        _eventDispatcher.DispatchedEvents.Clear();

        var createRoleHandler = new CreateRoleCommandHandler(_service);
        var assignRoleHandler = new AssignRoleToPersonCommandHandler(_service);

        var role = await createRoleHandler.Handle(
            new CreateRoleCommand("Management", "Executive oversight"),
            CancellationToken.None);

        role.Id.Should().NotBeEmpty();
        role.Name.Should().Be("Management");

        var assignment = await assignRoleHandler.Handle(
            new AssignRoleToPersonCommand(person.Id, role.Id),
            CancellationToken.None);

        assignment.PersonId.Should().Be(person.Id);
        assignment.RoleId.Should().Be(role.Id);
        assignment.IsActive.Should().BeTrue();

        _eventDispatcher.DispatchedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RoleAssigned>()
            .Which.Should().Match<RoleAssigned>(e => e.PersonId == person.Id && e.RoleId == role.Id);
    }

    [Fact]
    public async Task RevokeRoleFromPersonCommand_revokes_role_and_dispatches_RoleRevoked()
    {
        var person = await _service.CreatePersonAsync("Eka", "Putri", "eka@cakra.id");
        var role = await _service.CreateRoleAsync("Programmer", "Developer");
        await _service.AssignRoleToPersonAsync(person.Id, role.Id);
        _eventDispatcher.DispatchedEvents.Clear();

        var revokeHandler = new RevokeRoleFromPersonCommandHandler(_service);
        await revokeHandler.Handle(new RevokeRoleFromPersonCommand(person.Id, role.Id), CancellationToken.None);

        var activeRoles = await _roleAssignmentRepository.GetActiveByPersonIdAsync(person.Id);
        activeRoles.Should().BeEmpty();

        _eventDispatcher.DispatchedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RoleRevoked>()
            .Which.Should().Match<RoleRevoked>(e => e.PersonId == person.Id && e.RoleId == role.Id);
    }

    [Fact]
    public async Task CreateResponsibilityCommand_and_AssignResponsibilityToPersonCommand_assign_responsibility()
    {
        var person = await _service.CreatePersonAsync("Fajar", "Nugroho", "fajar@cakra.id");
        var createRespHandler = new CreateResponsibilityCommandHandler(_service);
        var assignRespHandler = new AssignResponsibilityToPersonCommandHandler(_service);

        var responsibility = await createRespHandler.Handle(
            new CreateResponsibilityCommand("Module PIC", "Accountable for product module"),
            CancellationToken.None);

        responsibility.Id.Should().NotBeEmpty();
        responsibility.Name.Should().Be("Module PIC");

        var assignment = await assignRespHandler.Handle(
            new AssignResponsibilityToPersonCommand(person.Id, responsibility.Id),
            CancellationToken.None);

        assignment.PersonId.Should().Be(person.Id);
        assignment.ResponsibilityId.Should().Be(responsibility.Id);

        var exists = await _responsibilityAssignmentRepository.ExistsAsync(person.Id, responsibility.Id);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivatePersonCommand_marks_person_inactive_and_dispatches_PersonDeactivated()
    {
        var person = await _service.CreatePersonAsync("Gita", "Sari", "gita@cakra.id");
        _eventDispatcher.DispatchedEvents.Clear();

        var deactivateHandler = new DeactivatePersonCommandHandler(_service);
        var deactivated = await deactivateHandler.Handle(
            new DeactivatePersonCommand(person.Id),
            CancellationToken.None);

        deactivated.Status.Should().Be(Person.StatusInactive);
        deactivated.IsActive.Should().BeFalse();

        var persisted = await _personRepository.GetByIdAsync(person.Id);
        persisted!.Status.Should().Be(Person.StatusInactive);

        _eventDispatcher.DispatchedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PersonDeactivated>()
            .Which.PersonId.Should().Be(person.Id);
    }

    [Fact]
    public void Command_validators_reject_invalid_inputs()
    {
        var createValidation = new CreatePersonCommandValidator()
            .Validate(new CreatePersonCommand("", "", "not-an-email", Array.Empty<Guid>()));
        createValidation.IsValid.Should().BeFalse();
        createValidation.Errors.Should().Contain(e => e.PropertyName == "RoleIds" && e.ErrorMessage == "At least one role must be assigned.");

        var updateValidation = new UpdatePersonCommandValidator()
            .Validate(new UpdatePersonCommand(Guid.Empty, "", "", "bad", Array.Empty<Guid>()));
        updateValidation.IsValid.Should().BeFalse();
        updateValidation.Errors.Should().Contain(e => e.PropertyName == "RoleIds" && e.ErrorMessage == "At least one role must be assigned.");

        new CreateTeamCommandValidator()
            .Validate(new CreateTeamCommand(""))
            .IsValid.Should().BeFalse();

        new AssignPersonToTeamCommandValidator()
            .Validate(new AssignPersonToTeamCommand(Guid.Empty, Guid.Empty))
            .IsValid.Should().BeFalse();

        new CreateRoleCommandValidator()
            .Validate(new CreateRoleCommand(""))
            .IsValid.Should().BeFalse();

        new AssignRoleToPersonCommandValidator()
            .Validate(new AssignRoleToPersonCommand(Guid.Empty, Guid.Empty))
            .IsValid.Should().BeFalse();

        new CreateResponsibilityCommandValidator()
            .Validate(new CreateResponsibilityCommand(""))
            .IsValid.Should().BeFalse();

        new AssignResponsibilityToPersonCommandValidator()
            .Validate(new AssignResponsibilityToPersonCommand(Guid.Empty, Guid.Empty))
            .IsValid.Should().BeFalse();

        new DeactivatePersonCommandValidator()
            .Validate(new DeactivatePersonCommand(Guid.Empty))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreatePersonCommandValidator_rejects_empty_RoleIds()
    {
        var validator = new CreatePersonCommandValidator();
        var command = new CreatePersonCommand("John", "Doe", "john.doe@cakra.id", Array.Empty<Guid>());

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RoleIds" && e.ErrorMessage == "At least one role must be assigned.");
    }

    [Fact]
    public void UpdatePersonCommandValidator_rejects_empty_RoleIds()
    {
        var validator = new UpdatePersonCommandValidator();
        var command = new UpdatePersonCommand(Guid.NewGuid(), "John", "Doe", "john.doe@cakra.id", Array.Empty<Guid>());

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RoleIds" && e.ErrorMessage == "At least one role must be assigned.");
    }

    [Fact]
    public async Task CreatePersonCommand_assigns_multiple_roles_atomically_and_dispatches_events()
    {
        var role1 = await _service.CreateRoleAsync("Lead");
        var role2 = await _service.CreateRoleAsync("Developer");
        _eventDispatcher.DispatchedEvents.Clear();

        var handler = new CreatePersonCommandHandler(_service);
        var command = new CreatePersonCommand("Dewi", "Lestari", "dewi@cakra.id", new[] { role1.Id, role2.Id });

        var person = await handler.Handle(command, CancellationToken.None);

        var activeRoles = await _roleAssignmentRepository.GetActiveByPersonIdAsync(person.Id);
        activeRoles.Should().HaveCount(2);
        activeRoles.Select(a => a.RoleId).Should().BeEquivalentTo(new[] { role1.Id, role2.Id });

        _eventDispatcher.DispatchedEvents.Should().HaveCount(3); // PersonCreated + 2x RoleAssigned
        _eventDispatcher.DispatchedEvents.OfType<PersonCreated>().Should().ContainSingle();
        _eventDispatcher.DispatchedEvents.OfType<RoleAssigned>().Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdatePersonCommand_performs_role_set_reconciliation_adding_and_revoking_roles()
    {
        var role1 = await _service.CreateRoleAsync("RoleA");
        var role2 = await _service.CreateRoleAsync("RoleB");
        var role3 = await _service.CreateRoleAsync("RoleC");

        var created = await _service.CreatePersonAsync("Indra", "Gunawan", "indra@cakra.id", new[] { role1.Id, role2.Id });
        _eventDispatcher.DispatchedEvents.Clear();

        // Update to have RoleB and RoleC (keep RoleB, revoke RoleA, add RoleC)
        var handler = new UpdatePersonCommandHandler(_service);
        var command = new UpdatePersonCommand(created.Id, "Indra", "Gunawan", "indra@cakra.id", new[] { role2.Id, role3.Id });

        await handler.Handle(command, CancellationToken.None);

        var activeRoles = await _roleAssignmentRepository.GetActiveByPersonIdAsync(created.Id);
        activeRoles.Should().HaveCount(2);
        activeRoles.Select(a => a.RoleId).Should().BeEquivalentTo(new[] { role2.Id, role3.Id });

        _eventDispatcher.DispatchedEvents.OfType<RoleRevoked>().Should().ContainSingle(e => e.RoleId == role1.Id);
        _eventDispatcher.DispatchedEvents.OfType<RoleAssigned>().Should().ContainSingle(e => e.RoleId == role3.Id);
    }

    [Fact]
    public async Task UpdatePersonCommand_throws_InvalidOperationException_when_administrator_removes_admin_role_from_self()
    {
        var adminRole = await _service.CreateRoleAsync("Administrator");
        var devRole = await _service.CreateRoleAsync("Programmer");

        var adminPerson = await _service.CreatePersonAsync("Super", "Admin", "admin@cakra.id", new[] { adminRole.Id });

        var handler = new UpdatePersonCommandHandler(_service);

        // Attempt self-demotion: Actor is the person being updated, roleIds does not contain Administrator
        var demoteCommand = new UpdatePersonCommand(
            adminPerson.Id,
            "Super",
            "Admin",
            "admin@cakra.id",
            new[] { devRole.Id },
            ActorPersonId: adminPerson.Id);

        var act = async () => await handler.Handle(demoteCommand, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot remove the Administrator role from your own person record.");
    }

    [Fact]
    public async Task UpdatePersonCommandValidator_validates_self_demotion_invariant()
    {
        var adminRole = await _service.CreateRoleAsync("Administrator");
        var otherRole = await _service.CreateRoleAsync("Support");

        var targetId = Guid.NewGuid();

        var validator = new UpdatePersonCommandValidator(_roleRepository);

        // 1. Actor == PersonId without Admin role -> invalid
        var selfDemoteCmd = new UpdatePersonCommand(targetId, "Test", "User", "test@cakra.id", new[] { otherRole.Id }, ActorPersonId: targetId);
        var result = await validator.ValidateAsync(selfDemoteCmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Cannot remove the Administrator role from your own person record.");

        // 2. Actor == PersonId with Admin role -> valid
        var validSelfCmd = new UpdatePersonCommand(targetId, "Test", "User", "test@cakra.id", new[] { adminRole.Id, otherRole.Id }, ActorPersonId: targetId);
        var validResult = await validator.ValidateAsync(validSelfCmd);
        validResult.IsValid.Should().BeTrue();

        // 3. Actor != PersonId without Admin role -> valid (admin editing another person can remove admin role)
        var differentActorCmd = new UpdatePersonCommand(targetId, "Test", "User", "test@cakra.id", new[] { otherRole.Id }, ActorPersonId: Guid.NewGuid());
        var diffResult = await validator.ValidateAsync(differentActorCmd);
        diffResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UpdatePersonCommandValidator_with_assignment_repository_checks_current_admin_membership()
    {
        var adminRole = await _service.CreateRoleAsync("Administrator");
        var devRole = await _service.CreateRoleAsync("Developer");

        var nonAdminPerson = await _service.CreatePersonAsync("Budi", "Santoso", "budi@cakra.id", new[] { devRole.Id });
        var adminPerson = await _service.CreatePersonAsync("Siti", "Aminah", "siti@cakra.id", new[] { adminRole.Id });

        var validator = new UpdatePersonCommandValidator(_roleRepository, _roleAssignmentRepository);

        // 1. Non-admin user self-updating without admin role -> valid because they are not currently an admin
        var nonAdminSelfUpdate = new UpdatePersonCommand(
            nonAdminPerson.Id, "Budi", "Santoso", "budi@cakra.id", new[] { devRole.Id }, ActorPersonId: nonAdminPerson.Id);
        var nonAdminResult = await validator.ValidateAsync(nonAdminSelfUpdate);
        nonAdminResult.IsValid.Should().BeTrue();

        // 2. Admin user self-updating and attempting to remove admin role -> invalid
        var adminSelfDemote = new UpdatePersonCommand(
            adminPerson.Id, "Siti", "Aminah", "siti@cakra.id", new[] { devRole.Id }, ActorPersonId: adminPerson.Id);
        var adminDemoteResult = await validator.ValidateAsync(adminSelfDemote);
        adminDemoteResult.IsValid.Should().BeFalse();
        adminDemoteResult.Errors.Should().Contain(e => e.ErrorMessage == "Cannot remove the Administrator role from your own person record.");

        // 3. Admin user self-updating keeping admin role -> valid
        var adminSelfValid = new UpdatePersonCommand(
            adminPerson.Id, "Siti", "Aminah", "siti@cakra.id", new[] { adminRole.Id, devRole.Id }, ActorPersonId: adminPerson.Id);
        var adminValidResult = await validator.ValidateAsync(adminSelfValid);
        adminValidResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivatePersonCommand_retains_existing_role_assignments_untouched()
    {
        var role = await _service.CreateRoleAsync("Analyst");
        var person = await _service.CreatePersonAsync("Maya", "Putri", "maya@cakra.id", new[] { role.Id });

        var deactivateHandler = new DeactivatePersonCommandHandler(_service);
        await deactivateHandler.Handle(new DeactivatePersonCommand(person.Id), CancellationToken.None);

        var activeRoles = await _roleAssignmentRepository.GetActiveByPersonIdAsync(person.Id);
        activeRoles.Should().ContainSingle(a => a.RoleId == role.Id);
    }

    [Fact]
    public void Published_interfaces_are_public_and_internal_repositories_are_not_exposed()
    {
        // Published query & application service interfaces are public
        typeof(IOrganizationQueryService).IsPublic.Should().BeTrue();
        typeof(OrganizationQueryService).IsPublic.Should().BeTrue();
        typeof(IOrganizationService).IsPublic.Should().BeTrue();
        typeof(OrganizationService).IsPublic.Should().BeTrue();

        // Internal repositories in Cakra.Modules.Organization.Persistence are not publicly exposed (Architecture §20)
        var persistenceTypes = typeof(OrganizationModule).Assembly
            .GetTypes()
            .Where(t => string.Equals(t.Namespace, "Cakra.Modules.Organization.Persistence", StringComparison.Ordinal))
            .ToList();

        persistenceTypes.Should().NotBeEmpty();
        persistenceTypes.Should().OnlyContain(t => !t.IsPublic,
            "Internal repositories in Organization module must not be publicly exposed to other modules (Architecture §20)");
    }
}

// =========================================================================
// Test Fakes
// =========================================================================

internal sealed class FixedClock(DateTime initialUtcNow) : ISystemClock
    {
        public DateTime UtcNow { get; set; } = initialUtcNow;
    }

    internal sealed class RecordingEventDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> DispatchedEvents { get; } = new();

        public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            DispatchedEvents.Add(domainEvent);
            return Task.CompletedTask;
        }
    }

    internal sealed class InMemoryPersonRepository : IPersonRepository
    {
        private readonly Dictionary<Guid, Person> _items = new();

        public Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.GetValueOrDefault(id));

        public Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Person>>(_items.Values.ToList());

        public Task<IReadOnlyList<Person>> GetActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Person>>(_items.Values.Where(p => p.IsActive).ToList());

        public Task<Person?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Values.FirstOrDefault(p =>
                string.Equals(p.Email, email, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(Person entity, CancellationToken cancellationToken = default)
        {
            _items[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Person entity, CancellationToken cancellationToken = default)
        {
            _items[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
        {
            if (_items.TryGetValue(id, out var person))
            {
                person.Status = status;
                person.UpdatedAt = DateTime.UtcNow;
            }
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _items.Remove(id);
            return Task.CompletedTask;
        }
    }

    internal sealed class InMemoryTeamRepository : ITeamRepository
    {
        private readonly Dictionary<Guid, Team> _items = new();

        public Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.GetValueOrDefault(id));

        public Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Team>>(_items.Values.ToList());

        public Task<Team?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Values.FirstOrDefault(t =>
                string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(Team entity, CancellationToken cancellationToken = default)
        {
            _items[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Team entity, CancellationToken cancellationToken = default)
        {
            _items[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _items.Remove(id);
            return Task.CompletedTask;
        }
    }

    internal sealed class InMemoryRoleRepository : IRoleRepository
    {
        private readonly Dictionary<Guid, Role> _items = new();

        public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.GetValueOrDefault(id));

        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Role>>(_items.Values.ToList());

        public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(Role entity, CancellationToken cancellationToken = default)
        {
            _items[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Role entity, CancellationToken cancellationToken = default)
        {
            _items[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _items.Remove(id);
            return Task.CompletedTask;
        }
    }

    internal sealed class InMemoryResponsibilityRepository : IResponsibilityRepository
    {
        private readonly Dictionary<Guid, Responsibility> _items = new();

        public Task<Responsibility?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.GetValueOrDefault(id));

        public Task<IReadOnlyList<Responsibility>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Responsibility>>(_items.Values.ToList());

        public Task<Responsibility?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(Responsibility entity, CancellationToken cancellationToken = default)
        {
            _items[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Responsibility entity, CancellationToken cancellationToken = default)
        {
            _items[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _items.Remove(id);
            return Task.CompletedTask;
        }
    }

    internal sealed class InMemoryTeamMembershipRepository : ITeamMembershipRepository
    {
        private readonly List<TeamMembership> _items = new();

        public Task AddAsync(TeamMembership membership, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(m => m.PersonId == membership.PersonId && m.TeamId == membership.TeamId);
            _items.Add(membership);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid personId, Guid teamId, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(m => m.PersonId == personId && m.TeamId == teamId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TeamMembership>> GetByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TeamMembership>>(_items.Where(m => m.PersonId == personId).ToList());

        public Task<IReadOnlyList<TeamMembership>> GetByTeamIdAsync(Guid teamId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TeamMembership>>(_items.Where(m => m.TeamId == teamId).ToList());

        public Task<bool> ExistsAsync(Guid personId, Guid teamId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Any(m => m.PersonId == personId && m.TeamId == teamId));
    }

    internal sealed class InMemoryRoleAssignmentRepository : IRoleAssignmentRepository
    {
        private readonly List<RoleAssignment> _items = new();

        public Task AssignAsync(RoleAssignment assignment, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(a => a.PersonId == assignment.PersonId && a.RoleId == assignment.RoleId);
            _items.Add(assignment);
            return Task.CompletedTask;
        }

        public Task RevokeAsync(Guid personId, Guid roleId, DateTime revokedAt, CancellationToken cancellationToken = default)
        {
            var existing = _items.FirstOrDefault(a => a.PersonId == personId && a.RoleId == roleId && a.RevokedAt == null);
            existing?.Revoke(revokedAt);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RoleAssignment>> GetByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RoleAssignment>>(_items.Where(a => a.PersonId == personId).ToList());

        public Task<IReadOnlyList<RoleAssignment>> GetActiveByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RoleAssignment>>(_items.Where(a => a.PersonId == personId && a.IsActive).ToList());

        public Task<IReadOnlyList<RoleAssignment>> GetByRoleIdAsync(Guid roleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RoleAssignment>>(_items.Where(a => a.RoleId == roleId && a.IsActive).ToList());

        public Task<RoleAssignment?> GetAsync(Guid personId, Guid roleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(a => a.PersonId == personId && a.RoleId == roleId));
    }

    internal sealed class InMemoryResponsibilityAssignmentRepository : IResponsibilityAssignmentRepository
    {
        private readonly List<ResponsibilityAssignment> _items = new();

        public Task AssignAsync(ResponsibilityAssignment assignment, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(a => a.PersonId == assignment.PersonId && a.ResponsibilityId == assignment.ResponsibilityId);
            _items.Add(assignment);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid personId, Guid responsibilityId, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(a => a.PersonId == personId && a.ResponsibilityId == responsibilityId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ResponsibilityAssignment>> GetByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResponsibilityAssignment>>(_items.Where(a => a.PersonId == personId).ToList());

        public Task<IReadOnlyList<ResponsibilityAssignment>> GetByResponsibilityIdAsync(Guid responsibilityId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResponsibilityAssignment>>(_items.Where(a => a.ResponsibilityId == responsibilityId).ToList());

        public Task<bool> ExistsAsync(Guid personId, Guid responsibilityId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Any(a => a.PersonId == personId && a.ResponsibilityId == responsibilityId));
    }
