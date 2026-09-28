namespace ICS.Modules.Customer;

using ICS.Modules.Customer.Application.DTOs;

/// <summary>
/// In-process query contract exposed by the Customer module for cross-module integration and UI queries.
/// Architecture §7, §15, §16, and §20.
/// </summary>
public interface ICustomerQueryService
{
    /// <summary>
    /// Gets a Customer by identifier.
    /// </summary>
    Task<CustomerDto?> GetCustomerByIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all active Customers.
    /// </summary>
    Task<IReadOnlyList<CustomerDto>> ListActiveCustomersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all contacts for a specified Customer.
    /// </summary>
    Task<IReadOnlyList<CustomerContactDto>> GetCustomerContactsAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the Customer and its authoritative maintenance contract status.
    /// </summary>
    Task<CustomerWithContractStatusDto?> GetCustomerWithContractStatusAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronously gets a Customer by identifier.
    /// </summary>
    CustomerDto? GetCustomerById(Guid customerId);

    /// <summary>
    /// Synchronously lists all active Customers.
    /// </summary>
    IReadOnlyList<CustomerDto> ListActiveCustomers();

    /// <summary>
    /// Synchronously gets all contacts for a specified Customer.
    /// </summary>
    IReadOnlyList<CustomerContactDto> GetCustomerContacts(Guid customerId);

    /// <summary>
    /// Synchronously gets the Customer and its authoritative maintenance contract status.
    /// </summary>
    CustomerWithContractStatusDto? GetCustomerWithContractStatus(Guid customerId);
}
