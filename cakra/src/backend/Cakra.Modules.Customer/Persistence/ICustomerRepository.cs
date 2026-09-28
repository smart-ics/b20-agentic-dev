using Cakra.Core;
using Cakra.Modules.Customer.Domain;

namespace Cakra.Modules.Customer.Persistence;

/// <summary>
/// Internal persistence contract for <see cref="Domain.Customer"/> aggregate root (Architecture §6, §19.3, §21).
/// Internal repositories are not exposed outside the Customer module boundary.
/// </summary>
internal interface ICustomerRepository : IRepository<Domain.Customer>
{
    /// <summary>Retrieves a customer by its unique business code, or <c>null</c> if not found.</summary>
    Task<Domain.Customer?> GetByCodeAsync(string customerCode, CancellationToken cancellationToken = default);

    /// <summary>Retrieves all customers currently in <c>ACTIVE</c> status.</summary>
    Task<IReadOnlyList<Domain.Customer>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates the lifecycle status of a customer.</summary>
    Task UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);
}
