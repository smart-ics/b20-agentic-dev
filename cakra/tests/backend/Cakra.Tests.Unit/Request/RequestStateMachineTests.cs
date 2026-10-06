using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Domain.Events;
using Cakra.Modules.Request.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Request;

public sealed class RequestStateMachineTests
{
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _secondOwnerId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _workPackageId = Guid.NewGuid();

    private Cakra.Modules.Request.Domain.Request CreateCapturedRequest()
    {
        return Cakra.Modules.Request.Domain.Request.Record(
            id: Guid.NewGuid(),
            title: "BPJS billing validation discrepancy",
            description: "Billing claims intermittently fail BPJS validation checks.",
            requestType: "Bug",
            actorPersonId: _actorId,
            customerId: _customerId,
            productId: _productId,
            workPackageId: _workPackageId,
            priority: "HIGH");
    }

    private Cakra.Modules.Request.Domain.Request CreateAssignedRequest()
    {
        var request = CreateCapturedRequest();
        request.AssignOwner(_ownerId, _actorId, "Assigned to primary programmer");
        request.ClearDomainEvents();
        return request;
    }

    private Cakra.Modules.Request.Domain.Request CreateInProgressRequest()
    {
        var request = CreateAssignedRequest();
        request.StartWork(_ownerId, "Starting investigation and fix");
        request.ClearDomainEvents();
        return request;
    }

    private Cakra.Modules.Request.Domain.Request CreatePausedRequest()
    {
        var request = CreateInProgressRequest();
        request.PauseWork(_ownerId, "Resource constraint and architecture blocker");
        request.ClearDomainEvents();
        return request;
    }

    private Cakra.Modules.Request.Domain.Request CreateCompletedRequest()
    {
        var request = CreateInProgressRequest();
        request.Complete("Fix delivered and verified", _ownerId);
        request.ClearDomainEvents();
        return request;
    }

    private Cakra.Modules.Request.Domain.Request CreateCancelledRequest()
    {
        var request = CreateAssignedRequest();
        request.Cancel("Superseded by new requirements", _actorId);
        request.ClearDomainEvents();
        return request;
    }

    #region 1. Record & Initialization

    [Fact]
    public void Record_WithValidParameters_InitializesCapturedRequestAndEmitsEvent()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var request = Cakra.Modules.Request.Domain.Request.Record(
            id: id,
            title: "Fix pharmacy report",
            description: "Pharmacy inventory reports incorrect totals.",
            requestType: "Bug",
            actorPersonId: _actorId,
            customerId: _customerId,
            productId: _productId,
            workPackageId: _workPackageId,
            priority: "URGENT",
            utcNow: now);

        request.Id.Should().Be(id);
        request.Title.Should().Be("Fix pharmacy report");
        request.Description.Should().Be("Pharmacy inventory reports incorrect totals.");
        request.RequestType.Should().Be("Bug");
        request.Status.Should().Be(RequestStatus.Captured);
        request.Priority.Should().Be("URGENT");
        request.OwnerPersonId.Should().BeNull();
        request.CustomerId.Should().Be(_customerId);
        request.ProductId.Should().Be(_productId);
        request.WorkPackageId.Should().Be(_workPackageId);
        request.CreatedAt.Should().Be(now);
        request.UpdatedAt.Should().BeNull();
        request.Resolution.Should().BeNull();

        request.Assignments.Should().ContainSingle();
        var initialAudit = request.Assignments.First();
        initialAudit.RequestId.Should().Be(id);
        initialAudit.PreviousStatus.Should().BeNull();
        initialAudit.NewStatus.Should().Be(RequestStatus.Captured);
        initialAudit.ActorPersonId.Should().Be(_actorId);
        initialAudit.AssignedOwnerPersonId.Should().BeNull();

        request.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RequestRecorded>()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = id,
                Title = "Fix pharmacy report",
                Description = "Pharmacy inventory reports incorrect totals.",
                RequestType = "Bug",
                ActorPersonId = _actorId,
                CustomerId = (Guid?)_customerId,
                ProductId = (Guid?)_productId,
                WorkPackageId = (Guid?)_workPackageId,
                Priority = "URGENT",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_WithEmptyTitle_ThrowsValidationException(string title)
    {
        var act = () => Cakra.Modules.Request.Domain.Request.Record(
            Guid.NewGuid(), title, "Description", "Bug", _actorId);

        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("title");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Record_WithEmptyDescription_IsAllowed_AndSetsEmptyDescription(string? description)
    {
        var request = Cakra.Modules.Request.Domain.Request.Record(
            Guid.NewGuid(), "Title", description, "Bug", _actorId);

        request.Description.Should().Be(string.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_WithEmptyRequestType_ThrowsValidationException(string type)
    {
        var act = () => Cakra.Modules.Request.Domain.Request.Record(
            Guid.NewGuid(), "Title", "Description", type, _actorId);

        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("requestType");
    }

    [Fact]
    public void Record_WithEmptyId_ThrowsValidationException()
    {
        var act = () => Cakra.Modules.Request.Domain.Request.Record(
            Guid.Empty, "Title", "Description", "Bug", _actorId);

        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("id");
    }

    [Fact]
    public void Record_WithEmptyActorId_ThrowsValidationException()
    {
        var act = () => Cakra.Modules.Request.Domain.Request.Record(
            Guid.NewGuid(), "Title", "Description", "Bug", Guid.Empty);

        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("actorPersonId");
    }

    #endregion

    #region 2. AssignOwner (CAPTURED -> ASSIGNED, reassignments)

    [Fact]
    public void AssignOwner_FromCaptured_TransitionsToAssignedAndEmitsEvent()
    {
        var request = CreateCapturedRequest();
        var now = DateTime.UtcNow;

        request.AssignOwner(_ownerId, _actorId, "Assigning to Alice", now);

        request.Status.Should().Be(RequestStatus.Assigned);
        request.OwnerPersonId.Should().Be(_ownerId);
        request.UpdatedAt.Should().Be(now);

        request.Assignments.Should().HaveCount(2);
        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.Captured);
        audit.NewStatus.Should().Be(RequestStatus.Assigned);
        audit.PreviousOwnerPersonId.Should().BeNull();
        audit.AssignedOwnerPersonId.Should().Be(_ownerId);
        audit.ActorPersonId.Should().Be(_actorId);

        request.DomainEvents.OfType<RequestAssigned>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                OwnerPersonId = _ownerId,
                PreviousOwnerPersonId = (Guid?)null,
                ActorPersonId = _actorId,
                PreviousStatus = RequestStatus.Captured,
                NewStatus = RequestStatus.Assigned,
                Notes = "Assigning to Alice",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void AssignOwner_FromAssigned_UpdatesOwnerAndPreservesAssignedStatus()
    {
        var request = CreateAssignedRequest();
        var now = DateTime.UtcNow;

        request.AssignOwner(_secondOwnerId, _actorId, "Reassigned to Bob", now);

        request.Status.Should().Be(RequestStatus.Assigned);
        request.OwnerPersonId.Should().Be(_secondOwnerId);
        request.UpdatedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.Assigned);
        audit.NewStatus.Should().Be(RequestStatus.Assigned);
        audit.PreviousOwnerPersonId.Should().Be(_ownerId);
        audit.AssignedOwnerPersonId.Should().Be(_secondOwnerId);

        request.DomainEvents.OfType<RequestAssigned>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                OwnerPersonId = _secondOwnerId,
                PreviousOwnerPersonId = (Guid?)_ownerId,
                ActorPersonId = _actorId,
                PreviousStatus = RequestStatus.Assigned,
                NewStatus = RequestStatus.Assigned,
                Notes = "Reassigned to Bob",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void AssignOwner_FromAssigned_ToSameOwner_ThrowsInvalidOperationException()
    {
        var request = CreateAssignedRequest();
        var act = () => request.AssignOwner(_ownerId, _actorId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already assigned to this person*");
    }

    [Fact]
    public void AssignOwner_FromInProgress_ResetsStatusToAssigned()
    {
        var request = CreateInProgressRequest();
        var now = DateTime.UtcNow;

        request.AssignOwner(_secondOwnerId, _actorId, "Reassigning active work to Bob", now);

        request.Status.Should().Be(RequestStatus.Assigned);
        request.OwnerPersonId.Should().Be(_secondOwnerId);
        request.UpdatedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.InProgress);
        audit.NewStatus.Should().Be(RequestStatus.Assigned);
        audit.PreviousOwnerPersonId.Should().Be(_ownerId);
        audit.AssignedOwnerPersonId.Should().Be(_secondOwnerId);

        request.DomainEvents.OfType<RequestAssigned>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                OwnerPersonId = _secondOwnerId,
                PreviousOwnerPersonId = (Guid?)_ownerId,
                ActorPersonId = _actorId,
                PreviousStatus = RequestStatus.InProgress,
                NewStatus = RequestStatus.Assigned,
                Notes = "Reassigning active work to Bob",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void AssignOwner_FromInProgress_ToSameOwner_ThrowsInvalidOperationException()
    {
        var request = CreateInProgressRequest();
        var act = () => request.AssignOwner(_ownerId, _actorId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already assigned to this person*");
    }

    [Fact]
    public void AssignOwner_FromPaused_ResetsStatusToAssigned()
    {
        var request = CreatePausedRequest();
        var now = DateTime.UtcNow;

        request.AssignOwner(_secondOwnerId, _actorId, "Reassigning paused work to Bob", now);

        request.Status.Should().Be(RequestStatus.Assigned);
        request.OwnerPersonId.Should().Be(_secondOwnerId);
        request.UpdatedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.Paused);
        audit.NewStatus.Should().Be(RequestStatus.Assigned);
        audit.PreviousOwnerPersonId.Should().Be(_ownerId);
        audit.AssignedOwnerPersonId.Should().Be(_secondOwnerId);

        request.DomainEvents.OfType<RequestAssigned>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                OwnerPersonId = _secondOwnerId,
                PreviousOwnerPersonId = (Guid?)_ownerId,
                ActorPersonId = _actorId,
                PreviousStatus = RequestStatus.Paused,
                NewStatus = RequestStatus.Assigned,
                Notes = "Reassigning paused work to Bob",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void AssignOwner_FromPaused_ToSameOwner_ThrowsInvalidOperationException()
    {
        var request = CreatePausedRequest();
        var act = () => request.AssignOwner(_ownerId, _actorId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already assigned to this person*");
    }

    [Theory]
    [InlineData(RequestStatus.Completed)]
    [InlineData(RequestStatus.Cancelled)]
    public void AssignOwner_WhenClosed_ThrowsInvalidRequestStateTransitionException(RequestStatus closedStatus)
    {
        var request = closedStatus == RequestStatus.Completed ? CreateCompletedRequest() : CreateCancelledRequest();
        var act = () => request.AssignOwner(_secondOwnerId, _actorId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .WithMessage("*Closed requests cannot be assigned or reassigned*");
    }

    [Fact]
    public void AssignOwner_WithEmptyOwner_ThrowsValidationException()
    {
        var request = CreateCapturedRequest();
        var act = () => request.AssignOwner(Guid.Empty, _actorId);

        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("ownerPersonId");
    }

    [Fact]
    public void AssignOwner_WithEmptyActor_ThrowsValidationException()
    {
        var request = CreateCapturedRequest();
        var act = () => request.AssignOwner(_ownerId, Guid.Empty);

        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("actorPersonId");
    }

    #endregion

    #region 3. StartWork (ASSIGNED / PAUSED -> IN_PROGRESS, Owner Enforcement)

    [Fact]
    public void StartWork_FromAssigned_TransitionsToInProgressAndEmitsEvent()
    {
        var request = CreateAssignedRequest();
        var now = DateTime.UtcNow;

        request.StartWork(_ownerId, "Beginning implementation", now);

        request.Status.Should().Be(RequestStatus.InProgress);
        request.UpdatedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.Assigned);
        audit.NewStatus.Should().Be(RequestStatus.InProgress);
        audit.ActorPersonId.Should().Be(_ownerId);

        request.DomainEvents.OfType<RequestWorkStarted>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                OwnerPersonId = _ownerId,
                ActorPersonId = _ownerId,
                PreviousStatus = RequestStatus.Assigned,
                Notes = "Beginning implementation",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void StartWork_FromPaused_TransitionsToInProgressAndEmitsEvent()
    {
        var request = CreatePausedRequest();
        var now = DateTime.UtcNow;

        request.StartWork(_ownerId, "Resuming work after unblock", now);

        request.Status.Should().Be(RequestStatus.InProgress);
        request.UpdatedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.Paused);
        audit.NewStatus.Should().Be(RequestStatus.InProgress);
        audit.ActorPersonId.Should().Be(_ownerId);

        request.DomainEvents.OfType<RequestWorkStarted>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                OwnerPersonId = _ownerId,
                ActorPersonId = _ownerId,
                PreviousStatus = RequestStatus.Paused,
                Notes = "Resuming work after unblock",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void StartWork_ByCallerNotAssignedOwner_ThrowsInvalidOperationException()
    {
        var request = CreateAssignedRequest();
        var nonOwnerId = Guid.NewGuid();

        var act = () => request.StartWork(nonOwnerId, "Trying to start someone else's work");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Only the assigned owner can start work on this request*");
    }

    [Fact]
    public void StartWork_WhenNoOwnerAssigned_ThrowsInvalidOperationException()
    {
        var request = CreateCapturedRequest();
        var act = () => request.StartWork(_actorId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Only the assigned owner can start work on this request*");
    }

    [Theory]
    [InlineData(RequestStatus.InProgress)]
    public void StartWork_WhenAlreadyInProgress_ThrowsInvalidRequestStateTransitionException(RequestStatus status)
    {
        var request = CreateInProgressRequest();
        var act = () => request.StartWork(_ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == status && e.TargetStatus == RequestStatus.InProgress);
    }

    [Fact]
    public void StartWork_WithEmptyActor_ThrowsValidationException()
    {
        var request = CreateAssignedRequest();
        var act = () => request.StartWork(Guid.Empty);

        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("actorPersonId");
    }

    #endregion

    #region 4. PauseWork (IN_PROGRESS -> PAUSED)

    [Fact]
    public void PauseWork_FromInProgress_TransitionsToPausedAndEmitsEvent()
    {
        var request = CreateInProgressRequest();
        var now = DateTime.UtcNow;

        request.PauseWork(_ownerId, "Waiting on external dependency", now);

        request.Status.Should().Be(RequestStatus.Paused);
        request.UpdatedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.InProgress);
        audit.NewStatus.Should().Be(RequestStatus.Paused);
        audit.ActorPersonId.Should().Be(_ownerId);

        request.DomainEvents.OfType<RequestWorkPaused>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                OwnerPersonId = (Guid?)_ownerId,
                ActorPersonId = _ownerId,
                Notes = "Waiting on external dependency",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void PauseWork_WithoutNote_SucceedsWithDefaultAuditNote()
    {
        var request = CreateInProgressRequest();
        request.PauseWork(_ownerId);

        request.Status.Should().Be(RequestStatus.Paused);
        request.DomainEvents.OfType<RequestWorkPaused>().Should().ContainSingle()
            .Which.Notes.Should().BeNull();
    }

    [Theory]
    [InlineData(RequestStatus.Captured)]
    [InlineData(RequestStatus.Assigned)]
    [InlineData(RequestStatus.Paused)]
    public void PauseWork_FromNonInProgressStates_ThrowsInvalidRequestStateTransitionException(RequestStatus invalidStatus)
    {
        var request = invalidStatus switch
        {
            RequestStatus.Captured => CreateCapturedRequest(),
            RequestStatus.Assigned => CreateAssignedRequest(),
            RequestStatus.Paused => CreatePausedRequest(),
            _ => CreateCapturedRequest()
        };

        var act = () => request.PauseWork(_ownerId, "Pause attempt");

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == invalidStatus && e.TargetStatus == RequestStatus.Paused);
    }

    [Fact]
    public void PauseWork_WithEmptyActor_ThrowsValidationException()
    {
        var request = CreateInProgressRequest();
        var act = () => request.PauseWork(Guid.Empty);

        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("actorPersonId");
    }

    #endregion

    #region 5. Cancel (Active States -> CANCELLED, Subtask Immunity)

    [Theory]
    [InlineData(RequestStatus.Captured)]
    [InlineData(RequestStatus.Assigned)]
    [InlineData(RequestStatus.InProgress)]
    [InlineData(RequestStatus.Paused)]
    public void Cancel_FromAnyActiveState_TransitionsToCancelledAndCreatesResolution(RequestStatus activeStatus)
    {
        var request = activeStatus switch
        {
            RequestStatus.Captured => CreateCapturedRequest(),
            RequestStatus.Assigned => CreateAssignedRequest(),
            RequestStatus.InProgress => CreateInProgressRequest(),
            RequestStatus.Paused => CreatePausedRequest(),
            _ => CreateCapturedRequest()
        };

        var now = DateTime.UtcNow;
        request.Cancel("Customer decided not to proceed with this requirement.", _actorId, now);

        request.Status.Should().Be(RequestStatus.Cancelled);
        request.UpdatedAt.Should().Be(now);
        request.Resolution.Should().NotBeNull();
        request.Resolution!.Outcome.Should().Be(ResolutionOutcomeNames.Cancelled);
        request.Resolution.Description.Should().Be("Customer decided not to proceed with this requirement.");
        request.Resolution.ResolvedBy.Should().Be(_actorId);
        request.Resolution.ResolvedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(activeStatus);
        audit.NewStatus.Should().Be(RequestStatus.Cancelled);
        audit.ActorPersonId.Should().Be(_actorId);

        request.DomainEvents.OfType<RequestCancelled>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                CancelledByPersonId = _actorId,
                CancellationReason = "Customer decided not to proceed with this requirement.",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void Cancel_WithUnfinishedSubTasks_SucceedsWithoutBlocking()
    {
        var request = CreateInProgressRequest();
        request.AddSubTask("Pending database schema check", null, _ownerId);
        request.AddSubTask("Pending integration verification", null, _ownerId);

        request.SubTasks.Any(t => !t.IsCompleted).Should().BeTrue();

        var act = () => request.Cancel("Cancelled due to architectural obsolescence", _actorId);

        act.Should().NotThrow();
        request.Status.Should().Be(RequestStatus.Cancelled);
        request.Resolution!.Outcome.Should().Be(ResolutionOutcomeNames.Cancelled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Cancel_WithEmptyReason_ThrowsValidationException(string? invalidReason)
    {
        var request = CreateInProgressRequest();
        var act = () => request.Cancel(invalidReason!, _actorId);

        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("reason");
    }

    [Fact]
    public void Cancel_WithEmptyActor_ThrowsValidationException()
    {
        var request = CreateInProgressRequest();
        var act = () => request.Cancel("Valid reason", Guid.Empty);

        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("actorPersonId");
    }

    [Theory]
    [InlineData(RequestStatus.Completed)]
    [InlineData(RequestStatus.Cancelled)]
    public void Cancel_WhenAlreadyClosed_ThrowsInvalidRequestStateTransitionException(RequestStatus closedStatus)
    {
        var request = closedStatus == RequestStatus.Completed ? CreateCompletedRequest() : CreateCancelledRequest();
        var act = () => request.Cancel("Trying to cancel closed request", _actorId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .WithMessage("*Closed requests cannot be cancelled*");
    }

    #endregion

    #region 6. Complete (IN_PROGRESS -> COMPLETED)

    [Fact]
    public void Complete_FromInProgress_TransitionsToCompletedCreatesResolutionAndEmitsEvent()
    {
        var request = CreateInProgressRequest();
        var now = DateTime.UtcNow;

        request.Complete("Applied BPJS header sanitization and verified with integration test suite.", _ownerId, now);

        request.Status.Should().Be(RequestStatus.Completed);
        request.UpdatedAt.Should().Be(now);
        request.Resolution.Should().NotBeNull();
        request.Resolution!.Outcome.Should().Be(ResolutionOutcomeNames.Completed);
        request.Resolution.Description.Should().Be("Applied BPJS header sanitization and verified with integration test suite.");
        request.Resolution.ResolvedBy.Should().Be(_ownerId);
        request.Resolution.ResolvedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.InProgress);
        audit.NewStatus.Should().Be(RequestStatus.Completed);

        request.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RequestCompleted>()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                CompletedByPersonId = _ownerId,
                ResolutionDescription = "Applied BPJS header sanitization and verified with integration test suite.",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Theory]
    [InlineData(RequestStatus.Captured)]
    [InlineData(RequestStatus.Assigned)]
    [InlineData(RequestStatus.Paused)]
    public void Complete_FromNonInProgressStates_ThrowsInvalidRequestStateTransitionException(RequestStatus invalidStatus)
    {
        var request = invalidStatus switch
        {
            RequestStatus.Captured => CreateCapturedRequest(),
            RequestStatus.Assigned => CreateAssignedRequest(),
            RequestStatus.Paused => CreatePausedRequest(),
            _ => CreateCapturedRequest()
        };

        var act = () => request.Complete("Complete attempt", _ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == invalidStatus && e.TargetStatus == RequestStatus.Completed);
    }

    [Fact]
    public void Complete_WithUnfinishedSubTasks_ThrowsRequestHasUnfinishedSubTasksException()
    {
        var request = CreateInProgressRequest();
        var task = request.AddSubTask("Pending test verification", null, _ownerId);

        var act = () => request.Complete("Done with code", _ownerId);

        act.Should().Throw<RequestHasUnfinishedSubTasksException>()
            .Where(e => e.UnfinishedCount == 1);
    }

    [Fact]
    public void Complete_WithCompletedSubTasks_Succeeds()
    {
        var request = CreateInProgressRequest();
        var task = request.AddSubTask("Verification", null, _ownerId);
        request.CompleteSubTask(task.Id, _ownerId);

        var act = () => request.Complete("Done and verified", _ownerId);

        act.Should().NotThrow();
        request.Status.Should().Be(RequestStatus.Completed);
    }

    #endregion

    #region 7. Terminal States (COMPLETED and CANCELLED) Reject Mutations

    [Fact]
    public void CompletedState_RejectsAllMutations()
    {
        var request = CreateCompletedRequest();

        Assert.Throws<InvalidRequestStateTransitionException>(() => request.AssignOwner(_secondOwnerId, _actorId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.StartWork(_ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.PauseWork(_ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.Cancel("Cancel completed", _actorId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.Complete("Complete again", _ownerId));
    }

    [Fact]
    public void CancelledState_RejectsAllMutations()
    {
        var request = CreateCancelledRequest();

        Assert.Throws<InvalidRequestStateTransitionException>(() => request.AssignOwner(_secondOwnerId, _actorId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.StartWork(_ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.PauseWork(_ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.Cancel("Cancel cancelled", _actorId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.Complete("Complete cancelled", _ownerId));
    }

    #endregion

    #region 8. End-to-End Lifecycle Scenarios

    [Fact]
    public void HappyPath_FullLifecycle_Captured_Assigned_InProgress_Completed()
    {
        // 1. CAPTURED
        var request = CreateCapturedRequest();
        request.Status.Should().Be(RequestStatus.Captured);
        request.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RequestRecorded>();

        // 2. CAPTURED -> ASSIGNED
        request.AssignOwner(_ownerId, _actorId, "Assigned to Alice");
        request.Status.Should().Be(RequestStatus.Assigned);
        request.DomainEvents.OfType<RequestAssigned>().Should().ContainSingle();

        // 3. ASSIGNED -> IN_PROGRESS
        request.StartWork(_ownerId, "Coding started");
        request.Status.Should().Be(RequestStatus.InProgress);
        request.DomainEvents.OfType<RequestWorkStarted>().Should().ContainSingle();

        // 4. IN_PROGRESS -> COMPLETED
        request.Complete("Resolved, validated, and deployed to staging", _ownerId);
        request.Status.Should().Be(RequestStatus.Completed);
        request.Resolution.Should().NotBeNull();
        request.Resolution!.Outcome.Should().Be(ResolutionOutcomeNames.Completed);
        request.DomainEvents.OfType<RequestCompleted>().Should().ContainSingle();

        // All 4 audit entries recorded
        request.Assignments.Should().HaveCount(4);
        request.Assignments.Select(a => a.NewStatus).Should().ContainInOrder(
            RequestStatus.Captured,
            RequestStatus.Assigned,
            RequestStatus.InProgress,
            RequestStatus.Completed);
    }

    [Fact]
    public void HappyPath_PauseAndResumeLifecycle_Captured_Assigned_InProgress_Paused_InProgress_Completed()
    {
        var request = CreateCapturedRequest();
        request.AssignOwner(_ownerId, _actorId);
        request.StartWork(_ownerId);

        // Pause work
        request.PauseWork(_ownerId, "Waiting on customer test credentials");
        request.Status.Should().Be(RequestStatus.Paused);

        // Resume work
        request.StartWork(_ownerId, "Credentials received; resuming development");
        request.Status.Should().Be(RequestStatus.InProgress);

        // Complete work
        request.Complete("Integrated and tested", _ownerId);
        request.Status.Should().Be(RequestStatus.Completed);

        request.Assignments.Select(a => a.NewStatus).Should().ContainInOrder(
            RequestStatus.Captured,
            RequestStatus.Assigned,
            RequestStatus.InProgress,
            RequestStatus.Paused,
            RequestStatus.InProgress,
            RequestStatus.Completed);
    }

    [Fact]
    public void HappyPath_ActiveWorkReassignment_ResetsToAssigned_ThenCompleted()
    {
        var request = CreateCapturedRequest();
        request.AssignOwner(_ownerId, _actorId);
        request.StartWork(_ownerId);

        // Reassign while in progress -> resets to ASSIGNED
        request.AssignOwner(_secondOwnerId, _actorId, "Reassigned to Bob for specialized expertise");
        request.Status.Should().Be(RequestStatus.Assigned);
        request.OwnerPersonId.Should().Be(_secondOwnerId);

        // Bob starts work
        request.StartWork(_secondOwnerId, "Bob picked up task");
        request.Status.Should().Be(RequestStatus.InProgress);

        // Bob completes work
        request.Complete("Finished by Bob", _secondOwnerId);
        request.Status.Should().Be(RequestStatus.Completed);
        request.Resolution!.ResolvedBy.Should().Be(_secondOwnerId);
    }

    #endregion

    #region 9. Entity & Base Class Invariant Verification

    [Fact]
    public void ClearDomainEvents_EmptiesDomainEventsCollection()
    {
        var request = CreateCapturedRequest();
        request.DomainEvents.Should().NotBeEmpty();

        request.ClearDomainEvents();
        request.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RequestResolution_Create_WithValidArgs_PopulatesProperties()
    {
        var reqId = Guid.NewGuid();
        var resolvedBy = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var resolution = RequestResolution.Create(reqId, "COMPLETED", "All tests passed", resolvedBy, now);

        resolution.Id.Should().NotBeEmpty();
        resolution.RequestId.Should().Be(reqId);
        resolution.Outcome.Should().Be("COMPLETED");
        resolution.Description.Should().Be("All tests passed");
        resolution.ResolvedBy.Should().Be(resolvedBy);
        resolution.ResolvedAt.Should().Be(now);
        resolution.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void RequestAssignment_Create_WithValidArgs_PopulatesProperties()
    {
        var reqId = Guid.NewGuid();
        var prevOwner = Guid.NewGuid();
        var newOwner = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var assignment = RequestAssignment.Create(
            reqId, prevOwner, newOwner, actor, RequestStatus.Captured, RequestStatus.Assigned, now, "Notes");

        assignment.Id.Should().NotBeEmpty();
        assignment.RequestId.Should().Be(reqId);
        assignment.PreviousOwnerPersonId.Should().Be(prevOwner);
        assignment.AssignedOwnerPersonId.Should().Be(newOwner);
        assignment.ActorPersonId.Should().Be(actor);
        assignment.PreviousStatus.Should().Be(RequestStatus.Captured);
        assignment.NewStatus.Should().Be(RequestStatus.Assigned);
        assignment.AssignedAtUtc.Should().Be(now);
        assignment.Notes.Should().Be("Notes");
        assignment.CreatedAt.Should().Be(now);
    }

    #endregion
}
