using Cakra.Core;
using Cakra.Modules.Organization.Commands;
using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Domain.Events;
using Cakra.Modules.Organization.Persistence;
using Cakra.Modules.Organization.Services;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Organization;

public class ActivatePersonCommandHandlerTests
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

    public ActivatePersonCommandHandlerTests()
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
    public async Task ActivatePersonCommand_activates_inactive_person_and_dispatches_PersonActivated_event()
    {
        var person = await _service.CreatePersonAsync("Hadi", "Pranoto", "hadi@cakra.id");
        await _service.DeactivatePersonAsync(person.Id);
        _eventDispatcher.DispatchedEvents.Clear();
        _clock.UtcNow = _clock.UtcNow.AddMinutes(30);

        var handler = new ActivatePersonCommandHandler(_service);
        var activated = await handler.Handle(
            new ActivatePersonCommand(person.Id),
            CancellationToken.None);

        activated.Status.Should().Be(Person.StatusActive);
        activated.IsActive.Should().BeTrue();
        activated.UpdatedAt.Should().NotBeNull();

        var persisted = await _personRepository.GetByIdAsync(person.Id);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(Person.StatusActive);
        persisted.IsActive.Should().BeTrue();

        _eventDispatcher.DispatchedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PersonActivated>()
            .Which.PersonId.Should().Be(person.Id);
    }

    [Fact]
    public void ActivatePersonCommand_rejects_empty_PersonId()
    {
        var validator = new ActivatePersonCommandValidator();

        var invalidResult = validator.Validate(new ActivatePersonCommand(Guid.Empty));
        invalidResult.IsValid.Should().BeFalse();
        invalidResult.Errors.Should().Contain(e => e.PropertyName == nameof(ActivatePersonCommand.PersonId));

        var validResult = validator.Validate(new ActivatePersonCommand(Guid.NewGuid()));
        validResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ActivatePersonCommandHandler_throws_when_person_not_found()
    {
        var handler = new ActivatePersonCommandHandler(_service);
        var nonExistentId = Guid.NewGuid();

        var act = async () => await handler.Handle(
            new ActivatePersonCommand(nonExistentId),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{nonExistentId}*");
    }

    [Fact]
    public void ActivatePersonCommandHandler_throws_when_service_is_null()
    {
        Action act = () => new ActivatePersonCommandHandler(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("organizationService");
    }

    [Fact]
    public async Task ActivatePersonCommandHandler_throws_when_request_is_null()
    {
        var handler = new ActivatePersonCommandHandler(_service);

        var act = async () => await handler.Handle(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
