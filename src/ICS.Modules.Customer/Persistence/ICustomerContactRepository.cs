namespace ICS.Modules.Customer.Persistence;

using ICS.Modules.Customer.Domain;

/// <summary>
/// Internal persistence contract for CustomerContact entity.
/// </summary>
internal interface ICustomerContactRepository
{
    Task<CustomerContact?> GetByIdAsync(Guid contactId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerContact>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task AddAsync(CustomerContact contact, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomerContact contact, CancellationToken cancellationToken = default);
}
