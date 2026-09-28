namespace ICS.Modules.Customer.Application;

using ICS.Modules.Customer.Application.DTOs;

/// <summary>
/// Application service contract for executing Customer and CustomerContact commands.
/// Architecture §6, §7, §16, §19.2.
/// </summary>
public interface ICustomerService
{
    Task<CustomerDto> CreateCustomerAsync(
        string customerCode,
        string customerName,
        bool hasActiveMaintenanceContract = false,
        Guid? customerId = null,
        CancellationToken cancellationToken = default);

    Task<CustomerDto> UpdateCustomerMasterDataAsync(
        Guid customerId,
        string customerCode,
        string customerName,
        bool hasActiveMaintenanceContract,
        CancellationToken cancellationToken = default);

    Task<bool> DeactivateCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<bool> ActivateCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<CustomerContactDto> CreateCustomerContactAsync(
        Guid customerId,
        string name,
        string? position = null,
        string? phoneNumber = null,
        string? email = null,
        Guid? contactId = null,
        CancellationToken cancellationToken = default);

    Task<CustomerContactDto> UpdateCustomerContactAsync(
        Guid contactId,
        string name,
        string? position = null,
        string? phoneNumber = null,
        string? email = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeactivateCustomerContactAsync(
        Guid contactId,
        CancellationToken cancellationToken = default);
}
