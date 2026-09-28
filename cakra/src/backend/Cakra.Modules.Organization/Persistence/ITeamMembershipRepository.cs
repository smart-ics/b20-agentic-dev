using Cakra.Modules.Organization.Domain;

namespace Cakra.Modules.Organization.Persistence;

/// <summary>
/// Persistence contract for <see cref="TeamMembership"/> junction records (Architecture §6, §16, §19.3).
/// </summary>
internal interface ITeamMembershipRepository
{
    /// <summary>Adds a person to a team.</summary>
    Task AddAsync(TeamMembership membership, CancellationToken cancellationToken = default);

    /// <summary>Removes a person from a team.</summary>
    Task RemoveAsync(Guid personId, Guid teamId, CancellationToken cancellationToken = default);

    /// <summary>Retrieves all team memberships for a person.</summary>
    Task<IReadOnlyList<TeamMembership>> GetByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>Retrieves all person memberships for a team.</summary>
    Task<IReadOnlyList<TeamMembership>> GetByTeamIdAsync(Guid teamId, CancellationToken cancellationToken = default);

    /// <summary>Checks whether a person is currently a member of a team.</summary>
    Task<bool> ExistsAsync(Guid personId, Guid teamId, CancellationToken cancellationToken = default);
}
