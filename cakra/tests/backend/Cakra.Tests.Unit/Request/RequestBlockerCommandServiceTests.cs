using Cakra.Core;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Cakra.Modules.Product;
using Cakra.Modules.Request;
using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Domain.Events;
using Cakra.Modules.Request.Domain.Exceptions;
using Cakra.Modules.Request.Persistence;
using Cakra.Modules.Request.Services;
using FluentAssertions;
using Xunit;
using RequestAggregate = Cakra.Modules.Request.Domain.Request;

namespace Cakra.Tests.Unit.Request;

/// <summary>
/// Unit tests for CR-032 Slice P2-S04:
/// - AddRequestBlockerCommand and ClearRequestBlockerCommand validators
/// - RequestService.AddBlockerAsync and ClearBlockerAsync state transitions and domain events
/// - RequestService.ClearBlockerAsync authorization rules (Owner vs Management roles)
/// - MediatR command handlers for blocker operations
/// </summary>
public sealed class RequestBlockerCommandServiceTests
{
    private static readonly DateTime TestNow = new(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);

    // =========================================================================
    // 1. Validator Tests
    // =========================================================================

    [Fact]
    public void Blocker_commands_and_validators_are_public()
    {
        typeof(AddRequestBlockerCommand).IsPublic.Should().BeTrue();
        typeof(AddRequestBlockerCommandValidator).IsPublic.Should().BeTrue();
        typeof(ClearRequestBlockerCommand).IsPublic.Should().BeTrue();
        typeof(ClearRequestBlockerCommandValidator).IsPublic.Should().BeTrue();
        typeof(RequestBlockerDto).IsPublic.Should().BeTrue();
    }

    [Fact]
    public void AddRequestBlockerCommandValidator_validates_required_fields()
    {
        var validator = new AddRequestBlockerCommandValidator();

        // Valid
        validator.Validate(new AddRequestBlockerCommand(Guid.NewGuid(), "External payment API down")).IsValid.Should().BeTrue();

        // Empty RequestId
        var emptyReq = validator.Validate(new AddRequestBlockerCommand(Guid.Empty, "External payment API down"));
        emptyReq.IsValid.Should().BeFalse();
        emptyReq.Errors.Should().Contain(e => e.PropertyName == nameof(AddRequestBlockerCommand.RequestId));

        // Empty Reason
        var emptyReason = validator.Validate(new AddRequestBlockerCommand(Guid.NewGuid(), "   "));
        emptyReason.IsValid.Should().BeFalse();
        emptyReason.Errors.Should().Contain(e => e.PropertyName == nameof(AddRequestBlockerCommand.Reason));

        // Reason > 1000 characters
        var longReason = validator.Validate(new AddRequestBlockerCommand(Guid.NewGuid(), new string('X', 1001)));
        longReason.IsValid.Should().BeFalse();
        longReason.Errors.Should().Contain(e => e.PropertyName == nameof(AddRequestBlockerCommand.Reason));

        // Empty ActorPersonId GUID
        var emptyActor = validator.Validate(new AddRequestBlockerCommand(Guid.NewGuid(), "Valid reason", ActorPersonId: Guid.Empty));
        emptyActor.IsValid.Should().BeFalse();
        emptyActor.Errors.Should().Contain(e => e.PropertyName == nameof(AddRequestBlockerCommand.ActorPersonId));

        // Valid ActorPersonId
        var validActor = validator.Validate(new AddRequestBlockerCommand(Guid.NewGuid(), "Valid reason", ActorPersonId: Guid.NewGuid()));
        validActor.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ClearRequestBlockerCommandValidator_validates_required_fields()
    {
        var validator = new ClearRequestBlockerCommandValidator();

        // Valid
        validator.Validate(new ClearRequestBlockerCommand(Guid.NewGuid(), Guid.NewGuid(), "Vendor provided fix")).IsValid.Should().BeTrue();

        // Empty RequestId
        var emptyReq = validator.Validate(new ClearRequestBlockerCommand(Guid.Empty, Guid.NewGuid(), "Vendor provided fix"));
        emptyReq.IsValid.Should().BeFalse();
        emptyReq.Errors.Should().Contain(e => e.PropertyName == nameof(ClearRequestBlockerCommand.RequestId));

        // Empty BlockerId
        var emptyBlocker = validator.Validate(new ClearRequestBlockerCommand(Guid.NewGuid(), Guid.Empty, "Vendor provided fix"));
        emptyBlocker.IsValid.Should().BeFalse();
        emptyBlocker.Errors.Should().Contain(e => e.PropertyName == nameof(ClearRequestBlockerCommand.BlockerId));

        // ClearanceReason > 1000 characters
        var longClearance = validator.Validate(new ClearRequestBlockerCommand(Guid.NewGuid(), Guid.NewGuid(), new string('Y', 1001)));
        longClearance.IsValid.Should().BeFalse();
        longClearance.Errors.Should().Contain(e => e.PropertyName == nameof(ClearRequestBlockerCommand.ClearanceReason));

        // Empty ActorPersonId GUID
        var emptyActor = validator.Validate(new ClearRequestBlockerCommand(Guid.NewGuid(), Guid.NewGuid(), "Fix", ActorPersonId: Guid.Empty));
        emptyActor.IsValid.Should().BeFalse();
        emptyActor.Errors.Should().Contain(e => e.PropertyName == nameof(ClearRequestBlockerCommand.ActorPersonId));

        // Null ClearanceReason is valid
        var nullClearance = validator.Validate(new ClearRequestBlockerCommand(Guid.NewGuid(), Guid.NewGuid(), null));
        nullClearance.IsValid.Should().BeTrue();
    }

    // =========================================================================
    // 2. RequestService Blocker Operations Tests
    // =========================================================================

    [Fact]
    public async Task AddBlockerAsync_on_in_progress_request_transitions_to_paused_and_increments_counter()
    {
        var (service, repository, org, _, dispatcher) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true);

        var request = CreateRequest(ownerId, RequestStatus.InProgress);
        await repository.AddAsync(request);

        var actorId = Guid.NewGuid();
        org.SetPerson(actorId, isActive: true);

        var result = await service.AddBlockerAsync(request.Id, "Database credentials expired", actorId);

        result.Status.Should().Be("PAUSED");
        result.ActiveBlockersCount.Should().Be(1);
        result.HasActiveBlockers.Should().BeTrue();
        result.Blockers.Should().HaveCount(1);
        result.Blockers[0].Reason.Should().Be("Database credentials expired");
        result.Blockers[0].CreatedByPersonId.Should().Be(actorId);
        result.Blockers[0].IsCleared.Should().BeFalse();

        // Repository updated
        var persisted = await repository.GetByIdAsync(request.Id);
        persisted!.Status.Should().Be(RequestStatus.Paused);
        persisted.ActiveBlockersCount.Should().Be(1);

        // Domain events emitted
        dispatcher.Events.OfType<RequestBlockerAdded>().Should().ContainSingle();
        dispatcher.Events.OfType<RequestWorkPaused>().Should().ContainSingle();
    }

    [Fact]
    public async Task AddBlockerAsync_on_assigned_request_transitions_to_paused()
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true);

        var request = CreateRequest(ownerId, RequestStatus.Assigned);
        await repository.AddAsync(request);

        var result = await service.AddBlockerAsync(request.Id, "Missing business specs", ownerId);

        result.Status.Should().Be("PAUSED");
        result.ActiveBlockersCount.Should().Be(1);
        result.HasActiveBlockers.Should().BeTrue();
    }

    [Fact]
    public async Task AddBlockerAsync_on_completed_request_throws_InvalidRequestStateTransitionException()
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true);

        var request = CreateRequest(ownerId, RequestStatus.Completed);
        await repository.AddAsync(request);

        var act = () => service.AddBlockerAsync(request.Id, "Cannot add to completed", ownerId);
        await act.Should().ThrowAsync<InvalidRequestStateTransitionException>();
    }

    [Fact]
    public async Task AddBlockerAsync_validates_empty_inputs()
    {
        var (service, _, _, _, _) = CreateTestSetup();

        var actEmptyId = () => service.AddBlockerAsync(Guid.Empty, "Reason");
        await actEmptyId.Should().ThrowAsync<RequestDomainValidationException>();

        var actEmptyReason = () => service.AddBlockerAsync(Guid.NewGuid(), "   ");
        await actEmptyReason.Should().ThrowAsync<RequestDomainValidationException>();
    }

    [Fact]
    public async Task ClearBlockerAsync_clears_blocker_decrements_count_and_leaves_paused()
    {
        var (service, repository, org, _, dispatcher) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true);

        var request = CreateRequest(ownerId, RequestStatus.InProgress);
        var blocker = request.AddBlocker(Guid.NewGuid(), "Firewall port closed", ownerId, TestNow.AddMinutes(20));
        await repository.AddAsync(request);

        var result = await service.ClearBlockerAsync(request.Id, blocker.Id, "Port 443 opened", ownerId);

        result.Status.Should().Be("PAUSED");
        result.ActiveBlockersCount.Should().Be(0);
        result.HasActiveBlockers.Should().BeFalse();
        result.Blockers.Should().HaveCount(1);
        result.Blockers[0].IsCleared.Should().BeTrue();
        result.Blockers[0].ClearanceReason.Should().Be("Port 443 opened");
        result.Blockers[0].ClearedByPersonId.Should().Be(ownerId);

        dispatcher.Events.OfType<RequestBlockerCleared>().Should().ContainSingle();
    }

    [Fact]
    public async Task ClearBlockerAsync_validates_empty_inputs_and_non_existent_blocker()
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true);

        var request = CreateRequest(ownerId, RequestStatus.Assigned);
        await repository.AddAsync(request);

        var actEmptyReq = () => service.ClearBlockerAsync(Guid.Empty, Guid.NewGuid(), "Cleared", ownerId);
        await actEmptyReq.Should().ThrowAsync<RequestDomainValidationException>();

        var actEmptyBlocker = () => service.ClearBlockerAsync(request.Id, Guid.Empty, "Cleared", ownerId);
        await actEmptyBlocker.Should().ThrowAsync<RequestDomainValidationException>();

        var actNonExistent = () => service.ClearBlockerAsync(request.Id, Guid.NewGuid(), "Cleared", ownerId);
        await actNonExistent.Should().ThrowAsync<KeyNotFoundException>();
    }

    // =========================================================================
    // 3. ClearBlockerAsync Authorization Tests (TD-006)
    // =========================================================================

    [Fact]
    public async Task ClearBlockerAsync_permits_assigned_owner_without_management_roles()
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true, roles: new[] { "Developer" }); // Developer is NOT in AuthorizedClearBlockerRoles

        var request = CreateRequest(ownerId, RequestStatus.Assigned);
        var blocker = request.AddBlocker(Guid.NewGuid(), "Some impediment", ownerId, TestNow);
        await repository.AddAsync(request);

        var result = await service.ClearBlockerAsync(request.Id, blocker.Id, "Resolved by owner", ownerId);
        result.ActiveBlockersCount.Should().Be(0);
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Manager")]
    [InlineData("System Administrator")]
    [InlineData("Team Lead")]
    public async Task ClearBlockerAsync_permits_non_owner_with_authorized_roles(string authorizedRole)
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true, roles: new[] { "Developer" });

        var managerId = Guid.NewGuid();
        org.SetPerson(managerId, isActive: true, roles: new[] { authorizedRole });

        var request = CreateRequest(ownerId, RequestStatus.Assigned);
        var blocker = request.AddBlocker(Guid.NewGuid(), "Some impediment", ownerId, TestNow);
        await repository.AddAsync(request);

        var result = await service.ClearBlockerAsync(request.Id, blocker.Id, "Resolved by management", managerId);
        result.ActiveBlockersCount.Should().Be(0);
    }

    [Theory]
    [InlineData("Programmer")]
    [InlineData("Developer")]
    [InlineData("QA")]
    [InlineData("Viewer")]
    public async Task ClearBlockerAsync_rejects_non_owner_without_authorized_roles(string unauthorizedRole)
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true, roles: new[] { "Developer" });

        var nonOwnerId = Guid.NewGuid();
        org.SetPerson(nonOwnerId, isActive: true, roles: new[] { unauthorizedRole });

        var request = CreateRequest(ownerId, RequestStatus.Assigned);
        var blocker = request.AddBlocker(Guid.NewGuid(), "Impediment", ownerId, TestNow);
        await repository.AddAsync(request);

        var act = () => service.ClearBlockerAsync(request.Id, blocker.Id, "Attempted clear", nonOwnerId);
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage($"*{nonOwnerId}*not authorized to clear blocker*");
    }

    [Fact]
    public async Task ClearBlockerAsync_permits_system_actor()
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true);

        var request = CreateRequest(ownerId, RequestStatus.Assigned);
        var blocker = request.AddBlocker(Guid.NewGuid(), "Impediment", ownerId, TestNow);
        await repository.AddAsync(request);

        var result = await service.ClearBlockerAsync(request.Id, blocker.Id, "Cleared by system", RequestService.SystemActorPersonId);
        result.ActiveBlockersCount.Should().Be(0);
    }

    // =========================================================================
    // 4. MediatR Handler Tests
    // =========================================================================

    [Fact]
    public async Task MediatR_Handle_executes_AddRequestBlockerCommand()
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true);

        var request = CreateRequest(ownerId, RequestStatus.Assigned);
        await repository.AddAsync(request);

        var command = new AddRequestBlockerCommand(request.Id, "MediatR blocker", ownerId);
        var result = await service.Handle(command, CancellationToken.None);

        result.ActiveBlockersCount.Should().Be(1);
        result.Blockers.Should().ContainSingle(b => b.Reason == "MediatR blocker");
    }

    [Fact]
    public async Task MediatR_Handle_executes_ClearRequestBlockerCommand()
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true);

        var request = CreateRequest(ownerId, RequestStatus.Assigned);
        var blocker = request.AddBlocker(Guid.NewGuid(), "Impediment to clear", ownerId, TestNow);
        await repository.AddAsync(request);

        var command = new ClearRequestBlockerCommand(request.Id, blocker.Id, "MediatR clear", ownerId);
        var result = await service.Handle(command, CancellationToken.None);

        result.ActiveBlockersCount.Should().Be(0);
        result.Blockers.Should().ContainSingle(b => b.IsCleared && b.ClearanceReason == "MediatR clear");
    }

    // =========================================================================
    // Test Setup Helpers
    // =========================================================================

    private static RequestAggregate CreateRequest(Guid ownerId, RequestStatus status = RequestStatus.Assigned)
    {
        var request = RequestAggregate.Record(
            id: Guid.NewGuid(),
            title: "Blocker Test Request",
            description: "Description",
            requestType: "FEATURE",
            actorPersonId: ownerId,
            initialOwnerPersonId: ownerId,
            utcNow: TestNow);

        if (status == RequestStatus.Assigned)
        {
            return request;
        }

        request.StartWork(ownerId, "Starting work", TestNow.AddMinutes(5));

        if (status == RequestStatus.InProgress)
        {
            return request;
        }

        if (status == RequestStatus.Paused)
        {
            request.PauseWork(ownerId, "Paused for testing", TestNow.AddMinutes(10));
            return request;
        }

        if (status == RequestStatus.Completed)
        {
            request.Complete("Work finished", ownerId, utcNow: TestNow.AddMinutes(20));
            return request;
        }

        if (status == RequestStatus.Cancelled)
        {
            request.Cancel("Cancelled for testing", ownerId, utcNow: TestNow.AddMinutes(20));
            return request;
        }

        return request;
    }

    private static (
        RequestService Service,
        FakeRequestRepository Repository,
        FakeOrganizationQueryService OrgService,
        FakeCustomerQueryService CustomerService,
        RecordingDomainEventDispatcher EventDispatcher)
        CreateTestSetup()
    {
        var repository = new FakeRequestRepository();
        var orgService = new FakeOrganizationQueryService();
        var customerService = new FakeCustomerQueryService();
        var productService = new FakeProductQueryService();
        var eventDispatcher = new RecordingDomainEventDispatcher();
        var clock = new FakeSystemClock(TestNow);

        var service = new RequestService(
            repository,
            orgService,
            customerService,
            productService,
            eventDispatcher,
            clock: clock);

        return (service, repository, orgService, customerService, eventDispatcher);
    }

    private sealed class FakeRequestRepository : IRequestRepository
    {
        private readonly Dictionary<Guid, RequestAggregate> _requests = new();
        public List<RequestAssignment> Assignments { get; } = new();

        public Task<RequestAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_requests.TryGetValue(id, out var r) ? r : null);

        public Task<IReadOnlyList<RequestAggregate>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestAggregate>>(_requests.Values.ToList());

        public Task AddAsync(RequestAggregate entity, CancellationToken cancellationToken = default)
        {
            _requests[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(RequestAggregate entity, CancellationToken cancellationToken = default)
        {
            _requests[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task SaveAsync(RequestAggregate entity, CancellationToken cancellationToken = default)
        {
            _requests[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _requests.Remove(id);
            return Task.CompletedTask;
        }

        public Task AddAssignmentAsync(RequestAssignment assignment, CancellationToken cancellationToken = default)
        {
            Assignments.Add(assignment);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RequestAssignment>> GetAssignmentsByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestAssignment>>(
                Assignments.Where(a => a.RequestId == requestId).OrderBy(a => a.AssignedAtUtc).ToList());

        public Task AddResolutionAsync(RequestResolution resolution, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<RequestResolution?> GetResolutionByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<RequestResolution?>(null);

        public Task<RequestAggregate?> GetActiveInProgressByOwnerAsync(
            Guid ownerPersonId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<RequestAggregate?>(
                _requests.Values.FirstOrDefault(r => r.OwnerPersonId == ownerPersonId && r.Status == RequestStatus.InProgress));
    }

    private sealed class FakeOrganizationQueryService : IOrganizationQueryService
    {
        private readonly Dictionary<Guid, (PersonDto Person, IReadOnlyList<string> Roles)> _persons = new();

        public void SetPerson(Guid id, bool isActive, string[]? roles = null)
        {
            _persons[id] = (
                new PersonDto
                {
                    Id = id,
                    FirstName = "Test",
                    LastName = "Person",
                    Email = $"person-{id:N}@cakra.id",
                    Status = isActive ? "ACTIVE" : "INACTIVE",
                    CreatedAt = DateTime.UtcNow
                },
                roles ?? Array.Empty<string>());
        }

        public Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_persons.TryGetValue(personId, out var p) && p.Person.IsActive);

        public Task<PersonDto?> GetPersonByIdAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_persons.TryGetValue(personId, out var p) ? p.Person : null);

        public Task<IReadOnlyList<string>> GetPersonRolesAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_persons.TryGetValue(personId, out var p) ? p.Roles : (IReadOnlyList<string>)Array.Empty<string>());
    }

    private sealed class FakeCustomerQueryService : ICustomerQueryService
    {
        public Task<bool> IsCustomerActiveAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<CustomerDto?> GetCustomerByIdAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CustomerDto?>(null);

        public Task<CustomerDto?> GetCustomerByCodeAsync(string customerCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<CustomerDto?>(null);

        public Task<IReadOnlyList<CustomerDto>> ListActiveCustomersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerDto>>(Array.Empty<CustomerDto>());

        public Task<IReadOnlyList<CustomerDto>> ListAllCustomersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerDto>>(Array.Empty<CustomerDto>());

        public Task<IReadOnlyList<CustomerContactDto>> GetCustomerContactsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerContactDto>>(Array.Empty<CustomerContactDto>());

        public Task<CustomerWithContractStatusDto?> GetCustomerWithContractStatusAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CustomerWithContractStatusDto?>(null);
    }

    private sealed class FakeProductQueryService : IProductQueryService
    {
        public Task<bool> IsProductActiveAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<ProductDto?> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductDto?>(null);

        public Task<ProductDto?> GetProductByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductDto?>(null);

        public Task<IReadOnlyList<ProductDto>> ListActiveProductsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductDto>>(Array.Empty<ProductDto>());

        public Task<IReadOnlyList<ProductDto>> ListAllProductsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductDto>>(Array.Empty<ProductDto>());
    }

    private sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> Events { get; } = new();

        public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(domainEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSystemClock : ISystemClock
    {
        public DateTime UtcNow { get; }

        public FakeSystemClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }
    }
}
