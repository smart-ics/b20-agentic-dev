using Cakra.Modules.WorkPackage.Domain;
using Cakra.Modules.WorkPackage.Domain.Events;
using Cakra.Modules.WorkPackage.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.WorkPackage;

public sealed class WorkPackageContextDomainTests
{
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _customerId1 = Guid.NewGuid();
    private readonly Guid _customerId2 = Guid.NewGuid();
    private readonly Guid _productId1 = Guid.NewGuid();
    private readonly Guid _productId2 = Guid.NewGuid();

    [Fact]
    public void UpdateContext_WhenDraft_UpdatesCustomerAndProduct_SetsUpdatedAt_AndRecordsEvent()
    {
        // Arrange
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Draft Package",
            objective: "Initial objectives",
            ownerPersonId: _ownerId,
            customerId: _customerId1,
            productId: _productId1);

        package.ClearDomainEvents();
        var updateTime = DateTime.UtcNow.AddMinutes(5);

        // Act
        package.UpdateContext(_customerId2, _productId2, updateTime);

        // Assert
        package.CustomerId.Should().Be(_customerId2);
        package.ProductId.Should().Be(_productId2);
        package.UpdatedAt.Should().Be(updateTime);

        package.DomainEvents.Should().ContainSingle();
        var domainEvent = package.DomainEvents.OfType<WorkPackageContextChanged>().Single();
        domainEvent.WorkPackageId.Should().Be(package.Id);
        domainEvent.PreviousCustomerId.Should().Be(_customerId1);
        domainEvent.NewCustomerId.Should().Be(_customerId2);
        domainEvent.PreviousProductId.Should().Be(_productId1);
        domainEvent.NewProductId.Should().Be(_productId2);
        domainEvent.OccurredAtUtc.Should().Be(updateTime);
    }

    [Fact]
    public void UpdateContext_WhenActive_UpdatesCustomerAndProduct_SetsUpdatedAt_AndRecordsEvent()
    {
        // Arrange
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Active Package",
            objective: "Initial objectives",
            ownerPersonId: _ownerId,
            customerId: _customerId1,
            productId: _productId1);
        package.Activate();
        package.ClearDomainEvents();

        var updateTime = DateTime.UtcNow.AddMinutes(10);

        // Act
        package.UpdateContext(_customerId2, _productId2, updateTime);

        // Assert
        package.CustomerId.Should().Be(_customerId2);
        package.ProductId.Should().Be(_productId2);
        package.UpdatedAt.Should().Be(updateTime);

        var domainEvent = package.DomainEvents.OfType<WorkPackageContextChanged>().Single();
        domainEvent.PreviousCustomerId.Should().Be(_customerId1);
        domainEvent.NewCustomerId.Should().Be(_customerId2);
        domainEvent.PreviousProductId.Should().Be(_productId1);
        domainEvent.NewProductId.Should().Be(_productId2);
    }

    [Fact]
    public void UpdateContext_WithoutExplicitUpdatedAt_UsesCurrentTime()
    {
        // Arrange
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Draft Package",
            objective: "Initial objectives",
            ownerPersonId: _ownerId);
        package.ClearDomainEvents();

        var before = DateTime.UtcNow;

        // Act
        package.UpdateContext(_customerId1, _productId1);
        var after = DateTime.UtcNow;

        // Assert
        package.CustomerId.Should().Be(_customerId1);
        package.ProductId.Should().Be(_productId1);
        package.UpdatedAt.Should().NotBeNull();
        package.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);

        var domainEvent = package.DomainEvents.OfType<WorkPackageContextChanged>().Single();
        domainEvent.OccurredAtUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void UpdateContext_ClearingWithNull_SetsToNull_AndRecordsEvent()
    {
        // Arrange
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Package With Context",
            objective: "Initial objectives",
            ownerPersonId: _ownerId,
            customerId: _customerId1,
            productId: _productId1);
        package.ClearDomainEvents();

        var updateTime = DateTime.UtcNow.AddMinutes(5);

        // Act
        package.UpdateContext(null, null, updateTime);

        // Assert
        package.CustomerId.Should().BeNull();
        package.ProductId.Should().BeNull();
        package.UpdatedAt.Should().Be(updateTime);

        var domainEvent = package.DomainEvents.OfType<WorkPackageContextChanged>().Single();
        domainEvent.PreviousCustomerId.Should().Be(_customerId1);
        domainEvent.NewCustomerId.Should().BeNull();
        domainEvent.PreviousProductId.Should().Be(_productId1);
        domainEvent.NewProductId.Should().BeNull();
    }

    [Fact]
    public void UpdateContext_ClearingWithGuidEmpty_NormalizesToNull_AndRecordsEvent()
    {
        // Arrange
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Package With Context",
            objective: "Initial objectives",
            ownerPersonId: _ownerId,
            customerId: _customerId1,
            productId: _productId1);
        package.ClearDomainEvents();

        var updateTime = DateTime.UtcNow.AddMinutes(5);

        // Act
        package.UpdateContext(Guid.Empty, Guid.Empty, updateTime);

        // Assert
        package.CustomerId.Should().BeNull();
        package.ProductId.Should().BeNull();
        package.UpdatedAt.Should().Be(updateTime);

        var domainEvent = package.DomainEvents.OfType<WorkPackageContextChanged>().Single();
        domainEvent.PreviousCustomerId.Should().Be(_customerId1);
        domainEvent.NewCustomerId.Should().BeNull();
        domainEvent.PreviousProductId.Should().Be(_productId1);
        domainEvent.NewProductId.Should().BeNull();
    }

    [Fact]
    public void UpdateContext_UpdatingCustomerOnly_PreservesProduct_AndRecordsEvent()
    {
        // Arrange
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Package With Context",
            objective: "Initial objectives",
            ownerPersonId: _ownerId,
            customerId: _customerId1,
            productId: _productId1);
        package.ClearDomainEvents();

        // Act
        package.UpdateContext(_customerId2, _productId1);

        // Assert
        package.CustomerId.Should().Be(_customerId2);
        package.ProductId.Should().Be(_productId1);

        var domainEvent = package.DomainEvents.OfType<WorkPackageContextChanged>().Single();
        domainEvent.PreviousCustomerId.Should().Be(_customerId1);
        domainEvent.NewCustomerId.Should().Be(_customerId2);
        domainEvent.PreviousProductId.Should().Be(_productId1);
        domainEvent.NewProductId.Should().Be(_productId1);
    }

    [Fact]
    public void UpdateContext_UpdatingProductOnly_PreservesCustomer_AndRecordsEvent()
    {
        // Arrange
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Package With Context",
            objective: "Initial objectives",
            ownerPersonId: _ownerId,
            customerId: _customerId1,
            productId: _productId1);
        package.ClearDomainEvents();

        // Act
        package.UpdateContext(_customerId1, _productId2);

        // Assert
        package.CustomerId.Should().Be(_customerId1);
        package.ProductId.Should().Be(_productId2);

        var domainEvent = package.DomainEvents.OfType<WorkPackageContextChanged>().Single();
        domainEvent.PreviousCustomerId.Should().Be(_customerId1);
        domainEvent.NewCustomerId.Should().Be(_customerId1);
        domainEvent.PreviousProductId.Should().Be(_productId1);
        domainEvent.NewProductId.Should().Be(_productId2);
    }

    [Fact]
    public void UpdateContext_WhenUnchanged_DoesNotChangeUpdatedAt_AndDoesNotRecordEvent()
    {
        // Arrange
        var initialCreatedAt = DateTime.UtcNow.AddHours(-1);
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Package With Context",
            objective: "Initial objectives",
            ownerPersonId: _ownerId,
            customerId: _customerId1,
            productId: _productId1,
            createdAtUtc: initialCreatedAt);
        package.ClearDomainEvents();

        var attemptedUpdateTime = DateTime.UtcNow;

        // Act - same IDs
        package.UpdateContext(_customerId1, _productId1, attemptedUpdateTime);

        // Assert
        package.CustomerId.Should().Be(_customerId1);
        package.ProductId.Should().Be(_productId1);
        package.UpdatedAt.Should().BeNull();
        package.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateContext_WhenBothNullAndPassingGuidEmpty_TreatedAsUnchanged()
    {
        // Arrange
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Package Without Context",
            objective: "Initial objectives",
            ownerPersonId: _ownerId,
            customerId: null,
            productId: null);
        package.ClearDomainEvents();

        // Act - Guid.Empty normalizes to null, which matches current null
        package.UpdateContext(Guid.Empty, Guid.Empty);

        // Assert
        package.CustomerId.Should().BeNull();
        package.ProductId.Should().BeNull();
        package.UpdatedAt.Should().BeNull();
        package.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateContext_WhenPackageClosed_ThrowsWorkPackageDomainException()
    {
        // Arrange
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Closed Package",
            objective: "Objectives",
            ownerPersonId: _ownerId);
        package.Close("Completed");
        package.ClearDomainEvents();

        // Act
        var actUpdate = () => package.UpdateContext(_customerId1, _productId1);
        var actClear = () => package.UpdateContext(null, null);

        // Assert
        actUpdate.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*closed*");
        actClear.Should().Throw<WorkPackageDomainException>()
            .WithMessage("*closed*");
        package.DomainEvents.Should().BeEmpty();
    }
}
