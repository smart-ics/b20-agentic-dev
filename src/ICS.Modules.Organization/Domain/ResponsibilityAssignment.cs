namespace ICS.Modules.Organization.Domain;

using ICS.Core.Domain;
using ICS.Modules.Organization.Domain.Events;

/// <summary>
/// Domain entity representing assignment of a Responsibility to a Person.
/// Architecture §6, §7, §17.
/// </summary>
public class ResponsibilityAssignment : Entity
{
    public Guid PersonId { get; private set; }
    public Guid ResponsibilityId { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public bool IsActive { get; private set; } = true;

    protected ResponsibilityAssignment() { }

    public ResponsibilityAssignment(Guid id, Guid personId, Guid responsibilityId, DateTime assignedAt, DateTime? revokedAt = null, bool isActive = true)
        : base(id)
    {
        Id = id;
        PersonId = personId;
        ResponsibilityId = responsibilityId;
        AssignedAt = assignedAt;
        CreatedAt = assignedAt;
        RevokedAt = revokedAt;
        IsActive = isActive;
    }

    public static ResponsibilityAssignment Create(Guid id, Guid personId, Guid responsibilityId, string responsibilityName, DateTime assignedAt)
    {
        if (personId == Guid.Empty)
            throw new ArgumentException("PersonId cannot be empty.", nameof(personId));
        if (responsibilityId == Guid.Empty)
            throw new ArgumentException("ResponsibilityId cannot be empty.", nameof(responsibilityId));

        var assignment = new ResponsibilityAssignment(id, personId, responsibilityId, assignedAt, null, true);
        assignment.AddDomainEvent(new ResponsibilityAssigned(assignment.Id, personId, responsibilityId, responsibilityName));
        return assignment;
    }

    public void Revoke(DateTime revokedAt)
    {
        IsActive = false;
        RevokedAt = revokedAt;
        UpdatedAt = revokedAt;
    }
}
