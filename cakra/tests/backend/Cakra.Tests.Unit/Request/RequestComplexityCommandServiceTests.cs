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
using RequestAggregate = Cakra.Modules.Request.Domain.Request;

namespace Cakra.Tests.Unit.Request;

/// <summary>
/// Unit tests for P3-S03:
/// - Request complexity application commands and FluentValidation rules
/// - RequestService handling of UpdateRequestComplexityCommand, RecordRequestCommand, and EvaluateRequestCommand
/// - Role-based authorization enforcement (Programmer, Administrator, Admin, Developer, Team Lead, Manager)
/// - Domain event emission (RequestComplexityUpdated)
/// </summary>
public sealed class RequestComplexityCommandServiceTests
{
    [Fact]
    public void UpdateRequestComplexityCommand_and_validator_are_public()
    {
        typeof(UpdateRequestComplexityCommand).IsPublic.Should().BeTrue();
        typeof(UpdateRequestComplexityCommandValidator).IsPublic.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void UpdateRequestComplexityCommandValidator_accepts_valid_complexity_range(int complexity)
    {
        var validator = new UpdateRequestComplexityCommandValidator();
        var command = new UpdateRequestComplexityCommand(Guid.NewGuid(), complexity, "Triage assessment adjustment");
        var result = validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    [InlineData(99)]
    public void UpdateRequestComplexityCommandValidator_rejects_out_of_range_complexity(int complexity)
    {
        var validator = new UpdateRequestComplexityCommandValidator();
        var command = new UpdateRequestComplexityCommand(Guid.NewGuid(), complexity);
        var result = validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRequestComplexityCommand.Complexity));
    }

    [Fact]
    public void UpdateRequestComplexityCommandValidator_validates_request_id_actor_and_reason_length()
    {
        var validator = new UpdateRequestComplexityCommandValidator();

        // Empty RequestId
        var emptyReq = validator.Validate(new UpdateRequestComplexityCommand(Guid.Empty, 3));
        emptyReq.IsValid.Should().BeFalse();
        emptyReq.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRequestComplexityCommand.RequestId));

        // Empty ActorPersonId
        var emptyActor = validator.Validate(new UpdateRequestComplexityCommand(Guid.NewGuid(), 3, ActorPersonId: Guid.Empty));
        emptyActor.IsValid.Should().BeFalse();
        emptyActor.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRequestComplexityCommand.ActorPersonId));

        // Reason > 500 chars
        var longReason = new string('A', 501);
        var longReasonRes = validator.Validate(new UpdateRequestComplexityCommand(Guid.NewGuid(), 3, Reason: longReason));
        longReasonRes.IsValid.Should().BeFalse();
        longReasonRes.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRequestComplexityCommand.Reason));

        // Valid reason = 500 chars
        var maxReason = new string('A', 500);
        var maxReasonRes = validator.Validate(new UpdateRequestComplexityCommand(Guid.NewGuid(), 3, Reason: maxReason));
        maxReasonRes.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(6, false)]
    public void RecordRequestCommandValidator_validates_optional_complexity(int? complexity, bool isValid)
    {
        var validator = new RecordRequestCommandValidator();
        var command = new RecordRequestCommand("Title", "Description", Complexity: complexity);
        var result = validator.Validate(command);
        result.IsValid.Should().Be(isValid);
    }



    [Theory]
    [InlineData("Programmer")]
    [InlineData("Administrator")]
    [InlineData("Admin")]
    [InlineData("Developer")]
    [InlineData("Team Lead")]
    [InlineData("Manager")]
    public async Task UpdateRequestComplexity_succeeds_when_actor_has_authorized_role(string roleName)
    {
        var (service, repo, orgQuery, dispatcher) = CreateTestSetup();
        var actorId = Guid.NewGuid();
        orgQuery.SetPerson(actorId, isActive: true, roles: new[] { roleName });

        var request = RequestAggregate.Record(
            Guid.NewGuid(), "Sample Request", "Description", "Bug", actorId);
        await repo.AddAsync(request);

        var result = await service.Handle(
            new UpdateRequestComplexityCommand(request.Id, 4, "High architectural complexity", actorId),
            CancellationToken.None);

        result.Should().NotBeNull();
        result.Complexity.Should().Be(4);

        var persisted = await repo.GetByIdAsync(request.Id);
        persisted!.Complexity.Should().Be(4);

        dispatcher.Events.OfType<RequestComplexityUpdated>().Should().ContainSingle(e =>
            e.RequestId == request.Id &&
            e.PreviousComplexity == 1 &&
            e.NewComplexity == 4 &&
            e.ActorPersonId == actorId &&
            e.Reason == "High architectural complexity");
    }

    [Theory]
    [InlineData("Viewer")]
    [InlineData("Auditor")]
    [InlineData("Guest")]
    [InlineData("Sales")]
    public async Task UpdateRequestComplexity_throws_UnauthorizedAccessException_when_actor_lacks_authorized_role(string roleName)
    {
        var (service, repo, orgQuery, _) = CreateTestSetup();
        var actorId = Guid.NewGuid();
        orgQuery.SetPerson(actorId, isActive: true, roles: new[] { roleName });

        var request = RequestAggregate.Record(
            Guid.NewGuid(), "Sample Request", "Description", "Bug", actorId);
        await repo.AddAsync(request);

        var act = async () => await service.Handle(
            new UpdateRequestComplexityCommand(request.Id, 4, "High architectural complexity", actorId),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage($"*Actor '{actorId}' does not possess an authorized role*");
    }

    [Fact]
    public async Task UpdateRequestComplexity_throws_when_request_is_closed()
    {
        var (service, repo, orgQuery, _) = CreateTestSetup();
        var actorId = Guid.NewGuid();
        orgQuery.SetPerson(actorId, isActive: true, roles: new[] { "Programmer" });

        var request = RequestAggregate.Record(
            Guid.NewGuid(), "Sample Request", "Description", "Bug", actorId);
        request.AssignOwner(actorId, actorId);
        request.StartWork(actorId);
        request.Complete("Done", actorId);
        await repo.AddAsync(request);

        var act = async () => await service.Handle(
            new UpdateRequestComplexityCommand(request.Id, 3, "Attempting update on completed", actorId),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidRequestStateTransitionException>();
    }

    [Fact]
    public async Task UpdateRequestComplexity_is_idempotent_when_value_unchanged()
    {
        var (service, repo, orgQuery, dispatcher) = CreateTestSetup();
        var actorId = Guid.NewGuid();
        orgQuery.SetPerson(actorId, isActive: true, roles: new[] { "Programmer" });

        var request = RequestAggregate.Record(
            Guid.NewGuid(), "Sample Request", "Description", "Bug", actorId);
        await repo.AddAsync(request);

        // Current complexity is 1, updating to 1
        var result = await service.Handle(
            new UpdateRequestComplexityCommand(request.Id, 1, "No change", actorId),
            CancellationToken.None);

        result.Complexity.Should().Be(1);
        dispatcher.Events.OfType<RequestComplexityUpdated>().Should().BeEmpty();
    }

    [Fact]
    public async Task RecordRequest_with_custom_complexity_succeeds_when_authorized()
    {
        var (service, _, orgQuery, _) = CreateTestSetup();
        var actorId = Guid.NewGuid();
        orgQuery.SetPerson(actorId, isActive: true, roles: new[] { "Developer" });

        var result = await service.Handle(
            new RecordRequestCommand("New Complex Feature", "Details", ActorPersonId: actorId, Complexity: 5),
            CancellationToken.None);

        result.Complexity.Should().Be(5);
    }

    [Fact]
    public async Task RecordRequest_with_custom_complexity_throws_UnauthorizedAccessException_when_unauthorized()
    {
        var (service, _, orgQuery, _) = CreateTestSetup();
        var actorId = Guid.NewGuid();
        orgQuery.SetPerson(actorId, isActive: true, roles: new[] { "Viewer" });

        var act = async () => await service.Handle(
            new RecordRequestCommand("New Complex Feature", "Details", ActorPersonId: actorId, Complexity: 5),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RecordRequest_with_default_complexity_succeeds_even_for_unprivileged_actor()
    {
        var (service, _, orgQuery, _) = CreateTestSetup();
        var actorId = Guid.NewGuid();
        orgQuery.SetPerson(actorId, isActive: true, roles: new[] { "Viewer" });

        var result = await service.Handle(
            new RecordRequestCommand("Simple Bug", "Details", ActorPersonId: actorId, Complexity: null),
            CancellationToken.None);

        result.Complexity.Should().Be(1);
    }

    private static (RequestService Service, InMemoryRequestRepository Repo, FakeOrganizationQueryService OrgQuery, RecordingDomainEventDispatcher Dispatcher) CreateTestSetup()
    {
        var repo = new InMemoryRequestRepository();
        var orgQuery = new FakeOrganizationQueryService();
        var customerQuery = new FakeCustomerQueryService();
        var productQuery = new FakeProductQueryService();
        var dispatcher = new RecordingDomainEventDispatcher();
        var clock = new FakeSystemClock(DateTime.UtcNow);

        var service = new RequestService(
            repo,
            orgQuery,
            customerQuery,
            productQuery,
            dispatcher,
            currentContextProvider: null,
            auditContext: null,
            clock: clock);

        return (service, repo, orgQuery, dispatcher);
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
                req.Complexity,
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

        public Task<IReadOnlyList<RequestAssignment>> GetAssignmentsByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestAssignment>>(
                Assignments.Where(a => a.RequestId == requestId).OrderBy(a => a.AssignedAtUtc).ToList());

        public Task AddResolutionAsync(RequestResolution resolution, CancellationToken cancellationToken = default)
        {
            _resolutions[resolution.RequestId] = resolution;
            return Task.CompletedTask;
        }

        public Task<RequestResolution?> GetResolutionByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_resolutions.TryGetValue(requestId, out var res) ? res : null);

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
