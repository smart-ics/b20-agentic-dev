using Cakra.Modules.WorkPackage.Domain;
using Cakra.Modules.WorkPackage.Domain.Events;
using Cakra.Modules.WorkPackage.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.WorkPackage;

public sealed class WorkPackageDomainTests
{
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_InitializesDraftWorkPackageAndEmitsEvent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "RSUD Go-Live Preparation",
            objective: "Coordinate all launch tasks for RSUD",
            ownerPersonId: _ownerId,
            customerId: _customerId,
            productId: _productId,
            id: id,
            createdAtUtc: now);

        // Assert
        package.Id.Should().Be(id);
        package.Name.Should().Be("RSUD Go-Live Preparation");
        package.Objective.Should().Be("Coordinate all launch tasks for RSUD");
        package.OwnerPersonId.Should().Be(_ownerId);
        package.CustomerId.Should().Be(_customerId);
        package.ProductId.Should().Be(_productId);
        package.Status.Should().Be(WorkPackageStatus.Draft);
        package.CreatedAt.Should().Be(now);
        package.UpdatedAt.Should().BeNull();
        package.ClosedReason.Should().BeNull();
        package.ClosedAt.Should().BeNull();
        package.Requests.Should().BeEmpty();

        package.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkPackageCreated>()
            .Which.Should().BeEquivalentTo(new
            {
                WorkPackageId = id,
                Name = "RSUD Go-Live Preparation",
                Objective = "Coordinate all launch tasks for RSUD",
                OwnerPersonId = _ownerId,
                CustomerId = (Guid?)_customerId,
                ProductId = (Guid?)_productId,
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Act
        var act = () => Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: invalidName!,
            objective: "Valid objective",
            ownerPersonId: _ownerId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidObjective_ThrowsArgumentException(string? invalidObjective)
    {
        // Act
        var act = () => Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Valid name",
            objective: invalidObjective!,
            ownerPersonId: _ownerId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("objective");
    }

    [Fact]
    public void Create_WithEmptyOwnerPersonId_ThrowsArgumentException()
    {
        // Act
        var act = () => Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Valid name",
            objective: "Valid objective",
            ownerPersonId: Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("ownerPersonId");
    }

    [Fact]
    public void Create_WithEmptyId_ThrowsArgumentException()
    {
        // Act
        var act = () => Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Valid name",
            objective: "Valid objective",
            ownerPersonId: _ownerId,
            id: Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("id");
    }

    #region Lifecycle State Machine Tests

    [Fact]
    public void Activate_FromDraft_TransitionsToActiveAndEmitsEvent()
    {
        // Arrange
        var package = CreateDraftPackage();
        package.ClearDomainEvents();
        var activateTime = DateTime.UtcNow;

        // Act
        package.Activate(activateTime);

        // Assert
        package.Status.Should().Be(WorkPackageStatus.Active);
        package.UpdatedAt.Should().Be(activateTime);
        package.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkPackageActivated>()
            .Which.Should().BeEquivalentTo(new
            {
                WorkPackageId = package.Id,
                PreviousStatus = WorkPackageStatus.Draft,
                NewStatus = WorkPackageStatus.Active,
                OccurredAtUtc = activateTime
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ThrowsInvalidWorkPackageStateTransitionException()
    {
        // Arrange
        var package = CreateDraftPackage();
        package.Activate();

        // Act
        var act = () => package.Activate();

        // Assert
        var ex = act.Should().Throw<InvalidWorkPackageStateTransitionException>().Which;
        ex.WorkPackageId.Should().Be(package.Id);
        ex.FromStatus.Should().Be(WorkPackageStatus.Active);
        ex.ToStatus.Should().Be(WorkPackageStatus.Active);
    }

    [Fact]
    public void Activate_WhenClosed_ThrowsInvalidWorkPackageStateTransitionException()
    {
        // Arrange
        var package = CreateDraftPackage();
        package.Close("Done");

        // Act
        var act = () => package.Activate();

        // Assert
        var ex = act.Should().Throw<InvalidWorkPackageStateTransitionException>().Which;
        ex.WorkPackageId.Should().Be(package.Id);
        ex.FromStatus.Should().Be(WorkPackageStatus.Closed);
        ex.ToStatus.Should().Be(WorkPackageStatus.Active);
    }

    [Fact]
    public void Close_FromActive_TransitionsToClosedAndEmitsEvent()
    {
        // Arrange
        var package = CreateDraftPackage();
        package.Activate();
        package.ClearDomainEvents();
        var closeTime = DateTime.UtcNow;

        // Act
        package.Close("Scope fulfilled", closeTime);

        // Assert
        package.Status.Should().Be(WorkPackageStatus.Closed);
        package.ClosedReason.Should().Be("Scope fulfilled");
        package.ClosedAt.Should().Be(closeTime);
        package.UpdatedAt.Should().Be(closeTime);
        package.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkPackageClosed>()
            .Which.Should().BeEquivalentTo(new
            {
                WorkPackageId = package.Id,
                Reason = "Scope fulfilled",
                PreviousStatus = WorkPackageStatus.Active,
                NewStatus = WorkPackageStatus.Closed,
                OccurredAtUtc = closeTime
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void Close_FromDraftDirectly_TransitionsToClosedAndEmitsEvent()
    {
        // Arrange
        var package = CreateDraftPackage();
        package.ClearDomainEvents();
        var closeTime = DateTime.UtcNow;

        // Act
        package.Close("Cancelled before activation", closeTime);

        // Assert
        package.Status.Should().Be(WorkPackageStatus.Closed);
        package.ClosedReason.Should().Be("Cancelled before activation");
        package.ClosedAt.Should().Be(closeTime);
        package.UpdatedAt.Should().Be(closeTime);
        package.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkPackageClosed>()
            .Which.Should().BeEquivalentTo(new
            {
                WorkPackageId = package.Id,
                Reason = "Cancelled before activation",
                PreviousStatus = WorkPackageStatus.Draft,
                NewStatus = WorkPackageStatus.Closed,
                OccurredAtUtc = closeTime
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void Close_WhenAlreadyClosed_ThrowsInvalidWorkPackageStateTransitionException()
    {
        // Arrange
        var package = CreateDraftPackage();
        package.Close("Completed");

        // Act
        var act = () => package.Close("Closing again");

        // Assert
        var ex = act.Should().Throw<InvalidWorkPackageStateTransitionException>().Which;
        ex.WorkPackageId.Should().Be(package.Id);
        ex.FromStatus.Should().Be(WorkPackageStatus.Closed);
        ex.ToStatus.Should().Be(WorkPackageStatus.Closed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Close_WithEmptyReason_ThrowsArgumentException(string? invalidReason)
    {
        // Arrange
        var package = CreateDraftPackage();

        // Act
        var act = () => package.Close(invalidReason!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("reason");
    }

    #endregion

    #region Objective & Owner Management Tests

    [Fact]
    public void UpdateObjective_WhenNotClosed_UpdatesValues()
    {
        // Arrange
        var package = CreateDraftPackage();
        var updateTime = DateTime.UtcNow;

        // Act
        package.UpdateObjective("Updated Name", "Updated Objective", updateTime);

        // Assert
        package.Name.Should().Be("Updated Name");
        package.Objective.Should().Be("Updated Objective");
        package.UpdatedAt.Should().Be(updateTime);
    }

    [Fact]
    public void UpdateObjective_WhenClosed_ThrowsWorkPackageDomainException()
    {
        // Arrange
        var package = CreateDraftPackage();
        package.Close("Finished");

        // Act
        var act = () => package.UpdateObjective("New Name", "New Objective");

        // Assert
        act.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*closed*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateObjective_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Arrange
        var package = CreateDraftPackage();

        // Act
        var act = () => package.UpdateObjective(invalidName!, "Valid Objective");

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateObjective_WithInvalidObjective_ThrowsArgumentException(string? invalidObjective)
    {
        // Arrange
        var package = CreateDraftPackage();

        // Act
        var act = () => package.UpdateObjective("Valid Name", invalidObjective!);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("objective");
    }

    [Fact]
    public void AssignOwner_WhenDifferentPerson_UpdatesOwnerAndEmitsEvent()
    {
        // Arrange
        var package = CreateDraftPackage();
        package.ClearDomainEvents();
        var newOwnerId = Guid.NewGuid();
        var updateTime = DateTime.UtcNow;

        // Act
        package.AssignOwner(newOwnerId, updateTime);

        // Assert
        package.OwnerPersonId.Should().Be(newOwnerId);
        package.UpdatedAt.Should().Be(updateTime);
        package.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkPackageOwnerChanged>()
            .Which.Should().BeEquivalentTo(new
            {
                WorkPackageId = package.Id,
                PreviousOwnerPersonId = _ownerId,
                NewOwnerPersonId = newOwnerId,
                OccurredAtUtc = updateTime
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void AssignOwner_WhenSamePerson_DoesNotEmitEvent()
    {
        // Arrange
        var package = CreateDraftPackage();
        package.ClearDomainEvents();

        // Act
        package.AssignOwner(_ownerId);

        // Assert
        package.OwnerPersonId.Should().Be(_ownerId);
        package.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AssignOwner_WhenEmptyGuid_ThrowsArgumentException()
    {
        // Arrange
        var package = CreateDraftPackage();

        // Act
        var act = () => package.AssignOwner(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("newOwnerPersonId");
    }

    [Fact]
    public void AssignOwner_WhenClosed_ThrowsWorkPackageDomainException()
    {
        // Arrange
        var package = CreateDraftPackage();
        package.Close("Finished");

        // Act
        var act = () => package.AssignOwner(Guid.NewGuid());

        // Assert
        act.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*closed*");
    }

    #endregion

    #region Request Addition & Removal Tests

    [Fact]
    public void AddRequest_InDraftState_AddsMembershipAndEmitsEvent()
    {
        // Arrange
        var package = CreateDraftPackage();
        package.ClearDomainEvents();
        var requestId = Guid.NewGuid();
        var addTime = DateTime.UtcNow;

        // Act
        var membership = package.AddRequest(requestId, addTime);

        // Assert
        membership.WorkPackageId.Should().Be(package.Id);
        membership.RequestId.Should().Be(requestId);
        membership.AddedAt.Should().Be(addTime);
        membership.RemovedAt.Should().BeNull();
        membership.IsActive.Should().BeTrue();

        package.Requests.Should().ContainSingle().Which.Should().Be(membership);
        package.ActiveRequests.Should().ContainSingle().Which.Should().Be(membership);
        package.UpdatedAt.Should().Be(addTime);

        package.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RequestAddedToWorkPackage>()
            .Which.Should().BeEquivalentTo(new
            {
                WorkPackageId = package.Id,
                RequestId = requestId,
                OccurredAtUtc = addTime
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void AddRequest_InActiveState_AddsMembershipAndEmitsEvent()
    {
        // Arrange
        var package = CreateDraftPackage();
        package.Activate();
        package.ClearDomainEvents();
        var requestId = Guid.NewGuid();
        var addTime = DateTime.UtcNow;

        // Act
        var membership = package.AddRequest(requestId, addTime);

        // Assert
        package.Requests.Should().ContainSingle().Which.Should().Be(membership);
        package.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RequestAddedToWorkPackage>();
    }

    [Fact]
    public void AddRequest_InClosedState_ThrowsWorkPackageDomainException()
    {
        // Arrange
        var package = CreateDraftPackage();
        package.Close("Finished");

        // Act
        var act = () => package.AddRequest(Guid.NewGuid());

        // Assert
        act.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*closed*");
    }

    [Fact]
    public void AddRequest_WithEmptyRequestId_ThrowsArgumentException()
    {
        // Arrange
        var package = CreateDraftPackage();

        // Act
        var act = () => package.AddRequest(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("requestId");
    }

    [Fact]
    public void RemoveRequest_InDraftState_MarksRemovedAndEmitsEvent()
    {
        // Arrange
        var package = CreateDraftPackage();
        var requestId = Guid.NewGuid();
        var addTime = DateTime.UtcNow.AddMinutes(-5);
        package.AddRequest(requestId, addTime);
        package.ClearDomainEvents();

        var removeTime = DateTime.UtcNow;

        // Act
        package.RemoveRequest(requestId, removeTime);

        // Assert
        package.Requests.Should().ContainSingle();
        package.ActiveRequests.Should().BeEmpty();

        var membership = package.Requests.Single();
        membership.IsActive.Should().BeFalse();
        membership.RemovedAt.Should().Be(removeTime);
        package.UpdatedAt.Should().Be(removeTime);

        package.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RequestRemovedFromWorkPackage>()
            .Which.Should().BeEquivalentTo(new
            {
                WorkPackageId = package.Id,
                RequestId = requestId,
                OccurredAtUtc = removeTime
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void RemoveRequest_InActiveState_MarksRemovedAndEmitsEvent()
    {
        // Arrange
        var package = CreateDraftPackage();
        var requestId = Guid.NewGuid();
        package.AddRequest(requestId);
        package.Activate();
        package.ClearDomainEvents();

        // Act
        package.RemoveRequest(requestId);

        // Assert
        package.ActiveRequests.Should().BeEmpty();
        package.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RequestRemovedFromWorkPackage>();
    }

    [Fact]
    public void RemoveRequest_InClosedState_ThrowsWorkPackageDomainException()
    {
        // Arrange
        var package = CreateDraftPackage();
        var requestId = Guid.NewGuid();
        package.AddRequest(requestId);
        package.Close("Closed");

        // Act
        var act = () => package.RemoveRequest(requestId);

        // Assert
        act.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*closed*");
    }

    [Fact]
    public void RemoveRequest_WhenRequestNotPresent_ThrowsWorkPackageDomainException()
    {
        // Arrange
        var package = CreateDraftPackage();

        // Act
        var act = () => package.RemoveRequest(Guid.NewGuid());

        // Assert
        act.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*not an active member*");
    }

    [Fact]
    public void RemoveRequest_WhenAlreadyRemoved_ThrowsWorkPackageDomainException()
    {
        // Arrange
        var package = CreateDraftPackage();
        var requestId = Guid.NewGuid();
        package.AddRequest(requestId);
        package.RemoveRequest(requestId);

        // Act
        var act = () => package.RemoveRequest(requestId);

        // Assert
        act.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*not an active member*");
    }

    [Fact]
    public void ReAddingRemovedRequest_SucceedsAndPreservesHistoricalTraceability()
    {
        // Arrange
        var package = CreateDraftPackage();
        var requestId = Guid.NewGuid();
        var t1 = DateTime.UtcNow.AddHours(-2);
        var t2 = DateTime.UtcNow.AddHours(-1);
        var t3 = DateTime.UtcNow;

        package.AddRequest(requestId, t1);
        package.RemoveRequest(requestId, t2);

        // Act - re-add same request
        package.AddRequest(requestId, t3);

        // Assert (Business Rule 15: Historical Work Package membership must remain traceable)
        package.Requests.Should().HaveCount(2);
        package.Requests.Count(r => r.RequestId == requestId && !r.IsActive).Should().Be(1);
        package.Requests.Count(r => r.RequestId == requestId && r.IsActive).Should().Be(1);
        package.ActiveRequests.Should().ContainSingle().Which.RequestId.Should().Be(requestId);
    }

    #endregion

    #region Reordering & SortOrder Tests

    [Fact]
    public void WorkPackage_AddRequest_AssignsSequentialSortOrder()
    {
        // Arrange
        var package = CreateDraftPackage();
        var req1 = Guid.NewGuid();
        var req2 = Guid.NewGuid();
        var req3 = Guid.NewGuid();

        // Act
        var m1 = package.AddRequest(req1);
        var m2 = package.AddRequest(req2);
        var m3 = package.AddRequest(req3);

        // Assert
        m1.SortOrder.Should().Be(0);
        m2.SortOrder.Should().Be(1);
        m3.SortOrder.Should().Be(2);

        package.ActiveRequests.Select(r => r.RequestId)
            .Should().ContainInOrder(req1, req2, req3);
    }

    [Fact]
    public void WorkPackage_ReorderRequests_UpdatesSortOrderCorrectly()
    {
        // Arrange
        var package = CreateDraftPackage();
        var req1 = Guid.NewGuid();
        var req2 = Guid.NewGuid();
        var req3 = Guid.NewGuid();

        var m1 = package.AddRequest(req1);
        var m2 = package.AddRequest(req2);
        var m3 = package.AddRequest(req3);

        var reorderTime = DateTime.UtcNow.AddMinutes(5);

        // Act - Reorder: [req3, req1, req2]
        package.ReorderRequests(new[] { req3, req1, req2 }, reorderTime);

        // Assert
        m3.SortOrder.Should().Be(0);
        m1.SortOrder.Should().Be(1);
        m2.SortOrder.Should().Be(2);

        package.UpdatedAt.Should().Be(reorderTime);
        package.ActiveRequests.Select(r => r.RequestId)
            .Should().ContainInOrder(req3, req1, req2);

        // Also verify reordering in ACTIVE state works
        package.Activate();
        var reorderActiveTime = DateTime.UtcNow.AddMinutes(10);
        package.ReorderRequests(new[] { req2, req3, req1 }, reorderActiveTime);

        m2.SortOrder.Should().Be(0);
        m3.SortOrder.Should().Be(1);
        m1.SortOrder.Should().Be(2);
        package.ActiveRequests.Select(r => r.RequestId)
            .Should().ContainInOrder(req2, req3, req1);
    }

    [Fact]
    public void WorkPackage_ReorderRequests_WhenClosed_ThrowsException()
    {
        // Arrange
        var package = CreateDraftPackage();
        var req1 = Guid.NewGuid();
        var req2 = Guid.NewGuid();
        package.AddRequest(req1);
        package.AddRequest(req2);
        package.Close("Scope finished");

        // Act
        var act = () => package.ReorderRequests(new[] { req2, req1 });

        // Assert
        act.Should().Throw<InvalidWorkPackageStateTransitionException>()
            .WithMessage("*CLOSED*");
    }

    [Fact]
    public void WorkPackage_ReorderRequests_WithMismatchedIds_ThrowsException()
    {
        // Arrange
        var package = CreateDraftPackage();
        var req1 = Guid.NewGuid();
        var req2 = Guid.NewGuid();
        var req3 = Guid.NewGuid();
        package.AddRequest(req1);
        package.AddRequest(req2);
        package.AddRequest(req3);

        var foreignReq = Guid.NewGuid();

        // 1. Less IDs than active requests count
        var actLess = () => package.ReorderRequests(new[] { req1, req2 });
        actLess.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*count*");

        // 2. More IDs than active requests count
        var actMore = () => package.ReorderRequests(new[] { req1, req2, req3, foreignReq });
        actMore.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*count*");

        // 3. Duplicate IDs
        var actDuplicate = () => package.ReorderRequests(new[] { req1, req1, req2 });
        actDuplicate.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*duplicate*");

        // 4. Same count but contains foreign/inactive ID
        var actForeign = () => package.ReorderRequests(new[] { req1, req2, foreignReq });
        actForeign.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*not an active member*");

        // 5. Null argument
        var actNull = () => package.ReorderRequests(null!);
        actNull.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Business Rule 9 Tests

    [Fact]
    public void AddRequest_WhenAlreadyActiveInSamePackage_ThrowsBusinessRuleViolationException()
    {
        // Arrange
        var package = CreateDraftPackage();
        var requestId = Guid.NewGuid();
        package.AddRequest(requestId);

        // Act (adding again while still active)
        var act = () => package.AddRequest(requestId);

        // Assert
        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.RuleNumber.Should().Be(9);
        ex.Message.Should().Contain("Business Rule 9");
    }

    [Fact]
    public void AddRequest_WhenExternalCheckerReportsRequestInAnotherActivePackage_ThrowsBusinessRuleViolationException()
    {
        // Arrange
        var package = CreateDraftPackage();
        var requestId = Guid.NewGuid();
        var checker = new StubActiveWorkPackageChecker(rId => rId == requestId);

        // Act
        var act = () => package.AddRequest(requestId, activeWorkPackageChecker: checker);

        // Assert
        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.RuleNumber.Should().Be(9);
        ex.Message.Should().Contain("Business Rule 9");
    }

    [Fact]
    public void AddRequest_WhenPredicateReportsRequestInAnotherActivePackage_ThrowsBusinessRuleViolationException()
    {
        // Arrange
        var package = CreateDraftPackage();
        var requestId = Guid.NewGuid();

        // Act
        var act = () => package.AddRequest(
            requestId,
            isAlreadyInActiveWorkPackage: reqId => reqId == requestId);

        // Assert
        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.RuleNumber.Should().Be(9);
        ex.Message.Should().Contain("Business Rule 9");
    }

    [Fact]
    public void AddRequest_WhenExternalCheckerReportsRequestNotInAnotherActivePackage_Succeeds()
    {
        // Arrange
        var package = CreateDraftPackage();
        var requestId = Guid.NewGuid();
        var checker = new StubActiveWorkPackageChecker(_ => false);

        // Act
        var membership = package.AddRequest(requestId, activeWorkPackageChecker: checker);

        // Assert
        membership.Should().NotBeNull();
        package.ActiveRequests.Should().ContainSingle().Which.RequestId.Should().Be(requestId);
    }

    #endregion

    #region Rehydration & Domain Event Management Tests

    [Fact]
    public void ClearDomainEvents_RemovesAllRecordedEvents()
    {
        // Arrange
        var package = CreateDraftPackage();
        package.DomainEvents.Should().NotBeEmpty();

        // Act
        package.ClearDomainEvents();

        // Assert
        package.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Rehydrate_RestoresWorkPackageAndMembershipWithoutEmittingEvents()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var created = DateTime.UtcNow.AddDays(-2);
        var updated = DateTime.UtcNow.AddDays(-1);
        var request = WorkPackageRequest.Rehydrate(
            id: Guid.NewGuid(),
            workPackageId: packageId,
            requestId: requestId,
            addedAt: created,
            removedAt: null);

        // Act
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Rehydrate(
            id: packageId,
            name: "Rehydrated Package",
            objective: "Rehydrated Objective",
            status: WorkPackageStatus.Active,
            ownerPersonId: _ownerId,
            customerId: _customerId,
            productId: _productId,
            closedReason: null,
            closedAt: null,
            createdAt: created,
            updatedAt: updated,
            requests: new[] { request });

        // Assert
        package.Id.Should().Be(packageId);
        package.Name.Should().Be("Rehydrated Package");
        package.Objective.Should().Be("Rehydrated Objective");
        package.Status.Should().Be(WorkPackageStatus.Active);
        package.OwnerPersonId.Should().Be(_ownerId);
        package.CustomerId.Should().Be(_customerId);
        package.ProductId.Should().Be(_productId);
        package.CreatedAt.Should().Be(created);
        package.UpdatedAt.Should().Be(updated);
        package.Requests.Should().ContainSingle().Which.RequestId.Should().Be(requestId);
        package.ActiveRequests.Should().ContainSingle();
        package.DomainEvents.Should().BeEmpty();
    }

    #endregion

    private Cakra.Modules.WorkPackage.Domain.WorkPackage CreateDraftPackage()
    {
        return Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Default Test Package",
            objective: "Default Test Objective",
            ownerPersonId: _ownerId,
            customerId: _customerId,
            productId: _productId);
    }

    private sealed class StubActiveWorkPackageChecker : IActiveWorkPackageChecker
    {
        private readonly Func<Guid, bool> _isInActiveWorkPackage;

        public StubActiveWorkPackageChecker(Func<Guid, bool> isInActiveWorkPackage)
        {
            _isInActiveWorkPackage = isInActiveWorkPackage;
        }

        public bool IsRequestInActiveWorkPackage(Guid requestId, Guid? excludingWorkPackageId = null)
        {
            return _isInActiveWorkPackage(requestId);
        }
    }
}
