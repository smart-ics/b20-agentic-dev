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
/// Unit tests for Request Module Reassignment Commands (Architecture CR-016 TD-003):
/// - Published visibility of ReassignRequestOwnershipCommand
/// - ReassignRequestOwnership from IN_PROGRESS and PAUSED resets status to ASSIGNED, audit logging, RequestAssigned event
/// - ReassignRequestOwnership owner validation via IOrganizationQueryService
/// - Invalid state transitions and FluentValidation rules
/// </summary>
public sealed class RequestEscalationAndManagementTests
{
    [Fact]
    public void Reassign_command_and_validator_are_publicly_accessible()
    {
        typeof(ReassignRequestOwnershipCommand).IsPublic.Should().BeTrue();
        typeof(ReassignRequestOwnershipCommandValidator).IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task ReassignRequestOwnership_from_InProgress_and_Paused_resets_status_to_Assigned_and_records_audit()
    {
        var recorderId = Guid.NewGuid();
        var firstOwnerId = Guid.NewGuid();
        var secondOwnerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        var repo = new InMemoryRequestRepository();
        var orgQuery = new FakeOrganizationQueryService();
        orgQuery.SetPerson(recorderId, isActive: true);
        orgQuery.SetPerson(firstOwnerId, isActive: true);
        orgQuery.SetPerson(secondOwnerId, isActive: true);
        orgQuery.SetPerson(managerId, isActive: true);

        var dispatcher = new RecordingDomainEventDispatcher();
        var contextProvider = new FakeCurrentContextProvider(recorderId);
        var clock = new FakeSystemClock(new DateTime(2026, 9, 28, 11, 0, 0, DateTimeKind.Utc));

        var service = new RequestService(
            repo,
            orgQuery,
            new FakeCustomerQueryService(),
            new FakeProductQueryService(),
            dispatcher,
            contextProvider,
            auditContext: null,
            clock: clock);

        var recorded = await service.RecordRequest("Pharmacy inventory discrepancy", "Batch stock mismatch");
        await service.AssignRequestOwner(recorded.Id, firstOwnerId);
        await service.StartWorkAsync(recorded.Id, actorPersonId: firstOwnerId);

        // 1. Reassign from IN_PROGRESS -> resets to ASSIGNED (TD-003)
        clock.AdvanceMinutes(20);
        contextProvider.CurrentPersonId = managerId;

        var reassignedFromInProgress = await service.Handle(
            new ReassignRequestOwnershipCommand(
                RequestId: recorded.Id,
                NewOwnerPersonId: secondOwnerId,
                Notes: "Reassigned to senior database architect"),
            CancellationToken.None);

        reassignedFromInProgress.Status.Should().Be(RequestStatusNames.Assigned);
        reassignedFromInProgress.OwnerPersonId.Should().Be(secondOwnerId);

        var audit1 = repo.Assignments.Last(a => a.RequestId == recorded.Id);
        audit1.PreviousOwnerPersonId.Should().Be(firstOwnerId);
        audit1.AssignedOwnerPersonId.Should().Be(secondOwnerId);
        audit1.PreviousStatus.Should().Be(RequestStatus.InProgress);
        audit1.NewStatus.Should().Be(RequestStatus.Assigned);
        audit1.ActorPersonId.Should().Be(managerId);

        dispatcher.Events.OfType<RequestAssigned>().Last().Should().BeEquivalentTo(new
        {
            RequestId = recorded.Id,
            OwnerPersonId = secondOwnerId,
            PreviousOwnerPersonId = (Guid?)firstOwnerId,
            ActorPersonId = managerId,
            PreviousStatus = RequestStatus.InProgress,
            NewStatus = RequestStatus.Assigned
        }, options => options.ExcludingMissingMembers());

        // 2. Second owner starts work, then pauses work
        clock.AdvanceMinutes(10);
        contextProvider.CurrentPersonId = secondOwnerId;
        await service.StartWorkAsync(recorded.Id, actorPersonId: secondOwnerId);
        await service.PauseWorkAsync(recorded.Id, "Blocked on vendor", actorPersonId: secondOwnerId);

        // 3. Reassign from PAUSED -> resets to ASSIGNED (TD-003)
        var thirdOwnerId = Guid.NewGuid();
        orgQuery.SetPerson(thirdOwnerId, isActive: true);
        contextProvider.CurrentPersonId = managerId;

        var reassignedFromPaused = await service.ReassignRequestOwnership(
            requestId: recorded.Id,
            newOwnerPersonId: thirdOwnerId,
            notes: "Reassigning paused request");

        reassignedFromPaused.Status.Should().Be(RequestStatusNames.Assigned);
        reassignedFromPaused.OwnerPersonId.Should().Be(thirdOwnerId);

        var audit2 = repo.Assignments.Last(a => a.RequestId == recorded.Id);
        audit2.PreviousStatus.Should().Be(RequestStatus.Paused);
        audit2.NewStatus.Should().Be(RequestStatus.Assigned);
    }

    [Fact]
    public async Task ReassignRequestOwnership_validates_assignee_and_rejects_invalid_assignees()
    {
        var actorId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var inactivePersonId = Guid.NewGuid();

        var repo = new InMemoryRequestRepository();
        var orgQuery = new FakeOrganizationQueryService();
        orgQuery.SetPerson(actorId, isActive: true);
        orgQuery.SetPerson(ownerId, isActive: true);
        orgQuery.SetPerson(inactivePersonId, isActive: false);

        var service = new RequestService(
            repo,
            orgQuery,
            new FakeCustomerQueryService(),
            new FakeProductQueryService(),
            currentContextProvider: new FakeCurrentContextProvider(actorId));

        var recorded = await service.RecordRequest("Title", "Desc");
        await service.AssignRequestOwner(recorded.Id, ownerId);

        // Rejects unknown assignee
        var unknownAssigneeAct = async () => await service.ReassignRequestOwnership(recorded.Id, Guid.NewGuid());
        await unknownAssigneeAct.Should().ThrowAsync<KeyNotFoundException>();

        // Rejects inactive assignee
        var inactiveAssigneeAct = async () => await service.ReassignRequestOwnership(recorded.Id, inactivePersonId);
        await inactiveAssigneeAct.Should().ThrowAsync<InvalidOperationException>();

        // Rejects identical assignee
        var sameAssigneeAct = async () => await service.ReassignRequestOwnership(recorded.Id, ownerId);
        await sameAssigneeAct.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReassignRequestOwnership_on_closed_request_throws_InvalidRequestStateTransitionException()
    {
        var actorId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();

        var repo = new InMemoryRequestRepository();
        var orgQuery = new FakeOrganizationQueryService();
        orgQuery.SetPerson(actorId, isActive: true);
        orgQuery.SetPerson(ownerId, isActive: true);
        orgQuery.SetPerson(otherOwnerId, isActive: true);

        var service = new RequestService(
            repo,
            orgQuery,
            new FakeCustomerQueryService(),
            new FakeProductQueryService(),
            currentContextProvider: new FakeCurrentContextProvider(actorId));

        // 1. Completed request rejects ReassignRequestOwnership
        var req1 = await service.RecordRequest("Title 1", "Desc 1");
        await service.AssignRequestOwner(req1.Id, ownerId);
        await service.StartWorkAsync(req1.Id, actorPersonId: ownerId);
        await service.ReviewRequestCompletion(req1.Id, "Completed");

        var reassignCompleted = async () => await service.ReassignRequestOwnership(req1.Id, otherOwnerId);
        await reassignCompleted.Should().ThrowAsync<InvalidRequestStateTransitionException>();

        // 2. Cancelled request rejects ReassignRequestOwnership
        var req2 = await service.RecordRequest("Title 2", "Desc 2");
        await service.CancelRequestAsync(req2.Id, "Cancelled reason", actorPersonId: ownerId);

        var reassignCancelled = async () => await service.ReassignRequestOwnership(req2.Id, otherOwnerId);
        await reassignCancelled.Should().ThrowAsync<InvalidRequestStateTransitionException>();
    }

    [Fact]
    public void Reassign_command_validator_enforces_required_fields()
    {
        var reassignValidator = new ReassignRequestOwnershipCommandValidator();
        reassignValidator.Validate(new ReassignRequestOwnershipCommand(Guid.Empty, Guid.NewGuid())).IsValid.Should().BeFalse();
        reassignValidator.Validate(new ReassignRequestOwnershipCommand(Guid.NewGuid(), Guid.Empty)).IsValid.Should().BeFalse();
        reassignValidator.Validate(new ReassignRequestOwnershipCommand(Guid.NewGuid(), Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    private sealed class InMemoryRequestRepository : IRequestRepository
    {
        private readonly Dictionary<Guid, Cakra.Modules.Request.Domain.Request> _requests = new();
        public Dictionary<Guid, RequestResolution> Resolutions { get; } = new();
        public List<RequestAssignment> Assignments { get; } = new();

        public Task<Cakra.Modules.Request.Domain.Request?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (!_requests.TryGetValue(id, out var req))
            {
                return Task.FromResult<Cakra.Modules.Request.Domain.Request?>(null);
            }

            Resolutions.TryGetValue(id, out var resolution);
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
            Resolutions[resolution.RequestId] = resolution;
            return Task.CompletedTask;
        }

        public Task<RequestResolution?> GetResolutionByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Resolutions.TryGetValue(requestId, out var res) ? res : null);

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

        public Task<bool> IsCustomerActiveAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeProductQueryService : IProductQueryService
    {
        public Task<ProductDto?> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductDto?>(null);

        public Task<ProductDto?> GetProductByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductDto?>(null);

        public Task<IReadOnlyList<ProductDto>> ListActiveProductsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductDto>>(Array.Empty<ProductDto>());

        public Task<IReadOnlyList<ProductDto>> ListAllProductsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductDto>>(Array.Empty<ProductDto>());

        public Task<bool> IsProductActiveAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
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
