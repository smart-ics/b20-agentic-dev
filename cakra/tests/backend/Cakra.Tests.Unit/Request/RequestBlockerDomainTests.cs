using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Domain.Events;
using Cakra.Modules.Request.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Request;

public sealed class RequestBlockerDomainTests
{
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _collaboratorId = Guid.NewGuid();

    private Cakra.Modules.Request.Domain.Request CreateRequest(RequestStatus status = RequestStatus.Assigned)
    {
        var request = Cakra.Modules.Request.Domain.Request.Record(
            id: Guid.NewGuid(),
            title: "Blocker Test Request",
            description: "Request for verifying blocker domain invariants",
            requestType: "Feature",
            actorPersonId: _actorId,
            initialOwnerPersonId: _ownerId);

        if (status == RequestStatus.Assigned)
        {
            return request;
        }

        if (status == RequestStatus.Captured)
        {
            return Cakra.Modules.Request.Domain.Request.Record(
                id: Guid.NewGuid(),
                title: "Captured Request",
                description: "Unassigned captured request",
                requestType: "Feature",
                actorPersonId: _actorId);
        }

        request.StartWork(_ownerId);

        if (status == RequestStatus.InProgress)
        {
            return request;
        }

        if (status == RequestStatus.Paused)
        {
            request.PauseWork(_ownerId, "Paused for testing");
            return request;
        }

        if (status == RequestStatus.Completed)
        {
            request.Complete("Work finished", _ownerId);
            return request;
        }

        if (status == RequestStatus.Cancelled)
        {
            request.Cancel("Cancelled for testing", _ownerId);
            return request;
        }

        return request;
    }

    [Fact]
    public void AddBlocker_WhenInProgress_TransitionsToPausedAndEmitsEvents()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        request.ClearDomainEvents();
        var blockerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var blocker = request.AddBlocker(
            blockerId: blockerId,
            reason: "Waiting for third-party API credentials",
            createdByPersonId: _collaboratorId,
            utcNow: now);

        // Assert
        blocker.Should().NotBeNull();
        blocker.Id.Should().Be(blockerId);
        blocker.RequestId.Should().Be(request.Id);
        blocker.Reason.Should().Be("Waiting for third-party API credentials");
        blocker.CreatedByPersonId.Should().Be(_collaboratorId);
        blocker.CreatedAtUtc.Should().Be(now);
        blocker.IsCleared.Should().BeFalse();
        blocker.ClearedAtUtc.Should().BeNull();
        blocker.ClearedByPersonId.Should().BeNull();
        blocker.ClearanceReason.Should().BeNull();

        request.Status.Should().Be(RequestStatus.Paused);
        request.ActiveBlockersCount.Should().Be(1);
        request.Blockers.Should().ContainSingle(b => b.Id == blockerId);

        // Verify assignment record
        var assignment = request.Assignments.Last();
        assignment.PreviousStatus.Should().Be(RequestStatus.InProgress);
        assignment.NewStatus.Should().Be(RequestStatus.Paused);
        assignment.Notes.Should().Be("Work paused due to blocker");

        // Verify events
        var pausedEvents = request.DomainEvents.OfType<RequestWorkPaused>().ToList();
        pausedEvents.Should().HaveCount(1);
        pausedEvents[0].RequestId.Should().Be(request.Id);

        var blockerAddedEvents = request.DomainEvents.OfType<RequestBlockerAdded>().ToList();
        blockerAddedEvents.Should().HaveCount(1);
        var evt = blockerAddedEvents[0];
        evt.RequestId.Should().Be(request.Id);
        evt.BlockerId.Should().Be(blockerId);
        evt.Reason.Should().Be("Waiting for third-party API credentials");
        evt.CreatedByPersonId.Should().Be(_collaboratorId);
    }

    [Fact]
    public void AddBlocker_WhenAssigned_TransitionsToPausedAndEmitsBlockerAdded()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.Assigned);
        request.ClearDomainEvents();
        var blockerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var blocker = request.AddBlocker(
            blockerId: blockerId,
            reason: "External dependency blocked before start",
            createdByPersonId: _collaboratorId,
            utcNow: now);

        // Assert
        request.Status.Should().Be(RequestStatus.Paused);
        request.ActiveBlockersCount.Should().Be(1);
        request.Blockers.Should().ContainSingle(b => b.Id == blockerId);

        var assignment = request.Assignments.Last();
        assignment.PreviousStatus.Should().Be(RequestStatus.Assigned);
        assignment.NewStatus.Should().Be(RequestStatus.Paused);
        assignment.Notes.Should().Be("Assigned request paused due to blocker");

        var blockerAddedEvents = request.DomainEvents.OfType<RequestBlockerAdded>().ToList();
        blockerAddedEvents.Should().HaveCount(1);
        blockerAddedEvents[0].BlockerId.Should().Be(blockerId);
    }

    [Fact]
    public void AddBlocker_WhenPaused_RemainsPausedAndIncrementsCount()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.Paused);
        request.ClearDomainEvents();
        var blockerId1 = Guid.NewGuid();
        var blockerId2 = Guid.NewGuid();

        // Act
        request.AddBlocker(blockerId1, "First impediment", _collaboratorId);
        request.AddBlocker(blockerId2, "Second impediment", _collaboratorId);

        // Assert
        request.Status.Should().Be(RequestStatus.Paused);
        request.ActiveBlockersCount.Should().Be(2);
        request.Blockers.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(RequestStatus.Completed)]
    [InlineData(RequestStatus.Cancelled)]
    public void AddBlocker_InClosedState_ThrowsInvalidRequestStateTransitionException(RequestStatus closedStatus)
    {
        // Arrange
        var request = CreateRequest(closedStatus);

        // Act
        var act = () => request.AddBlocker(Guid.NewGuid(), "Impediment", _collaboratorId);

        // Assert
        act.Should().Throw<InvalidRequestStateTransitionException>();
    }

    [Fact]
    public void AddBlocker_InCapturedState_ThrowsInvalidRequestStateTransitionException()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.Captured);

        // Act
        var act = () => request.AddBlocker(Guid.NewGuid(), "Impediment", _collaboratorId);

        // Assert
        act.Should().Throw<InvalidRequestStateTransitionException>()
            .WithMessage("*ASSIGNED, IN_PROGRESS, or PAUSED*");
    }

    [Fact]
    public void AddBlocker_WithInvalidParameters_ThrowsRequestDomainValidationException()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.Assigned);

        // Act & Assert
        Assert.Throws<RequestDomainValidationException>(() =>
            request.AddBlocker(Guid.Empty, "Reason", _collaboratorId));

        Assert.Throws<RequestDomainValidationException>(() =>
            request.AddBlocker(Guid.NewGuid(), "", _collaboratorId));

        Assert.Throws<RequestDomainValidationException>(() =>
            request.AddBlocker(Guid.NewGuid(), "   ", _collaboratorId));

        Assert.Throws<RequestDomainValidationException>(() =>
            request.AddBlocker(Guid.NewGuid(), "Reason", Guid.Empty));
    }

    [Fact]
    public void AddBlocker_WithDuplicateBlockerId_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.Assigned);
        var blockerId = Guid.NewGuid();
        request.AddBlocker(blockerId, "First reason", _collaboratorId);

        // Act
        var act = () => request.AddBlocker(blockerId, "Duplicate reason", _collaboratorId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*already exists on request '{request.Id}'*");
    }

    [Fact]
    public void ClearBlocker_WhenActive_ClearsBlockerDecrementsCountAndLeavesStatusPaused()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        var blockerId = Guid.NewGuid();
        request.AddBlocker(blockerId, "Database credentials missing", _collaboratorId);
        request.ClearDomainEvents();
        var clearTime = DateTime.UtcNow;

        // Act
        request.ClearBlocker(
            blockerId: blockerId,
            clearedByPersonId: _ownerId,
            clearanceReason: "Credentials provided by DBA team",
            utcNow: clearTime);

        // Assert
        request.Status.Should().Be(RequestStatus.Paused); // TD-002: Does NOT auto-resume
        request.ActiveBlockersCount.Should().Be(0);

        var blocker = request.Blockers.Single(b => b.Id == blockerId);
        blocker.IsCleared.Should().BeTrue();
        blocker.ClearedAtUtc.Should().Be(clearTime);
        blocker.ClearedByPersonId.Should().Be(_ownerId);
        blocker.ClearanceReason.Should().Be("Credentials provided by DBA team");

        var events = request.DomainEvents.OfType<RequestBlockerCleared>().ToList();
        events.Should().HaveCount(1);
        var evt = events[0];
        evt.RequestId.Should().Be(request.Id);
        evt.BlockerId.Should().Be(blockerId);
        evt.ClearanceReason.Should().Be("Credentials provided by DBA team");
        evt.ClearedByPersonId.Should().Be(_ownerId);
    }

    [Fact]
    public void ClearBlocker_WhenAlreadyCleared_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        var blockerId = Guid.NewGuid();
        request.AddBlocker(blockerId, "Impediment", _collaboratorId);
        request.ClearBlocker(blockerId, _ownerId, "Resolved");

        // Act
        var act = () => request.ClearBlocker(blockerId, _ownerId, "Trying to clear again");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Blocker is already cleared.");
    }

    [Fact]
    public void ClearBlocker_WithNonExistentBlockerId_ThrowsRequestDomainValidationException()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.Assigned);

        // Act
        var act = () => request.ClearBlocker(Guid.NewGuid(), _ownerId);

        // Assert
        act.Should().Throw<RequestDomainValidationException>()
            .WithMessage("*not found on Request*");
    }

    [Fact]
    public void StartWork_WithActiveBlockers_ThrowsRequestHasActiveBlockersException()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        var blockerId = Guid.NewGuid();
        request.AddBlocker(blockerId, "Network firewall blocking port", _collaboratorId);
        request.Status.Should().Be(RequestStatus.Paused);

        // Act
        var act = () => request.StartWork(_ownerId);

        // Assert
        var ex = act.Should().Throw<RequestHasActiveBlockersException>().Which;
        ex.RequestId.Should().Be(request.Id);
        ex.ActiveBlockersCount.Should().Be(1);
        ex.Message.Should().Contain("1 active blocker(s)");
    }

    [Fact]
    public void StartWork_AfterAllBlockersCleared_Succeeds()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        var blockerId = Guid.NewGuid();
        request.AddBlocker(blockerId, "Pending approval", _collaboratorId);
        request.ClearBlocker(blockerId, _ownerId, "Approval granted");

        // Act
        request.StartWork(_ownerId);

        // Assert
        request.Status.Should().Be(RequestStatus.InProgress);
    }

    [Fact]
    public void Complete_WithActiveBlockers_ThrowsRequestHasActiveBlockersException()
    {
        // Arrange - Force request into InProgress with active blockers via Rehydrate
        var blocker = RequestBlocker.Create(Guid.NewGuid(), Guid.NewGuid(), "Open blocker", _collaboratorId, DateTime.UtcNow);
        var request = Cakra.Modules.Request.Domain.Request.Rehydrate(
            id: Guid.NewGuid(),
            title: "Testing Completion Guard",
            description: "Request with blocker",
            requestType: "Feature",
            status: RequestStatus.InProgress,
            priority: "NORMAL",
            ownerPersonId: _ownerId,
            customerId: null,
            productId: null,
            workPackageId: null,
            evaluationNotes: null,
            createdAt: DateTime.UtcNow,
            updatedAt: null,
            blockers: new[] { blocker },
            activeBlockersCount: 1);

        // Act
        var act = () => request.Complete("Finishing work", _ownerId);

        // Assert
        var ex = act.Should().Throw<RequestHasActiveBlockersException>().Which;
        ex.RequestId.Should().Be(request.Id);
        ex.ActiveBlockersCount.Should().Be(1);
    }

    [Fact]
    public void Cancel_WithActiveBlockers_SucceedsAndPreservesBlockers()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        var blockerId = Guid.NewGuid();
        request.AddBlocker(blockerId, "Critical blocker", _collaboratorId);

        // Act
        request.Cancel("Business priority changed, project stopped", _ownerId);

        // Assert
        request.Status.Should().Be(RequestStatus.Cancelled);
        request.ActiveBlockersCount.Should().Be(1);
        request.Blockers.Should().ContainSingle(b => b.Id == blockerId && !b.IsCleared);
    }

    [Fact]
    public void Rehydrate_WithBlockers_PopulatesCollectionAndActiveCount()
    {
        // Arrange
        var blocker1 = RequestBlocker.Rehydrate(
            id: Guid.NewGuid(),
            requestId: Guid.NewGuid(),
            reason: "First blocker",
            createdByPersonId: _collaboratorId,
            createdAtUtc: DateTime.UtcNow.AddHours(-2),
            isCleared: true,
            clearedAtUtc: DateTime.UtcNow.AddHours(-1),
            clearedByPersonId: _ownerId,
            clearanceReason: "Fixed",
            createdAt: DateTime.UtcNow.AddHours(-2));

        var blocker2 = RequestBlocker.Rehydrate(
            id: Guid.NewGuid(),
            requestId: Guid.NewGuid(),
            reason: "Second blocker",
            createdByPersonId: _collaboratorId,
            createdAtUtc: DateTime.UtcNow.AddHours(-1),
            isCleared: false,
            clearedAtUtc: null,
            clearedByPersonId: null,
            clearanceReason: null,
            createdAt: DateTime.UtcNow.AddHours(-1));

        // Act
        var request = Cakra.Modules.Request.Domain.Request.Rehydrate(
            id: Guid.NewGuid(),
            title: "Rehydrated Request",
            description: "Desc",
            requestType: "Bug",
            status: RequestStatus.Paused,
            priority: "HIGH",
            ownerPersonId: _ownerId,
            customerId: null,
            productId: null,
            workPackageId: null,
            evaluationNotes: null,
            createdAt: DateTime.UtcNow.AddDays(-1),
            updatedAt: null,
            blockers: new[] { blocker1, blocker2 },
            activeBlockersCount: 1);

        // Assert
        request.Blockers.Should().HaveCount(2);
        request.ActiveBlockersCount.Should().Be(1);
        request.Blockers.Should().Contain(b => b.Id == blocker1.Id && b.IsCleared);
        request.Blockers.Should().Contain(b => b.Id == blocker2.Id && !b.IsCleared);
    }
}
