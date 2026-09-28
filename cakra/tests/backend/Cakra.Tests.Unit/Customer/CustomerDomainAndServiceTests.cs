using Cakra.Core;
using Cakra.Modules.Customer;
using Cakra.Modules.Customer.Domain;
using Cakra.Modules.Customer.Domain.Events;
using Cakra.Modules.Customer.Persistence;
using Cakra.Modules.Customer.Services;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Customer;

/// <summary>
/// Unit tests for the Customer module domain entities, events, validators, and application service (P3-S14).
/// </summary>
public class CustomerDomainAndServiceTests
{
    [Fact]
    public void Customer_Create_initializes_active_customer_with_expected_attributes()
    {
        var now = new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc);
        var customer = Cakra.Modules.Customer.Domain.Customer.Create(
            " CUST-001 ",
            " RS Harapan Bunda ",
            hasActiveMaintenanceContract: true,
            createdAtUtc: now);

        customer.Id.Should().NotBeEmpty();
        customer.CustomerId.Should().Be(customer.Id);
        customer.CustomerCode.Should().Be("CUST-001");
        customer.Code.Should().Be("CUST-001");
        customer.CustomerName.Should().Be("RS Harapan Bunda");
        customer.Name.Should().Be("RS Harapan Bunda");
        customer.Status.Should().Be("ACTIVE");
        customer.IsActive.Should().BeTrue();
        customer.HasActiveMaintenanceContract.Should().BeTrue();
        customer.CreatedAt.Should().Be(now);
        customer.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Customer_UpdateMasterData_and_Deactivate_and_Activate_transition_correctly()
    {
        var customer = Cakra.Modules.Customer.Domain.Customer.Create("CUST-001", "Initial Name", false);
        var updateTime = new DateTime(2026, 9, 28, 11, 0, 0, DateTimeKind.Utc);

        customer.UpdateMasterData("CUST-001-NEW", "Updated Name", true, updateTime);

        customer.CustomerCode.Should().Be("CUST-001-NEW");
        customer.CustomerName.Should().Be("Updated Name");
        customer.HasActiveMaintenanceContract.Should().BeTrue();
        customer.UpdatedAt.Should().Be(updateTime);

        var deactivateTime = updateTime.AddHours(1);
        customer.Deactivate(deactivateTime);
        customer.Status.Should().Be("INACTIVE");
        customer.IsActive.Should().BeFalse();
        customer.UpdatedAt.Should().Be(deactivateTime);

        var activateTime = deactivateTime.AddHours(1);
        customer.Activate(activateTime);
        customer.Status.Should().Be("ACTIVE");
        customer.IsActive.Should().BeTrue();
        customer.UpdatedAt.Should().Be(activateTime);
    }

    [Fact]
    public void CustomerContact_Create_and_Update_enforce_invariants()
    {
        var customerId = Guid.NewGuid();
        var contact = CustomerContact.Create(
            customerId,
            " Budi Santoso ",
            " IT Manager ",
            " +62812345678 ",
            " budi@example.com ");

        contact.Id.Should().NotBeEmpty();
        contact.ContactId.Should().Be(contact.Id);
        contact.CustomerId.Should().Be(customerId);
        contact.Name.Should().Be("Budi Santoso");
        contact.Position.Should().Be("IT Manager");
        contact.PhoneNumber.Should().Be("+62812345678");
        contact.Email.Should().Be("budi@example.com");
        contact.Status.Should().Be("ACTIVE");
        contact.IsActive.Should().BeTrue();

        contact.Update("Budi S.", "CIO", "+628111111", "cio@example.com", "INACTIVE");
        contact.Name.Should().Be("Budi S.");
        contact.Position.Should().Be("CIO");
        contact.Status.Should().Be("INACTIVE");
        contact.IsActive.Should().BeFalse();

        var invalidStatusAct = () => contact.Update("Budi S.", "CIO", null, null, "DELETED");
        invalidStatusAct.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Internal_repositories_are_not_publicly_exposed_while_ICustomerQueryService_is_published()
    {
        typeof(ICustomerQueryService).IsPublic.Should().BeTrue();
        typeof(CustomerDto).IsPublic.Should().BeTrue();
        typeof(CustomerContactDto).IsPublic.Should().BeTrue();
        typeof(CustomerWithContractStatusDto).IsPublic.Should().BeTrue();

        typeof(ICustomerRepository).IsPublic.Should().BeFalse();
        typeof(CustomerRepository).IsPublic.Should().BeFalse();
        typeof(ICustomerContactRepository).IsPublic.Should().BeFalse();
        typeof(CustomerContactRepository).IsPublic.Should().BeFalse();
    }

    [Fact]
    public async Task CustomerService_handles_Create_Update_Contact_and_Deactivate_commands_and_dispatches_events()
    {
        var customerRepo = new InMemoryCustomerRepository();
        var contactRepo = new InMemoryCustomerContactRepository();
        var dispatcher = new RecordingDomainEventDispatcher();
        var service = new CustomerService(customerRepo, contactRepo, dispatcher);

        // 1. Create Customer via MediatR handler
        var created = await service.Handle(
            new CreateCustomerCommand("CUST-100", "RS Mitra Keluarga", true),
            CancellationToken.None);

        created.Id.Should().NotBeEmpty();
        created.CustomerCode.Should().Be("CUST-100");
        created.CustomerName.Should().Be("RS Mitra Keluarga");
        created.HasActiveMaintenanceContract.Should().BeTrue();
        created.IsActive.Should().BeTrue();
        dispatcher.Events.Should().ContainSingle(e => e is CustomerCreated);

        // Duplicate code should throw
        var dupAct = async () => await service.Handle(
            new CreateCustomerCommand("CUST-100", "Duplicate Customer", false),
            CancellationToken.None);
        await dupAct.Should().ThrowAsync<InvalidOperationException>();

        // 2. Update Customer master data
        var updated = await service.Handle(
            new UpdateCustomerCommand(created.Id, "CUST-100A", "RS Mitra Keluarga Group", false),
            CancellationToken.None);

        updated.CustomerCode.Should().Be("CUST-100A");
        updated.CustomerName.Should().Be("RS Mitra Keluarga Group");
        updated.HasActiveMaintenanceContract.Should().BeFalse();

        // 3. Create CustomerContact
        var contact = await service.Handle(
            new CreateCustomerContactCommand(created.Id, "Siti Aminah", "Head of IT", "08123456789", "siti@mitra.co.id"),
            CancellationToken.None);

        contact.Id.Should().NotBeEmpty();
        contact.CustomerId.Should().Be(created.Id);
        contact.Name.Should().Be("Siti Aminah");
        contact.IsActive.Should().BeTrue();
        dispatcher.Events.Should().Contain(e => e is CustomerContactAdded);

        // 4. Update CustomerContact
        var updatedContact = await service.Handle(
            new UpdateCustomerContactCommand(contact.Id, "Siti Aminah, M.Kom", "VP IT", "08129999999", "siti.vp@mitra.co.id", "INACTIVE"),
            CancellationToken.None);

        updatedContact.Name.Should().Be("Siti Aminah, M.Kom");
        updatedContact.Position.Should().Be("VP IT");
        updatedContact.Status.Should().Be("INACTIVE");
        dispatcher.Events.Should().Contain(e => e is CustomerContactInactivated);

        // 5. Deactivate Customer
        var deactivated = await service.Handle(
            new DeactivateCustomerCommand(created.Id),
            CancellationToken.None);

        deactivated.Status.Should().Be("INACTIVE");
        deactivated.IsActive.Should().BeFalse();
        dispatcher.Events.Should().Contain(e => e is CustomerInactivated);
        dispatcher.Events.Should().Contain(e => e is CustomerDeactivated);
    }

    [Fact]
    public void Command_validators_enforce_required_fields()
    {
        var createValidator = new CreateCustomerCommandValidator();
        createValidator.Validate(new CreateCustomerCommand("", "")).IsValid.Should().BeFalse();
        createValidator.Validate(new CreateCustomerCommand("C-01", "Valid Customer")).IsValid.Should().BeTrue();

        var contactValidator = new CreateCustomerContactCommandValidator();
        contactValidator.Validate(new CreateCustomerContactCommand(Guid.Empty, "")).IsValid.Should().BeFalse();
        contactValidator.Validate(new CreateCustomerContactCommand(Guid.NewGuid(), "Valid Contact")).IsValid.Should().BeTrue();

        var updateContactValidator = new UpdateCustomerContactCommandValidator();
        updateContactValidator.Validate(new UpdateCustomerContactCommand(Guid.NewGuid(), "Name", Status: "INVALID")).IsValid.Should().BeFalse();
        updateContactValidator.Validate(new UpdateCustomerContactCommand(Guid.NewGuid(), "Name", Status: "ACTIVE")).IsValid.Should().BeTrue();
    }

    private sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> Events { get; } = new();

        public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(domainEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryCustomerRepository : ICustomerRepository
    {
        private readonly Dictionary<Guid, Cakra.Modules.Customer.Domain.Customer> _store = new();

        public Task<Cakra.Modules.Customer.Domain.Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.TryGetValue(id, out var c) ? c : null);

        public Task<Cakra.Modules.Customer.Domain.Customer?> GetByCodeAsync(string customerCode, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Values.FirstOrDefault(c => string.Equals(c.CustomerCode, customerCode, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<Cakra.Modules.Customer.Domain.Customer>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Cakra.Modules.Customer.Domain.Customer>>(_store.Values.ToList());

        public Task<IReadOnlyList<Cakra.Modules.Customer.Domain.Customer>> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Cakra.Modules.Customer.Domain.Customer>>(_store.Values.Where(c => c.IsActive).ToList());

        public Task AddAsync(Cakra.Modules.Customer.Domain.Customer entity, CancellationToken cancellationToken = default)
        {
            _store[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Cakra.Modules.Customer.Domain.Customer entity, CancellationToken cancellationToken = default)
        {
            _store[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
        {
            if (_store.TryGetValue(id, out var c))
            {
                c.Status = status;
            }
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _store.Remove(id);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryCustomerContactRepository : ICustomerContactRepository
    {
        private readonly Dictionary<Guid, CustomerContact> _store = new();

        public Task<CustomerContact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.TryGetValue(id, out var c) ? c : null);

        public Task<IReadOnlyList<CustomerContact>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CustomerContact>>(_store.Values.ToList());

        public Task<IReadOnlyList<CustomerContact>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CustomerContact>>(_store.Values.Where(c => c.CustomerId == customerId).ToList());

        public Task<IReadOnlyList<CustomerContact>> GetActiveByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CustomerContact>>(_store.Values.Where(c => c.CustomerId == customerId && c.IsActive).ToList());

        public Task AddAsync(CustomerContact entity, CancellationToken cancellationToken = default)
        {
            _store[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CustomerContact entity, CancellationToken cancellationToken = default)
        {
            _store[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
        {
            if (_store.TryGetValue(id, out var c))
            {
                c.Status = status;
            }
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _store.Remove(id);
            return Task.CompletedTask;
        }
    }
}
