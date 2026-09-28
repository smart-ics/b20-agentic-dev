namespace Cakra.Modules.Customer.Services;

/// <summary>
/// Application command service contract for Customer &amp; Contact creation and master updates (Architecture §7).
/// </summary>
public interface ICustomerService
{
    /// <summary>Creates a new active customer master record.</summary>
    Task<CustomerDto> CreateCustomerAsync(
        string customerCode,
        string customerName,
        bool hasActiveMaintenanceContract = false,
        CancellationToken cancellationToken = default);

    /// <summary>Updates master data of an existing customer.</summary>
    Task<CustomerDto> UpdateCustomerAsync(
        Guid customerId,
        string customerCode,
        string customerName,
        bool hasActiveMaintenanceContract,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a new contact person associated with an existing customer.</summary>
    Task<CustomerContactDto> CreateCustomerContactAsync(
        Guid customerId,
        string name,
        string? position = null,
        string? phoneNumber = null,
        string? email = null,
        CancellationToken cancellationToken = default);

    /// <summary>Updates an existing customer contact.</summary>
    Task<CustomerContactDto> UpdateCustomerContactAsync(
        Guid contactId,
        string name,
        string? position = null,
        string? phoneNumber = null,
        string? email = null,
        string? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>Deactivates a customer while preserving historical references.</summary>
    Task<CustomerDto> DeactivateCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    /// <summary>Activates an inactive customer.</summary>
    Task<CustomerDto> ActivateCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);
}
