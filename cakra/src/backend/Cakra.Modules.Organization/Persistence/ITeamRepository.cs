using Cakra.Core;
using Cakra.Modules.Organization.Domain;

namespace Cakra.Modules.Organization.Persistence;

/// <summary>
/// Persistence contract for <see cref="Team"/> aggregate root (Architecture §6, §19.3).
/// </summary>
internal interface ITeamRepository : IRepository<Team>
{
    /// <summary>Retrieves a team by name, or null if not found.</summary>
    Task<Team?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
