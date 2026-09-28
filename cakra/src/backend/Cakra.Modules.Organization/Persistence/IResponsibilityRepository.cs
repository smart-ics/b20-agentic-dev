using Cakra.Core;
using Cakra.Modules.Organization.Domain;

namespace Cakra.Modules.Organization.Persistence;

/// <summary>
/// Persistence contract for <see cref="Responsibility"/> aggregate root (Architecture §6, §16, §19.3).
/// </summary>
internal interface IResponsibilityRepository : IRepository<Responsibility>
{
    /// <summary>Retrieves a responsibility by name, or null if not found.</summary>
    Task<Responsibility?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
