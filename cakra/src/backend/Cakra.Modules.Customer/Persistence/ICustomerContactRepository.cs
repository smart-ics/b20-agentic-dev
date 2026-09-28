using Cakra.Core;
using Cakra.Modules.Customer.Domain;

namespace Cakra.Modules.Customer.Persistence;

/// <summary>
/// Internal persistence contract for <see cref="CustomerContact"/> entity (Architecture §6, §19.3, §21).
/// Internal repositories are not exposed outside the Customer module boundary.
/// </summary>
internal interface ICustomerContactRepository : IRepository<CustomerContact>
{
    /// <summary>Retrieves all contacts associated with a specific customer.</summary>
    Task<IReadOnlyList<CustomerContact>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Retrieves all active contacts associated with a specific customer.</summary>
    Task<IReadOnlyList<CustomerContact>> GetActiveByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Updates the lifecycle status of a customer contact.</summary>
    Task UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);
}
