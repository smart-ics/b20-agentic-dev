namespace ICS.Tests.Unit;

using System;
using System.Linq;
using FluentAssertions;
using ICS.Modules.Customer.Domain;
using ICS.Modules.Customer.Domain.Events;
using Xunit;

/// <summary>
/// Unit tests verifying domain model invariants and domain event emission in Customer module.
/// Architecture §6, §7, §16, §17; customer-domain.md.
/// </summary>
public class CustomerDomainTests
{
    [Fact]
    public void Customer_Create_ShouldInitializeCorrectly_AndEmitCustomerCreatedEvent()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var customer = Customer.Create(customerId, "CUST-001", "Hospital Medika", true, now);

        // Assert
        customer.Id.Should().Be(customerId);
        customer.CustomerCode.Should().Be("CUST-001");
        customer.CustomerName.Should().Be("Hospital Medika");
        customer.Status.Should().Be(Customer.StatusActive);
        customer.IsActive.Should().BeTrue();
        customer.HasActiveMaintenanceContract.Should().BeTrue();
        customer.CreatedAt.Should().Be(now);
        customer.UpdatedAt.Should().BeNull();

        customer.DomainEvents.Should().HaveCount(1);
        var createdEvent = customer.DomainEvents.First() as CustomerCreated;
        createdEvent.Should().NotBeNull();
        createdEvent!.CustomerId.Should().Be(customerId);
        createdEvent.CustomerCode.Should().Be("CUST-001");
        createdEvent.CustomerName.Should().Be("Hospital Medika");
        createdEvent.HasActiveMaintenanceContract.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Hospital Medika")]
    [InlineData("   ", "Hospital Medika")]
    [InlineData("CUST-001", "")]
    [InlineData("CUST-001", "   ")]
    public void Customer_Create_WithInvalidArguments_ShouldThrowArgumentException(string code, string name)
    {
        var act = () => Customer.Create(Guid.NewGuid(), code, name, false, DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Customer_Update_ShouldUpdateAttributes_AndEmitCustomerMasterDataUpdatedEvent()
    {
        // Arrange
        var customer = Customer.Create(Guid.NewGuid(), "CUST-001", "Hospital Medika", false, DateTime.UtcNow);
        customer.ClearDomainEvents();
        var updateTime = DateTime.UtcNow.AddMinutes(5);

        // Act
        customer.Update("CUST-001-MOD", "RS Medika Utama", true, updateTime);

        // Assert
        customer.CustomerCode.Should().Be("CUST-001-MOD");
        customer.CustomerName.Should().Be("RS Medika Utama");
        customer.HasActiveMaintenanceContract.Should().BeTrue();
        customer.UpdatedAt.Should().Be(updateTime);

        customer.DomainEvents.Should().HaveCount(1);
        var updatedEvent = customer.DomainEvents.First() as CustomerMasterDataUpdated;
        updatedEvent.Should().NotBeNull();
        updatedEvent!.CustomerId.Should().Be(customer.Id);
        updatedEvent.CustomerCode.Should().Be("CUST-001-MOD");
        updatedEvent.CustomerName.Should().Be("RS Medika Utama");
        updatedEvent.HasActiveMaintenanceContract.Should().BeTrue();
    }

    [Fact]
    public void Customer_Deactivate_ShouldSetStatusInactive_AndEmitCustomerDeactivatedEvent()
    {
        // Arrange
        var customer = Customer.Create(Guid.NewGuid(), "CUST-001", "Hospital Medika", true, DateTime.UtcNow);
        customer.ClearDomainEvents();
        var deactTime = DateTime.UtcNow.AddMinutes(10);

        // Act
        customer.Deactivate(deactTime);

        // Assert
        customer.Status.Should().Be(Customer.StatusInactive);
        customer.IsActive.Should().BeFalse();
        customer.UpdatedAt.Should().Be(deactTime);

        customer.DomainEvents.Should().HaveCount(1);
        var deactEvent = customer.DomainEvents.First() as CustomerDeactivated;
        deactEvent.Should().NotBeNull();
        deactEvent!.CustomerId.Should().Be(customer.Id);
        deactEvent.CustomerCode.Should().Be("CUST-001");
        deactEvent.CustomerName.Should().Be("Hospital Medika");
    }

    [Fact]
    public void Customer_Activate_ShouldSetStatusActive_AndEmitCustomerActivatedEvent()
    {
        // Arrange
        var customer = Customer.Create(Guid.NewGuid(), "CUST-001", "Hospital Medika", true, DateTime.UtcNow);
        customer.Deactivate(DateTime.UtcNow.AddMinutes(5));
        customer.ClearDomainEvents();
        var reactTime = DateTime.UtcNow.AddMinutes(10);

        // Act
        customer.Activate(reactTime);

        // Assert
        customer.Status.Should().Be(Customer.StatusActive);
        customer.IsActive.Should().BeTrue();
        customer.UpdatedAt.Should().Be(reactTime);

        customer.DomainEvents.Should().HaveCount(1);
        var actEvent = customer.DomainEvents.First() as CustomerActivated;
        actEvent.Should().NotBeNull();
        actEvent!.CustomerId.Should().Be(customer.Id);
    }

    [Fact]
    public void Customer_UpdateMaintenanceContractStatus_ShouldUpdateState()
    {
        // Arrange
        var customer = Customer.Create(Guid.NewGuid(), "CUST-001", "Hospital Medika", false, DateTime.UtcNow);
        var updateTime = DateTime.UtcNow.AddMinutes(2);

        // Act
        customer.UpdateMaintenanceContractStatus(true, updateTime);

        // Assert
        customer.HasActiveMaintenanceContract.Should().BeTrue();
        customer.UpdatedAt.Should().Be(updateTime);
    }

    [Fact]
    public void CustomerContact_Create_ShouldInitializeCorrectly_AndEmitCustomerContactAddedEvent()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var contact = CustomerContact.Create(
            contactId,
            customerId,
            "Dr. Budi Santoso",
            "IT Director",
            "08123456789",
            "budi@medika.id",
            now);

        // Assert
        contact.Id.Should().Be(contactId);
        contact.CustomerId.Should().Be(customerId);
        contact.Name.Should().Be("Dr. Budi Santoso");
        contact.Position.Should().Be("IT Director");
        contact.PhoneNumber.Should().Be("08123456789");
        contact.Email.Should().Be("budi@medika.id");
        contact.Status.Should().Be(CustomerContact.StatusActive);
        contact.IsActive.Should().BeTrue();
        contact.CreatedAt.Should().Be(now);
        contact.UpdatedAt.Should().BeNull();

        contact.DomainEvents.Should().HaveCount(1);
        var addedEvent = contact.DomainEvents.First() as CustomerContactAdded;
        addedEvent.Should().NotBeNull();
        addedEvent!.ContactId.Should().Be(contactId);
        addedEvent.CustomerId.Should().Be(customerId);
        addedEvent.Name.Should().Be("Dr. Budi Santoso");
        addedEvent.Email.Should().Be("budi@medika.id");
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "Valid Name")]
    [InlineData("11111111-1111-1111-1111-111111111111", "")]
    [InlineData("11111111-1111-1111-1111-111111111111", "   ")]
    public void CustomerContact_Create_WithInvalidArguments_ShouldThrowArgumentException(string customerIdStr, string name)
    {
        var customerId = Guid.Parse(customerIdStr);
        var act = () => CustomerContact.Create(Guid.NewGuid(), customerId, name, "Position", "08123456", "test@test.com", DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CustomerContact_Update_ShouldUpdateAttributes_AndEmitCustomerContactUpdatedEvent()
    {
        // Arrange
        var contact = CustomerContact.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Dr. Budi",
            "Staff",
            "0811",
            "budi@old.id",
            DateTime.UtcNow);
        contact.ClearDomainEvents();
        var updateTime = DateTime.UtcNow.AddMinutes(5);

        // Act
        contact.Update("Dr. Budi Santoso", "Head of IT", "0812", "budi@new.id", updateTime);

        // Assert
        contact.Name.Should().Be("Dr. Budi Santoso");
        contact.Position.Should().Be("Head of IT");
        contact.PhoneNumber.Should().Be("0812");
        contact.Email.Should().Be("budi@new.id");
        contact.UpdatedAt.Should().Be(updateTime);

        contact.DomainEvents.Should().HaveCount(1);
        var updatedEvent = contact.DomainEvents.First() as CustomerContactUpdated;
        updatedEvent.Should().NotBeNull();
        updatedEvent!.ContactId.Should().Be(contact.Id);
        updatedEvent.Name.Should().Be("Dr. Budi Santoso");
        updatedEvent.Email.Should().Be("budi@new.id");
    }

    [Fact]
    public void CustomerContact_Deactivate_ShouldSetStatusInactive_AndEmitCustomerContactDeactivatedEvent()
    {
        // Arrange
        var contact = CustomerContact.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Dr. Budi",
            null,
            null,
            null,
            DateTime.UtcNow);
        contact.ClearDomainEvents();
        var deactTime = DateTime.UtcNow.AddMinutes(10);

        // Act
        contact.Deactivate(deactTime);

        // Assert
        contact.Status.Should().Be(CustomerContact.StatusInactive);
        contact.IsActive.Should().BeFalse();
        contact.UpdatedAt.Should().Be(deactTime);

        contact.DomainEvents.Should().HaveCount(1);
        var deactEvent = contact.DomainEvents.First() as CustomerContactDeactivated;
        deactEvent.Should().NotBeNull();
        deactEvent!.ContactId.Should().Be(contact.Id);
        deactEvent.Name.Should().Be("Dr. Budi");
    }

    [Fact]
    public void CustomerContact_Activate_ShouldSetStatusActive_AndEmitCustomerContactActivatedEvent()
    {
        // Arrange
        var contact = CustomerContact.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Dr. Budi",
            null,
            null,
            null,
            DateTime.UtcNow);
        contact.Deactivate(DateTime.UtcNow.AddMinutes(5));
        contact.ClearDomainEvents();
        var reactTime = DateTime.UtcNow.AddMinutes(10);

        // Act
        contact.Activate(reactTime);

        // Assert
        contact.Status.Should().Be(CustomerContact.StatusActive);
        contact.IsActive.Should().BeTrue();
        contact.UpdatedAt.Should().Be(reactTime);

        contact.DomainEvents.Should().HaveCount(1);
        var actEvent = contact.DomainEvents.First() as CustomerContactActivated;
        actEvent.Should().NotBeNull();
        actEvent!.ContactId.Should().Be(contact.Id);
    }
}
