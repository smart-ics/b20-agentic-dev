using Cakra.Core;
using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Cakra.Modules.Product;
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
/// Unit tests for CR-030 (P1-S02):
/// - Atomic initial owner assignment during request intake in Request.Record and RequestService
/// - Lifecycle transition to ASSIGNED when OwnerPersonId is supplied
/// - Unassigned fallback to CAPTURED state when OwnerPersonId is omitted
/// - Authoritative initial RequestAssignment audit creation
/// - Person validation gating via IOrganizationQueryService
/// </summary>
public sealed class RecordRequestCommandTests
{
    [Fact]
    public void Domain_Record_with_initial_owner_creates_request_in_assigned_state_with_audit_entry()
    {
        var id = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var now = new DateTime(2026, 10, 15, 8, 30, 0, DateTimeKind.Utc);

        var request = RequestAggregate.Record(
            id: id,
            title: "Configure HL7 integration",
            description: "Setup receiver endpoint",
            requestType: "FEATURE",
            actorPersonId: actorId,
            utcNow: now,
            ownerPersonId: ownerId);

        request.Status.Should().Be(RequestStatus.Assigned);
        request.OwnerPersonId.Should().Be(ownerId);
        request.Assignments.Should().ContainSingle();

        var assignment = request.Assignments.First();
        assignment.RequestId.Should().Be(id);
        assignment.PreviousOwnerPersonId.Should().BeNull();
        assignment.AssignedOwnerPersonId.Should().Be(ownerId);
        assignment.ActorPersonId.Should().Be(actorId);
        assignment.PreviousStatus.Should().BeNull();
        assignment.NewStatus.Should().Be(RequestStatus.Assigned);
        assignment.AssignedAtUtc.Should().Be(now);
        assignment.Notes.Should().Be("Request recorded with initial assigned owner.");
    }

    [Fact]
    public void Domain_Record_without_initial_owner_creates_request_in_captured_state_with_audit_entry()
    {
        var id = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var now = new DateTime(2026, 10, 15, 8, 30, 0, DateTimeKind.Utc);

        var request = RequestAggregate.Record(
            id: id,
            title: "Reported intermittent bug",
            description: "Details",
            requestType: "BUG",
            actorPersonId: actorId,
            utcNow: now,
            ownerPersonId: null);

        request.Status.Should().Be(RequestStatus.Captured);
        request.OwnerPersonId.Should().BeNull();
        request.Assignments.Should().ContainSingle();

        var assignment = request.Assignments.First();
        assignment.RequestId.Should().Be(id);
        assignment.PreviousOwnerPersonId.Should().BeNull();
        assignment.AssignedOwnerPersonId.Should().BeNull();
        assignment.ActorPersonId.Should().Be(actorId);
        assignment.PreviousStatus.Should().BeNull();
        assignment.NewStatus.Should().Be(RequestStatus.Captured);
        assignment.AssignedAtUtc.Should().Be(now);
        assignment.Notes.Should().Be("Request recorded in CAPTURED state.");
    }

    [Fact]
    public void Domain_Record_with_empty_guid_owner_throws_validation_exception()
    {
        var act = () => RequestAggregate.Record(
            id: Guid.NewGuid(),
            title: "Invalid request",
            description: null,
            requestType: "GENERAL",
            actorPersonId: Guid.NewGuid(),
            ownerPersonId: Guid.Empty);

        act.Should().Throw<RequestDomainValidationException>()
            .WithMessage("*OwnerPersonId cannot be empty*");
    }

    [Fact]
    public async Task Service_RecordRequestAsync_with_valid_owner_transitions_to_assigned_and_persists_audit()
    {
        var (service, repo, orgQuery) = CreateServiceTestContext();
        var actorId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        orgQuery.SetPerson(actorId, isActive: true);
        orgQuery.SetPerson(ownerId, isActive: true);

        var command = new RecordRequestCommand(
            Title: "Setup patient portal module",
            Description: "Configure OAuth2 redirect",
            RequestType: "FEATURE",
            Priority: "HIGH",
            ActorPersonId: actorId,
            OwnerPersonId: ownerId);

        var result = await service.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be("ASSIGNED");
        result.OwnerPersonId.Should().Be(ownerId);
        result.Assignments.Should().ContainSingle();

        var dtoAssignment = result.Assignments[0];
        dtoAssignment.AssignedOwnerPersonId.Should().Be(ownerId);
        dtoAssignment.NewStatus.Should().Be("ASSIGNED");
        dtoAssignment.Notes.Should().Be("Request recorded with initial assigned owner.");

        repo.Requests.Should().ContainSingle(r => r.Id == result.Id && r.OwnerPersonId == ownerId && r.Status == RequestStatus.Assigned);
        repo.Assignments.Should().ContainSingle(a => a.RequestId == result.Id && a.AssignedOwnerPersonId == ownerId && a.NewStatus == RequestStatus.Assigned);
    }

    [Fact]
    public async Task Service_RecordRequestAsync_without_owner_records_in_captured_state_unassigned()
    {
        var (service, repo, orgQuery) = CreateServiceTestContext();
        var actorId = Guid.NewGuid();
        orgQuery.SetPerson(actorId, isActive: true);

        var command = new RecordRequestCommand(
            Title: "General inquiry on license renewal",
            Description: null,
            RequestType: "GENERAL",
            Priority: "NORMAL",
            ActorPersonId: actorId,
            OwnerPersonId: null);

        var result = await service.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be("CAPTURED");
        result.OwnerPersonId.Should().BeNull();
        result.Assignments.Should().ContainSingle();

        var dtoAssignment = result.Assignments[0];
        dtoAssignment.AssignedOwnerPersonId.Should().BeNull();
        dtoAssignment.NewStatus.Should().Be("CAPTURED");
        dtoAssignment.Notes.Should().Be("Request recorded in CAPTURED state.");

        repo.Requests.Should().ContainSingle(r => r.Id == result.Id && r.OwnerPersonId == null && r.Status == RequestStatus.Captured);
        repo.Assignments.Should().ContainSingle(a => a.RequestId == result.Id && a.AssignedOwnerPersonId == null && a.NewStatus == RequestStatus.Captured);
    }

    [Fact]
    public async Task Service_RecordRequestAsync_with_non_existent_owner_throws_key_not_found()
    {
        var (service, repo, orgQuery) = CreateServiceTestContext();
        var actorId = Guid.NewGuid();
        var nonExistentOwnerId = Guid.NewGuid();
        orgQuery.SetPerson(actorId, isActive: true);

        var command = new RecordRequestCommand(
            Title: "Task for missing person",
            Description: "Details",
            ActorPersonId: actorId,
            OwnerPersonId: nonExistentOwnerId);

        var act = async () => await service.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{nonExistentOwnerId}*");

        repo.Requests.Should().BeEmpty();
        repo.Assignments.Should().BeEmpty();
    }

    [Fact]
    public async Task Service_RecordRequestAsync_with_inactive_owner_throws_invalid_operation()
    {
        var (service, repo, orgQuery) = CreateServiceTestContext();
        var actorId = Guid.NewGuid();
        var inactiveOwnerId = Guid.NewGuid();
        orgQuery.SetPerson(actorId, isActive: true);
        orgQuery.SetPerson(inactiveOwnerId, isActive: false);

        var command = new RecordRequestCommand(
            Title: "Task for inactive person",
            Description: "Details",
            ActorPersonId: actorId,
            OwnerPersonId: inactiveOwnerId);

        var act = async () => await service.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Person '{inactiveOwnerId}' is not active*");

        repo.Requests.Should().BeEmpty();
        repo.Assignments.Should().BeEmpty();
    }

    private static (RequestService Service, InMemoryRequestRepository Repo, FakeOrganizationQueryService OrgQuery) CreateServiceTestContext()
    {
        var repo = new InMemoryRequestRepository();
        var orgQuery = new FakeOrganizationQueryService();
        var customerQuery = new FakeCustomerQueryService();
        var productQuery = new FakeProductQueryService();
        var clock = new FakeSystemClock(new DateTime(2026, 10, 15, 9, 0, 0, DateTimeKind.Utc));

        var service = new RequestService(
            repo,
            orgQuery,
            customerQuery,
            productQuery,
            clock: clock);

        return (service, repo, orgQuery);
    }

    private sealed class InMemoryRequestRepository : IRequestRepository
    {
        private readonly Dictionary<Guid, RequestAggregate> _requests = new();
        private readonly Dictionary<Guid, RequestResolution> _resolutions = new();
        public List<RequestAssignment> Assignments { get; } = new();

        public Task<RequestAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (!_requests.TryGetValue(id, out var req))
            {
                return Task.FromResult<RequestAggregate?>(null);
            }

            _resolutions.TryGetValue(id, out var resolution);
            var reqAssignments = Assignments.Where(a => a.RequestId == id).OrderBy(a => a.AssignedAtUtc).ToList();

            var hydrated = RequestAggregate.Rehydrate(
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

            return Task.FromResult<RequestAggregate?>(hydrated);
        }

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
            Task.FromResult(_resolutions.GetValueOrDefault(requestId));

        public Task UpdateResolutionAsync(RequestResolution resolution, CancellationToken cancellationToken = default)
        {
            _resolutions[resolution.RequestId] = resolution;
            return Task.CompletedTask;
        }

        public Task<RequestAggregate?> GetActiveInProgressByOwnerAsync(Guid ownerPersonId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_requests.Values.FirstOrDefault(r => r.OwnerPersonId == ownerPersonId && r.Status == RequestStatus.InProgress));

        public IReadOnlyList<RequestAggregate> Requests => _requests.Values.ToList();
    }

    private sealed class FakeOrganizationQueryService : IOrganizationQueryService
    {
        private readonly Dictionary<Guid, PersonDto> _persons = new();
        private readonly Dictionary<Guid, List<string>> _roles = new();

        public void SetPerson(Guid id, bool isActive = true)
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

        public void SetRoles(Guid personId, params string[] roles) =>
            _roles[personId] = roles.ToList();

        public Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_persons.TryGetValue(personId, out var p) && p.IsActive);

        public Task<PersonDto?> GetPersonByIdAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_persons.TryGetValue(personId, out var p) ? p : null);

        public Task<IReadOnlyList<string>> GetPersonRolesAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(_roles.TryGetValue(personId, out var r) ? r : Array.Empty<string>());

        public Task<IReadOnlyList<PersonDto>> ListActivePersonsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PersonDto>>(_persons.Values.Where(p => p.IsActive).ToList());

        public Task<IReadOnlyList<PersonDto>> ListAllPersonsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PersonDto>>(_persons.Values.ToList());
    }

    private sealed class FakeCustomerQueryService : ICustomerQueryService
    {
        private readonly Dictionary<Guid, CustomerDto> _customers = new();

        public void SetCustomer(Guid id, bool isActive = true)
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

        public void SetProduct(Guid id, bool isActive = true)
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
