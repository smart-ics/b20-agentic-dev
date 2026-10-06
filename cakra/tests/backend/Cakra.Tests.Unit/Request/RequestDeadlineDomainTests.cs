using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Domain.Events;
using Cakra.Modules.Request.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Request;

public sealed class RequestDeadlineDomainTests
{
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    [Fact]
    public void Record_WithDeadline_NormalizesToUtcMidnightAndEmitsDomainEvent()
    {
        // Arrange
        var targetDeadline = new DateTime(2026, 11, 15, 14, 30, 45, DateTimeKind.Local);
        var expectedNormalized = new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var request = Cakra.Modules.Request.Domain.Request.Record(
            id: Guid.NewGuid(),
            title: "Demand with Deadline",
            description: "Detailed description",
            requestType: "FEATURE",
            actorPersonId: _actorId,
            deadline: targetDeadline);

        // Assert
        request.Deadline.Should().Be(expectedNormalized);
        request.Deadline!.Value.Kind.Should().Be(DateTimeKind.Utc);
        request.Deadline.Value.Hour.Should().Be(0);
        request.Deadline.Value.Minute.Should().Be(0);
        request.Deadline.Value.Second.Should().Be(0);

        var recordedEvent = request.DomainEvents.OfType<RequestRecorded>().Single();
        recordedEvent.Deadline.Should().Be(expectedNormalized);
    }

    [Fact]
    public void Record_WithoutDeadline_LeavesDeadlineNull()
    {
        // Act
        var request = Cakra.Modules.Request.Domain.Request.Record(
            id: Guid.NewGuid(),
            title: "Demand without Deadline",
            description: "Detailed description",
            requestType: "BUG",
            actorPersonId: _actorId);

        // Assert
        request.Deadline.Should().BeNull();

        var recordedEvent = request.DomainEvents.OfType<RequestRecorded>().Single();
        recordedEvent.Deadline.Should().BeNull();
    }

    [Fact]
    public void UpdateCoreAttributes_SettingDeadlineWhenPreviouslyNull_AppendsAuditAssignmentAndEmitsEvent()
    {
        // Arrange
        var request = Cakra.Modules.Request.Domain.Request.Record(
            id: Guid.NewGuid(),
            title: "Initial Title",
            description: "Initial Description",
            requestType: "GENERAL",
            actorPersonId: _actorId);

        var initialAssignmentCount = request.Assignments.Count;
        var newDeadline = new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc);
        var expectedNormalized = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        request.UpdateCoreAttributes(
            title: "Initial Title",
            description: "Initial Description",
            priority: "NORMAL",
            requestType: "GENERAL",
            actorPersonId: _actorId,
            deadline: newDeadline);

        // Assert
        request.Deadline.Should().Be(expectedNormalized);
        request.Assignments.Should().HaveCount(initialAssignmentCount + 1);

        var audit = request.Assignments.Last();
        audit.RequestId.Should().Be(request.Id);
        audit.ActorPersonId.Should().Be(_actorId);
        audit.PreviousStatus.Should().Be(RequestStatus.Captured);
        audit.NewStatus.Should().Be(RequestStatus.Captured);
        audit.Notes.Should().Be("Deadline set to 2026-12-01");

        var domainEvent = request.DomainEvents.OfType<RequestCoreAttributesUpdated>().Single();
        domainEvent.Deadline.Should().Be(expectedNormalized);
    }

    [Fact]
    public void UpdateCoreAttributes_ChangingExistingDeadline_AppendsAuditAssignmentWithBothDates()
    {
        // Arrange
        var initialDeadline = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var request = Cakra.Modules.Request.Domain.Request.Record(
            id: Guid.NewGuid(),
            title: "Initial Title",
            description: "Initial Description",
            requestType: "GENERAL",
            actorPersonId: _actorId,
            deadline: initialDeadline);

        var initialAssignmentCount = request.Assignments.Count;
        var changedDeadline = new DateTime(2026, 12, 25, 18, 0, 0, DateTimeKind.Utc);
        var expectedNormalized = new DateTime(2026, 12, 25, 0, 0, 0, DateTimeKind.Utc);

        // Act
        request.UpdateCoreAttributes(
            title: "Initial Title",
            description: "Initial Description",
            priority: "NORMAL",
            requestType: "GENERAL",
            actorPersonId: _actorId,
            deadline: changedDeadline);

        // Assert
        request.Deadline.Should().Be(expectedNormalized);
        request.Assignments.Should().HaveCount(initialAssignmentCount + 1);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.Captured);
        audit.NewStatus.Should().Be(RequestStatus.Captured);
        audit.Notes.Should().Be("Deadline changed from 2026-12-01 to 2026-12-25");

        var domainEvent = request.DomainEvents.OfType<RequestCoreAttributesUpdated>().Single();
        domainEvent.Deadline.Should().Be(expectedNormalized);
    }

    [Fact]
    public void UpdateCoreAttributes_ClearingExistingDeadline_AppendsAuditAssignmentWithClearedNote()
    {
        // Arrange
        var initialDeadline = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var request = Cakra.Modules.Request.Domain.Request.Record(
            id: Guid.NewGuid(),
            title: "Initial Title",
            description: "Initial Description",
            requestType: "GENERAL",
            actorPersonId: _actorId,
            deadline: initialDeadline);

        var initialAssignmentCount = request.Assignments.Count;

        // Act
        request.UpdateCoreAttributes(
            title: "Initial Title",
            description: "Initial Description",
            priority: "NORMAL",
            requestType: "GENERAL",
            actorPersonId: _actorId,
            deadline: null);

        // Assert
        request.Deadline.Should().BeNull();
        request.Assignments.Should().HaveCount(initialAssignmentCount + 1);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.Captured);
        audit.NewStatus.Should().Be(RequestStatus.Captured);
        audit.Notes.Should().Be("Deadline cleared");

        var domainEvent = request.DomainEvents.OfType<RequestCoreAttributesUpdated>().Single();
        domainEvent.Deadline.Should().BeNull();
    }

    [Fact]
    public void UpdateCoreAttributes_WhenDeadlineUnchanged_DoesNotAppendAuditAssignment()
    {
        // Arrange
        var initialDeadline = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var request = Cakra.Modules.Request.Domain.Request.Record(
            id: Guid.NewGuid(),
            title: "Initial Title",
            description: "Initial Description",
            requestType: "GENERAL",
            actorPersonId: _actorId,
            deadline: initialDeadline);

        var initialAssignmentCount = request.Assignments.Count;

        // Act - updating core attributes without modifying deadline
        request.UpdateCoreAttributes(
            title: "Updated Title",
            description: "Updated Description",
            priority: "HIGH",
            requestType: "FEATURE",
            actorPersonId: _actorId,
            deadline: new DateTime(2026, 12, 1, 15, 30, 0, DateTimeKind.Local)); // Same day when normalized

        // Assert
        request.Deadline.Should().Be(initialDeadline);
        request.Assignments.Should().HaveCount(initialAssignmentCount);
    }

    [Fact]
    public void UpdateCoreAttributes_WhenRequestCompleted_ThrowsInvalidRequestStateTransitionException()
    {
        // Arrange
        var request = Cakra.Modules.Request.Domain.Request.Record(
            id: Guid.NewGuid(),
            title: "Title",
            description: "Description",
            requestType: "GENERAL",
            actorPersonId: _actorId);

        request.AssignOwner(_ownerId, _actorId);
        request.StartWork(_ownerId);
        request.Complete("Done", _ownerId);

        // Act
        var act = () => request.UpdateCoreAttributes(
            title: "New Title",
            description: "New Description",
            priority: "NORMAL",
            requestType: "GENERAL",
            actorPersonId: _actorId,
            deadline: DateTime.UtcNow.AddDays(7));

        // Assert
        act.Should().Throw<InvalidRequestStateTransitionException>()
            .WithMessage($"*Cannot edit core attributes of closed request '{request.Id}' in status 'Completed'*");
    }

    [Fact]
    public void UpdateCoreAttributes_WhenRequestCancelled_ThrowsInvalidRequestStateTransitionException()
    {
        // Arrange
        var request = Cakra.Modules.Request.Domain.Request.Record(
            id: Guid.NewGuid(),
            title: "Title",
            description: "Description",
            requestType: "GENERAL",
            actorPersonId: _actorId);

        request.Cancel("No longer needed", _actorId);

        // Act
        var act = () => request.UpdateCoreAttributes(
            title: "New Title",
            description: "New Description",
            priority: "NORMAL",
            requestType: "GENERAL",
            actorPersonId: _actorId,
            deadline: DateTime.UtcNow.AddDays(7));

        // Assert
        act.Should().Throw<InvalidRequestStateTransitionException>()
            .WithMessage($"*Cannot edit core attributes of closed request '{request.Id}' in status 'Cancelled'*");
    }

    [Fact]
    public void Rehydrate_WithDeadline_SetsNormalizedDeadline()
    {
        // Arrange
        var deadline = new DateTime(2026, 12, 10, 8, 15, 0, DateTimeKind.Utc);
        var expectedNormalized = new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var request = Cakra.Modules.Request.Domain.Request.Rehydrate(
            id: Guid.NewGuid(),
            title: "Rehydrated Request",
            description: "Rehydrated Description",
            requestType: "SUPPORT",
            status: RequestStatus.Assigned,
            priority: "HIGH",
            ownerPersonId: _ownerId,
            customerId: null,
            productId: null,
            workPackageId: null,
            evaluationNotes: null,
            createdAt: DateTime.UtcNow.AddDays(-1),
            updatedAt: DateTime.UtcNow,
            deadline: deadline);

        // Assert
        request.Deadline.Should().Be(expectedNormalized);
    }

    [Fact]
    public void Rehydrate_WithoutDeadline_LeavesDeadlineNull()
    {
        // Act
        var request = Cakra.Modules.Request.Domain.Request.Rehydrate(
            id: Guid.NewGuid(),
            title: "Rehydrated Request",
            description: "Rehydrated Description",
            requestType: "SUPPORT",
            status: RequestStatus.Assigned,
            priority: "HIGH",
            ownerPersonId: _ownerId,
            customerId: null,
            productId: null,
            workPackageId: null,
            evaluationNotes: null,
            createdAt: DateTime.UtcNow.AddDays(-1),
            updatedAt: DateTime.UtcNow);

        // Assert
        request.Deadline.Should().BeNull();
    }

    [Fact]
    public void FromDomain_MapsDeadlineProperty()
    {
        var deadline = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
        var request = Cakra.Modules.Request.Domain.Request.Record(
            Guid.NewGuid(), "Title", "Desc", "GENERAL", _actorId, deadline: deadline);

        var dto = Cakra.Modules.Request.RequestDto.FromDomain(request);

        dto.Deadline.Should().Be(deadline);
    }
}
