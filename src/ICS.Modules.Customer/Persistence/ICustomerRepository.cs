namespace ICS.Modules.Customer.Persistence;

using ICS.Modules.Customer.Domain;

/// <summary>
/// Internal persistence contract for Customer aggregate root.
/// </summary>
internal interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<Customer?> GetByCodeAsync(string customerCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Customer>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);
    Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default);
}
