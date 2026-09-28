using Cakra.Core;
using Cakra.Modules.Organization.Domain;

namespace Cakra.Modules.Organization.Persistence;

/// <summary>
/// Persistence contract for <see cref="Person"/> aggregate root (Architecture §6, §19.3).
/// </summary>
internal interface IPersonRepository : IRepository<Person>
{
    /// <summary>Retrieves a person by email, or null if not found.</summary>
    Task<Person?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Retrieves all persons currently in ACTIVE status.</summary>
    Task<IReadOnlyList<Person>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates the lifecycle status of a person.</summary>
    Task UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);
}
