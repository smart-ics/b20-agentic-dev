using Cakra.Core;
using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Cakra.Modules.Product;
using Cakra.Modules.Request;
using Cakra.Modules.WorkPackage.Domain;
using Cakra.Modules.WorkPackage.Domain.Events;
using Cakra.Modules.WorkPackage.Domain.Exceptions;
using Cakra.Modules.WorkPackage.Persistence;
using Cakra.Modules.WorkPackage.Services;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.WorkPackage;

public sealed class WorkPackageServiceTests
{
    private readonly InMemoryWorkPackageRepository _repository = new();
    private readonly FakeOrganizationQueryService _organizationQuery = new();
    private readonly FakeCustomerQueryService _customerQuery = new();
    private readonly FakeProductQueryService _productQuery = new();
    private readonly FakeRequestQueryService _requestQuery = new();
    private readonly RecordingEventDispatcher _eventDispatcher = new();
    private readonly FakeSystemClock _clock = new();
    private readonly FakeCurrentContextProvider _currentContextProvider = new();
    private readonly WorkPackageService _service;

    private readonly Guid _activeOwnerId = Guid.NewGuid();
    private readonly Guid _secondActiveOwnerId = Guid.NewGuid();
    private readonly Guid _inactiveOwnerId = Guid.NewGuid();
    private readonly Guid _activeCustomerId = Guid.NewGuid();
    private readonly Guid _inactiveCustomerId = Guid.NewGuid();
    private readonly Guid _activeProductId = Guid.NewGuid();
    private readonly Guid _inactiveProductId = Guid.NewGuid();
    private readonly Guid _existingRequestId = Guid.NewGuid();

    public WorkPackageServiceTests()
    {
        _organizationQuery.AddPerson(_activeOwnerId, "Budi", "Santoso", isActive: true);
        _organizationQuery.AddPerson(_secondActiveOwnerId, "Siti", "Rahma", isActive: true);
        _organizationQuery.AddPerson(_inactiveOwnerId, "Former", "Employee", isActive: false);

        _customerQuery.AddCustomer(_activeCustomerId, "CUST-001", "RSUD Soetomo", isActive: true);
        _customerQuery.AddCustomer(_inactiveCustomerId, "CUST-002", "Inactive Hospital", isActive: false);

        _productQuery.AddProduct(_activeProductId, "PRD-001", "MyHospital", _activeOwnerId, isActive: true);
        _productQuery.AddProduct(_inactiveProductId, "PRD-002", "LegacyHIS", _activeOwnerId, isActive: false);

        _requestQuery.AddRequest(_existingRequestId, "REQ-1: Billing calculation issue", "IN_PROGRESS", _activeOwnerId);

        _service = new WorkPackageService(
            _repository,
            _organizationQuery,
            _customerQuery,
            _productQuery,
            _requestQuery,
            _eventDispatcher,
            currentContextProvider: _currentContextProvider,
            clock: _clock);
    }

    [Fact]
    public async Task CreateWorkPackage_WithValidReferences_CreatesDraftPackageAndDispatchesEvent()
    {
        var dto = await _service.CreateWorkPackageAsync(
            "RSUD Go-Live Package",
            "Complete all critical pre-launch fixes",
            _activeOwnerId,
            _activeCustomerId,
            _activeProductId);

        dto.Id.Should().NotBeEmpty();
        dto.Name.Should().Be("RSUD Go-Live Package");
        dto.Objective.Should().Be("Complete all critical pre-launch fixes");
        dto.Status.Should().Be(WorkPackageStatusNames.Draft);
        dto.OwnerPersonId.Should().Be(_activeOwnerId);
        dto.CustomerId.Should().Be(_activeCustomerId);
        dto.ProductId.Should().Be(_activeProductId);
        dto.CreatedAt.Should().Be(_clock.UtcNow);

        var persisted = await _repository.GetByIdAsync(dto.Id);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(WorkPackageStatus.Draft);

        _eventDispatcher.DispatchedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkPackageCreated>()
            .Which.WorkPackageId.Should().Be(dto.Id);
    }

    [Fact]
    public async Task CreateWorkPackage_WithNonexistentOwner_ThrowsKeyNotFoundException()
    {
        var act = async () => await _service.CreateWorkPackageAsync(
            "Package",
            "Objective",
            Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Owner person*");
    }

    [Fact]
    public async Task CreateWorkPackage_WithInactiveOwner_ThrowsInvalidOperationException()
    {
        var act = async () => await _service.CreateWorkPackageAsync(
            "Package",
            "Objective",
            _inactiveOwnerId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not active*");
    }

    [Fact]
    public async Task CreateWorkPackage_WithNonexistentCustomer_ThrowsKeyNotFoundException()
    {
        var act = async () => await _service.CreateWorkPackageAsync(
            "Package",
            "Objective",
            _activeOwnerId,
            customerId: Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Customer*");
    }

    [Fact]
    public async Task CreateWorkPackage_WithInactiveCustomer_ThrowsInvalidOperationException()
    {
        var act = async () => await _service.CreateWorkPackageAsync(
            "Package",
            "Objective",
            _activeOwnerId,
            customerId: _inactiveCustomerId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Customer*not active*");
    }

    [Fact]
    public async Task CreateWorkPackage_WithNonexistentProduct_ThrowsKeyNotFoundException()
    {
        var act = async () => await _service.CreateWorkPackageAsync(
            "Package",
            "Objective",
            _activeOwnerId,
            productId: Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Product*");
    }

    [Fact]
    public async Task CreateWorkPackage_WithInactiveProduct_ThrowsInvalidOperationException()
    {
        var act = async () => await _service.CreateWorkPackageAsync(
            "Package",
            "Objective",
            _activeOwnerId,
            productId: _inactiveProductId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Product*not active*");
    }

    [Fact]
    public async Task UpdateObjective_UpdatesNameAndObjectiveInRepository()
    {
        var created = await _service.CreateWorkPackageAsync(
            "Initial Name",
            "Initial Objective",
            _activeOwnerId);

        _clock.Advance(TimeSpan.FromMinutes(5));

        var updated = await _service.UpdateObjectiveAsync(
            created.Id,
            "Revised Name",
            "Revised Objective");

        updated.Name.Should().Be("Revised Name");
        updated.Objective.Should().Be("Revised Objective");
        updated.UpdatedAt.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public async Task AssignOwner_WithValidNewOwner_UpdatesOwnerAndDispatchesEvent()
    {
        var created = await _service.CreateWorkPackageAsync(
            "Package Name",
            "Package Objective",
            _activeOwnerId);
        _eventDispatcher.Clear();

        _clock.Advance(TimeSpan.FromMinutes(5));

        var updated = await _service.AssignOwnerAsync(created.Id, _secondActiveOwnerId);

        updated.OwnerPersonId.Should().Be(_secondActiveOwnerId);
        _eventDispatcher.DispatchedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkPackageOwnerChanged>()
            .Which.Should().BeEquivalentTo(new
            {
                WorkPackageId = created.Id,
                PreviousOwnerPersonId = _activeOwnerId,
                NewOwnerPersonId = _secondActiveOwnerId
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public async Task AddRequestToWorkPackage_ValidatesRequestExistsAndEnforcesBusinessRule9()
    {
        var wp1 = await _service.CreateWorkPackageAsync("WP 1", "Objective 1", _activeOwnerId);
        var wp2 = await _service.CreateWorkPackageAsync("WP 2", "Objective 2", _activeOwnerId);
        _eventDispatcher.Clear();

        // Non-existent request should fail with KeyNotFoundException
        var nonExistentAct = async () => await _service.AddRequestToWorkPackageAsync(wp1.Id, Guid.NewGuid());
        await nonExistentAct.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Request*was not found*");

        // Adding existing request to WP 1 succeeds
        var updatedWp1 = await _service.AddRequestToWorkPackageAsync(wp1.Id, _existingRequestId);
        updatedWp1.ActiveRequests.Should().ContainSingle().Which.RequestId.Should().Be(_existingRequestId);
        _eventDispatcher.DispatchedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RequestAddedToWorkPackage>();

        // Business Rule 9: Adding the same request to WP 2 while active in WP 1 fails
        var duplicateAcrossPackagesAct = async () =>
            await _service.AddRequestToWorkPackageAsync(wp2.Id, _existingRequestId);
        var ruleEx = (await duplicateAcrossPackagesAct.Should().ThrowAsync<BusinessRuleViolationException>()).Which;
        ruleEx.RuleNumber.Should().Be(9);

        // Removing from WP 1 allows adding to WP 2
        _clock.Advance(TimeSpan.FromMinutes(2));
        var afterRemoveWp1 = await _service.RemoveRequestFromWorkPackageAsync(wp1.Id, _existingRequestId);
        afterRemoveWp1.ActiveRequests.Should().BeEmpty();
        afterRemoveWp1.Requests.Should().ContainSingle().Which.IsActive.Should().BeFalse();

        var updatedWp2 = await _service.AddRequestToWorkPackageAsync(wp2.Id, _existingRequestId);
        updatedWp2.ActiveRequests.Should().ContainSingle().Which.RequestId.Should().Be(_existingRequestId);
    }

    [Fact]
    public async Task ActivateAndCloseWorkPackage_TransitionsLifecycleAndDispatchesEvents()
    {
        var created = await _service.CreateWorkPackageAsync("WP", "Objective", _activeOwnerId);
        await _service.AddRequestToWorkPackageAsync(created.Id, _existingRequestId);
        _eventDispatcher.Clear();

        _clock.Advance(TimeSpan.FromMinutes(10));
        var activated = await _service.ActivateWorkPackageAsync(created.Id);
        activated.Status.Should().Be(WorkPackageStatusNames.Active);
        _eventDispatcher.DispatchedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkPackageActivated>();

        _eventDispatcher.Clear();
        _clock.Advance(TimeSpan.FromMinutes(20));
        var closed = await _service.CloseWorkPackageAsync(created.Id, "All go-live objectives verified");
        closed.Status.Should().Be(WorkPackageStatusNames.Closed);
        closed.ClosedReason.Should().Be("All go-live objectives verified");
        closed.ClosedAt.Should().Be(_clock.UtcNow);
        _eventDispatcher.DispatchedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkPackageClosed>();

        // Constituent request in RequestQueryService remains unaltered in IN_PROGRESS state
        var requestAfterClose = await _requestQuery.GetRequestByIdAsync(_existingRequestId);
        requestAfterClose.Should().NotBeNull();
        requestAfterClose!.Status.Should().Be("IN_PROGRESS");
        requestAfterClose.OwnerPersonId.Should().Be(_activeOwnerId);
    }

    [Fact]
    public async Task ReorderRequests_WithValidOrder_ReordersMembershipsAndPersists()
    {
        var secondRequestId = Guid.NewGuid();
        _requestQuery.AddRequest(secondRequestId, "REQ-2: UI adjustment", "IN_PROGRESS", _activeOwnerId);

        var wp = await _service.CreateWorkPackageAsync("WP", "Objective", _activeOwnerId);
        await _service.AddRequestToWorkPackageAsync(wp.Id, _existingRequestId);
        await _service.AddRequestToWorkPackageAsync(wp.Id, secondRequestId);

        var initialPackage = await _repository.GetByIdAsync(wp.Id);
        initialPackage!.ActiveRequests.Select(r => r.RequestId)
            .Should().ContainInOrder(_existingRequestId, secondRequestId);

        _clock.Advance(TimeSpan.FromMinutes(5));

        // Act - Reorder so secondRequestId comes first
        var updated = await _service.ReorderRequestsAsync(wp.Id, new[] { secondRequestId, _existingRequestId });

        // Assert
        updated.ActiveRequests.Select(r => r.RequestId)
            .Should().ContainInOrder(secondRequestId, _existingRequestId);

        var persisted = await _repository.GetByIdAsync(wp.Id);
        persisted!.ActiveRequests.Select(r => r.RequestId)
            .Should().ContainInOrder(secondRequestId, _existingRequestId);
    }

    [Fact]
    public async Task ReorderRequests_WithNonexistentWorkPackage_ThrowsKeyNotFoundException()
    {
        var act = async () => await _service.ReorderRequestsAsync(Guid.NewGuid(), new[] { Guid.NewGuid() });
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CreateWorkPackage_WithDeadline_SetsNormalizedDeadline()
    {
        var rawDeadline = new DateTime(2026, 11, 15, 14, 30, 0, DateTimeKind.Local);
        var dto = await _service.CreateWorkPackageAsync(
            "Package with Deadline",
            "Objective with target deadline",
            _activeOwnerId,
            deadline: rawDeadline);

        dto.Deadline.Should().Be(new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc));

        var persisted = await _repository.GetByIdAsync(dto.Id);
        persisted.Should().NotBeNull();
        persisted!.Deadline.Should().Be(new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task CreateWorkPackage_ViaMediatRCommand_WithDeadline_SetsDeadline()
    {
        var deadline = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var command = new CreateWorkPackageCommand(
            "MediatR Package",
            "Objective",
            _activeOwnerId,
            Deadline: deadline);

        var dto = await _service.Handle(command, CancellationToken.None);
        dto.Deadline.Should().Be(deadline);
    }

    [Fact]
    public async Task UpdateDeadline_WithValidDate_UpdatesDeadlineAndTimestamp()
    {
        var dto = await _service.CreateWorkPackageAsync(
            "Package to update deadline",
            "Objective",
            _activeOwnerId);

        dto.Deadline.Should().BeNull();

        _clock.Advance(TimeSpan.FromHours(2));
        var newDeadline = new DateTime(2026, 12, 25, 0, 0, 0, DateTimeKind.Utc);

        var updated = await _service.UpdateDeadlineAsync(dto.Id, newDeadline);
        updated.Deadline.Should().Be(newDeadline);
        updated.UpdatedAt.Should().Be(_clock.UtcNow);

        var persisted = await _repository.GetByIdAsync(dto.Id);
        persisted!.Deadline.Should().Be(newDeadline);
        persisted.UpdatedAt.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public async Task UpdateDeadline_ViaMediatRCommand_UpdatesDeadline()
    {
        var dto = await _service.CreateWorkPackageAsync(
            "MediatR update deadline",
            "Objective",
            _activeOwnerId);

        var deadline = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var command = new UpdateWorkPackageDeadlineCommand(dto.Id, deadline);

        var updated = await _service.Handle(command, CancellationToken.None);
        updated.Deadline.Should().Be(deadline);
    }

    [Fact]
    public async Task UpdateDeadline_ClearingDeadline_SetsNull()
    {
        var initialDeadline = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
        var dto = await _service.CreateWorkPackageAsync(
            "Package to clear deadline",
            "Objective",
            _activeOwnerId,
            deadline: initialDeadline);

        dto.Deadline.Should().Be(initialDeadline);

        var updated = await _service.UpdateDeadlineAsync(dto.Id, null);
        updated.Deadline.Should().BeNull();

        var persisted = await _repository.GetByIdAsync(dto.Id);
        persisted!.Deadline.Should().BeNull();
    }

    [Fact]
    public async Task UpdateDeadline_OnClosedPackage_ThrowsWorkPackageDomainException()
    {
        var dto = await _service.CreateWorkPackageAsync(
            "Package to close",
            "Objective",
            _activeOwnerId);

        await _service.CloseWorkPackageAsync(dto.Id, "Finished");

        var act = async () => await _service.UpdateDeadlineAsync(
            dto.Id,
            new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));

        await act.Should().ThrowAsync<WorkPackageDomainException>()
            .WithMessage("*Cannot update deadline for closed Work Package*");
    }

    [Fact]
    public async Task UpdateDeadline_WithEmptyId_ThrowsArgumentException()
    {
        var act = async () => await _service.UpdateDeadlineAsync(
            Guid.Empty,
            new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*WorkPackageId cannot be empty*");
    }

    [Fact]
    public async Task UpdateContext_WithValidReferences_UpdatesPackageAndDispatchesEvent()
    {
        var dto = await _service.CreateWorkPackageAsync(
            "Context Test Package",
            "Objective",
            _activeOwnerId);

        _eventDispatcher.Clear();
        _clock.Advance(TimeSpan.FromHours(1));

        var updated = await _service.UpdateContextAsync(dto.Id, _activeCustomerId, _activeProductId);

        updated.CustomerId.Should().Be(_activeCustomerId);
        updated.ProductId.Should().Be(_activeProductId);
        updated.UpdatedAt.Should().Be(_clock.UtcNow);

        var persisted = await _repository.GetByIdAsync(dto.Id);
        persisted.Should().NotBeNull();
        persisted!.CustomerId.Should().Be(_activeCustomerId);
        persisted.ProductId.Should().Be(_activeProductId);

        _eventDispatcher.DispatchedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkPackageContextChanged>()
            .Which.Should().Match<WorkPackageContextChanged>(e =>
                e.WorkPackageId == dto.Id &&
                e.PreviousCustomerId == null &&
                e.NewCustomerId == _activeCustomerId &&
                e.PreviousProductId == null &&
                e.NewProductId == _activeProductId);
    }

    [Fact]
    public async Task UpdateContext_ClearingReferences_SetsNullAndDispatchesEvent()
    {
        var dto = await _service.CreateWorkPackageAsync(
            "Context Package to Clear",
            "Objective",
            _activeOwnerId,
            customerId: _activeCustomerId,
            productId: _activeProductId);

        _eventDispatcher.Clear();
        _clock.Advance(TimeSpan.FromHours(2));

        var updated = await _service.UpdateContextAsync(dto.Id, null, null);

        updated.CustomerId.Should().BeNull();
        updated.ProductId.Should().BeNull();
        updated.UpdatedAt.Should().Be(_clock.UtcNow);

        var persisted = await _repository.GetByIdAsync(dto.Id);
        persisted.Should().NotBeNull();
        persisted!.CustomerId.Should().BeNull();
        persisted.ProductId.Should().BeNull();

        _eventDispatcher.DispatchedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkPackageContextChanged>()
            .Which.Should().Match<WorkPackageContextChanged>(e =>
                e.WorkPackageId == dto.Id &&
                e.PreviousCustomerId == _activeCustomerId &&
                e.NewCustomerId == null &&
                e.PreviousProductId == _activeProductId &&
                e.NewProductId == null);
    }

    [Fact]
    public async Task UpdateContext_WithInactiveCustomer_ThrowsInvalidOperationException()
    {
        var dto = await _service.CreateWorkPackageAsync(
            "Package",
            "Objective",
            _activeOwnerId);

        var act = async () => await _service.UpdateContextAsync(dto.Id, _inactiveCustomerId, null);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Customer*not active*");
    }

    [Fact]
    public async Task UpdateContext_WithNonexistentCustomer_ThrowsKeyNotFoundException()
    {
        var dto = await _service.CreateWorkPackageAsync(
            "Package",
            "Objective",
            _activeOwnerId);

        var act = async () => await _service.UpdateContextAsync(dto.Id, Guid.NewGuid(), null);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Customer*");
    }

    [Fact]
    public async Task UpdateContext_WithInactiveProduct_ThrowsInvalidOperationException()
    {
        var dto = await _service.CreateWorkPackageAsync(
            "Package",
            "Objective",
            _activeOwnerId);

        var act = async () => await _service.UpdateContextAsync(dto.Id, null, _inactiveProductId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Product*not active*");
    }

    [Fact]
    public async Task UpdateContext_WithNonexistentProduct_ThrowsKeyNotFoundException()
    {
        var dto = await _service.CreateWorkPackageAsync(
            "Package",
            "Objective",
            _activeOwnerId);

        var act = async () => await _service.UpdateContextAsync(dto.Id, null, Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Product*");
    }

    [Fact]
    public async Task UpdateContext_WithNonexistentWorkPackage_ThrowsKeyNotFoundException()
    {
        var act = async () => await _service.UpdateContextAsync(Guid.NewGuid(), _activeCustomerId, _activeProductId);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*WorkPackage*");
    }

    [Fact]
    public async Task UpdateContext_OnClosedPackage_ThrowsWorkPackageDomainException()
    {
        var dto = await _service.CreateWorkPackageAsync(
            "Package to close",
            "Objective",
            _activeOwnerId);

        await _service.CloseWorkPackageAsync(dto.Id, "Finished");

        var act = async () => await _service.UpdateContextAsync(dto.Id, _activeCustomerId, null);

        await act.Should().ThrowAsync<WorkPackageDomainException>()
            .WithMessage("*Cannot update context for closed Work Package*");
    }

    [Fact]
    public async Task UpdateContext_WithEmptyId_ThrowsArgumentException()
    {
        var act = async () => await _service.UpdateContextAsync(Guid.Empty, _activeCustomerId, null);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*WorkPackageId cannot be empty*");
    }

    [Fact]
    public async Task Handle_UpdateWorkPackageContextCommand_ExecutesSuccessfully()
    {
        var dto = await _service.CreateWorkPackageAsync(
            "Command test package",
            "Objective",
            _activeOwnerId);

        var command = new UpdateWorkPackageContextCommand(dto.Id, _activeCustomerId, _activeProductId);
        var updated = await _service.Handle(command, CancellationToken.None);

        updated.CustomerId.Should().Be(_activeCustomerId);
        updated.ProductId.Should().Be(_activeProductId);
    }

    [Fact]
    public void CommandValidators_ValidateRequiredFields()
    {
        new CreateWorkPackageCommandValidator()
            .Validate(new CreateWorkPackageCommand("", "", Guid.Empty))
            .IsValid.Should().BeFalse();

        new UpdateObjectiveCommandValidator()
            .Validate(new UpdateObjectiveCommand(Guid.Empty, "", ""))
            .IsValid.Should().BeFalse();

        new AssignOwnerCommandValidator()
            .Validate(new AssignOwnerCommand(Guid.Empty, Guid.Empty))
            .IsValid.Should().BeFalse();

        new AddRequestToWorkPackageCommandValidator()
            .Validate(new AddRequestToWorkPackageCommand(Guid.Empty, Guid.Empty))
            .IsValid.Should().BeFalse();

        new RemoveRequestFromWorkPackageCommandValidator()
            .Validate(new RemoveRequestFromWorkPackageCommand(Guid.Empty, Guid.Empty))
            .IsValid.Should().BeFalse();

        new ActivateWorkPackageCommandValidator()
            .Validate(new ActivateWorkPackageCommand(Guid.Empty))
            .IsValid.Should().BeFalse();

        new CloseWorkPackageCommandValidator()
            .Validate(new CloseWorkPackageCommand(Guid.Empty, ""))
            .IsValid.Should().BeFalse();

        new ReorderWorkPackageRequestsCommandValidator()
            .Validate(new ReorderWorkPackageRequestsCommand(Guid.Empty, Array.Empty<Guid>()))
            .IsValid.Should().BeFalse();

        new ReorderWorkPackageRequestsCommandValidator()
            .Validate(new ReorderWorkPackageRequestsCommand(Guid.NewGuid(), Array.Empty<Guid>()))
            .IsValid.Should().BeFalse();

        new UpdateWorkPackageDeadlineCommandValidator()
            .Validate(new UpdateWorkPackageDeadlineCommand(Guid.Empty, DateTime.UtcNow))
            .IsValid.Should().BeFalse();

        new UpdateWorkPackageDeadlineCommandValidator()
            .Validate(new UpdateWorkPackageDeadlineCommand(Guid.NewGuid(), null))
            .IsValid.Should().BeTrue();

        new UpdateWorkPackageContextCommandValidator()
            .Validate(new UpdateWorkPackageContextCommand(Guid.Empty, null, null))
            .IsValid.Should().BeFalse();

        new UpdateWorkPackageContextCommandValidator()
            .Validate(new UpdateWorkPackageContextCommand(Guid.NewGuid(), null, null))
            .IsValid.Should().BeTrue();

        new ReopenWorkPackageCommandValidator()
            .Validate(new ReopenWorkPackageCommand(Guid.Empty, "Reason"))
            .IsValid.Should().BeFalse();

        new ReopenWorkPackageCommandValidator()
            .Validate(new ReopenWorkPackageCommand(Guid.NewGuid(), ""))
            .IsValid.Should().BeFalse();

        new ReopenWorkPackageCommandValidator()
            .Validate(new ReopenWorkPackageCommand(Guid.NewGuid(), new string('x', 1001)))
            .IsValid.Should().BeFalse();

        new ReopenWorkPackageCommandValidator()
            .Validate(new ReopenWorkPackageCommand(Guid.NewGuid(), "Valid reason"))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ReopenWorkPackage_WhenClosedWithUnfinishedRequests_ReopensToDraftAndDispatchesEvent()
    {
        // Arrange
        var created = await _service.CreateWorkPackageAsync("WP to Reopen", "Objective", _activeOwnerId);
        await _service.AddRequestToWorkPackageAsync(created.Id, _existingRequestId);
        await _service.ActivateWorkPackageAsync(created.Id);

        _clock.Advance(TimeSpan.FromMinutes(10));
        var closedAt = _clock.UtcNow;
        await _service.CloseWorkPackageAsync(created.Id, "Initial closure reason");

        _eventDispatcher.Clear();
        _clock.Advance(TimeSpan.FromHours(1));

        var newDeadline = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var reopened = await _service.ReopenWorkPackageAsync(
            created.Id,
            "Operational needs required reopening",
            newDeadline: newDeadline);

        // Assert DTO
        reopened.Id.Should().Be(created.Id);
        reopened.Status.Should().Be(WorkPackageStatusNames.Draft);
        reopened.ClosedReason.Should().BeNull();
        reopened.ClosedAt.Should().BeNull();
        reopened.Deadline.Should().Be(newDeadline);
        reopened.UpdatedAt.Should().Be(_clock.UtcNow);

        // Assert Repository
        var persisted = await _repository.GetByIdAsync(created.Id);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(WorkPackageStatus.Draft);
        persisted.ClosedReason.Should().BeNull();
        persisted.ClosedAt.Should().BeNull();
        persisted.Deadline.Should().Be(newDeadline);
        persisted.UpdatedAt.Should().Be(_clock.UtcNow);

        // Assert Event
        _eventDispatcher.DispatchedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkPackageReopened>()
            .Which.Should().Match<WorkPackageReopened>(e =>
                e.WorkPackageId == created.Id &&
                e.Reason == "Operational needs required reopening" &&
                e.PreviousClosedReason == "Initial closure reason" &&
                e.PreviousClosedAt == closedAt &&
                e.ReopenedAtUtc == _clock.UtcNow);
    }

    [Fact]
    public async Task ReopenWorkPackage_PreservesExistingDeadlineWhenNewDeadlineIsNull()
    {
        var initialDeadline = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
        var created = await _service.CreateWorkPackageAsync("WP with Deadline", "Objective", _activeOwnerId, deadline: initialDeadline);
        await _service.AddRequestToWorkPackageAsync(created.Id, _existingRequestId);
        await _service.CloseWorkPackageAsync(created.Id, "Closed for now");

        _clock.Advance(TimeSpan.FromDays(1));

        var reopened = await _service.ReopenWorkPackageAsync(created.Id, "Reopening without deadline change", newDeadline: null);

        reopened.Deadline.Should().Be(initialDeadline);

        var persisted = await _repository.GetByIdAsync(created.Id);
        persisted!.Deadline.Should().Be(initialDeadline);
    }

    [Fact]
    public async Task ReopenWorkPackage_ViaMediatRCommand_ExecutesSuccessfully()
    {
        var created = await _service.CreateWorkPackageAsync("WP MediatR", "Objective", _activeOwnerId);
        await _service.AddRequestToWorkPackageAsync(created.Id, _existingRequestId);
        await _service.CloseWorkPackageAsync(created.Id, "Finished");

        var command = new ReopenWorkPackageCommand(created.Id, "Reopening via MediatR");
        var result = await _service.Handle(command, CancellationToken.None);

        result.Status.Should().Be(WorkPackageStatusNames.Draft);
        result.ClosedReason.Should().BeNull();
        result.ClosedAt.Should().BeNull();
    }

    [Fact]
    public async Task ReopenWorkPackage_WhenCallerIsNonOwnerWithoutLeadershipRoles_ThrowsUnauthorizedAccessException()
    {
        var created = await _service.CreateWorkPackageAsync("WP Auth", "Objective", _activeOwnerId);
        await _service.AddRequestToWorkPackageAsync(created.Id, _existingRequestId);
        await _service.CloseWorkPackageAsync(created.Id, "Finished");

        // Caller is a different person and has no leadership roles
        var nonOwnerId = Guid.NewGuid();
        _currentContextProvider.CurrentPersonId = nonOwnerId;
        _currentContextProvider.Roles.Clear();

        var act = async () => await _service.ReopenWorkPackageAsync(created.Id, "Unauthorized reopen");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage($"*Actor '{nonOwnerId}' is not authorized to re-open Work Package '{created.Id}'*");
    }

    [Fact]
    public async Task ReopenWorkPackage_WhenCallerIsAdministrator_Succeeds()
    {
        var created = await _service.CreateWorkPackageAsync("WP Admin Auth", "Objective", _activeOwnerId);
        await _service.AddRequestToWorkPackageAsync(created.Id, _existingRequestId);
        await _service.CloseWorkPackageAsync(created.Id, "Finished");

        _currentContextProvider.CurrentPersonId = Guid.NewGuid();
        _currentContextProvider.Roles.Add("Administrator");

        var reopened = await _service.ReopenWorkPackageAsync(created.Id, "Admin reopen");
        reopened.Status.Should().Be(WorkPackageStatusNames.Draft);
    }

    [Fact]
    public async Task ReopenWorkPackage_WhenCallerIsManagement_Succeeds()
    {
        var created = await _service.CreateWorkPackageAsync("WP Mgmt Auth", "Objective", _activeOwnerId);
        await _service.AddRequestToWorkPackageAsync(created.Id, _existingRequestId);
        await _service.CloseWorkPackageAsync(created.Id, "Finished");

        _currentContextProvider.CurrentPersonId = Guid.NewGuid();
        _currentContextProvider.Roles.Add("Management");

        var reopened = await _service.ReopenWorkPackageAsync(created.Id, "Management reopen");
        reopened.Status.Should().Be(WorkPackageStatusNames.Draft);
    }

    [Fact]
    public async Task ReopenWorkPackage_WhenCallerIsOwner_Succeeds()
    {
        var created = await _service.CreateWorkPackageAsync("WP Owner Auth", "Objective", _activeOwnerId);
        await _service.AddRequestToWorkPackageAsync(created.Id, _existingRequestId);
        await _service.CloseWorkPackageAsync(created.Id, "Finished");

        _currentContextProvider.CurrentPersonId = _activeOwnerId;
        _currentContextProvider.Roles.Clear();

        var reopened = await _service.ReopenWorkPackageAsync(created.Id, "Owner reopen");
        reopened.Status.Should().Be(WorkPackageStatusNames.Draft);
    }

    [Fact]
    public async Task ReopenWorkPackage_WhenNoRequestsInPackage_ThrowsWorkPackageDomainException()
    {
        var created = await _service.CreateWorkPackageAsync("WP Empty", "Objective", _activeOwnerId);
        await _service.CloseWorkPackageAsync(created.Id, "Closed empty package");

        var act = async () => await _service.ReopenWorkPackageAsync(created.Id, "Reopening empty package");

        await act.Should().ThrowAsync<WorkPackageDomainException>()
            .WithMessage($"*Work Package '{created.Id}' has no constituent requests and cannot be re-opened. Closure is permanent.*");
    }

    [Fact]
    public async Task ReopenWorkPackage_WhenAllRequestsAreCompletedOrCancelledOrRejected_ThrowsWorkPackageDomainException()
    {
        var req1 = Guid.NewGuid();
        var req2 = Guid.NewGuid();
        var req3 = Guid.NewGuid();
        _requestQuery.AddRequest(req1, "Completed Request", "COMPLETED", _activeOwnerId);
        _requestQuery.AddRequest(req2, "Cancelled Request", "CANCELLED", _activeOwnerId);
        _requestQuery.AddRequest(req3, "Rejected Request", "REJECTED", _activeOwnerId);

        var created = await _service.CreateWorkPackageAsync("WP Finished Requests", "Objective", _activeOwnerId);
        await _service.AddRequestToWorkPackageAsync(created.Id, req1);
        await _service.AddRequestToWorkPackageAsync(created.Id, req2);
        await _service.AddRequestToWorkPackageAsync(created.Id, req3);
        await _service.CloseWorkPackageAsync(created.Id, "All done");

        var act = async () => await _service.ReopenWorkPackageAsync(created.Id, "Attempting to reopen permanently sealed");

        await act.Should().ThrowAsync<WorkPackageDomainException>()
            .WithMessage($"*Work Package '{created.Id}' contains no unfinished requests. All constituent requests are completed or cancelled. Closure is permanent.*");
    }

    [Fact]
    public async Task ReopenWorkPackage_WhenAtLeastOneRequestIsUnfinished_PassesGate2()
    {
        var reqDone = Guid.NewGuid();
        var reqUnfinished = Guid.NewGuid();
        _requestQuery.AddRequest(reqDone, "Completed Request", "COMPLETED", _activeOwnerId);
        _requestQuery.AddRequest(reqUnfinished, "Unfinished Request", "IN_PROGRESS", _activeOwnerId);

        var created = await _service.CreateWorkPackageAsync("WP Mixed Requests", "Objective", _activeOwnerId);
        await _service.AddRequestToWorkPackageAsync(created.Id, reqDone);
        await _service.AddRequestToWorkPackageAsync(created.Id, reqUnfinished);
        await _service.CloseWorkPackageAsync(created.Id, "Closed prematurely");

        var reopened = await _service.ReopenWorkPackageAsync(created.Id, "Reopening because one request remains unfinished");

        reopened.Status.Should().Be(WorkPackageStatusNames.Draft);
    }

    [Fact]
    public async Task ReopenWorkPackage_WhenUnfinishedRequestBelongsToAnotherActivePackage_ThrowsInvalidOperationException()
    {
        var wp1 = await _service.CreateWorkPackageAsync("WP 1", "Objective 1", _activeOwnerId);
        await _service.AddRequestToWorkPackageAsync(wp1.Id, _existingRequestId);
        await _service.CloseWorkPackageAsync(wp1.Id, "Closed WP 1");

        // Request is now claimed by another package WP 2
        var wp2 = await _service.CreateWorkPackageAsync("WP 2", "Objective 2", _activeOwnerId);
        await _service.AddRequestToWorkPackageAsync(wp2.Id, _existingRequestId);

        // Attempting to reopen WP 1 violates Business Rule 9
        var act = async () => await _service.ReopenWorkPackageAsync(wp1.Id, "Reopen WP 1 with conflict");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Cannot re-open Work Package '{wp1.Id}': The following unfinished request(s) are currently assigned to another active or draft Work Package: 'REQ-1: Billing calculation issue' ({_existingRequestId}). Resolve the conflicts before re-opening.*");
    }

    [Fact]
    public async Task ReopenWorkPackage_WhenPackageIsDraft_ThrowsInvalidWorkPackageStateTransitionException()
    {
        var created = await _service.CreateWorkPackageAsync("WP Draft", "Objective", _activeOwnerId);

        var act = async () => await _service.ReopenWorkPackageAsync(created.Id, "Reopening draft");

        await act.Should().ThrowAsync<InvalidWorkPackageStateTransitionException>()
            .WithMessage($"*Work Package '{created.Id}' is in status 'DRAFT' and cannot be re-opened. Only CLOSED packages can be re-opened.*");
    }

    [Fact]
    public async Task ReopenWorkPackage_WhenPackageIsActive_ThrowsInvalidWorkPackageStateTransitionException()
    {
        var created = await _service.CreateWorkPackageAsync("WP Active", "Objective", _activeOwnerId);
        await _service.AddRequestToWorkPackageAsync(created.Id, _existingRequestId);
        await _service.ActivateWorkPackageAsync(created.Id);

        var act = async () => await _service.ReopenWorkPackageAsync(created.Id, "Reopening active");

        await act.Should().ThrowAsync<InvalidWorkPackageStateTransitionException>()
            .WithMessage($"*Work Package '{created.Id}' is in status 'ACTIVE' and cannot be re-opened. Only CLOSED packages can be re-opened.*");
    }

    [Fact]
    public async Task ReopenWorkPackage_WithEmptyId_ThrowsArgumentException()
    {
        var act = async () => await _service.ReopenWorkPackageAsync(Guid.Empty, "Reopen");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*WorkPackageId cannot be empty*");
    }

    [Fact]
    public async Task ReopenWorkPackage_WithEmptyReason_ThrowsArgumentException()
    {
        var created = await _service.CreateWorkPackageAsync("WP Reason Test", "Objective", _activeOwnerId);
        await _service.AddRequestToWorkPackageAsync(created.Id, _existingRequestId);
        await _service.CloseWorkPackageAsync(created.Id, "Closed");

        var act = async () => await _service.ReopenWorkPackageAsync(created.Id, "   ");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Re-open reason cannot be null or empty*");
    }

    private sealed class InMemoryWorkPackageRepository : IWorkPackageRepository
    {
        private readonly Dictionary<Guid, Cakra.Modules.WorkPackage.Domain.WorkPackage> _packages = new();

        public Task<Cakra.Modules.WorkPackage.Domain.WorkPackage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _packages.TryGetValue(id, out var pkg);
            return Task.FromResult(pkg);
        }

        public Task<IReadOnlyList<Cakra.Modules.WorkPackage.Domain.WorkPackage>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Cakra.Modules.WorkPackage.Domain.WorkPackage>>(_packages.Values.ToList());

        public Task AddAsync(Cakra.Modules.WorkPackage.Domain.WorkPackage entity, CancellationToken cancellationToken = default)
        {
            _packages[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Cakra.Modules.WorkPackage.Domain.WorkPackage entity, CancellationToken cancellationToken = default)
        {
            _packages[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task SaveAsync(Cakra.Modules.WorkPackage.Domain.WorkPackage entity, CancellationToken cancellationToken = default)
            => UpdateAsync(entity, cancellationToken);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _packages.Remove(id);
            return Task.CompletedTask;
        }

        public Task AddRequestMembershipAsync(WorkPackageRequest membership, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateRequestMembershipAsync(WorkPackageRequest membership, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<WorkPackageRequest>> GetMembershipsByWorkPackageIdAsync(Guid workPackageId, CancellationToken cancellationToken = default)
        {
            if (_packages.TryGetValue(workPackageId, out var pkg))
            {
                return Task.FromResult<IReadOnlyList<WorkPackageRequest>>(pkg.Requests.ToList());
            }

            return Task.FromResult<IReadOnlyList<WorkPackageRequest>>(Array.Empty<WorkPackageRequest>());
        }

        public bool IsRequestInActiveWorkPackage(Guid requestId, Guid? excludingWorkPackageId = null)
        {
            return _packages.Values
                .Where(p => p.Status != WorkPackageStatus.Closed && (!excludingWorkPackageId.HasValue || p.Id != excludingWorkPackageId.Value))
                .Any(p => p.ActiveRequests.Any(r => r.RequestId == requestId));
        }

        public Task<bool> IsRequestInActiveWorkPackageAsync(Guid requestId, Guid? excludingWorkPackageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(IsRequestInActiveWorkPackage(requestId, excludingWorkPackageId));
    }

    private sealed class FakeOrganizationQueryService : IOrganizationQueryService
    {
        private readonly Dictionary<Guid, PersonDto> _persons = new();

        public void AddPerson(Guid id, string firstName, string lastName, bool isActive)
        {
            _persons[id] = new PersonDto
            {
                Id = id,
                FirstName = firstName,
                LastName = lastName,
                Email = $"{firstName.ToLowerInvariant()}@cakra.id",
                Status = isActive ? "ACTIVE" : "INACTIVE",
                CreatedAt = DateTime.UtcNow
            };
        }

        public Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default)
            => Task.FromResult(_persons.TryGetValue(personId, out var p) && p.IsActive);

        public Task<PersonDto?> GetPersonByIdAsync(Guid personId, CancellationToken cancellationToken = default)
            => Task.FromResult(_persons.TryGetValue(personId, out var p) ? p : null);

        public Task<IReadOnlyList<string>> GetPersonRolesAsync(Guid personId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class FakeCustomerQueryService : ICustomerQueryService
    {
        private readonly Dictionary<Guid, CustomerDto> _customers = new();

        public void AddCustomer(Guid id, string code, string name, bool isActive)
        {
            _customers[id] = new CustomerDto
            {
                Id = id,
                CustomerCode = code,
                CustomerName = name,
                Status = isActive ? "ACTIVE" : "INACTIVE",
                HasActiveMaintenanceContract = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        public Task<CustomerDto?> GetCustomerByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(_customers.TryGetValue(customerId, out var c) ? c : null);

        public Task<CustomerDto?> GetCustomerByCodeAsync(string customerCode, CancellationToken cancellationToken = default)
            => Task.FromResult(_customers.Values.FirstOrDefault(c => c.CustomerCode == customerCode));

        public Task<IReadOnlyList<CustomerDto>> ListActiveCustomersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CustomerDto>>(_customers.Values.Where(c => c.IsActive).ToList());

        public Task<IReadOnlyList<CustomerDto>> ListAllCustomersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CustomerDto>>(_customers.Values.ToList());

        public Task<IReadOnlyList<CustomerContactDto>> GetCustomerContactsAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CustomerContactDto>>(Array.Empty<CustomerContactDto>());

        public Task<CustomerWithContractStatusDto?> GetCustomerWithContractStatusAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<CustomerWithContractStatusDto?>(null);

        public Task<bool> IsCustomerActiveAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(_customers.TryGetValue(customerId, out var c) && c.IsActive);
    }

    private sealed class FakeProductQueryService : IProductQueryService
    {
        private readonly Dictionary<Guid, ProductDto> _products = new();

        public void AddProduct(Guid id, string code, string name, Guid ownerPersonId, bool isActive)
        {
            _products[id] = new ProductDto
            {
                Id = id,
                Code = code,
                Name = name,
                OwnerPersonId = ownerPersonId,
                Status = isActive ? "ACTIVE" : "INACTIVE",
                CreatedAt = DateTime.UtcNow
            };
        }

        public Task<ProductDto?> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult(_products.TryGetValue(productId, out var p) ? p : null);

        public Task<ProductDto?> GetProductByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Task.FromResult(_products.Values.FirstOrDefault(p => p.Code == code));

        public Task<IReadOnlyList<ProductDto>> ListActiveProductsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductDto>>(_products.Values.Where(p => p.IsActive).ToList());

        public Task<IReadOnlyList<ProductDto>> ListAllProductsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductDto>>(_products.Values.ToList());

        public Task<bool> IsProductActiveAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult(_products.TryGetValue(productId, out var p) && p.IsActive);
    }

    private sealed class FakeRequestQueryService : IRequestQueryService
    {
        private readonly Dictionary<Guid, RequestDto> _requests = new();

        public void AddRequest(Guid id, string title, string status, Guid? ownerPersonId)
        {
            _requests[id] = new RequestDto
            {
                Id = id,
                Title = title,
                Description = title,
                Status = status,
                OwnerPersonId = ownerPersonId,
                CreatedAt = DateTime.UtcNow
            };
        }

        public Task<RequestDto?> GetRequestByIdAsync(Guid requestId, CancellationToken cancellationToken = default)
            => Task.FromResult(_requests.TryGetValue(requestId, out var r) ? r : null);

        public Task<IReadOnlyList<RequestAssignmentDto>> GetRequestStateHistoryAsync(Guid requestId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RequestAssignmentDto>>(Array.Empty<RequestAssignmentDto>());

        public Task<IReadOnlyList<RequestDto>> ListMyAssignedRequestsAsync(Guid? personId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RequestDto>>(Array.Empty<RequestDto>());

        public Task<IReadOnlyList<RequestDto>> GetRequestsWithAssignedSubTasksAsync(Guid personId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RequestDto>>(Array.Empty<RequestDto>());

        public Task<PagedRequestGridResult> GetFilteredRequestGridAsync(RequestGridFilter? filter = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedRequestGridResult());

        public Task<bool> RequestExistsAsync(Guid requestId, CancellationToken cancellationToken = default)
            => Task.FromResult(_requests.ContainsKey(requestId));

        public Task<IReadOnlyList<RequestDto>> GetRequestsByIdsAsync(IEnumerable<Guid> requestIds, CancellationToken cancellationToken = default)
        {
            var list = requestIds
                .Where(id => _requests.ContainsKey(id))
                .Select(id => _requests[id])
                .ToList();
            return Task.FromResult<IReadOnlyList<RequestDto>>(list);
        }
    }

    private sealed class RecordingEventDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> DispatchedEvents { get; } = new();

        public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            DispatchedEvents.Add(domainEvent);
            return Task.CompletedTask;
        }

        public void Clear() => DispatchedEvents.Clear();
    }

    private sealed class FakeSystemClock : ISystemClock
    {
        public DateTime UtcNow { get; private set; } = new(2026, 9, 28, 7, 0, 0, DateTimeKind.Utc);

        public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
    }

    private sealed class FakeCurrentContextProvider : ICurrentContextProvider
    {
        public Guid? CurrentUserId { get; set; }
        public Guid? CurrentPersonId { get; set; }
        public List<string> Roles { get; } = new();
        public IReadOnlyCollection<string> CurrentRoles => Roles;
    }
}
