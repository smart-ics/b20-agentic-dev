using Cakra.Core;
using Cakra.Modules.Organization.Domain;

namespace Cakra.Modules.Organization.Persistence;

/// <summary>
/// Persistence contract for <see cref="Role"/> aggregate root (Architecture §6, §14, §19.3).
/// </summary>
internal interface IRoleRepository : IRepository<Role>
{
    /// <summary>Retrieves a role by name, or null if not found.</summary>
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
