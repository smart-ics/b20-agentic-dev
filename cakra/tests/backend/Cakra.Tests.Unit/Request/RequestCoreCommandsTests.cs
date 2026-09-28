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
        typeof(EvaluateRequestCommand).IsPublic.Should().BeTrue();

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

        // 2. AssignRequestOwner (validates assignee, transitions to EVALUATING, records audit, emits RequestAssigned)
        clock.AdvanceMinutes(15);
        var assigned = await service.Handle(
            new AssignRequestOwnerCommand(
                RequestId: recorded.Id,
                OwnerPersonId: ownerId,
                Notes: "Assigned to hospital integration specialist"),
            CancellationToken.None);

        assigned.Status.Should().Be(RequestStatusNames.Evaluating);
        assigned.OwnerPersonId.Should().Be(ownerId);
        assigned.Assignments.Should().HaveCount(2);
        assigned.Assignments[1].PreviousStatus.Should().Be(RequestStatusNames.Captured);
        assigned.Assignments[1].NewStatus.Should().Be(RequestStatusNames.Evaluating);
        assigned.Assignments[1].AssignedOwnerPersonId.Should().Be(ownerId);
        assigned.Assignments[1].ActorPersonId.Should().Be(actorId);

        repo.Assignments.Should().HaveCount(2);
        repo.Assignments.Last().NewStatus.Should().Be(RequestStatus.Evaluating);

        dispatcher.Events.OfType<RequestAssigned>().Should().ContainSingle(e =>
            e.RequestId == recorded.Id &&
            e.OwnerPersonId == ownerId &&
            e.PreviousStatus == RequestStatus.Captured &&
            e.NewStatus == RequestStatus.Evaluating);

        // 3. EvaluateRequest (records evaluation notes, emits RequestEvaluated)
        clock.AdvanceMinutes(20);
        contextProvider.CurrentPersonId = ownerId;

        var evaluated = await service.Handle(
            new EvaluateRequestCommand(
                RequestId: recorded.Id,
                EvaluationNotes: "Root cause is static HttpClient timeout of 5s; configurable retry policy needed."),
            CancellationToken.None);

        evaluated.Status.Should().Be(RequestStatusNames.Evaluating);
        evaluated.EvaluationNotes.Should().Be("Root cause is static HttpClient timeout of 5s; configurable retry policy needed.");

        dispatcher.Events.OfType<RequestEvaluated>().Should().ContainSingle(e =>
            e.RequestId == recorded.Id &&
            e.EvaluatedByPersonId == ownerId &&
            e.EvaluationNotes.Contains("HttpClient timeout"));
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

        // Evaluate before AssignOwner (still CAPTURED) -> InvalidRequestStateTransitionException
        var prematureEvaluateAct = async () => await service.EvaluateRequest(
            created.Id, "Evaluating prematurely");
        await prematureEvaluateAct.Should().ThrowAsync<InvalidRequestStateTransitionException>();
    }

    [Fact]
    public void Command_validators_enforce_required_fields()
    {
        var recordValidator = new RecordRequestCommandValidator();
        recordValidator.Validate(new RecordRequestCommand("", "Desc")).IsValid.Should().BeFalse();
        recordValidator.Validate(new RecordRequestCommand("Title", "")).IsValid.Should().BeFalse();
        recordValidator.Validate(new RecordRequestCommand("Title", "Desc", CustomerId: Guid.Empty)).IsValid.Should().BeFalse();
        recordValidator.Validate(new RecordRequestCommand("Title", "Desc", ProductId: Guid.Empty)).IsValid.Should().BeFalse();
        recordValidator.Validate(new RecordRequestCommand("Title", "Desc", Guid.NewGuid(), Guid.NewGuid())).IsValid.Should().BeTrue();

        var assignValidator = new AssignRequestOwnerCommandValidator();
        assignValidator.Validate(new AssignRequestOwnerCommand(Guid.Empty, Guid.NewGuid())).IsValid.Should().BeFalse();
        assignValidator.Validate(new AssignRequestOwnerCommand(Guid.NewGuid(), Guid.Empty)).IsValid.Should().BeFalse();
        assignValidator.Validate(new AssignRequestOwnerCommand(Guid.NewGuid(), Guid.NewGuid())).IsValid.Should().BeTrue();

        var evaluateValidator = new EvaluateRequestCommandValidator();
        evaluateValidator.Validate(new EvaluateRequestCommand(Guid.Empty, "Notes")).IsValid.Should().BeFalse();
        evaluateValidator.Validate(new EvaluateRequestCommand(Guid.NewGuid(), " ")).IsValid.Should().BeFalse();
        evaluateValidator.Validate(new EvaluateRequestCommand(Guid.NewGuid(), "Valid triage notes")).IsValid.Should().BeTrue();
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
                req.EscalationReason,
                req.ManagementDecisionNotes,
                req.CreatedAt,
                req.UpdatedAt,
                resolution,
                reqAssignments);

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
