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
/// Unit tests for CR-018 (P1-S02):
/// - UpdateRequestCoreAttributesCommand and UpdateRequestCoreAttributesCommandValidator
/// - RequestService.UpdateRequestCoreAttributesAsync and MediatR Handle
/// - Role and ownership authorization gating
/// - Domain event emission and state persistence
/// </summary>
public sealed class RequestCoreAttributesCommandServiceTests
{
    [Fact]
    public void UpdateRequestCoreAttributesCommand_and_validator_are_public()
    {
        typeof(UpdateRequestCoreAttributesCommand).IsPublic.Should().BeTrue();
        typeof(UpdateRequestCoreAttributesCommandValidator).IsPublic.Should().BeTrue();
    }

    [Fact]
    public void Validator_passes_valid_command()
    {
        var validator = new UpdateRequestCoreAttributesCommandValidator();
        var command = new UpdateRequestCoreAttributesCommand(
            RequestId: Guid.NewGuid(),
            Title: "Valid Title",
            Description: "Valid Description",
            Priority: "HIGH",
            RequestType: "BUG",
            ActorPersonId: Guid.NewGuid());

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_rejects_empty_request_id()
    {
        var validator = new UpdateRequestCoreAttributesCommandValidator();
        var command = new UpdateRequestCoreAttributesCommand(
            RequestId: Guid.Empty,
            Title: "Valid Title",
            Description: "Valid Description",
            Priority: "NORMAL",
            RequestType: "GENERAL");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRequestCoreAttributesCommand.RequestId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validator_rejects_empty_title(string? title)
    {
        var validator = new UpdateRequestCoreAttributesCommandValidator();
        var command = new UpdateRequestCoreAttributesCommand(
            RequestId: Guid.NewGuid(),
            Title: title!,
            Description: "Valid Description",
            Priority: "NORMAL",
            RequestType: "GENERAL");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRequestCoreAttributesCommand.Title));
    }

    [Fact]
    public void Validator_rejects_title_exceeding_255_chars()
    {
        var validator = new UpdateRequestCoreAttributesCommandValidator();
        var longTitle = new string('x', 256);
        var command = new UpdateRequestCoreAttributesCommand(
            RequestId: Guid.NewGuid(),
            Title: longTitle,
            Description: "Valid Description",
            Priority: "NORMAL",
            RequestType: "GENERAL");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRequestCoreAttributesCommand.Title));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validator_rejects_empty_description(string? description)
    {
        var validator = new UpdateRequestCoreAttributesCommandValidator();
        var command = new UpdateRequestCoreAttributesCommand(
            RequestId: Guid.NewGuid(),
            Title: "Valid Title",
            Description: description!,
            Priority: "NORMAL",
            RequestType: "GENERAL");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRequestCoreAttributesCommand.Description));
    }

    [Theory]
    [InlineData("LOW")]
    [InlineData("NORMAL")]
    [InlineData("HIGH")]
    [InlineData("URGENT")]
    [InlineData("low")]
    [InlineData("Normal")]
    [InlineData("urgent")]
    public void Validator_accepts_valid_priorities(string priority)
    {
        var validator = new UpdateRequestCoreAttributesCommandValidator();
        var command = new UpdateRequestCoreAttributesCommand(
            RequestId: Guid.NewGuid(),
            Title: "Valid Title",
            Description: "Valid Description",
            Priority: priority,
            RequestType: "GENERAL");

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("CRITICAL")]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_rejects_invalid_priority(string priority)
    {
        var validator = new UpdateRequestCoreAttributesCommandValidator();
        var command = new UpdateRequestCoreAttributesCommand(
            RequestId: Guid.NewGuid(),
            Title: "Valid Title",
            Description: "Valid Description",
            Priority: priority,
            RequestType: "GENERAL");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRequestCoreAttributesCommand.Priority));
    }

    [Theory]
    [InlineData("GENERAL")]
    [InlineData("BUG")]
    [InlineData("FEATURE")]
    [InlineData("SUPPORT")]
    [InlineData("CHANGE_REQUEST")]
    [InlineData("INCIDENT")]
    [InlineData("bug")]
    [InlineData("feature")]
    [InlineData("change_request")]
    public void Validator_accepts_valid_request_types(string requestType)
    {
        var validator = new UpdateRequestCoreAttributesCommandValidator();
        var command = new UpdateRequestCoreAttributesCommand(
            RequestId: Guid.NewGuid(),
            Title: "Valid Title",
            Description: "Valid Description",
            Priority: "NORMAL",
            RequestType: requestType);

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("TASK")]
    [InlineData("EPIC")]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_rejects_invalid_request_type(string requestType)
    {
        var validator = new UpdateRequestCoreAttributesCommandValidator();
        var command = new UpdateRequestCoreAttributesCommand(
            RequestId: Guid.NewGuid(),
            Title: "Valid Title",
            Description: "Valid Description",
            Priority: "NORMAL",
            RequestType: requestType);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRequestCoreAttributesCommand.RequestType));
    }

    [Fact]
    public void Validator_rejects_empty_actor_person_id_guid()
    {
        var validator = new UpdateRequestCoreAttributesCommandValidator();
        var command = new UpdateRequestCoreAttributesCommand(
            RequestId: Guid.NewGuid(),
            Title: "Valid Title",
            Description: "Valid Description",
            Priority: "NORMAL",
            RequestType: "GENERAL",
            ActorPersonId: Guid.Empty);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRequestCoreAttributesCommand.ActorPersonId));
    }

    [Fact]
    public async Task UpdateRequestCoreAttributes_allows_operational_user_in_unassigned_captured_state()
    {
        var (service, repo, _, dispatcher) = CreateTestSetup();
        var actorId = Guid.NewGuid();

        var request = RequestAggregate.Record(
            Guid.NewGuid(), "Original Title", "Original Description", "GENERAL", actorId);
        await repo.AddAsync(request);

        var result = await service.UpdateRequestCoreAttributesAsync(
            request.Id,
            "Updated Title",
            "Updated Description",
            "HIGH",
            "BUG",
            actorId,
            CancellationToken.None);

        result.Title.Should().Be("Updated Title");
        result.Description.Should().Be("Updated Description");
        result.Priority.Should().Be("HIGH");
        result.RequestType.Should().Be("BUG");

        var emitted = dispatcher.Events.OfType<RequestCoreAttributesUpdated>().Single();
        emitted.RequestId.Should().Be(request.Id);
        emitted.Title.Should().Be("Updated Title");
        emitted.Description.Should().Be("Updated Description");
        emitted.Priority.Should().Be("HIGH");
        emitted.RequestType.Should().Be("BUG");
        emitted.ActorPersonId.Should().Be(actorId);
    }

    [Fact]
    public async Task UpdateRequestCoreAttributes_allows_assigned_owner()
    {
        var (service, repo, orgQuery, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        orgQuery.SetPerson(ownerId, isActive: true, roles: new[] { "Developer" });

        var request = RequestAggregate.Record(
            Guid.NewGuid(), "Original Title", "Original Description", "GENERAL", ownerId);
        request.AssignOwner(ownerId, ownerId);
        await repo.AddAsync(request);

        var result = await service.UpdateRequestCoreAttributesAsync(
            request.Id,
            "Owner Updated Title",
            "Owner Updated Description",
            "URGENT",
            "FEATURE",
            ownerId,
            CancellationToken.None);

        result.Title.Should().Be("Owner Updated Title");
        result.Priority.Should().Be("URGENT");
        result.RequestType.Should().Be("FEATURE");
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Admin")]
    [InlineData("Manager")]
    public async Task UpdateRequestCoreAttributes_allows_privileged_roles_even_if_not_owner(string role)
    {
        var (service, repo, orgQuery, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        orgQuery.SetPerson(adminId, isActive: true, roles: new[] { role });

        var request = RequestAggregate.Record(
            Guid.NewGuid(), "Original Title", "Original Description", "GENERAL", ownerId);
        request.AssignOwner(ownerId, ownerId);
        await repo.AddAsync(request);

        var result = await service.UpdateRequestCoreAttributesAsync(
            request.Id,
            "Privileged Updated Title",
            "Privileged Updated Description",
            "LOW",
            "SUPPORT",
            adminId,
            CancellationToken.None);

        result.Title.Should().Be("Privileged Updated Title");
        result.Priority.Should().Be("LOW");
        result.RequestType.Should().Be("SUPPORT");
    }

    [Fact]
    public async Task UpdateRequestCoreAttributes_throws_UnauthorizedAccessException_when_non_owner_and_non_admin_on_assigned_request()
    {
        var (service, repo, orgQuery, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        orgQuery.SetPerson(otherUserId, isActive: true, roles: new[] { "Developer" });

        var request = RequestAggregate.Record(
            Guid.NewGuid(), "Original Title", "Original Description", "GENERAL", ownerId);
        request.AssignOwner(ownerId, ownerId);
        await repo.AddAsync(request);

        var act = async () => await service.UpdateRequestCoreAttributesAsync(
            request.Id,
            "Hacker Title",
            "Hacker Description",
            "HIGH",
            "BUG",
            otherUserId,
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage($"*Actor '{otherUserId}' does not possess an authorized role or request ownership*");
    }

    [Fact]
    public async Task UpdateRequestCoreAttributes_via_mediatr_handle_succeeds()
    {
        var (service, repo, _, _) = CreateTestSetup();
        var actorId = Guid.NewGuid();

        var request = RequestAggregate.Record(
            Guid.NewGuid(), "Original Title", "Original Description", "GENERAL", actorId);
        await repo.AddAsync(request);

        var command = new UpdateRequestCoreAttributesCommand(
            request.Id,
            "MediatR Updated Title",
            "MediatR Updated Description",
            "HIGH",
            "CHANGE_REQUEST",
            actorId);

        var result = await service.Handle(command, CancellationToken.None);

        result.Title.Should().Be("MediatR Updated Title");
        result.RequestType.Should().Be("CHANGE_REQUEST");
    }

    [Fact]
    public async Task Handle_WithDeadline_SetsDeadlineToUtcMidnightAndReturnsInDto()
    {
        var (service, repo, _, _) = CreateTestSetup();
        var actorId = Guid.NewGuid();

        var request = RequestAggregate.Record(
            Guid.NewGuid(), "Original Title", "Original Description", "GENERAL", actorId);
        await repo.AddAsync(request);

        var targetDeadline = new DateTime(2026, 11, 20, 15, 30, 0, DateTimeKind.Local);
        var expectedNormalized = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);

        var command = new UpdateRequestCoreAttributesCommand(
            request.Id,
            "Updated Title",
            "Updated Description",
            "HIGH",
            "BUG",
            actorId,
            Deadline: targetDeadline);

        var result = await service.Handle(command, CancellationToken.None);

        result.Deadline.Should().Be(expectedNormalized);
        var persisted = await repo.GetByIdAsync(request.Id);
        persisted!.Deadline.Should().Be(expectedNormalized);
    }

    [Fact]
    public async Task Handle_RecordRequestCommand_WithDeadline_SetsDeadlineOnCreatedDtoAndPersists()
    {
        var (service, repo, _, _) = CreateTestSetup();
        var actorId = Guid.NewGuid();

        var targetDeadline = new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc);
        var expectedNormalized = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var command = new RecordRequestCommand(
            "New Feature",
            "Feature details",
            ActorPersonId: actorId,
            Deadline: targetDeadline);

        var result = await service.Handle(command, CancellationToken.None);

        result.Deadline.Should().Be(expectedNormalized);
        var persisted = await repo.GetByIdAsync(result.Id);
        persisted!.Deadline.Should().Be(expectedNormalized);
    }

    [Fact]
    public async Task UpdateRequestCoreAttributes_throws_when_request_is_closed()
    {
        var (service, repo, orgQuery, _) = CreateTestSetup();
        var actorId = Guid.NewGuid();
        orgQuery.SetPerson(actorId, isActive: true, roles: new[] { "Administrator" });

        var request = RequestAggregate.Record(
            Guid.NewGuid(), "Original Title", "Original Description", "GENERAL", actorId);
        request.AssignOwner(actorId, actorId);
        request.StartWork(actorId);
        request.Complete("Finished", actorId);
        await repo.AddAsync(request);

        var act = async () => await service.UpdateRequestCoreAttributesAsync(
            request.Id,
            "New Title",
            "New Description",
            "HIGH",
            "BUG",
            actorId,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidRequestStateTransitionException>();
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
