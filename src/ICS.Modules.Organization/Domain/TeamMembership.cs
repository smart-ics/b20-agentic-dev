namespace ICS.Modules.Organization.Domain;

using ICS.Core.Domain;
using ICS.Modules.Organization.Domain.Events;

/// <summary>
/// Domain entity representing participation of a Person within a Team.
/// Architecture §6, §7, §17.
/// </summary>
public class TeamMembership : Entity
{
    public Guid PersonId { get; private set; }
    public Guid TeamId { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? LeftAt { get; private set; }
    public bool IsActive { get; private set; } = true;

    protected TeamMembership() { }

    public TeamMembership(Guid id, Guid personId, Guid teamId, DateTime joinedAt, DateTime? leftAt = null, bool isActive = true)
        : base(id)
    {
        Id = id;
        PersonId = personId;
        TeamId = teamId;
        JoinedAt = joinedAt;
        CreatedAt = joinedAt;
        LeftAt = leftAt;
        IsActive = isActive;
    }

    public static TeamMembership Create(Guid id, Guid personId, Guid teamId, DateTime joinedAt)
    {
        if (personId == Guid.Empty)
            throw new ArgumentException("PersonId cannot be empty.", nameof(personId));
        if (teamId == Guid.Empty)
            throw new ArgumentException("TeamId cannot be empty.", nameof(teamId));

        var membership = new TeamMembership(id, personId, teamId, joinedAt, null, true);
        membership.AddDomainEvent(new TeamMemberAssigned(membership.Id, personId, teamId));
        return membership;
    }

    public void EndMembership(DateTime leftAt)
    {
        IsActive = false;
        LeftAt = leftAt;
        UpdatedAt = leftAt;
    }
}
