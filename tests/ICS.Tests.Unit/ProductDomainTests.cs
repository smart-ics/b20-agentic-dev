namespace ICS.Tests.Unit;

using System;
using System.Linq;
using FluentAssertions;
using ICS.Modules.Product.Domain;
using ICS.Modules.Product.Domain.Events;
using Xunit;

/// <summary>
/// Unit tests verifying domain model invariants and domain event emission in Product module.
/// Architecture §10, §16, §17; product-domain.md.
/// </summary>
public class ProductDomainTests
{
    [Fact]
    public void Product_Create_ShouldInitializeCorrectly_AndEmitProductCreatedEvent()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var ownerPersonId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var product = Product.Create(productId, "PRD-001", "MyHospital", "Hospital ERP System", ownerPersonId, now);

        // Assert
        product.Id.Should().Be(productId);
        product.Code.Should().Be("PRD-001");
        product.Name.Should().Be("MyHospital");
        product.Description.Should().Be("Hospital ERP System");
        product.OwnerPersonId.Should().Be(ownerPersonId);
        product.Status.Should().Be(Product.StatusActive);
        product.IsActive.Should().BeTrue();
        product.CreatedAt.Should().Be(now);
        product.UpdatedAt.Should().BeNull();

        product.DomainEvents.Should().HaveCount(1);
        var createdEvent = product.DomainEvents.First() as ProductCreated;
        createdEvent.Should().NotBeNull();
        createdEvent!.ProductId.Should().Be(productId);
        createdEvent.Code.Should().Be("PRD-001");
        createdEvent.Name.Should().Be("MyHospital");
        createdEvent.Description.Should().Be("Hospital ERP System");
        createdEvent.OwnerPersonId.Should().Be(ownerPersonId);
    }

    [Theory]
    [InlineData("", "MyHospital")]
    [InlineData("   ", "MyHospital")]
    [InlineData("PRD-001", "")]
    [InlineData("PRD-001", "   ")]
    public void Product_Create_WithInvalidCodeOrName_ShouldThrowArgumentException(string code, string name)
    {
        var act = () => Product.Create(Guid.NewGuid(), code, name, "Desc", Guid.NewGuid(), DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Product_Create_WithEmptyOwner_ShouldThrowArgumentException()
    {
        var act = () => Product.Create(Guid.NewGuid(), "PRD-001", "MyHospital", "Desc", Guid.Empty, DateTime.UtcNow);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("ownerPersonId");
    }

    [Fact]
    public void Product_Update_ShouldUpdateNameAndDescription_AndSetUpdatedAt()
    {
        // Arrange
        var product = Product.Create(Guid.NewGuid(), "PRD-001", "MyHospital", "Old Desc", Guid.NewGuid(), DateTime.UtcNow);
        var updateTime = DateTime.UtcNow.AddMinutes(5);

        // Act
        product.Update("MyHospital Pro", "New Enhanced System", updateTime);

        // Assert
        product.Name.Should().Be("MyHospital Pro");
        product.Description.Should().Be("New Enhanced System");
        product.UpdatedAt.Should().Be(updateTime);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Product_Update_WithInvalidName_ShouldThrowArgumentException(string name)
    {
        var product = Product.Create(Guid.NewGuid(), "PRD-001", "MyHospital", "Old Desc", Guid.NewGuid(), DateTime.UtcNow);
        var act = () => product.Update(name, "Desc", DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Product_AssignOwner_ShouldUpdateOwner_AndEmitProductOwnerChangedEvent()
    {
        // Arrange
        var initialOwner = Guid.NewGuid();
        var product = Product.Create(Guid.NewGuid(), "PRD-001", "MyHospital", "Desc", initialOwner, DateTime.UtcNow);
        product.ClearDomainEvents();

        var newOwner = Guid.NewGuid();
        var updateTime = DateTime.UtcNow.AddMinutes(10);

        // Act
        product.AssignOwner(newOwner, updateTime);

        // Assert
        product.OwnerPersonId.Should().Be(newOwner);
        product.UpdatedAt.Should().Be(updateTime);

        product.DomainEvents.Should().HaveCount(1);
        var eventItem = product.DomainEvents.First() as ProductOwnerChanged;
        eventItem.Should().NotBeNull();
        eventItem!.ProductId.Should().Be(product.Id);
        eventItem.PreviousOwnerPersonId.Should().Be(initialOwner);
        eventItem.NewOwnerPersonId.Should().Be(newOwner);
    }

    [Fact]
    public void Product_AssignOwner_WithEmptyOwner_ShouldThrowArgumentException()
    {
        var product = Product.Create(Guid.NewGuid(), "PRD-001", "MyHospital", "Desc", Guid.NewGuid(), DateTime.UtcNow);
        var act = () => product.AssignOwner(Guid.Empty, DateTime.UtcNow);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newOwnerPersonId");
    }

    [Fact]
    public void Product_Deactivate_WhenActive_ShouldTransitionToInactive_AndEmitProductDeactivatedEvent()
    {
        // Arrange
        var product = Product.Create(Guid.NewGuid(), "PRD-001", "MyHospital", "Desc", Guid.NewGuid(), DateTime.UtcNow);
        product.ClearDomainEvents();
        var updateTime = DateTime.UtcNow.AddMinutes(15);

        // Act
        product.Deactivate(updateTime);

        // Assert
        product.Status.Should().Be(Product.StatusInactive);
        product.IsActive.Should().BeFalse();
        product.UpdatedAt.Should().Be(updateTime);

        product.DomainEvents.Should().HaveCount(1);
        var deactivatedEvent = product.DomainEvents.First() as ProductDeactivated;
        deactivatedEvent.Should().NotBeNull();
        deactivatedEvent!.ProductId.Should().Be(product.Id);
        deactivatedEvent.Code.Should().Be("PRD-001");
        deactivatedEvent.Name.Should().Be("MyHospital");
    }

    [Fact]
    public void Product_Deactivate_WhenAlreadyInactive_ShouldBeNoOp()
    {
        // Arrange
        var product = Product.Create(Guid.NewGuid(), "PRD-001", "MyHospital", "Desc", Guid.NewGuid(), DateTime.UtcNow);
        product.Deactivate(DateTime.UtcNow);
        product.ClearDomainEvents();

        // Act
        product.Deactivate(DateTime.UtcNow.AddMinutes(5));

        // Assert
        product.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Product_Activate_WhenInactive_ShouldTransitionToActive_AndEmitProductActivatedEvent()
    {
        // Arrange
        var product = Product.Create(Guid.NewGuid(), "PRD-001", "MyHospital", "Desc", Guid.NewGuid(), DateTime.UtcNow);
        product.Deactivate(DateTime.UtcNow);
        product.ClearDomainEvents();
        var reactivateTime = DateTime.UtcNow.AddMinutes(20);

        // Act
        product.Activate(reactivateTime);

        // Assert
        product.Status.Should().Be(Product.StatusActive);
        product.IsActive.Should().BeTrue();
        product.UpdatedAt.Should().Be(reactivateTime);

        product.DomainEvents.Should().HaveCount(1);
        var activatedEvent = product.DomainEvents.First() as ProductActivated;
        activatedEvent.Should().NotBeNull();
        activatedEvent!.ProductId.Should().Be(product.Id);
        activatedEvent.Code.Should().Be("PRD-001");
        activatedEvent.Name.Should().Be("MyHospital");
    }

    [Fact]
    public void Product_Activate_WhenAlreadyActive_ShouldBeNoOp()
    {
        // Arrange
        var product = Product.Create(Guid.NewGuid(), "PRD-001", "MyHospital", "Desc", Guid.NewGuid(), DateTime.UtcNow);
        product.ClearDomainEvents();

        // Act
        product.Activate(DateTime.UtcNow.AddMinutes(5));

        // Assert
        product.DomainEvents.Should().BeEmpty();
    }
}
