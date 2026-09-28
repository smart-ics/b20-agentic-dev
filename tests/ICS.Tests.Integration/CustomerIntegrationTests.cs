namespace ICS.Tests.Integration;

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using ICS.Core.Domain.Verification;
using ICS.Modules.Customer;
using ICS.Modules.Customer.Application;
using ICS.Modules.Customer.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Integration tests verifying the Customer module:
/// Domain entities, Dapper repositories, CustomerService commands, CustomerQueryService queries,
/// domain event emissions, and Respawn test database isolation.
/// Architecture §6, §7, §16, §17, §19.3, §20; customer-domain.md.
/// </summary>
public class CustomerIntegrationTests : IntegrationTestBase
{
    public CustomerIntegrationTests(IcsWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CreateCustomer_ShouldPersistInDatabase_EmitCustomerCreatedEvent_AndBeQueryable()
    {
        // Arrange
        TestDomainEventCollector.Clear();
        using var scope = CreateScope();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var customerQueryService = scope.ServiceProvider.GetRequiredService<ICustomerQueryService>();

        var code = $"CUST-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var name = "RS Mitra Kasih";

        // Act
        var created = await customerService.CreateCustomerAsync(code, name, hasActiveMaintenanceContract: true);

        // Assert
        created.Should().NotBeNull();
        created.CustomerId.Should().NotBeEmpty();
        created.CustomerCode.Should().Be(code);
        created.CustomerName.Should().Be(name);
        created.Status.Should().Be("ACTIVE");
        created.HasActiveMaintenanceContract.Should().BeTrue();

        // Verify QueryService
        var fetched = await customerQueryService.GetCustomerByIdAsync(created.CustomerId);
        fetched.Should().NotBeNull();
        fetched!.CustomerId.Should().Be(created.CustomerId);
        fetched.CustomerCode.Should().Be(code);
        fetched.CustomerName.Should().Be(name);
        fetched.Status.Should().Be("ACTIVE");
        fetched.HasActiveMaintenanceContract.Should().BeTrue();

        // Verify Domain Event
        var events = TestDomainEventCollector.PublishedEvents;
        var createdEvent = events.OfType<CustomerCreated>().FirstOrDefault(e => e.CustomerId == created.CustomerId);
        createdEvent.Should().NotBeNull();
        createdEvent!.CustomerCode.Should().Be(code);
        createdEvent.CustomerName.Should().Be(name);
        createdEvent.HasActiveMaintenanceContract.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateCustomerMasterData_ShouldUpdateDatabase_EmitEvent_AndReflectInQueryService()
    {
        // Arrange
        TestDomainEventCollector.Clear();
        using var scope = CreateScope();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var customerQueryService = scope.ServiceProvider.GetRequiredService<ICustomerQueryService>();

        var code = $"CUST-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var customer = await customerService.CreateCustomerAsync(code, "Klinik Sehat Awal", false);

        var updatedCode = $"CUST-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var updatedName = "RS Sehat Bersama";

        // Act
        var updated = await customerService.UpdateCustomerMasterDataAsync(
            customer.CustomerId,
            updatedCode,
            updatedName,
            hasActiveMaintenanceContract: true);

        // Assert
        updated.Should().NotBeNull();
        updated.CustomerCode.Should().Be(updatedCode);
        updated.CustomerName.Should().Be(updatedName);
        updated.HasActiveMaintenanceContract.Should().BeTrue();

        var queried = await customerQueryService.GetCustomerByIdAsync(customer.CustomerId);
        queried.Should().NotBeNull();
        queried!.CustomerCode.Should().Be(updatedCode);
        queried.CustomerName.Should().Be(updatedName);
        queried.HasActiveMaintenanceContract.Should().BeTrue();

        // Verify Domain Event
        var events = TestDomainEventCollector.PublishedEvents;
        var updateEvent = events.OfType<CustomerMasterDataUpdated>().FirstOrDefault(e => e.CustomerId == customer.CustomerId);
        updateEvent.Should().NotBeNull();
        updateEvent!.CustomerCode.Should().Be(updatedCode);
        updateEvent.CustomerName.Should().Be(updatedName);
        updateEvent.HasActiveMaintenanceContract.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateCustomer_ShouldSetInactive_EmitCustomerDeactivatedEvent_AndExcludeFromListActiveCustomers()
    {
        // Arrange
        TestDomainEventCollector.Clear();
        using var scope = CreateScope();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var customerQueryService = scope.ServiceProvider.GetRequiredService<ICustomerQueryService>();

        var code = $"CUST-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var customer = await customerService.CreateCustomerAsync(code, "RS Permata", true);

        // Act
        var deactivated = await customerService.DeactivateCustomerAsync(customer.CustomerId);

        // Assert
        deactivated.Should().BeTrue();

        var queried = await customerQueryService.GetCustomerByIdAsync(customer.CustomerId);
        queried.Should().NotBeNull();
        queried!.Status.Should().Be("INACTIVE");

        var activeList = await customerQueryService.ListActiveCustomersAsync();
        activeList.Should().NotContain(c => c.CustomerId == customer.CustomerId);

        // Verify Domain Event
        var events = TestDomainEventCollector.PublishedEvents;
        var deactEvent = events.OfType<CustomerDeactivated>().FirstOrDefault(e => e.CustomerId == customer.CustomerId);
        deactEvent.Should().NotBeNull();
        deactEvent!.CustomerCode.Should().Be(code);
    }

    [Fact]
    public async Task CreateCustomerContact_ShouldPersistInDatabase_EmitCustomerContactAddedEvent_AndBeQueryable()
    {
        // Arrange
        TestDomainEventCollector.Clear();
        using var scope = CreateScope();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var customerQueryService = scope.ServiceProvider.GetRequiredService<ICustomerQueryService>();

        var code = $"CUST-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var customer = await customerService.CreateCustomerAsync(code, "RS Royal Taruma", true);

        // Act
        var contact = await customerService.CreateCustomerContactAsync(
            customer.CustomerId,
            "dr. Hendra Wijaya",
            "Direktur Medis",
            "08129876543",
            "hendra@royaltaruma.com");

        // Assert
        contact.Should().NotBeNull();
        contact.ContactId.Should().NotBeEmpty();
        contact.CustomerId.Should().Be(customer.CustomerId);
        contact.Name.Should().Be("dr. Hendra Wijaya");
        contact.Position.Should().Be("Direktur Medis");
        contact.PhoneNumber.Should().Be("08129876543");
        contact.Email.Should().Be("hendra@royaltaruma.com");
        contact.Status.Should().Be("ACTIVE");

        // Verify QueryService
        var contacts = await customerQueryService.GetCustomerContactsAsync(customer.CustomerId);
        contacts.Should().ContainSingle(c => c.ContactId == contact.ContactId);
        var fetchedContact = contacts.First(c => c.ContactId == contact.ContactId);
        fetchedContact.Name.Should().Be("dr. Hendra Wijaya");
        fetchedContact.Position.Should().Be("Direktur Medis");

        // Verify Domain Event
        var events = TestDomainEventCollector.PublishedEvents;
        var addedEvent = events.OfType<CustomerContactAdded>().FirstOrDefault(e => e.ContactId == contact.ContactId);
        addedEvent.Should().NotBeNull();
        addedEvent!.CustomerId.Should().Be(customer.CustomerId);
        addedEvent.Name.Should().Be("dr. Hendra Wijaya");
    }

    [Fact]
    public async Task UpdateCustomerContact_ShouldUpdateDatabase_EmitEvent_AndReflectInQueryService()
    {
        // Arrange
        TestDomainEventCollector.Clear();
        using var scope = CreateScope();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var customerQueryService = scope.ServiceProvider.GetRequiredService<ICustomerQueryService>();

        var code = $"CUST-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var customer = await customerService.CreateCustomerAsync(code, "RS Cipto", true);
        var contact = await customerService.CreateCustomerContactAsync(customer.CustomerId, "dr. Budi", "Staff", "0811", "budi@cipto.com");

        // Act
        var updated = await customerService.UpdateCustomerContactAsync(
            contact.ContactId,
            "Prof. Dr. Budi Santoso",
            "Kepala Divisi IT",
            "081299998888",
            "prof.budi@cipto.com");

        // Assert
        updated.Should().NotBeNull();
        updated.Name.Should().Be("Prof. Dr. Budi Santoso");
        updated.Position.Should().Be("Kepala Divisi IT");
        updated.PhoneNumber.Should().Be("081299998888");
        updated.Email.Should().Be("prof.budi@cipto.com");

        var contacts = await customerQueryService.GetCustomerContactsAsync(customer.CustomerId);
        var fetched = contacts.First(c => c.ContactId == contact.ContactId);
        fetched.Name.Should().Be("Prof. Dr. Budi Santoso");
        fetched.Position.Should().Be("Kepala Divisi IT");

        // Verify Domain Event
        var events = TestDomainEventCollector.PublishedEvents;
        var updatedEvent = events.OfType<CustomerContactUpdated>().FirstOrDefault(e => e.ContactId == contact.ContactId);
        updatedEvent.Should().NotBeNull();
        updatedEvent!.Name.Should().Be("Prof. Dr. Budi Santoso");
    }

    [Fact]
    public async Task DeactivateCustomerContact_ShouldSetInactive_AndEmitEvent()
    {
        // Arrange
        TestDomainEventCollector.Clear();
        using var scope = CreateScope();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var customerQueryService = scope.ServiceProvider.GetRequiredService<ICustomerQueryService>();

        var code = $"CUST-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var customer = await customerService.CreateCustomerAsync(code, "RS Siloam", true);
        var contact = await customerService.CreateCustomerContactAsync(customer.CustomerId, "dr. Lisa", "Admin", "0813", "lisa@siloam.com");

        // Act
        var deactivated = await customerService.DeactivateCustomerContactAsync(contact.ContactId);

        // Assert
        deactivated.Should().BeTrue();

        var contacts = await customerQueryService.GetCustomerContactsAsync(customer.CustomerId);
        var fetched = contacts.First(c => c.ContactId == contact.ContactId);
        fetched.Status.Should().Be("INACTIVE");

        // Verify Domain Event
        var events = TestDomainEventCollector.PublishedEvents;
        var deactEvent = events.OfType<CustomerContactDeactivated>().FirstOrDefault(e => e.ContactId == contact.ContactId);
        deactEvent.Should().NotBeNull();
        deactEvent!.Name.Should().Be("dr. Lisa");
    }

    [Fact]
    public async Task CustomerQueryService_GetCustomerWithContractStatus_ShouldReturnAuthoritativeContractStatus()
    {
        // Arrange
        using var scope = CreateScope();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var customerQueryService = scope.ServiceProvider.GetRequiredService<ICustomerQueryService>();

        var codeWithContract = $"CUST-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var customer1 = await customerService.CreateCustomerAsync(codeWithContract, "RS Santosa", hasActiveMaintenanceContract: true);

        var codeWithoutContract = $"CUST-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var customer2 = await customerService.CreateCustomerAsync(codeWithoutContract, "RS Borromeus", hasActiveMaintenanceContract: false);

        // Act
        var status1 = await customerQueryService.GetCustomerWithContractStatusAsync(customer1.CustomerId);
        var status2 = await customerQueryService.GetCustomerWithContractStatusAsync(customer2.CustomerId);

        // Assert
        status1.Should().NotBeNull();
        status1!.CustomerId.Should().Be(customer1.CustomerId);
        status1.CustomerCode.Should().Be(codeWithContract);
        status1.CustomerName.Should().Be("RS Santosa");
        status1.Status.Should().Be("ACTIVE");
        status1.HasActiveMaintenanceContract.Should().BeTrue();

        status2.Should().NotBeNull();
        status2!.CustomerId.Should().Be(customer2.CustomerId);
        status2.CustomerCode.Should().Be(codeWithoutContract);
        status2.CustomerName.Should().Be("RS Borromeus");
        status2.Status.Should().Be("ACTIVE");
        status2.HasActiveMaintenanceContract.Should().BeFalse();

        // Also test synchronous overloads
        var syncStatus1 = customerQueryService.GetCustomerWithContractStatus(customer1.CustomerId);
        syncStatus1.Should().NotBeNull();
        syncStatus1!.HasActiveMaintenanceContract.Should().BeTrue();
    }

    [Fact]
    public async Task ValidationPipeline_CreateCustomer_WithInvalidData_ShouldThrowValidationException()
    {
        // Arrange
        using var scope = CreateScope();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();

        // Act & Assert
        var actEmptyCode = () => customerService.CreateCustomerAsync("", "Valid Name");
        await actEmptyCode.Should().ThrowAsync<ValidationException>();

        var actEmptyName = () => customerService.CreateCustomerAsync("VALIDCODE", "");
        await actEmptyName.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task DuplicateCustomerCode_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var scope = CreateScope();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();

        var code = $"CUST-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await customerService.CreateCustomerAsync(code, "Customer Original", true);

        // Act & Assert
        var actDuplicate = () => customerService.CreateCustomerAsync(code, "Customer Duplicate", false);
        await actDuplicate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{code}*already exists*");
    }
}
