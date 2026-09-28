namespace ICS.Modules.Organization.Persistence;

using ICS.Modules.Organization.Application.DTOs;
using ICS.Modules.Organization.Domain;

/// <summary>
/// Internal repository contract for Team and TeamMembership persistence.
/// </summary>
internal interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task<Team?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task AddAsync(Team team, CancellationToken cancellationToken = default);
    Task UpdateAsync(Team team, CancellationToken cancellationToken = default);
    Task AddMembershipAsync(TeamMembership membership, CancellationToken cancellationToken = default);
    Task<TeamMembership?> GetActiveMembershipAsync(Guid personId, Guid teamId, CancellationToken cancellationToken = default);
    Task EndMembershipAsync(Guid membershipId, DateTime leftAt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeamMemberDto>> GetRosterByTeamIdAsync(Guid teamId, CancellationToken cancellationToken = default);
}
