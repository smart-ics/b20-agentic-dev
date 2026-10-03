using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Domain.Events;
using Cakra.Modules.Request.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Request;

public sealed class RequestSubTaskDomainTests
{
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _collaboratorId = Guid.NewGuid();

    private Cakra.Modules.Request.Domain.Request CreateRequest(RequestStatus status = RequestStatus.Captured)
    {
        var request = Cakra.Modules.Request.Domain.Request.Record(
            id: Guid.NewGuid(),
            title: "Demand Implementation Request",
            description: "Detailed description for sub-task test",
            requestType: "Feature",
            actorPersonId: _actorId);

        if (status == RequestStatus.Captured)
        {
            return request;
        }

        request.AssignOwner(_ownerId, _actorId);

        if (status == RequestStatus.Evaluating)
        {
            return request;
        }

        if (status == RequestStatus.Rejected)
        {
            request.Reject("Not viable", _ownerId);
            return request;
        }

        request.Accept(_ownerId);

        if (status == RequestStatus.Accepted)
        {
            return request;
        }

        if (status == RequestStatus.InProgress)
        {
            request.StartProgress(_ownerId);
            return request;
        }

        if (status == RequestStatus.Escalated)
        {
            request.Escalate("Blocker", _ownerId);
            return request;
        }

        if (status == RequestStatus.Completed)
        {
            request.StartProgress(_ownerId);
            request.Complete("Finished resolution", _ownerId);
            return request;
        }

        return request;
    }

    [Fact]
    public void AddSubTask_InCapturedState_SucceedsAndEmitsEvent()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.Captured);
        request.ClearDomainEvents();
        var now = DateTime.UtcNow;

        // Act
        var subTask = request.AddSubTask(
            title: "Implement database migration",
            assigneePersonId: _collaboratorId,
            actorPersonId: _actorId,
            utcNow: now);

        // Assert
        subTask.Should().NotBeNull();
        subTask.RequestId.Should().Be(request.Id);
        subTask.Title.Should().Be("Implement database migration");
        subTask.AssigneePersonId.Should().Be(_collaboratorId);
        subTask.IsCompleted.Should().BeFalse();
        subTask.SortOrder.Should().Be(0);
        subTask.CreatedAt.Should().Be(now);
        subTask.UpdatedAt.Should().Be(now);

        request.SubTasks.Should().ContainSingle(t => t.Id == subTask.Id);
        request.TotalSubTasksCount.Should().Be(1);
        request.CompletedSubTasksCount.Should().Be(0);
        request.CompletionPercentage.Should().Be(0);
        request.UpdatedAt.Should().Be(now);

        var events = request.DomainEvents.OfType<RequestSubTaskAdded>().ToList();
        events.Should().HaveCount(1);
        var evt = events[0];
        evt.RequestId.Should().Be(request.Id);
        evt.SubTaskId.Should().Be(subTask.Id);
        evt.Title.Should().Be("Implement database migration");
        evt.AssigneePersonId.Should().Be(_collaboratorId);
        evt.TotalSubTasksCount.Should().Be(1);
        evt.CompletionPercentage.Should().Be(0);
        evt.ActorPersonId.Should().Be(_actorId);
        evt.OccurredAtUtc.Should().Be(now);
    }

    [Fact]
    public void AddSubTask_SequentialAdditions_IncrementsSortOrderAndCounters()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);

        // Act
        var task1 = request.AddSubTask("Task 1", null, _actorId);
        var task2 = request.AddSubTask("Task 2", _collaboratorId, _actorId);
        var task3 = request.AddSubTask("Task 3", null, _actorId);

        // Assert
        task1.SortOrder.Should().Be(0);
        task2.SortOrder.Should().Be(1);
        task3.SortOrder.Should().Be(2);

        request.TotalSubTasksCount.Should().Be(3);
        request.CompletedSubTasksCount.Should().Be(0);
        request.CompletionPercentage.Should().Be(0);
        request.SubTasks.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AddSubTask_WithEmptyOrWhitespaceTitle_ThrowsValidationException(string? invalidTitle)
    {
        // Arrange
        var request = CreateRequest();

        // Act
        var act = () => request.AddSubTask(invalidTitle!, null, _actorId);

        // Assert
        act.Should().Throw<RequestDomainValidationException>()
            .WithMessage("*Title cannot be empty.*");
    }

    [Fact]
    public void AddSubTask_WithTitleExceeding255Chars_ThrowsValidationException()
    {
        // Arrange
        var request = CreateRequest();
        var longTitle = new string('A', 256);

        // Act
        var act = () => request.AddSubTask(longTitle, null, _actorId);

        // Assert
        act.Should().Throw<RequestDomainValidationException>()
            .WithMessage("*Title cannot exceed 255 characters.*");
    }

    [Fact]
    public void AddSubTask_WithEmptyActorId_ThrowsValidationException()
    {
        // Arrange
        var request = CreateRequest();

        // Act
        var act = () => request.AddSubTask("Valid Title", null, Guid.Empty);

        // Assert
        act.Should().Throw<RequestDomainValidationException>()
            .WithMessage("*ActorPersonId cannot be empty.*");
    }

    [Fact]
    public void AddSubTask_WithEmptyAssigneeGuid_ThrowsValidationException()
    {
        // Arrange
        var request = CreateRequest();

        // Act
        var act = () => request.AddSubTask("Valid Title", Guid.Empty, _actorId);

        // Assert
        act.Should().Throw<RequestDomainValidationException>()
            .WithMessage("*AssigneePersonId cannot be an empty GUID.*");
    }

    [Theory]
    [InlineData(RequestStatus.Completed)]
    [InlineData(RequestStatus.Rejected)]
    public void AddSubTask_WhenRequestIsClosed_ThrowsInvalidRequestStateTransitionException(RequestStatus closedStatus)
    {
        // Arrange
        var request = CreateRequest(closedStatus);

        // Act
        var act = () => request.AddSubTask("New Task", null, _actorId);

        // Assert
        act.Should().Throw<InvalidRequestStateTransitionException>()
            .WithMessage("*Cannot perform 'AddSubTask' on a closed request.*");
    }

    [Fact]
    public void CompleteSubTask_ValidPendingTask_MarksCompletedAndRecalculatesProgress()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        var task1 = request.AddSubTask("Task 1", _collaboratorId, _actorId);
        var task2 = request.AddSubTask("Task 2", null, _actorId);
        request.ClearDomainEvents();
        var now = DateTime.UtcNow;

        // Act
        request.CompleteSubTask(task1.Id, _collaboratorId, now);

        // Assert
        task1.IsCompleted.Should().BeTrue();
        task1.CompletedByPersonId.Should().Be(_collaboratorId);
        task1.CompletedAt.Should().Be(now);
        task1.UpdatedAt.Should().Be(now);

        request.TotalSubTasksCount.Should().Be(2);
        request.CompletedSubTasksCount.Should().Be(1);
        request.CompletionPercentage.Should().Be(50);
        request.UpdatedAt.Should().Be(now);

        var events = request.DomainEvents.OfType<RequestSubTaskCompleted>().ToList();
        events.Should().HaveCount(1);
        var evt = events[0];
        evt.RequestId.Should().Be(request.Id);
        evt.SubTaskId.Should().Be(task1.Id);
        evt.CompletedByPersonId.Should().Be(_collaboratorId);
        evt.CompletedSubTasksCount.Should().Be(1);
        evt.CompletionPercentage.Should().Be(50);
        evt.OccurredAtUtc.Should().Be(now);
    }

    [Fact]
    public void CompleteSubTask_WithNonExistentSubTaskId_ThrowsKeyNotFoundException()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => request.CompleteSubTask(nonExistentId, _actorId);

        // Assert
        act.Should().Throw<KeyNotFoundException>()
            .WithMessage($"*{nonExistentId}*");
    }

    [Fact]
    public void CompleteSubTask_WithEmptyCompletedByPersonId_ThrowsValidationException()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        var task = request.AddSubTask("Task", null, _actorId);

        // Act
        var act = () => request.CompleteSubTask(task.Id, Guid.Empty);

        // Assert
        act.Should().Throw<RequestDomainValidationException>()
            .WithMessage("*CompletedByPersonId cannot be empty.*");
    }

    [Theory]
    [InlineData(RequestStatus.Completed)]
    [InlineData(RequestStatus.Rejected)]
    public void CompleteSubTask_WhenRequestIsClosed_ThrowsInvalidRequestStateTransitionException(RequestStatus closedStatus)
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        var task = request.AddSubTask("Task", null, _actorId);

        if (closedStatus == RequestStatus.Completed)
        {
            request.CompleteSubTask(task.Id, _actorId);
            request.Complete("Done", _ownerId);
        }
        else
        {
            // Reset to evaluating then reject
            var rejectRequest = CreateRequest(RequestStatus.Rejected);
            var actReject = () => rejectRequest.CompleteSubTask(task.Id, _actorId);
            actReject.Should().Throw<InvalidRequestStateTransitionException>()
                .WithMessage("*Cannot perform 'CompleteSubTask' on a closed request.*");
            return;
        }

        // Act
        var act = () => request.CompleteSubTask(task.Id, _actorId);

        // Assert
        act.Should().Throw<InvalidRequestStateTransitionException>()
            .WithMessage("*Cannot perform 'CompleteSubTask' on a closed request.*");
    }

    [Fact]
    public void ReopenSubTask_CompletedTask_RevertsToPendingAndRecalculatesProgress()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        var task = request.AddSubTask("Checklist Item", _collaboratorId, _actorId);
        request.CompleteSubTask(task.Id, _collaboratorId);
        request.CompletionPercentage.Should().Be(100);
        request.ClearDomainEvents();
        var now = DateTime.UtcNow;

        // Act
        request.ReopenSubTask(task.Id, _actorId, now);

        // Assert
        task.IsCompleted.Should().BeFalse();
        task.CompletedByPersonId.Should().BeNull();
        task.CompletedAt.Should().BeNull();
        task.UpdatedAt.Should().Be(now);

        request.TotalSubTasksCount.Should().Be(1);
        request.CompletedSubTasksCount.Should().Be(0);
        request.CompletionPercentage.Should().Be(0);
        request.UpdatedAt.Should().Be(now);

        var events = request.DomainEvents.OfType<RequestSubTaskReopened>().ToList();
        events.Should().HaveCount(1);
        var evt = events[0];
        evt.RequestId.Should().Be(request.Id);
        evt.SubTaskId.Should().Be(task.Id);
        evt.ActorPersonId.Should().Be(_actorId);
        evt.CompletedSubTasksCount.Should().Be(0);
        evt.CompletionPercentage.Should().Be(0);
        evt.OccurredAtUtc.Should().Be(now);
    }

    [Fact]
    public void ReopenSubTask_WithNonExistentSubTaskId_ThrowsKeyNotFoundException()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => request.ReopenSubTask(nonExistentId, _actorId);

        // Assert
        act.Should().Throw<KeyNotFoundException>()
            .WithMessage($"*{nonExistentId}*");
    }

    [Fact]
    public void RemoveSubTask_ExistingTask_RemovesFromCollectionAndRecalculatesProgress()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        var task1 = request.AddSubTask("Task 1", null, _actorId);
        var task2 = request.AddSubTask("Task 2", null, _actorId);
        request.CompleteSubTask(task1.Id, _actorId);
        request.TotalSubTasksCount.Should().Be(2);
        request.CompletedSubTasksCount.Should().Be(1);
        request.CompletionPercentage.Should().Be(50);

        request.ClearDomainEvents();
        var now = DateTime.UtcNow;

        // Act - remove incomplete task2
        request.RemoveSubTask(task2.Id, _actorId, now);

        // Assert
        request.SubTasks.Should().NotContain(t => t.Id == task2.Id);
        request.TotalSubTasksCount.Should().Be(1);
        request.CompletedSubTasksCount.Should().Be(1);
        request.CompletionPercentage.Should().Be(100);
        request.UpdatedAt.Should().Be(now);

        var events = request.DomainEvents.OfType<RequestSubTaskRemoved>().ToList();
        events.Should().HaveCount(1);
        var evt = events[0];
        evt.RequestId.Should().Be(request.Id);
        evt.SubTaskId.Should().Be(task2.Id);
        evt.TotalSubTasksCount.Should().Be(1);
        evt.CompletionPercentage.Should().Be(100);
        evt.ActorPersonId.Should().Be(_actorId);
        evt.OccurredAtUtc.Should().Be(now);
    }

    [Fact]
    public void RemoveSubTask_WithNonExistentSubTaskId_ThrowsKeyNotFoundException()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => request.RemoveSubTask(nonExistentId, _actorId);

        // Assert
        act.Should().Throw<KeyNotFoundException>()
            .WithMessage($"*{nonExistentId}*");
    }

    [Fact]
    public void Complete_WhenRequestHasUnfinishedSubTasks_ThrowsRequestHasUnfinishedSubTasksException()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        var task1 = request.AddSubTask("Task 1", null, _actorId);
        var task2 = request.AddSubTask("Task 2", null, _actorId);
        request.CompleteSubTask(task1.Id, _actorId);

        // Act
        var act = () => request.Complete("Demand resolution notes", _ownerId);

        // Assert (Architecture TD-002)
        var exception = act.Should().Throw<RequestHasUnfinishedSubTasksException>().Which;
        exception.RequestId.Should().Be(request.Id);
        exception.UnfinishedCount.Should().Be(1);
        exception.Message.Should().Contain("1 unfinished sub-task(s)");

        request.Status.Should().Be(RequestStatus.InProgress);
        request.Resolution.Should().BeNull();
    }

    [Fact]
    public void Complete_WhenAllSubTasksAreCompleted_SucceedsAndSets100Percent()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        var task1 = request.AddSubTask("Task 1", null, _actorId);
        var task2 = request.AddSubTask("Task 2", null, _actorId);
        request.CompleteSubTask(task1.Id, _actorId);
        request.CompleteSubTask(task2.Id, _actorId);

        // Act
        request.Complete("All tasks completed satisfactorily", _ownerId);

        // Assert
        request.Status.Should().Be(RequestStatus.Completed);
        request.Resolution.Should().NotBeNull();
        request.CompletionPercentage.Should().Be(100);
        request.TotalSubTasksCount.Should().Be(2);
        request.CompletedSubTasksCount.Should().Be(2);
    }

    [Fact]
    public void Complete_WhenZeroSubTasks_SucceedsAndSets100Percent()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        request.TotalSubTasksCount.Should().Be(0);

        // Act
        request.Complete("No sub-tasks needed", _ownerId);

        // Assert
        request.Status.Should().Be(RequestStatus.Completed);
        request.Resolution.Should().NotBeNull();
        request.CompletionPercentage.Should().Be(100);
    }

    [Fact]
    public void ProgressCalculation_RoundsProperly()
    {
        // Arrange
        var request = CreateRequest(RequestStatus.InProgress);
        var t1 = request.AddSubTask("1", null, _actorId);
        var t2 = request.AddSubTask("2", null, _actorId);
        var t3 = request.AddSubTask("3", null, _actorId);

        // 1/3 = 33.333... -> 33
        request.CompleteSubTask(t1.Id, _actorId);
        request.CompletionPercentage.Should().Be(33);

        // 2/3 = 66.666... -> 67
        request.CompleteSubTask(t2.Id, _actorId);
        request.CompletionPercentage.Should().Be(67);

        // 3/3 = 100
        request.CompleteSubTask(t3.Id, _actorId);
        request.CompletionPercentage.Should().Be(100);
    }
}
