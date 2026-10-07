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
}
