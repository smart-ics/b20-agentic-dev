namespace ICS.Modules.Customer.Application;

using ICS.Modules.Customer.Application.Commands;
using ICS.Modules.Customer.Application.DTOs;
using MediatR;

/// <summary>
/// Concrete implementation of <see cref="ICustomerService"/> executing commands through MediatR.
/// Architecture §6, §7, §16, §19.2, §20.
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly ISender _sender;

    public CustomerService(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public Task<CustomerDto> CreateCustomerAsync(
        string customerCode,
        string customerName,
        bool hasActiveMaintenanceContract = false,
        Guid? customerId = null,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new CreateCustomerCommand(customerCode, customerName, hasActiveMaintenanceContract, customerId), cancellationToken);
    }

    public Task<CustomerDto> UpdateCustomerMasterDataAsync(
        Guid customerId,
        string customerCode,
        string customerName,
        bool hasActiveMaintenanceContract,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new UpdateCustomerMasterDataCommand(customerId, customerCode, customerName, hasActiveMaintenanceContract), cancellationToken);
    }

    public Task<bool> DeactivateCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new DeactivateCustomerCommand(customerId), cancellationToken);
    }

    public Task<bool> ActivateCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new ActivateCustomerCommand(customerId), cancellationToken);
    }

    public Task<CustomerContactDto> CreateCustomerContactAsync(
        Guid customerId,
        string name,
        string? position = null,
        string? phoneNumber = null,
        string? email = null,
        Guid? contactId = null,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new CreateCustomerContactCommand(customerId, name, position, phoneNumber, email, contactId), cancellationToken);
    }

    public Task<CustomerContactDto> UpdateCustomerContactAsync(
        Guid contactId,
        string name,
        string? position = null,
        string? phoneNumber = null,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new UpdateCustomerContactCommand(contactId, name, position, phoneNumber, email), cancellationToken);
    }

    public Task<bool> DeactivateCustomerContactAsync(
        Guid contactId,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new DeactivateCustomerContactCommand(contactId), cancellationToken);
    }
}
