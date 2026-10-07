using Cakra.Modules.WorkPackage.Domain;
using Cakra.Modules.WorkPackage.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.WorkPackage;

public sealed class WorkPackageDeadlineDomainTests
{
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    [Fact]
    public void Create_WithDeadline_NormalizesToUtcMidnight()
    {
        // Arrange
        var deadlineInput = new DateTime(2026, 11, 20, 16, 45, 30, DateTimeKind.Local);
        var expectedNormalized = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Launch Phase 1",
            objective: "Coordinate launch milestones",
            ownerPersonId: _ownerId,
            customerId: _customerId,
            productId: _productId,
            deadline: deadlineInput);

        // Assert
        package.Deadline.Should().Be(expectedNormalized);
        package.Deadline!.Value.Kind.Should().Be(DateTimeKind.Utc);
        package.Deadline.Value.Hour.Should().Be(0);
        package.Deadline.Value.Minute.Should().Be(0);
        package.Deadline.Value.Second.Should().Be(0);
    }

    [Fact]
    public void Create_WithoutDeadline_LeavesDeadlineNull()
    {
        // Act
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Launch Phase 1",
            objective: "Coordinate launch milestones",
            ownerPersonId: _ownerId,
            customerId: _customerId,
            productId: _productId);

        // Assert
        package.Deadline.Should().BeNull();
    }

    [Fact]
    public void UpdateDeadline_WhenDraft_UpdatesDeadlineAndSetsUpdatedAt()
    {
        // Arrange
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Draft Package",
            objective: "Initial objectives",
            ownerPersonId: _ownerId);

        var newDeadline = new DateTime(2026, 12, 1, 10, 30, 0, DateTimeKind.Utc);
        var expectedNormalized = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var updateTime = DateTime.UtcNow.AddMinutes(5);

        // Act
        package.UpdateDeadline(newDeadline, updateTime);

        // Assert
        package.Deadline.Should().Be(expectedNormalized);
        package.Deadline!.Value.Kind.Should().Be(DateTimeKind.Utc);
        package.UpdatedAt.Should().Be(updateTime);
    }

    [Fact]
    public void UpdateDeadline_WhenDraft_WithoutExplicitUpdatedAt_UsesCurrentTime()
    {
        // Arrange
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Draft Package",
            objective: "Initial objectives",
            ownerPersonId: _ownerId);

        var before = DateTime.UtcNow;
        var newDeadline = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        package.UpdateDeadline(newDeadline);
        var after = DateTime.UtcNow;

        // Assert
        package.Deadline.Should().Be(newDeadline);
        package.UpdatedAt.Should().NotBeNull();
        package.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void UpdateDeadline_WhenActive_UpdatesDeadlineAndSetsUpdatedAt()
    {
        // Arrange
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Active Package",
            objective: "Initial objectives",
            ownerPersonId: _ownerId);
        package.Activate();

        var newDeadline = new DateTime(2026, 12, 25, 18, 0, 0, DateTimeKind.Local);
        var expectedNormalized = new DateTime(2026, 12, 25, 0, 0, 0, DateTimeKind.Utc);
        var updateTime = DateTime.UtcNow.AddMinutes(10);

        // Act
        package.UpdateDeadline(newDeadline, updateTime);

        // Assert
        package.Deadline.Should().Be(expectedNormalized);
        package.UpdatedAt.Should().Be(updateTime);
    }

    [Fact]
    public void UpdateDeadline_ClearingExistingDeadlineToNull_UpdatesDeadlineAndSetsUpdatedAt()
    {
        // Arrange
        var initialDeadline = new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Package With Deadline",
            objective: "Objectives",
            ownerPersonId: _ownerId,
            deadline: initialDeadline);

        var updateTime = DateTime.UtcNow.AddMinutes(15);

        // Act
        package.UpdateDeadline(null, updateTime);

        // Assert
        package.Deadline.Should().BeNull();
        package.UpdatedAt.Should().Be(updateTime);
    }

    [Fact]
    public void UpdateDeadline_WhenDeadlineUnchanged_DoesNotChangeUpdatedAt()
    {
        // Arrange
        var initialDeadline = new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);
        var initialCreateTime = DateTime.UtcNow.AddHours(-1);
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Package With Deadline",
            objective: "Objectives",
            ownerPersonId: _ownerId,
            createdAtUtc: initialCreateTime,
            deadline: initialDeadline);

        var attemptedUpdateTime = DateTime.UtcNow;
        var sameDateDifferentTime = new DateTime(2026, 11, 15, 14, 20, 0, DateTimeKind.Local);

        // Act
        package.UpdateDeadline(sameDateDifferentTime, attemptedUpdateTime);

        // Assert
        package.Deadline.Should().Be(initialDeadline);
        package.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void UpdateDeadline_WhenPackageClosed_ThrowsWorkPackageDomainException()
    {
        // Arrange
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Closed Package",
            objective: "Objectives",
            ownerPersonId: _ownerId);
        package.Close("Scope finished");

        // Act
        var actNewDate = () => package.UpdateDeadline(DateTime.UtcNow.AddDays(7));
        var actClear = () => package.UpdateDeadline(null);

        // Assert
        actNewDate.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*closed*");
        actClear.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*closed*");
    }

    [Fact]
    public void Rehydrate_WithDeadline_SetsNormalizedDeadline()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var rawDeadline = new DateTime(2026, 12, 10, 8, 15, 0, DateTimeKind.Local);
        var expectedNormalized = new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc);
        var now = DateTime.UtcNow;

        // Act
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Rehydrate(
            id: packageId,
            name: "Rehydrated Package",
            objective: "Objectives",
            status: WorkPackageStatus.Active,
            ownerPersonId: _ownerId,
            customerId: _customerId,
            productId: _productId,
            closedReason: null,
            closedAt: null,
            createdAt: now.AddDays(-2),
            updatedAt: now.AddDays(-1),
            deadline: rawDeadline);

        // Assert
        package.Deadline.Should().Be(expectedNormalized);
        package.Deadline!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Rehydrate_WithoutDeadline_LeavesDeadlineNull()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Rehydrate(
            id: packageId,
            name: "Rehydrated Package",
            objective: "Objectives",
            status: WorkPackageStatus.Active,
            ownerPersonId: _ownerId,
            customerId: _customerId,
            productId: _productId,
            closedReason: null,
            closedAt: null,
            createdAt: now.AddDays(-2),
            updatedAt: now.AddDays(-1));

        // Assert
        package.Deadline.Should().BeNull();
    }
}
