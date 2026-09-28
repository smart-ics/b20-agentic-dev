namespace ICS.Tests.Unit;

using System;
using System.Linq;
using FluentAssertions;
using ICS.Modules.WorkPackage.Domain;
using ICS.Modules.WorkPackage.Domain.Events;
using ICS.Modules.WorkPackage.Domain.Exceptions;
using Xunit;

/// <summary>
/// Comprehensive unit tests verifying the Work Package domain model:
/// WorkPackage aggregate, WorkPackageRequest membership entity, lifecycle state machine (DRAFT → ACTIVE → CLOSED),
/// all domain events, and Business Rule 9 enforcement.
/// Architecture §11, §19.8; work-package-domain.md §8, §9, §10.
/// </summary>
public class WorkPackageDomainTests
{
    // =========================================================================
    // 1. Creation & Factory Tests
    // =========================================================================

    [Fact]
    public void WorkPackage_Create_ShouldInitializeInDraft_AndEmitWorkPackageCreatedEvent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var wp = WorkPackage.Create(id, "Phase 1 Rollout", "Prepare go-live infrastructure", ownerId, customerId, productId, now);

        // Assert
        wp.Id.Should().Be(id);
        wp.Name.Should().Be("Phase 1 Rollout");
        wp.Objective.Should().Be("Prepare go-live infrastructure");
        wp.Status.Should().Be(WorkPackageStatus.Draft);
        wp.OwnerPersonId.Should().Be(ownerId);
        wp.CustomerId.Should().Be(customerId);
        wp.ProductId.Should().Be(productId);
        wp.CreatedAt.Should().Be(now);
        wp.UpdatedAt.Should().BeNull();
        wp.ClosedAt.Should().BeNull();
        wp.CloseReason.Should().BeNull();
        wp.IsDraft.Should().BeTrue();
        wp.IsActive.Should().BeFalse();
        wp.IsClosed.Should().BeFalse();
        wp.Requests.Should().BeEmpty();
        wp.ActiveRequests.Should().BeEmpty();

        wp.DomainEvents.Should().HaveCount(1);
        var createdEvent = wp.DomainEvents.First().Should().BeOfType<WorkPackageCreated>().Subject;
        createdEvent.WorkPackageId.Should().Be(id);
        createdEvent.Name.Should().Be("Phase 1 Rollout");
        createdEvent.Objective.Should().Be("Prepare go-live infrastructure");
        createdEvent.OwnerPersonId.Should().Be(ownerId);
        createdEvent.CustomerId.Should().Be(customerId);
        createdEvent.ProductId.Should().Be(productId);
    }

    [Fact]
    public void WorkPackage_Create_WithOptionalCustomerAndProductAsNull_ShouldSucceed()
    {
        // Act
        var wp = WorkPackage.Create(Guid.NewGuid(), "General Internal Maintenance", "Perform general database tuning", Guid.NewGuid());

        // Assert
        wp.CustomerId.Should().BeNull();
        wp.ProductId.Should().BeNull();
        wp.Status.Should().Be(WorkPackageStatus.Draft);
    }

    [Fact]
    public void WorkPackage_Create_WithEmptyId_ShouldThrowArgumentException()
    {
        var act = () => WorkPackage.Create(Guid.Empty, "Name", "Objective", Guid.NewGuid());
        act.Should().Throw<ArgumentException>().WithParameterName("id");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void WorkPackage_Create_WithInvalidName_ShouldThrowArgumentException(string? invalidName)
    {
        var act = () => WorkPackage.Create(Guid.NewGuid(), invalidName!, "Objective", Guid.NewGuid());
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void WorkPackage_Create_WithInvalidObjective_ShouldThrowArgumentException(string? invalidObjective)
    {
        var act = () => WorkPackage.Create(Guid.NewGuid(), "Name", invalidObjective!, Guid.NewGuid());
        act.Should().Throw<ArgumentException>().WithParameterName("objective");
    }

    [Fact]
    public void WorkPackage_Create_WithEmptyOwnerPersonId_ShouldThrowArgumentException()
    {
        var act = () => WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.Empty);
        act.Should().Throw<ArgumentException>().WithParameterName("ownerPersonId");
    }

    // =========================================================================
    // 2. Lifecycle State Machine Tests (DRAFT → ACTIVE → CLOSED)
    // =========================================================================

    [Fact]
    public void WorkPackage_Activate_FromDraft_ShouldTransitionToActive_AndEmitWorkPackageActivated()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        wp.ClearDomainEvents();
        var activateTime = DateTime.UtcNow.AddHours(1);

        // Act
        wp.Activate(activateTime);

        // Assert
        wp.Status.Should().Be(WorkPackageStatus.Active);
        wp.IsActive.Should().BeTrue();
        wp.IsDraft.Should().BeFalse();
        wp.IsClosed.Should().BeFalse();
        wp.UpdatedAt.Should().Be(activateTime);

        wp.DomainEvents.Should().HaveCount(1);
        var activatedEvent = wp.DomainEvents.First().Should().BeOfType<WorkPackageActivated>().Subject;
        activatedEvent.WorkPackageId.Should().Be(wp.Id);
        activatedEvent.ActivatedAt.Should().Be(activateTime);
    }

    [Fact]
    public void WorkPackage_Close_FromActive_ShouldTransitionToClosed_AndEmitWorkPackageClosed()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        wp.Activate();
        wp.ClearDomainEvents();
        var closeTime = DateTime.UtcNow.AddDays(2);

        // Act
        wp.Close("Objective fulfilled successfully", closeTime);

        // Assert
        wp.Status.Should().Be(WorkPackageStatus.Closed);
        wp.IsClosed.Should().BeTrue();
        wp.IsActive.Should().BeFalse();
        wp.IsDraft.Should().BeFalse();
        wp.CloseReason.Should().Be("Objective fulfilled successfully");
        wp.ClosedAt.Should().Be(closeTime);
        wp.UpdatedAt.Should().Be(closeTime);

        wp.DomainEvents.Should().HaveCount(1);
        var closedEvent = wp.DomainEvents.First().Should().BeOfType<WorkPackageClosed>().Subject;
        closedEvent.WorkPackageId.Should().Be(wp.Id);
        closedEvent.Reason.Should().Be("Objective fulfilled successfully");
        closedEvent.ClosedAt.Should().Be(closeTime);
    }

    [Fact]
    public void WorkPackage_Close_DirectlyFromDraft_ShouldBeValidPerDomainRule()
    {
        // Arrange (A Work Package may move directly from DRAFT to CLOSED when no active work is required)
        var wp = WorkPackage.Create(Guid.NewGuid(), "Discarded Package", "No longer required", Guid.NewGuid());
        wp.ClearDomainEvents();
        var closeTime = DateTime.UtcNow;

        // Act
        wp.Close("Cancelled before start", closeTime);

        // Assert
        wp.Status.Should().Be(WorkPackageStatus.Closed);
        wp.IsClosed.Should().BeTrue();
        wp.CloseReason.Should().Be("Cancelled before start");
        wp.ClosedAt.Should().Be(closeTime);

        wp.DomainEvents.Should().HaveCount(1);
        wp.DomainEvents.First().Should().BeOfType<WorkPackageClosed>();
    }

    [Fact]
    public void WorkPackage_Activate_WhenAlreadyActive_ShouldThrowInvalidWorkPackageStateTransitionException()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        wp.Activate();

        // Act
        var act = () => wp.Activate();

        // Assert
        act.Should().Throw<InvalidWorkPackageStateTransitionException>()
            .Where(e => e.WorkPackageId == wp.Id &&
                        e.CurrentStatus == WorkPackageStatus.Active &&
                        e.TargetStatus == WorkPackageStatus.Active);
    }

    [Fact]
    public void WorkPackage_Activate_WhenClosed_ShouldThrowInvalidWorkPackageStateTransitionException()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        wp.Close("Completed");

        // Act
        var act = () => wp.Activate();

        // Assert
        act.Should().Throw<InvalidWorkPackageStateTransitionException>()
            .Where(e => e.WorkPackageId == wp.Id &&
                        e.CurrentStatus == WorkPackageStatus.Closed &&
                        e.TargetStatus == WorkPackageStatus.Active);
    }

    [Fact]
    public void WorkPackage_Close_WhenAlreadyClosed_ShouldThrowInvalidWorkPackageStateTransitionException()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        wp.Close("Done");

        // Act
        var act = () => wp.Close("Again");

        // Assert
        act.Should().Throw<InvalidWorkPackageStateTransitionException>()
            .Where(e => e.WorkPackageId == wp.Id &&
                        e.CurrentStatus == WorkPackageStatus.Closed &&
                        e.TargetStatus == WorkPackageStatus.Closed);
    }

    [Theory]
    [InlineData(WorkPackageStatus.Draft, WorkPackageStatus.Active, true)]
    [InlineData(WorkPackageStatus.Draft, WorkPackageStatus.Closed, true)]
    [InlineData(WorkPackageStatus.Active, WorkPackageStatus.Closed, true)]
    [InlineData(WorkPackageStatus.Active, WorkPackageStatus.Draft, false)]
    [InlineData(WorkPackageStatus.Closed, WorkPackageStatus.Draft, false)]
    [InlineData(WorkPackageStatus.Closed, WorkPackageStatus.Active, false)]
    [InlineData(WorkPackageStatus.Draft, WorkPackageStatus.Draft, false)]
    [InlineData(WorkPackageStatus.Active, WorkPackageStatus.Active, false)]
    [InlineData(WorkPackageStatus.Closed, WorkPackageStatus.Closed, false)]
    [InlineData("UNKNOWN", WorkPackageStatus.Active, false)]
    [InlineData(WorkPackageStatus.Draft, "UNKNOWN", false)]
    public void WorkPackageStateMachine_CanTransition_ShouldFollowValidTransitionsStrictly(
        string current, string target, bool expectedCanTransition)
    {
        WorkPackageStateMachine.CanTransition(current, target).Should().Be(expectedCanTransition);
    }

    [Fact]
    public void WorkPackageStateMachine_GetAllowedTransitions_ShouldReturnExpectedTargets()
    {
        WorkPackageStateMachine.GetAllowedTransitions(WorkPackageStatus.Draft)
            .Should().BeEquivalentTo([WorkPackageStatus.Active, WorkPackageStatus.Closed]);

        WorkPackageStateMachine.GetAllowedTransitions(WorkPackageStatus.Active)
            .Should().BeEquivalentTo([WorkPackageStatus.Closed]);

        WorkPackageStateMachine.GetAllowedTransitions(WorkPackageStatus.Closed)
            .Should().BeEmpty();

        WorkPackageStateMachine.GetAllowedTransitions("INVALID")
            .Should().BeEmpty();
    }

    [Fact]
    public void WorkPackageStatus_Helpers_ShouldClassifyCorrectly()
    {
        WorkPackageStatus.IsValid(WorkPackageStatus.Draft).Should().BeTrue();
        WorkPackageStatus.IsValid(WorkPackageStatus.Active).Should().BeTrue();
        WorkPackageStatus.IsValid(WorkPackageStatus.Closed).Should().BeTrue();
        WorkPackageStatus.IsValid("OTHER").Should().BeFalse();
        WorkPackageStatus.IsValid(null).Should().BeFalse();

        WorkPackageStatus.IsTerminal(WorkPackageStatus.Closed).Should().BeTrue();
        WorkPackageStatus.IsTerminal(WorkPackageStatus.Active).Should().BeFalse();
        WorkPackageStatus.IsTerminal(WorkPackageStatus.Draft).Should().BeFalse();

        WorkPackageStatus.IsActive(WorkPackageStatus.Active).Should().BeTrue();
        WorkPackageStatus.IsActive(WorkPackageStatus.Draft).Should().BeFalse();
        WorkPackageStatus.IsActive(WorkPackageStatus.Closed).Should().BeFalse();

        WorkPackageStatus.IsDraft(WorkPackageStatus.Draft).Should().BeTrue();
        WorkPackageStatus.IsDraft(WorkPackageStatus.Active).Should().BeFalse();
        WorkPackageStatus.IsDraft(WorkPackageStatus.Closed).Should().BeFalse();
    }

    // =========================================================================
    // 3. Update Objective & Title Tests
    // =========================================================================

    [Fact]
    public void WorkPackage_UpdateObjective_ShouldModifyNameAndObjective_AndSetUpdatedAt()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "Old Name", "Old Objective", Guid.NewGuid());
        var updateTime = DateTime.UtcNow.AddMinutes(15);

        // Act
        wp.UpdateObjective("New Name", "New Detailed Objective", updateTime);

        // Assert
        wp.Name.Should().Be("New Name");
        wp.Objective.Should().Be("New Detailed Objective");
        wp.UpdatedAt.Should().Be(updateTime);
    }

    [Theory]
    [InlineData("", "Valid Objective")]
    [InlineData("   ", "Valid Objective")]
    [InlineData("Valid Name", "")]
    [InlineData("Valid Name", "   ")]
    public void WorkPackage_UpdateObjective_WithInvalidArguments_ShouldThrowArgumentException(string name, string objective)
    {
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        var act = () => wp.UpdateObjective(name, objective);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkPackage_UpdateObjective_WhenClosed_ShouldThrowWorkPackageDomainException()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        wp.Close("Done");

        // Act
        var act = () => wp.UpdateObjective("Updated Name", "Updated Objective");

        // Assert
        act.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*closed*");
    }

    // =========================================================================
    // 4. Ownership Assignment Tests
    // =========================================================================

    [Fact]
    public void WorkPackage_AssignOwner_ShouldUpdateOwner_AndEmitWorkPackageOwnerChanged()
    {
        // Arrange
        var initialOwner = Guid.NewGuid();
        var newOwner = Guid.NewGuid();
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", initialOwner);
        wp.ClearDomainEvents();
        var changeTime = DateTime.UtcNow.AddHours(2);

        // Act
        wp.AssignOwner(newOwner, changeTime);

        // Assert
        wp.OwnerPersonId.Should().Be(newOwner);
        wp.UpdatedAt.Should().Be(changeTime);

        wp.DomainEvents.Should().HaveCount(1);
        var ownerChanged = wp.DomainEvents.First().Should().BeOfType<WorkPackageOwnerChanged>().Subject;
        ownerChanged.WorkPackageId.Should().Be(wp.Id);
        ownerChanged.PreviousOwnerPersonId.Should().Be(initialOwner);
        ownerChanged.NewOwnerPersonId.Should().Be(newOwner);
        ownerChanged.ChangedAt.Should().Be(changeTime);
    }

    [Fact]
    public void WorkPackage_AssignOwner_ToSameOwner_ShouldBeNoOp_AndNotEmitEvent()
    {
        // Arrange
        var owner = Guid.NewGuid();
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", owner);
        wp.ClearDomainEvents();

        // Act
        wp.AssignOwner(owner);

        // Assert
        wp.OwnerPersonId.Should().Be(owner);
        wp.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void WorkPackage_AssignOwner_WithEmptyGuid_ShouldThrowArgumentException()
    {
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        var act = () => wp.AssignOwner(Guid.Empty);
        act.Should().Throw<ArgumentException>().WithParameterName("newOwnerPersonId");
    }

    [Fact]
    public void WorkPackage_AssignOwner_WhenClosed_ShouldThrowWorkPackageDomainException()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        wp.Close("Done");

        // Act
        var act = () => wp.AssignOwner(Guid.NewGuid());

        // Assert
        act.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*closed*");
    }

    // =========================================================================
    // 5. WorkPackageRequest Membership & Traceability Tests
    // =========================================================================

    [Fact]
    public void WorkPackage_AddRequest_ShouldAddActiveMembership_AndEmitRequestAddedToWorkPackage()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        wp.ClearDomainEvents();
        var requestId = Guid.NewGuid();
        var addedAt = DateTime.UtcNow.AddMinutes(5);

        // Act
        var membership = wp.AddRequest(requestId, addedAt);

        // Assert
        membership.Should().NotBeNull();
        membership.WorkPackageId.Should().Be(wp.Id);
        membership.RequestId.Should().Be(requestId);
        membership.AddedAt.Should().Be(addedAt);
        membership.RemovedAt.Should().BeNull();
        membership.IsActive.Should().BeTrue();

        wp.Requests.Should().ContainSingle(m => m.RequestId == requestId && m.IsActive);
        wp.ActiveRequests.Should().ContainSingle(m => m.RequestId == requestId);
        wp.HasActiveRequest(requestId).Should().BeTrue();
        wp.UpdatedAt.Should().Be(addedAt);

        wp.DomainEvents.Should().HaveCount(1);
        var evt = wp.DomainEvents.First().Should().BeOfType<RequestAddedToWorkPackage>().Subject;
        evt.WorkPackageId.Should().Be(wp.Id);
        evt.RequestId.Should().Be(requestId);
        evt.AddedAt.Should().Be(addedAt);
    }

    [Fact]
    public void WorkPackage_RemoveRequest_ShouldDeactivateMembership_AndEmitRequestRemovedFromWorkPackage()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        var requestId = Guid.NewGuid();
        var addedAt = DateTime.UtcNow.AddMinutes(1);
        wp.AddRequest(requestId, addedAt);
        wp.ClearDomainEvents();

        var removedAt = DateTime.UtcNow.AddMinutes(10);

        // Act
        wp.RemoveRequest(requestId, removedAt);

        // Assert
        wp.HasActiveRequest(requestId).Should().BeFalse();
        wp.ActiveRequests.Should().BeEmpty();
        wp.Requests.Should().HaveCount(1); // Historical membership remains traceable (Rule 15)

        var deactivated = wp.Requests.First();
        deactivated.RequestId.Should().Be(requestId);
        deactivated.IsActive.Should().BeFalse();
        deactivated.RemovedAt.Should().Be(removedAt);
        deactivated.UpdatedAt.Should().Be(removedAt);
        wp.UpdatedAt.Should().Be(removedAt);

        wp.DomainEvents.Should().HaveCount(1);
        var evt = wp.DomainEvents.First().Should().BeOfType<RequestRemovedFromWorkPackage>().Subject;
        evt.WorkPackageId.Should().Be(wp.Id);
        evt.RequestId.Should().Be(requestId);
        evt.RemovedAt.Should().Be(removedAt);
    }

    [Fact]
    public void WorkPackage_ReAddingPreviouslyRemovedRequest_ShouldCreateNewActiveMembership_PreservingHistory()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        var requestId = Guid.NewGuid();
        var t1 = DateTime.UtcNow;
        var t2 = t1.AddMinutes(10);
        var t3 = t1.AddMinutes(20);

        wp.AddRequest(requestId, t1);
        wp.RemoveRequest(requestId, t2);

        // Act - re-add request
        wp.AddRequest(requestId, t3);

        // Assert
        wp.HasActiveRequest(requestId).Should().BeTrue();
        wp.Requests.Should().HaveCount(2); // 1 inactive historical, 1 active
        wp.ActiveRequests.Should().HaveCount(1);

        var historical = wp.Requests.First(r => !r.IsActive);
        historical.AddedAt.Should().Be(t1);
        historical.RemovedAt.Should().Be(t2);

        var current = wp.Requests.First(r => r.IsActive);
        current.AddedAt.Should().Be(t3);
        current.RemovedAt.Should().BeNull();
    }

    [Fact]
    public void WorkPackage_RemoveRequest_WhenNotActiveMember_ShouldThrowWorkPackageDomainException()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        var unknownRequestId = Guid.NewGuid();

        // Act
        var act = () => wp.RemoveRequest(unknownRequestId);

        // Assert
        act.Should().Throw<WorkPackageDomainException>()
            .WithMessage($"*Request '{unknownRequestId}' is not an active member*");
    }

    [Fact]
    public void WorkPackage_RemoveRequest_WhenAlreadyRemoved_ShouldThrowWorkPackageDomainException()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        var requestId = Guid.NewGuid();
        wp.AddRequest(requestId);
        wp.RemoveRequest(requestId);

        // Act
        var act = () => wp.RemoveRequest(requestId);

        // Assert
        act.Should().Throw<WorkPackageDomainException>()
            .WithMessage($"*Request '{requestId}' is not an active member*");
    }

    [Fact]
    public void WorkPackage_AddRequest_WhenClosed_ShouldThrowWorkPackageDomainException()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        wp.Close("Done");

        // Act
        var act = () => wp.AddRequest(Guid.NewGuid());

        // Assert
        act.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*Cannot add requests to a closed Work Package*");
    }

    [Fact]
    public void WorkPackage_RemoveRequest_WhenClosed_ShouldThrowWorkPackageDomainException()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        var requestId = Guid.NewGuid();
        wp.AddRequest(requestId);
        wp.Close("Done");

        // Act
        var act = () => wp.RemoveRequest(requestId);

        // Assert
        act.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*Cannot remove requests from a closed Work Package*");
    }

    [Fact]
    public void WorkPackage_AddRequest_WithEmptyRequestId_ShouldThrowArgumentException()
    {
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        var act = () => wp.AddRequest(Guid.Empty);
        act.Should().Throw<ArgumentException>().WithParameterName("requestId");
    }

    [Fact]
    public void WorkPackage_RemoveRequest_WithEmptyRequestId_ShouldThrowArgumentException()
    {
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        var act = () => wp.RemoveRequest(Guid.Empty);
        act.Should().Throw<ArgumentException>().WithParameterName("requestId");
    }

    // =========================================================================
    // 6. Business Rule 9 Invariant Tests
    // "A request may belong to at most one active Work Package"
    // =========================================================================

    [Fact]
    public void BusinessRule9_AddingRequestAlreadyActiveInSamePackage_ShouldThrowBusinessRule9ViolationException()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "Name", "Objective", Guid.NewGuid());
        var requestId = Guid.NewGuid();
        wp.AddRequest(requestId);

        // Act
        var act = () => wp.AddRequest(requestId);

        // Assert
        act.Should().Throw<BusinessRule9ViolationException>()
            .Where(e => e.RequestId == requestId && e.WorkPackageId == wp.Id)
            .WithMessage("*already an active member*");
    }

    [Fact]
    public void BusinessRule9_AddingRequestActiveInAnotherPackage_ViaDelegate_ShouldThrowBusinessRule9ViolationException()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "WP Beta", "Objective Beta", Guid.NewGuid());
        var requestId = Guid.NewGuid();

        // Delegate simulating external active package lookup returning true
        Func<Guid, bool> isRequestInAnotherActivePackage = id => id == requestId;

        // Act
        var act = () => wp.AddRequest(requestId, DateTime.UtcNow, isRequestInAnotherActivePackage);

        // Assert
        act.Should().Throw<BusinessRule9ViolationException>()
            .Where(e => e.RequestId == requestId && e.WorkPackageId == wp.Id)
            .WithMessage("*already belongs to an active Work Package*");
    }

    [Fact]
    public void BusinessRule9_AddingRequestActiveInAnotherPackage_ViaCheckerInterface_ShouldThrowBusinessRule9ViolationException()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "WP Gamma", "Objective Gamma", Guid.NewGuid());
        var requestId = Guid.NewGuid();

        var mockChecker = new StubMembershipChecker(activeRequestId: requestId);

        // Act
        var act = () => wp.AddRequest(requestId, DateTime.UtcNow, mockChecker);

        // Assert
        act.Should().Throw<BusinessRule9ViolationException>()
            .Where(e => e.RequestId == requestId && e.WorkPackageId == wp.Id);
    }

    [Fact]
    public void BusinessRule9_AddingRequestNotActiveInAnotherPackage_ShouldSucceed()
    {
        // Arrange
        var wp = WorkPackage.Create(Guid.NewGuid(), "WP Delta", "Objective Delta", Guid.NewGuid());
        var requestId = Guid.NewGuid();

        var mockChecker = new StubMembershipChecker(activeRequestId: Guid.NewGuid()); // different request

        // Act
        var membership = wp.AddRequest(requestId, DateTime.UtcNow, mockChecker);

        // Assert
        membership.Should().NotBeNull();
        wp.HasActiveRequest(requestId).Should().BeTrue();
    }

    [Fact]
    public void BusinessRule9_AddingRequestFromClosedPackage_ShouldSucceed()
    {
        // Arrange:
        // Package A was active with Request X, then Package A was closed.
        // Request X should now be allowed into Package B without violating Business Rule 9.
        var wpA = WorkPackage.Create(Guid.NewGuid(), "Package A", "Initial Phase", Guid.NewGuid());
        var requestId = Guid.NewGuid();
        wpA.AddRequest(requestId);
        wpA.Close("Phase finished");

        var wpB = WorkPackage.Create(Guid.NewGuid(), "Package B", "Subsequent Phase", Guid.NewGuid());

        // Checker verifies that since wpA is closed, requestId is NOT in an active package
        Func<Guid, bool> isRequestInAnotherActivePackage = _ => wpA.IsActive; // wpA is closed, so false

        // Act
        var membership = wpB.AddRequest(requestId, DateTime.UtcNow, isRequestInAnotherActivePackage);

        // Assert
        membership.Should().NotBeNull();
        wpB.HasActiveRequest(requestId).Should().BeTrue();
    }

    [Fact]
    public void BusinessRule9Policy_Enforce_ShouldThrowWhenTrue_AndPassWhenFalse()
    {
        var reqId = Guid.NewGuid();
        var wpId = Guid.NewGuid();

        var actViolated = () => BusinessRule9Policy.Enforce(reqId, wpId, isRequestInAnotherActivePackage: true);
        actViolated.Should().Throw<BusinessRule9ViolationException>()
            .Where(e => e.RequestId == reqId && e.WorkPackageId == wpId);

        var actAllowed = () => BusinessRule9Policy.Enforce(reqId, wpId, isRequestInAnotherActivePackage: false);
        actAllowed.Should().NotThrow();
    }

    // =========================================================================
    // 7. WorkPackageRequest Entity Tests
    // =========================================================================

    [Fact]
    public void WorkPackageRequest_Constructor_WithInvalidArguments_ShouldThrowArgumentException()
    {
        var id = Guid.NewGuid();
        var wpId = Guid.NewGuid();
        var reqId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var act1 = () => new WorkPackageRequest(Guid.Empty, wpId, reqId, now);
        act1.Should().Throw<ArgumentException>().WithParameterName("id");

        var act2 = () => new WorkPackageRequest(id, Guid.Empty, reqId, now);
        act2.Should().Throw<ArgumentException>().WithParameterName("workPackageId");

        var act3 = () => new WorkPackageRequest(id, wpId, Guid.Empty, now);
        act3.Should().Throw<ArgumentException>().WithParameterName("requestId");
    }

    // =========================================================================
    // 8. Hydration Constructor (Dapper Mapping) Test
    // =========================================================================

    [Fact]
    public void WorkPackage_HydrationConstructor_ShouldRestoreStateWithoutRaisingEvents()
    {
        // Arrange
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-10);
        var updatedAt = DateTime.UtcNow.AddDays(-2);
        var closedAt = DateTime.UtcNow.AddDays(-1);

        var req1 = new WorkPackageRequest(Guid.NewGuid(), id, Guid.NewGuid(), createdAt, updatedAt, isActive: false);
        var req2 = new WorkPackageRequest(Guid.NewGuid(), id, Guid.NewGuid(), updatedAt, null, isActive: true);

        // Act
        var wp = new WorkPackage(
            id,
            "Hydrated Package",
            "Restored Objective",
            WorkPackageStatus.Closed,
            ownerId,
            customerId,
            productId,
            createdAt,
            updatedAt,
            closedAt,
            "Completed all tasks",
            [req1, req2]);

        // Assert
        wp.Id.Should().Be(id);
        wp.Name.Should().Be("Hydrated Package");
        wp.Objective.Should().Be("Restored Objective");
        wp.Status.Should().Be(WorkPackageStatus.Closed);
        wp.OwnerPersonId.Should().Be(ownerId);
        wp.CustomerId.Should().Be(customerId);
        wp.ProductId.Should().Be(productId);
        wp.CreatedAt.Should().Be(createdAt);
        wp.UpdatedAt.Should().Be(updatedAt);
        wp.ClosedAt.Should().Be(closedAt);
        wp.CloseReason.Should().Be("Completed all tasks");
        wp.IsClosed.Should().BeTrue();
        wp.Requests.Should().HaveCount(2);
        wp.ActiveRequests.Should().HaveCount(1);
        wp.DomainEvents.Should().BeEmpty(); // No domain events raised during hydration
    }

    // Helper stub for tests
    private sealed class StubMembershipChecker : IWorkPackageMembershipChecker
    {
        private readonly Guid _activeRequestId;

        public StubMembershipChecker(Guid activeRequestId)
        {
            _activeRequestId = activeRequestId;
        }

        public bool IsRequestInActiveWorkPackage(Guid requestId, Guid? excludeWorkPackageId = null)
        {
            return requestId == _activeRequestId;
        }
    }
}
