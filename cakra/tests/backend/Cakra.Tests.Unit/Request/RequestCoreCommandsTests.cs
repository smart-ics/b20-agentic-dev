using Cakra.Core;
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

namespace Cakra.Tests.Unit.Request;

/// <summary>
/// Unit tests for P4-S18 Request Module — Persistence &amp; Core Commands:
/// - Internal visibility of repositories vs published contracts
/// - RecordRequest, AssignRequestOwner, EvaluateRequest via RequestService
/// - Cross-module validation via ICustomerQueryService, IProductQueryService, IOrganizationQueryService
/// - Audit logging pattern into RequestAssignments
/// - Domain event dispatching (RequestRecorded, RequestAssigned, RequestEvaluated)
/// - Command FluentValidation rules
/// </summary>
public sealed class RequestCoreCommandsTests
{
    [Fact]
    public void Internal_repositories_are_not_publicly_exposed_while_IRequestService_and_DTOs_are_published()
    {
        typeof(IRequestService).IsPublic.Should().BeTrue();
        typeof(RequestService).IsPublic.Should().BeTrue();
        typeof(RequestDto).IsPublic.Should().BeTrue();
        typeof(RequestResolutionDto).IsPublic.Should().BeTrue();
        typeof(RequestAssignmentDto).IsPublic.Should().BeTrue();
        typeof(RecordRequestCommand).IsPublic.Should().BeTrue();
        typeof(AssignRequestOwnerCommand).IsPublic.Should().BeTrue();
        typeof(StartWorkCommand).IsPublic.Should().BeTrue();
        typeof(PauseWorkCommand).IsPublic.Should().BeTrue();
        typeof(CancelRequestCommand).IsPublic.Should().BeTrue();
        typeof(ReassignRequestOwnershipCommand).IsPublic.Should().BeTrue();

        typeof(IRequestRepository).IsPublic.Should().BeFalse();
        typeof(RequestRepository).IsPublic.Should().BeFalse();
    }

    [Fact]
    public async Task Sequential_lifecycle_Record_AssignOwner_Evaluate_records_audit_trail_and_dispatches_events()
    {
        var actorId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var repo = new InMemoryRequestRepository();
        var orgQuery = new FakeOrganizationQueryService();
        orgQuery.SetPerson(actorId, isActive: true);
        orgQuery.SetPerson(ownerId, isActive: true);

        var customerQuery = new FakeCustomerQueryService();
        customerQuery.SetCustomer(customerId, isActive: true);

        var productQuery = new FakeProductQueryService();
        productQuery.SetProduct(productId, isActive: true);

        var dispatcher = new RecordingDomainEventDispatcher();
        var contextProvider = new FakeCurrentContextProvider(actorId);
        var clock = new FakeSystemClock(new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc));

        var service = new RequestService(
            repo,
            orgQuery,
            customerQuery,
            productQuery,
            dispatcher,
            contextProvider,
            auditContext: null,
            clock: clock);

        // 1. RecordRequest (creates request in CAPTURED state, validates customer & product, emits RequestRecorded)
        var recorded = await service.Handle(
            new RecordRequestCommand(
                Title: "BPJS SEP timeout on inpatient discharge",
                Description: "Inpatient discharge fails when BPJS VClaim API takes > 5 seconds.",
                CustomerId: customerId,
                ProductId: productId,
                RequestType: "Bug",
                Priority: "HIGH"),
            CancellationToken.None);

        recorded.Id.Should().NotBeEmpty();
        recorded.Title.Should().Be("BPJS SEP timeout on inpatient discharge");
        recorded.Status.Should().Be(RequestStatusNames.Captured);
        recorded.Priority.Should().Be("HIGH");
        recorded.CustomerId.Should().Be(customerId);
        recorded.ProductId.Should().Be(productId);
        recorded.OwnerPersonId.Should().BeNull();
        recorded.Assignments.Should().ContainSingle();
        recorded.Assignments[0].PreviousStatus.Should().BeNull();
        recorded.Assignments[0].NewStatus.Should().Be(RequestStatusNames.Captured);
        recorded.Assignments[0].ActorPersonId.Should().Be(actorId);

        repo.Assignments.Should().ContainSingle(a =>
            a.RequestId == recorded.Id &&
            a.PreviousStatus == null &&
            a.NewStatus == RequestStatus.Captured &&
            a.ActorPersonId == actorId);

        dispatcher.Events.Should().ContainSingle()
            .Which.Should().BeOfType<RequestRecorded>()
            .Which.RequestId.Should().Be(recorded.Id);

        // 2. AssignRequestOwner (validates assignee, transitions to ASSIGNED, records audit, emits RequestAssigned)
        clock.AdvanceMinutes(15);
        var assigned = await service.Handle(
            new AssignRequestOwnerCommand(
                RequestId: recorded.Id,
                OwnerPersonId: ownerId,
                Notes: "Assigned to hospital integration specialist"),
            CancellationToken.None);

        assigned.Status.Should().Be(RequestStatusNames.Assigned);
        assigned.OwnerPersonId.Should().Be(ownerId);
        assigned.Assignments.Should().HaveCount(2);
        assigned.Assignments[1].PreviousStatus.Should().Be(RequestStatusNames.Captured);
        assigned.Assignments[1].NewStatus.Should().Be(RequestStatusNames.Assigned);
        assigned.Assignments[1].AssignedOwnerPersonId.Should().Be(ownerId);
        assigned.Assignments[1].ActorPersonId.Should().Be(actorId);

        repo.Assignments.Should().HaveCount(2);
        repo.Assignments.Last().NewStatus.Should().Be(RequestStatus.Assigned);

        dispatcher.Events.OfType<RequestAssigned>().Should().ContainSingle(e =>
            e.RequestId == recorded.Id &&
            e.OwnerPersonId == ownerId &&
            e.PreviousStatus == RequestStatus.Captured &&
            e.NewStatus == RequestStatus.Assigned);

        // 3. StartWork (owner starts work, transitions ASSIGNED -> IN_PROGRESS, emits RequestWorkStarted)
        clock.AdvanceMinutes(20);
        contextProvider.CurrentPersonId = ownerId;

        var started = await service.Handle(
            new StartWorkCommand(
                RequestId: recorded.Id,
                Notes: "Root cause investigation started."),
            CancellationToken.None);

        started.Status.Should().Be(RequestStatusNames.InProgress);

        dispatcher.Events.OfType<RequestWorkStarted>().Should().ContainSingle(e =>
            e.RequestId == recorded.Id &&
            e.OwnerPersonId == ownerId &&
            e.Notes!.Contains("investigation started"));
    }

    [Fact]
    public async Task Cross_module_validation_rejects_unknown_or_inactive_customer_product_and_assignee()
    {
        var activeCustomer = Guid.NewGuid();
        var inactiveCustomer = Guid.NewGuid();
        var activeProduct = Guid.NewGuid();
        var inactiveProduct = Guid.NewGuid();
        var activeAssignee = Guid.NewGuid();
        var inactiveAssignee = Guid.NewGuid();

        var repo = new InMemoryRequestRepository();
        var orgQuery = new FakeOrganizationQueryService();
        orgQuery.SetPerson(activeAssignee, isActive: true);
        orgQuery.SetPerson(inactiveAssignee, isActive: false);

        var customerQuery = new FakeCustomerQueryService();
        customerQuery.SetCustomer(activeCustomer, isActive: true);
        customerQuery.SetCustomer(inactiveCustomer, isActive: false);

        var productQuery = new FakeProductQueryService();
        productQuery.SetProduct(activeProduct, isActive: true);
        productQuery.SetProduct(inactiveProduct, isActive: false);

        var service = new RequestService(repo, orgQuery, customerQuery, productQuery);

        // Unknown customer -> KeyNotFoundException
        var unknownCustomerAct = async () => await service.RecordRequest(
            "Title", "Desc", customerId: Guid.NewGuid(), productId: activeProduct);
        await unknownCustomerAct.Should().ThrowAsync<KeyNotFoundException>();

        // Inactive customer -> InvalidOperationException
        var inactiveCustomerAct = async () => await service.RecordRequest(
            "Title", "Desc", customerId: inactiveCustomer, productId: activeProduct);
        await inactiveCustomerAct.Should().ThrowAsync<InvalidOperationException>();

        // Unknown product -> KeyNotFoundException
        var unknownProductAct = async () => await service.RecordRequest(
            "Title", "Desc", customerId: activeCustomer, productId: Guid.NewGuid());
        await unknownProductAct.Should().ThrowAsync<KeyNotFoundException>();

        // Inactive product -> InvalidOperationException
        var inactiveProductAct = async () => await service.RecordRequest(
            "Title", "Desc", customerId: activeCustomer, productId: inactiveProduct);
        await inactiveProductAct.Should().ThrowAsync<InvalidOperationException>();

        // Valid request
        var created = await service.RecordRequest(
            "Title", "Desc", customerId: activeCustomer, productId: activeProduct);

        // Unknown assignee -> KeyNotFoundException
        var unknownAssigneeAct = async () => await service.AssignRequestOwner(
            created.Id, Guid.NewGuid());
        await unknownAssigneeAct.Should().ThrowAsync<KeyNotFoundException>();

        // Inactive assignee -> InvalidOperationException
        var inactiveAssigneeAct = async () => await service.AssignRequestOwner(
            created.Id, inactiveAssignee);
        await inactiveAssigneeAct.Should().ThrowAsync<InvalidOperationException>();

        // StartWork before AssignOwner (still CAPTURED, no owner) -> InvalidOperationException
        var prematureStartAct = async () => await service.StartWorkAsync(created.Id);
        await prematureStartAct.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Command_validators_enforce_required_fields()
    {
        var recordValidator = new RecordRequestCommandValidator();
        recordValidator.Validate(new RecordRequestCommand("", "Desc")).IsValid.Should().BeFalse();
        recordValidator.Validate(new RecordRequestCommand("Title", "")).IsValid.Should().BeTrue();
        recordValidator.Validate(new RecordRequestCommand("Title", "Desc", CustomerId: Guid.Empty)).IsValid.Should().BeFalse();
        recordValidator.Validate(new RecordRequestCommand("Title", "Desc", ProductId: Guid.Empty)).IsValid.Should().BeFalse();
        recordValidator.Validate(new RecordRequestCommand("Title", "Desc", Guid.NewGuid(), Guid.NewGuid())).IsValid.Should().BeTrue();

        var assignValidator = new AssignRequestOwnerCommandValidator();
        assignValidator.Validate(new AssignRequestOwnerCommand(Guid.Empty, Guid.NewGuid())).IsValid.Should().BeFalse();
        assignValidator.Validate(new AssignRequestOwnerCommand(Guid.NewGuid(), Guid.Empty)).IsValid.Should().BeFalse();
        assignValidator.Validate(new AssignRequestOwnerCommand(Guid.NewGuid(), Guid.NewGuid())).IsValid.Should().BeTrue();

        var startValidator = new StartWorkCommandValidator();
        startValidator.Validate(new StartWorkCommand(Guid.Empty)).IsValid.Should().BeFalse();
        startValidator.Validate(new StartWorkCommand(Guid.NewGuid())).IsValid.Should().BeTrue();

        var pauseValidator = new PauseWorkCommandValidator();
        pauseValidator.Validate(new PauseWorkCommand(Guid.Empty)).IsValid.Should().BeFalse();
        pauseValidator.Validate(new PauseWorkCommand(Guid.NewGuid())).IsValid.Should().BeTrue();

        var cancelValidator = new CancelRequestCommandValidator();
        cancelValidator.Validate(new CancelRequestCommand(Guid.Empty, "Reason")).IsValid.Should().BeFalse();
        cancelValidator.Validate(new CancelRequestCommand(Guid.NewGuid(), "")).IsValid.Should().BeFalse();
        cancelValidator.Validate(new CancelRequestCommand(Guid.NewGuid(), "Valid reason")).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task RecordRequest_supports_empty_and_null_description_for_quick_capture()
    {
        var recordValidator = new RecordRequestCommandValidator();

        // 1. Validator allows empty, null, or omitted description (CR-019 / P1-S01)
        recordValidator.Validate(new RecordRequestCommand("Quick demand", "")).IsValid.Should().BeTrue();
        recordValidator.Validate(new RecordRequestCommand("Quick demand", null!)).IsValid.Should().BeTrue();
        recordValidator.Validate(new RecordRequestCommand("Quick demand")).IsValid.Should().BeTrue();

        // 2. Command defaults null description to string.Empty
        var cmd1 = new RecordRequestCommand("Quick demand");
        cmd1.Description.Should().Be(string.Empty);

        var cmd2 = new RecordRequestCommand("Quick demand", null);
        cmd2.Description.Should().Be(string.Empty);

        // 3. RequestService records request with empty description successfully
        var repo = new InMemoryRequestRepository();
        var orgQuery = new FakeOrganizationQueryService();
        var customerQuery = new FakeCustomerQueryService();
        var productQuery = new FakeProductQueryService();
        var service = new RequestService(repo, orgQuery, customerQuery, productQuery);

        var result = await service.RecordRequest("Minimal demand", "");
        result.Should().NotBeNull();
        result.Title.Should().Be("Minimal demand");
        result.Description.Should().Be(string.Empty);
        result.Status.Should().Be("CAPTURED");
    }

    [Fact]
    public async Task RequestRepository_GetActiveInProgressByOwnerAsync_returns_active_request_when_in_progress_and_null_otherwise()
    {
        var repo = new InMemoryRequestRepository();
        var ownerId = Guid.NewGuid();

        // 1. Returns null when no request exists for owner
        var none = await repo.GetActiveInProgressByOwnerAsync(ownerId);
        none.Should().BeNull();

        // 2. Request recorded and assigned but not yet started (Status = ASSIGNED) -> returns null
        var req = Cakra.Modules.Request.Domain.Request.Record(
            Guid.NewGuid(),
            "Unit Test Demand",
            "Description",
            "GENERAL",
            ownerId);
        req.AssignOwner(ownerId, Guid.NewGuid());
        await repo.AddAsync(req);

        var assignedResult = await repo.GetActiveInProgressByOwnerAsync(ownerId);
        assignedResult.Should().BeNull();

        // 3. Request started (Status = IN_PROGRESS) -> returns active request
        req.StartWork(ownerId);
        await repo.UpdateAsync(req);

        var inProgressResult = await repo.GetActiveInProgressByOwnerAsync(ownerId);
        inProgressResult.Should().NotBeNull();
        inProgressResult!.Id.Should().Be(req.Id);
        inProgressResult.Title.Should().Be("Unit Test Demand");
        inProgressResult.Status.Should().Be(RequestStatus.InProgress);
        inProgressResult.OwnerPersonId.Should().Be(ownerId);

        // 4. Different owner returns null
        var otherOwnerResult = await repo.GetActiveInProgressByOwnerAsync(Guid.NewGuid());
        otherOwnerResult.Should().BeNull();

        // 5. Request paused (Status = PAUSED) -> returns null
        req.PauseWork(ownerId, "Pausing work");
        await repo.UpdateAsync(req);

        var pausedResult = await repo.GetActiveInProgressByOwnerAsync(ownerId);
        pausedResult.Should().BeNull();
    }

    [Fact]
    public async Task StartWorkAsync_enforces_single_in_progress_policy_per_owner()
    {
        var ownerId = Guid.NewGuid();
        var repo = new InMemoryRequestRepository();
        var orgQuery = new FakeOrganizationQueryService();
        orgQuery.SetPerson(ownerId, isActive: true);

        var customerQuery = new FakeCustomerQueryService();
        var productQuery = new FakeProductQueryService();
        var contextProvider = new FakeCurrentContextProvider(ownerId);
        var clock = new FakeSystemClock(new DateTime(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc));

        var service = new RequestService(
            repo,
            orgQuery,
            customerQuery,
            productQuery,
            eventDispatcher: null,
            currentContextProvider: contextProvider,
            auditContext: null,
            clock: clock);

        // 1. Create two requests assigned to the same owner
        var req1 = await service.RecordRequest("First Task", "Description 1", actorPersonId: ownerId);
        await service.AssignRequestOwner(req1.Id, ownerId, actorPersonId: ownerId);

        var req2 = await service.RecordRequest("Second Task", "Description 2", actorPersonId: ownerId);
        await service.AssignRequestOwner(req2.Id, ownerId, actorPersonId: ownerId);

        // 2. Starting req1 succeeds when zero active tasks are in progress
        var started1 = await service.StartWorkAsync(req1.Id, actorPersonId: ownerId);
        started1.Status.Should().Be(RequestStatusNames.InProgress);

        // 3. Attempting to start req2 while req1 is IN_PROGRESS throws RequestDomainValidationException
        var secondStartAct = async () => await service.StartWorkAsync(req2.Id, actorPersonId: ownerId);
        var ex = await secondStartAct.Should().ThrowAsync<RequestDomainValidationException>();
        ex.WithMessage("*Cannot start work on request 'Second Task' because the assigned owner already has an active task in progress: 'First Task'. Please pause or complete it first.*");
        ex.Which.ParamName.Should().Be("requestId");

        // 4. Starting req1 again (already IN_PROGRESS) follows existing transition validation
        var alreadyStartedAct = async () => await service.StartWorkAsync(req1.Id, actorPersonId: ownerId);
        await alreadyStartedAct.Should().ThrowAsync<InvalidRequestStateTransitionException>();

        // 5. Pausing req1 frees up the owner, allowing req2 to start
        await service.PauseWorkAsync(req1.Id, note: "Paused to switch tasks", actorPersonId: ownerId);
        var started2 = await service.StartWorkAsync(req2.Id, actorPersonId: ownerId);
        started2.Status.Should().Be(RequestStatusNames.InProgress);
    }

    private sealed class InMemoryRequestRepository : IRequestRepository
    {
        private readonly Dictionary<Guid, Cakra.Modules.Request.Domain.Request> _requests = new();
        private readonly Dictionary<Guid, RequestResolution> _resolutions = new();
        public List<RequestAssignment> Assignments { get; } = new();

        public Task<Cakra.Modules.Request.Domain.Request?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (!_requests.TryGetValue(id, out var req))
            {
                return Task.FromResult<Cakra.Modules.Request.Domain.Request?>(null);
            }

            _resolutions.TryGetValue(id, out var resolution);
            var reqAssignments = Assignments.Where(a => a.RequestId == id).OrderBy(a => a.AssignedAtUtc).ToList();

            var hydrated = Cakra.Modules.Request.Domain.Request.Rehydrate(
                req.Id,
                req.Title,
                req.Description,
                req.RequestType,
                req.Status,
                req.Priority,
                req.OwnerPersonId,
                req.CustomerId,
                req.ProductId,
                req.WorkPackageId,
                req.EvaluationNotes,
                req.CreatedAt,
                req.UpdatedAt,
                resolution,
                reqAssignments,
                deadline: req.Deadline);

            return Task.FromResult<Cakra.Modules.Request.Domain.Request?>(hydrated);
        }

        public Task<IReadOnlyList<Cakra.Modules.Request.Domain.Request>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Cakra.Modules.Request.Domain.Request>>(_requests.Values.ToList());

        public Task AddAsync(Cakra.Modules.Request.Domain.Request entity, CancellationToken cancellationToken = default)
        {
            _requests[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Cakra.Modules.Request.Domain.Request entity, CancellationToken cancellationToken = default)
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

        public Task<IReadOnlyList<RequestAssignment>> GetAssignmentsByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestAssignment>>(
                Assignments.Where(a => a.RequestId == requestId).OrderBy(a => a.AssignedAtUtc).ToList());

        public Task AddResolutionAsync(RequestResolution resolution, CancellationToken cancellationToken = default)
        {
            _resolutions[resolution.RequestId] = resolution;
            return Task.CompletedTask;
        }

        public Task<RequestResolution?> GetResolutionByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_resolutions.TryGetValue(requestId, out var res) ? res : null);

        public Task<Cakra.Modules.Request.Domain.Request?> GetActiveInProgressByOwnerAsync(
            Guid ownerPersonId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Cakra.Modules.Request.Domain.Request?>(
                _requests.Values.FirstOrDefault(r => r.OwnerPersonId == ownerPersonId && r.Status == RequestStatus.InProgress));
    }

    private sealed class FakeOrganizationQueryService : IOrganizationQueryService
    {
        private readonly Dictionary<Guid, PersonDto> _persons = new();

        public void SetPerson(Guid id, bool isActive)
        {
            _persons[id] = new PersonDto
            {
                Id = id,
                FirstName = "Test",
                LastName = "Person",
                Email = $"person-{id:N}@cakra.id",
                Status = isActive ? "ACTIVE" : "INACTIVE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };
        }

        public Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_persons.TryGetValue(personId, out var p) && p.IsActive);

        public Task<PersonDto?> GetPersonByIdAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_persons.TryGetValue(personId, out var p) ? p : null);

        public Task<IReadOnlyList<string>> GetPersonRolesAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class FakeCustomerQueryService : ICustomerQueryService
    {
        private readonly Dictionary<Guid, CustomerDto> _customers = new();

        public void SetCustomer(Guid id, bool isActive)
        {
            _customers[id] = new CustomerDto
            {
                Id = id,
                CustomerCode = $"CUST-{id:N}"[..12],
                CustomerName = "Test Customer",
                Status = isActive ? "ACTIVE" : "INACTIVE",
                HasActiveMaintenanceContract = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        public Task<CustomerDto?> GetCustomerByIdAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_customers.TryGetValue(customerId, out var c) ? c : null);

        public Task<CustomerDto?> GetCustomerByCodeAsync(string customerCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(_customers.Values.FirstOrDefault(c => c.CustomerCode == customerCode));

        public Task<IReadOnlyList<CustomerDto>> ListActiveCustomersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerDto>>(_customers.Values.Where(c => c.IsActive).ToList());

        public Task<IReadOnlyList<CustomerDto>> ListAllCustomersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerDto>>(_customers.Values.ToList());

        public Task<IReadOnlyList<CustomerContactDto>> GetCustomerContactsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerContactDto>>(Array.Empty<CustomerContactDto>());

        public Task<CustomerWithContractStatusDto?> GetCustomerWithContractStatusAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CustomerWithContractStatusDto?>(null);

        public Task<bool> IsCustomerActiveAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_customers.TryGetValue(customerId, out var c) && c.IsActive);
    }

    private sealed class FakeProductQueryService : IProductQueryService
    {
        private readonly Dictionary<Guid, ProductDto> _products = new();

        public void SetProduct(Guid id, bool isActive)
        {
            _products[id] = new ProductDto
            {
                Id = id,
                Code = $"PRD-{id:N}"[..12],
                Name = "Test Product",
                OwnerPersonId = Guid.NewGuid(),
                Status = isActive ? "ACTIVE" : "INACTIVE",
                CreatedAt = DateTime.UtcNow
            };
        }

        public Task<ProductDto?> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_products.TryGetValue(productId, out var p) ? p : null);

        public Task<ProductDto?> GetProductByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(_products.Values.FirstOrDefault(p => p.Code == code));

        public Task<IReadOnlyList<ProductDto>> ListActiveProductsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductDto>>(_products.Values.Where(p => p.IsActive).ToList());

        public Task<IReadOnlyList<ProductDto>> ListAllProductsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductDto>>(_products.Values.ToList());

        public Task<bool> IsProductActiveAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_products.TryGetValue(productId, out var p) && p.IsActive);
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

    private sealed class FakeCurrentContextProvider : ICurrentContextProvider
    {
        public FakeCurrentContextProvider(Guid? currentPersonId)
        {
            CurrentPersonId = currentPersonId;
            CurrentUserId = Guid.NewGuid();
        }

        public Guid? CurrentUserId { get; set; }
        public Guid? CurrentPersonId { get; set; }
        public IReadOnlyCollection<string> CurrentRoles { get; set; } = Array.Empty<string>();
    }

    private sealed class FakeSystemClock : ISystemClock
    {
        public FakeSystemClock(DateTime initialUtc)
        {
            UtcNow = initialUtc;
        }

        public DateTime UtcNow { get; private set; }

        public void AdvanceMinutes(double minutes) => UtcNow = UtcNow.AddMinutes(minutes);
    }
}
