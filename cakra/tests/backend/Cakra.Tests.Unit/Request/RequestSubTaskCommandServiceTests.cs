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
/// Unit tests for CR-006 Slice P3-S03:
/// - Sub-task commands and FluentValidation validators
/// - Dual-tier authorization rules (Owner vs Assignee vs Management roles)
/// - RequestService handler execution and aggregate integration
/// - Initial sub-task support in RecordRequestCommand
/// - Query projections and DTO mapping
/// </summary>
public sealed class RequestSubTaskCommandServiceTests
{
    private static readonly DateTime TestNow = new(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc);

    // =========================================================================
    // 1. Validator Tests
    // =========================================================================

    [Fact]
    public void SubTask_commands_and_validators_are_public()
    {
        typeof(AddRequestSubTaskCommand).IsPublic.Should().BeTrue();
        typeof(AddRequestSubTaskCommandValidator).IsPublic.Should().BeTrue();
        typeof(CompleteRequestSubTaskCommand).IsPublic.Should().BeTrue();
        typeof(CompleteRequestSubTaskCommandValidator).IsPublic.Should().BeTrue();
        typeof(ReopenRequestSubTaskCommand).IsPublic.Should().BeTrue();
        typeof(ReopenRequestSubTaskCommandValidator).IsPublic.Should().BeTrue();
        typeof(RemoveRequestSubTaskCommand).IsPublic.Should().BeTrue();
        typeof(RemoveRequestSubTaskCommandValidator).IsPublic.Should().BeTrue();
        typeof(RequestSubTaskDto).IsPublic.Should().BeTrue();
        typeof(InitialSubTaskDto).IsPublic.Should().BeTrue();
    }

    [Fact]
    public void AddRequestSubTaskCommandValidator_validates_required_fields()
    {
        var validator = new AddRequestSubTaskCommandValidator();

        // Valid
        validator.Validate(new AddRequestSubTaskCommand(Guid.NewGuid(), "Design database schema")).IsValid.Should().BeTrue();

        // Empty RequestId
        var emptyReq = validator.Validate(new AddRequestSubTaskCommand(Guid.Empty, "Design database schema"));
        emptyReq.IsValid.Should().BeFalse();
        emptyReq.Errors.Should().Contain(e => e.PropertyName == nameof(AddRequestSubTaskCommand.RequestId));

        // Empty Title
        var emptyTitle = validator.Validate(new AddRequestSubTaskCommand(Guid.NewGuid(), "   "));
        emptyTitle.IsValid.Should().BeFalse();
        emptyTitle.Errors.Should().Contain(e => e.PropertyName == nameof(AddRequestSubTaskCommand.Title));

        // Title > 255 chars
        var longTitle = validator.Validate(new AddRequestSubTaskCommand(Guid.NewGuid(), new string('A', 256)));
        longTitle.IsValid.Should().BeFalse();
        longTitle.Errors.Should().Contain(e => e.PropertyName == nameof(AddRequestSubTaskCommand.Title));

        // Empty AssigneePersonId GUID
        var emptyAssignee = validator.Validate(new AddRequestSubTaskCommand(Guid.NewGuid(), "Title", AssigneePersonId: Guid.Empty));
        emptyAssignee.IsValid.Should().BeFalse();
        emptyAssignee.Errors.Should().Contain(e => e.PropertyName == nameof(AddRequestSubTaskCommand.AssigneePersonId));

        // Empty ActorPersonId GUID
        var emptyActor = validator.Validate(new AddRequestSubTaskCommand(Guid.NewGuid(), "Title", ActorPersonId: Guid.Empty));
        emptyActor.IsValid.Should().BeFalse();
        emptyActor.Errors.Should().Contain(e => e.PropertyName == nameof(AddRequestSubTaskCommand.ActorPersonId));
    }

    [Fact]
    public void CompleteRequestSubTaskCommandValidator_validates_required_fields()
    {
        var validator = new CompleteRequestSubTaskCommandValidator();

        // Valid
        validator.Validate(new CompleteRequestSubTaskCommand(Guid.NewGuid(), Guid.NewGuid())).IsValid.Should().BeTrue();

        // Empty RequestId
        validator.Validate(new CompleteRequestSubTaskCommand(Guid.Empty, Guid.NewGuid())).IsValid.Should().BeFalse();

        // Empty SubTaskId
        validator.Validate(new CompleteRequestSubTaskCommand(Guid.NewGuid(), Guid.Empty)).IsValid.Should().BeFalse();

        // Empty ActorPersonId
        validator.Validate(new CompleteRequestSubTaskCommand(Guid.NewGuid(), Guid.NewGuid(), ActorPersonId: Guid.Empty)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ReopenRequestSubTaskCommandValidator_validates_required_fields()
    {
        var validator = new ReopenRequestSubTaskCommandValidator();

        // Valid
        validator.Validate(new ReopenRequestSubTaskCommand(Guid.NewGuid(), Guid.NewGuid())).IsValid.Should().BeTrue();

        // Empty RequestId
        validator.Validate(new ReopenRequestSubTaskCommand(Guid.Empty, Guid.NewGuid())).IsValid.Should().BeFalse();

        // Empty SubTaskId
        validator.Validate(new ReopenRequestSubTaskCommand(Guid.NewGuid(), Guid.Empty)).IsValid.Should().BeFalse();

        // Empty ActorPersonId
        validator.Validate(new ReopenRequestSubTaskCommand(Guid.NewGuid(), Guid.NewGuid(), ActorPersonId: Guid.Empty)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RemoveRequestSubTaskCommandValidator_validates_required_fields()
    {
        var validator = new RemoveRequestSubTaskCommandValidator();

        // Valid
        validator.Validate(new RemoveRequestSubTaskCommand(Guid.NewGuid(), Guid.NewGuid())).IsValid.Should().BeTrue();

        // Empty RequestId
        validator.Validate(new RemoveRequestSubTaskCommand(Guid.Empty, Guid.NewGuid())).IsValid.Should().BeFalse();

        // Empty SubTaskId
        validator.Validate(new RemoveRequestSubTaskCommand(Guid.NewGuid(), Guid.Empty)).IsValid.Should().BeFalse();

        // Empty ActorPersonId
        validator.Validate(new RemoveRequestSubTaskCommand(Guid.NewGuid(), Guid.NewGuid(), ActorPersonId: Guid.Empty)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RecordRequestCommandValidator_validates_initial_subtasks()
    {
        var validator = new RecordRequestCommandValidator();

        // Valid with null initial subtasks
        validator.Validate(new RecordRequestCommand("Title", "Description")).IsValid.Should().BeTrue();

        // Valid with items
        var validCmd = new RecordRequestCommand("Title", "Description", InitialSubTasks: new[]
        {
            new InitialSubTaskDto("Task 1", Guid.NewGuid()),
            new InitialSubTaskDto("Task 2")
        });
        validator.Validate(validCmd).IsValid.Should().BeTrue();

        // Invalid item title
        var invalidTitleCmd = new RecordRequestCommand("Title", "Description", InitialSubTasks: new[]
        {
            new InitialSubTaskDto("")
        });
        validator.Validate(invalidTitleCmd).IsValid.Should().BeFalse();

        // Invalid item empty assignee GUID
        var invalidAssigneeCmd = new RecordRequestCommand("Title", "Description", InitialSubTasks: new[]
        {
            new InitialSubTaskDto("Task 1", Guid.Empty)
        });
        validator.Validate(invalidAssigneeCmd).IsValid.Should().BeFalse();
    }

    // =========================================================================
    // 2. Initial Sub-Tasks in RecordRequestCommand Flow
    // =========================================================================

    [Fact]
    public async Task RecordRequest_with_initial_subtasks_creates_and_persists_subtasks()
    {
        var (service, repository, org, _, dispatcher) = CreateTestSetup();
        var assigneeId = Guid.NewGuid();
        org.SetPerson(assigneeId, isActive: true);

        var command = new RecordRequestCommand(
            Title: "Intake with Checklist",
            Description: "Intake description",
            InitialSubTasks: new[]
            {
                new InitialSubTaskDto("First checklist item", assigneeId),
                new InitialSubTaskDto("Second checklist item", null)
            });

        var result = await service.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.TotalSubTasksCount.Should().Be(2);
        result.CompletedSubTasksCount.Should().Be(0);
        result.CompletionPercentage.Should().Be(0);
        result.SubTasks.Should().HaveCount(2);
        result.SubTasks[0].Title.Should().Be("First checklist item");
        result.SubTasks[0].AssigneePersonId.Should().Be(assigneeId);
        result.SubTasks[0].IsCompleted.Should().BeFalse();
        result.SubTasks[1].Title.Should().Be("Second checklist item");
        result.SubTasks[1].AssigneePersonId.Should().BeNull();

        // Verified persisted aggregate in repository
        var persisted = await repository.GetByIdAsync(result.Id);
        persisted.Should().NotBeNull();
        persisted!.SubTasks.Should().HaveCount(2);
        persisted.TotalSubTasksCount.Should().Be(2);

        // Events emitted
        dispatcher.Events.OfType<RequestSubTaskAdded>().Should().HaveCount(2);
    }

    // =========================================================================
    // 3. Dual-Tier Authorization Tests (TD-006)
    // =========================================================================

    [Theory]
    [InlineData("Programmer")]
    [InlineData("Administrator")]
    [InlineData("Admin")]
    [InlineData("Developer")]
    [InlineData("Team Lead")]
    [InlineData("Manager")]
    public async Task Management_operations_succeed_for_authorized_roles(string roleName)
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true);
        org.SetPerson(actorId, isActive: true, roles: new[] { roleName });

        var request = CreateActiveRequest(ownerId);
        await repository.AddAsync(request);

        // AddSubTask
        var addCmd = new AddRequestSubTaskCommand(request.Id, "Task by Role", ActorPersonId: actorId);
        var addedResult = await service.Handle(addCmd, CancellationToken.None);
        addedResult.TotalSubTasksCount.Should().Be(1);

        var subTaskId = addedResult.SubTasks[0].Id;

        // RemoveSubTask
        var removeCmd = new RemoveRequestSubTaskCommand(request.Id, subTaskId, ActorPersonId: actorId);
        var removedResult = await service.Handle(removeCmd, CancellationToken.None);
        removedResult.TotalSubTasksCount.Should().Be(0);
    }

    [Fact]
    public async Task Management_operations_succeed_for_request_owner_without_special_role()
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true, roles: Array.Empty<string>()); // No management role

        var request = CreateActiveRequest(ownerId);
        await repository.AddAsync(request);

        // AddSubTask by owner
        var addCmd = new AddRequestSubTaskCommand(request.Id, "Task by Owner", ActorPersonId: ownerId);
        var addedResult = await service.Handle(addCmd, CancellationToken.None);
        addedResult.TotalSubTasksCount.Should().Be(1);

        var subTaskId = addedResult.SubTasks[0].Id;

        // RemoveSubTask by owner
        var removeCmd = new RemoveRequestSubTaskCommand(request.Id, subTaskId, ActorPersonId: ownerId);
        var removedResult = await service.Handle(removeCmd, CancellationToken.None);
        removedResult.TotalSubTasksCount.Should().Be(0);
    }

    [Fact]
    public async Task Management_operations_fail_for_unauthorized_actor()
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        var unauthorizedActorId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true);
        org.SetPerson(unauthorizedActorId, isActive: true, roles: new[] { "Viewer", "Guest" });

        var request = CreateActiveRequest(ownerId);
        await repository.AddAsync(request);

        // AddSubTask should throw UnauthorizedAccessException
        var addAct = async () => await service.Handle(
            new AddRequestSubTaskCommand(request.Id, "Unauthorized Task", ActorPersonId: unauthorizedActorId),
            CancellationToken.None);

        await addAct.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not possess an authorized role or request ownership*");
    }

    [Fact]
    public async Task Completion_operations_succeed_for_subtask_assignee_even_if_not_owner_or_role()
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true);
        org.SetPerson(assigneeId, isActive: true, roles: Array.Empty<string>()); // Regular collaborator

        var request = CreateActiveRequest(ownerId);
        var subTask = request.AddSubTask("Assignee Task", assigneeId, ownerId, TestNow);
        await repository.AddAsync(request);

        // CompleteSubTask by assignee
        var completeCmd = new CompleteRequestSubTaskCommand(request.Id, subTask.Id, ActorPersonId: assigneeId);
        var completedResult = await service.Handle(completeCmd, CancellationToken.None);
        completedResult.CompletedSubTasksCount.Should().Be(1);
        completedResult.CompletionPercentage.Should().Be(100);

        // ReopenSubTask by assignee
        var reopenCmd = new ReopenRequestSubTaskCommand(request.Id, subTask.Id, ActorPersonId: assigneeId);
        var reopenedResult = await service.Handle(reopenCmd, CancellationToken.None);
        reopenedResult.CompletedSubTasksCount.Should().Be(0);
        reopenedResult.CompletionPercentage.Should().Be(0);
    }

    [Fact]
    public async Task Completion_operations_succeed_for_request_owner_even_if_not_assignee()
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true, roles: Array.Empty<string>());
        org.SetPerson(assigneeId, isActive: true);

        var request = CreateActiveRequest(ownerId);
        var subTask = request.AddSubTask("Assignee Task", assigneeId, ownerId, TestNow);
        await repository.AddAsync(request);

        // CompleteSubTask by owner
        var completeCmd = new CompleteRequestSubTaskCommand(request.Id, subTask.Id, ActorPersonId: ownerId);
        var completedResult = await service.Handle(completeCmd, CancellationToken.None);
        completedResult.CompletedSubTasksCount.Should().Be(1);

        // ReopenSubTask by owner
        var reopenCmd = new ReopenRequestSubTaskCommand(request.Id, subTask.Id, ActorPersonId: ownerId);
        var reopenedResult = await service.Handle(reopenCmd, CancellationToken.None);
        reopenedResult.CompletedSubTasksCount.Should().Be(0);
    }

    [Theory]
    [InlineData("Programmer")]
    [InlineData("Administrator")]
    [InlineData("Admin")]
    [InlineData("Developer")]
    [InlineData("Team Lead")]
    [InlineData("Manager")]
    public async Task Completion_operations_succeed_for_authorized_roles_even_if_neither_owner_nor_assignee(string roleName)
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var privilegedActorId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true);
        org.SetPerson(assigneeId, isActive: true);
        org.SetPerson(privilegedActorId, isActive: true, roles: new[] { roleName });

        var request = CreateActiveRequest(ownerId);
        var subTask = request.AddSubTask("Assignee Task", assigneeId, ownerId, TestNow);
        await repository.AddAsync(request);

        // CompleteSubTask by privileged actor
        var completeCmd = new CompleteRequestSubTaskCommand(request.Id, subTask.Id, ActorPersonId: privilegedActorId);
        var completedResult = await service.Handle(completeCmd, CancellationToken.None);
        completedResult.CompletedSubTasksCount.Should().Be(1);

        // ReopenSubTask by privileged actor
        var reopenCmd = new ReopenRequestSubTaskCommand(request.Id, subTask.Id, ActorPersonId: privilegedActorId);
        var reopenedResult = await service.Handle(reopenCmd, CancellationToken.None);
        reopenedResult.CompletedSubTasksCount.Should().Be(0);
    }

    [Fact]
    public async Task Completion_operations_fail_for_unauthorized_actor()
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var thirdPartyId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true);
        org.SetPerson(assigneeId, isActive: true);
        org.SetPerson(thirdPartyId, isActive: true, roles: new[] { "Auditor" });

        var request = CreateActiveRequest(ownerId);
        var subTask = request.AddSubTask("Assignee Task", assigneeId, ownerId, TestNow);
        await repository.AddAsync(request);

        // CompleteSubTask by unauthorized third party
        var completeAct = async () => await service.Handle(
            new CompleteRequestSubTaskCommand(request.Id, subTask.Id, ActorPersonId: thirdPartyId),
            CancellationToken.None);

        await completeAct.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not possess an authorized role, request ownership, or sub-task assignment*");
    }

    // =========================================================================
    // 4. Invariant and Lifecycle Enforcement Tests
    // =========================================================================

    [Fact]
    public async Task Operations_throw_KeyNotFoundException_when_request_or_subtask_does_not_exist()
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var actorId = Guid.NewGuid();
        org.SetPerson(actorId, isActive: true, roles: new[] { "Administrator" });

        // Non-existent request
        var addAct = async () => await service.Handle(
            new AddRequestSubTaskCommand(Guid.NewGuid(), "Task", ActorPersonId: actorId),
            CancellationToken.None);
        await addAct.Should().ThrowAsync<KeyNotFoundException>();

        // Existing request, non-existent sub-task
        var request = CreateActiveRequest(actorId);
        await repository.AddAsync(request);

        var completeAct = async () => await service.Handle(
            new CompleteRequestSubTaskCommand(request.Id, Guid.NewGuid(), ActorPersonId: actorId),
            CancellationToken.None);
        await completeAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Subtask_operations_throw_InvalidRequestStateTransitionException_on_closed_request()
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var actorId = Guid.NewGuid();
        org.SetPerson(actorId, isActive: true, roles: new[] { "Administrator" });

        var request = CreateActiveRequest(actorId);
        request.AddSubTask("Initial subtask", null, actorId, TestNow);
        request.CompleteSubTask(request.SubTasks.First().Id, actorId, TestNow);
        request.Complete("Finished resolution", actorId, TestNow);
        request.Status.Should().Be(RequestStatus.Completed);
        await repository.AddAsync(request);

        // Attempt AddSubTask on completed request
        var addAct = async () => await service.Handle(
            new AddRequestSubTaskCommand(request.Id, "New task", ActorPersonId: actorId),
            CancellationToken.None);
        await addAct.Should().ThrowAsync<InvalidRequestStateTransitionException>();
    }

    [Fact]
    public async Task Subtask_operations_emit_fine_grained_domain_events()
    {
        var (service, repository, org, _, dispatcher) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true);

        var request = CreateActiveRequest(ownerId);
        await repository.AddAsync(request);
        dispatcher.Events.Clear();

        // 1. Add subtask
        var added = await service.Handle(
            new AddRequestSubTaskCommand(request.Id, "Feature breakdown", ActorPersonId: ownerId),
            CancellationToken.None);
        dispatcher.Events.Should().ContainSingle(e => e is RequestSubTaskAdded);
        var subTaskId = added.SubTasks[0].Id;

        // 2. Complete subtask
        dispatcher.Events.Clear();
        await service.Handle(
            new CompleteRequestSubTaskCommand(request.Id, subTaskId, ActorPersonId: ownerId),
            CancellationToken.None);
        dispatcher.Events.Should().ContainSingle(e => e is RequestSubTaskCompleted);

        // 3. Reopen subtask
        dispatcher.Events.Clear();
        await service.Handle(
            new ReopenRequestSubTaskCommand(request.Id, subTaskId, ActorPersonId: ownerId),
            CancellationToken.None);
        dispatcher.Events.Should().ContainSingle(e => e is RequestSubTaskReopened);

        // 4. Remove subtask
        dispatcher.Events.Clear();
        await service.Handle(
            new RemoveRequestSubTaskCommand(request.Id, subTaskId, ActorPersonId: ownerId),
            CancellationToken.None);
        dispatcher.Events.Should().ContainSingle(e => e is RequestSubTaskRemoved);
    }

    [Fact]
    public async Task AddSubTask_validates_assignee_is_active_in_organization()
    {
        var (service, repository, org, _, _) = CreateTestSetup();
        var ownerId = Guid.NewGuid();
        var inactivePersonId = Guid.NewGuid();
        var unknownPersonId = Guid.NewGuid();
        org.SetPerson(ownerId, isActive: true);
        org.SetPerson(inactivePersonId, isActive: false);

        var request = CreateActiveRequest(ownerId);
        await repository.AddAsync(request);

        // Inactive assignee throws InvalidOperationException
        var inactiveAct = async () => await service.Handle(
            new AddRequestSubTaskCommand(request.Id, "Task", AssigneePersonId: inactivePersonId, ActorPersonId: ownerId),
            CancellationToken.None);
        await inactiveAct.Should().ThrowAsync<InvalidOperationException>();

        // Unknown assignee throws KeyNotFoundException
        var unknownAct = async () => await service.Handle(
            new AddRequestSubTaskCommand(request.Id, "Task", AssigneePersonId: unknownPersonId, ActorPersonId: ownerId),
            CancellationToken.None);
        await unknownAct.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public void RequestSubTaskDto_FromDomain_correctly_maps_all_fields()
    {
        var requestId = Guid.NewGuid();
        var subTaskId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var completerId = Guid.NewGuid();

        var subTask = RequestSubTask.Rehydrate(
            id: subTaskId,
            requestId: requestId,
            title: "Rehydrated Item",
            isCompleted: true,
            assigneePersonId: assigneeId,
            completedAt: TestNow,
            completedByPersonId: completerId,
            sortOrder: 3,
            createdAt: TestNow.AddHours(-1),
            updatedAt: TestNow);

        var dto = RequestSubTaskDto.FromDomain(subTask);

        dto.Id.Should().Be(subTaskId);
        dto.RequestId.Should().Be(requestId);
        dto.Title.Should().Be("Rehydrated Item");
        dto.IsCompleted.Should().BeTrue();
        dto.AssigneePersonId.Should().Be(assigneeId);
        dto.AssigneeName.Should().BeNull(); // Enriched via query service
        dto.CompletedAt.Should().Be(TestNow);
        dto.CompletedByPersonId.Should().Be(completerId);
        dto.CompletedByName.Should().BeNull(); // Enriched via query service
        dto.SortOrder.Should().Be(3);
        dto.CreatedAt.Should().Be(TestNow.AddHours(-1));
        dto.UpdatedAt.Should().Be(TestNow);
    }

    private static RequestAggregate CreateActiveRequest(Guid ownerPersonId)
    {
        var request = RequestAggregate.Record(
            id: Guid.NewGuid(),
            title: "Operational Request",
            description: "Detailed description",
            requestType: "GENERAL",
            actorPersonId: ownerPersonId,
            utcNow: TestNow);

        request.AssignOwner(ownerPersonId, ownerPersonId, "Assigned owner", TestNow.AddMinutes(1));
        request.StartWork(ownerPersonId, "Started work", TestNow.AddMinutes(2));
        return request;
    }

    private static (
        RequestService Service,
        FakeRequestRepository Repository,
        FakeOrganizationQueryService Org,
        FakeCustomerQueryService Customer,
        RecordingDomainEventDispatcher Dispatcher)
        CreateTestSetup()
    {
        var repo = new FakeRequestRepository();
        var org = new FakeOrganizationQueryService();
        var customer = new FakeCustomerQueryService();
        var product = new FakeProductQueryService();
        var dispatcher = new RecordingDomainEventDispatcher();
        var clock = new FakeSystemClock(TestNow);

        var service = new RequestService(
            repo,
            org,
            customer,
            product,
            dispatcher,
            currentContextProvider: null,
            auditContext: null,
            clock: clock);

        return (service, repo, org, customer, dispatcher);
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
