using Cakra.Core;
using Cakra.Modules.Customer.Domain;
using Cakra.Modules.Customer.Domain.Events;
using Cakra.Modules.Customer.Persistence;
using MediatR;

namespace Cakra.Modules.Customer.Services;

/// <summary>
/// Application command service and MediatR command handler for Customer &amp; Contact master management
/// (Architecture §6, §7, §19.2).
/// </summary>
public sealed class CustomerService :
    ICustomerService,
    IRequestHandler<CreateCustomerCommand, CustomerDto>,
    IRequestHandler<UpdateCustomerCommand, CustomerDto>,
    IRequestHandler<CreateCustomerContactCommand, CustomerContactDto>,
    IRequestHandler<UpdateCustomerContactCommand, CustomerContactDto>,
    IRequestHandler<DeactivateCustomerCommand, CustomerDto>,
    IRequestHandler<ActivateCustomerCommand, CustomerDto>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerContactRepository _contactRepository;
    private readonly IDomainEventDispatcher? _eventDispatcher;
    private readonly ISystemClock? _clock;

    public CustomerService(
        Cakra.Core.Infrastructure.Persistence.IDbConnectionFactory connectionFactory,
        IDomainEventDispatcher? eventDispatcher = null,
        ISystemClock? clock = null)
        : this(
            new CustomerRepository(connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory))),
            new CustomerContactRepository(connectionFactory),
            eventDispatcher,
            clock)
    {
    }

    internal CustomerService(
        ICustomerRepository customerRepository,
        ICustomerContactRepository contactRepository,
        IDomainEventDispatcher? eventDispatcher = null,
        ISystemClock? clock = null)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _contactRepository = contactRepository ?? throw new ArgumentNullException(nameof(contactRepository));
        _eventDispatcher = eventDispatcher;
        _clock = clock;
    }

    private DateTime UtcNow => _clock?.UtcNow ?? DateTime.UtcNow;

    /// <inheritdoc />
    public async Task<CustomerDto> CreateCustomerAsync(
        string customerCode,
        string customerName,
        bool hasActiveMaintenanceContract = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerCode))
        {
            throw new ArgumentException("Customer code cannot be null or whitespace.", nameof(customerCode));
        }

        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new ArgumentException("Customer name cannot be null or whitespace.", nameof(customerName));
        }

        var normalizedCode = customerCode.Trim();
        var existing = await _customerRepository.GetByCodeAsync(normalizedCode, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"A customer with code '{normalizedCode}' already exists.");
        }

        var now = UtcNow;
        var customer = Domain.Customer.Create(
            normalizedCode,
            customerName.Trim(),
            hasActiveMaintenanceContract,
            createdAtUtc: now);

        await _customerRepository.AddAsync(customer, cancellationToken);

        if (_eventDispatcher is not null)
        {
            await _eventDispatcher.DispatchAsync(
                new CustomerCreated(
                    customer.Id,
                    customer.CustomerCode,
                    customer.CustomerName,
                    customer.HasActiveMaintenanceContract,
                    Guid.NewGuid(),
                    now),
                cancellationToken);
        }

        return CustomerDto.FromDomain(customer);
    }

    /// <inheritdoc />
    public async Task<CustomerDto> UpdateCustomerAsync(
        Guid customerId,
        string customerCode,
        string customerName,
        bool hasActiveMaintenanceContract,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId cannot be empty.", nameof(customerId));
        }

        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer '{customerId}' was not found.");

        var normalizedCode = customerCode.Trim();
        if (!string.Equals(customer.CustomerCode, normalizedCode, StringComparison.OrdinalIgnoreCase))
        {
            var existingByCode = await _customerRepository.GetByCodeAsync(normalizedCode, cancellationToken);
            if (existingByCode is not null && existingByCode.Id != customerId)
            {
                throw new InvalidOperationException($"A customer with code '{normalizedCode}' already exists.");
            }
        }

        customer.UpdateMasterData(normalizedCode, customerName.Trim(), hasActiveMaintenanceContract, UtcNow);
        await _customerRepository.UpdateAsync(customer, cancellationToken);

        return CustomerDto.FromDomain(customer);
    }

    /// <inheritdoc />
    public async Task<CustomerContactDto> CreateCustomerContactAsync(
        Guid customerId,
        string name,
        string? position = null,
        string? phoneNumber = null,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId cannot be empty.", nameof(customerId));
        }

        // Domain Rule 3 & 6: Customer Contacts cannot exist without a Customer.
        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer '{customerId}' was not found.");

        var now = UtcNow;
        var contact = CustomerContact.Create(
            customer.Id,
            name,
            position,
            phoneNumber,
            email,
            createdAtUtc: now);

        await _contactRepository.AddAsync(contact, cancellationToken);

        if (_eventDispatcher is not null)
        {
            await _eventDispatcher.DispatchAsync(
                new CustomerContactAdded(contact.Id, contact.CustomerId, contact.Name, Guid.NewGuid(), now),
                cancellationToken);
        }

        return CustomerContactDto.FromDomain(contact);
    }

    /// <inheritdoc />
    public async Task<CustomerContactDto> UpdateCustomerContactAsync(
        Guid contactId,
        string name,
        string? position = null,
        string? phoneNumber = null,
        string? email = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (contactId == Guid.Empty)
        {
            throw new ArgumentException("ContactId cannot be empty.", nameof(contactId));
        }

        var contact = await _contactRepository.GetByIdAsync(contactId, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer contact '{contactId}' was not found.");

        var previousStatus = contact.Status;
        var now = UtcNow;

        contact.Update(name, position, phoneNumber, email, status, now);
        await _contactRepository.UpdateAsync(contact, cancellationToken);

        if (_eventDispatcher is not null && !string.Equals(previousStatus, contact.Status, StringComparison.OrdinalIgnoreCase))
        {
            if (contact.IsActive)
            {
                await _eventDispatcher.DispatchAsync(
                    new CustomerContactActivated(contact.Id, contact.CustomerId, Guid.NewGuid(), now),
                    cancellationToken);
            }
            else
            {
                await _eventDispatcher.DispatchAsync(
                    new CustomerContactInactivated(contact.Id, contact.CustomerId, Guid.NewGuid(), now),
                    cancellationToken);
            }
        }

        return CustomerContactDto.FromDomain(contact);
    }

    /// <inheritdoc />
    public async Task<CustomerDto> DeactivateCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId cannot be empty.", nameof(customerId));
        }

        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer '{customerId}' was not found.");

        var now = UtcNow;
        customer.Deactivate(now);
        await _customerRepository.UpdateAsync(customer, cancellationToken);

        if (_eventDispatcher is not null)
        {
            await _eventDispatcher.DispatchAsync(
                new CustomerInactivated(customer.Id, Guid.NewGuid(), now),
                cancellationToken);
            await _eventDispatcher.DispatchAsync(
                new CustomerDeactivated(customer.Id, Guid.NewGuid(), now),
                cancellationToken);
        }

        return CustomerDto.FromDomain(customer);
    }

    /// <inheritdoc />
    public async Task<CustomerDto> ActivateCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId cannot be empty.", nameof(customerId));
        }

        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer '{customerId}' was not found.");

        var now = UtcNow;
        customer.Activate(now);
        await _customerRepository.UpdateAsync(customer, cancellationToken);

        if (_eventDispatcher is not null)
        {
            await _eventDispatcher.DispatchAsync(
                new CustomerActivated(customer.Id, Guid.NewGuid(), now),
                cancellationToken);
        }

        return CustomerDto.FromDomain(customer);
    }

    // MediatR command handler entry points
    public Task<CustomerDto> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CreateCustomerAsync(
            request.CustomerCode,
            request.CustomerName,
            request.HasActiveMaintenanceContract,
            cancellationToken);
    }

    public Task<CustomerDto> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return UpdateCustomerAsync(
            request.CustomerId,
            request.CustomerCode,
            request.CustomerName,
            request.HasActiveMaintenanceContract,
            cancellationToken);
    }

    public Task<CustomerContactDto> Handle(CreateCustomerContactCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CreateCustomerContactAsync(
            request.CustomerId,
            request.Name,
            request.Position,
            request.PhoneNumber,
            request.Email,
            cancellationToken);
    }

    public Task<CustomerContactDto> Handle(UpdateCustomerContactCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return UpdateCustomerContactAsync(
            request.ContactId,
            request.Name,
            request.Position,
            request.PhoneNumber,
            request.Email,
            request.Status,
            cancellationToken);
    }

    public Task<CustomerDto> Handle(DeactivateCustomerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DeactivateCustomerAsync(request.CustomerId, cancellationToken);
    }

    public Task<CustomerDto> Handle(ActivateCustomerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ActivateCustomerAsync(request.CustomerId, cancellationToken);
    }
}
