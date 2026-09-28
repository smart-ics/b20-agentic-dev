namespace ICS.Modules.Organization.Domain;

using ICS.Core.Domain;
using ICS.Modules.Organization.Domain.Events;

/// <summary>
/// Domain entity representing assignment of a Role to a Person.
/// Architecture §6, §14, §17.
/// </summary>
public class RoleAssignment : Entity
{
    public Guid PersonId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public bool IsActive { get; private set; } = true;

    protected RoleAssignment() { }

    public RoleAssignment(Guid id, Guid personId, Guid roleId, DateTime assignedAt, DateTime? revokedAt = null, bool isActive = true)
        : base(id)
    {
        Id = id;
        PersonId = personId;
        RoleId = roleId;
        AssignedAt = assignedAt;
        CreatedAt = assignedAt;
        RevokedAt = revokedAt;
        IsActive = isActive;
    }

    public static RoleAssignment Create(Guid id, Guid personId, Guid roleId, string roleName, DateTime assignedAt)
    {
        if (personId == Guid.Empty)
            throw new ArgumentException("PersonId cannot be empty.", nameof(personId));
        if (roleId == Guid.Empty)
            throw new ArgumentException("RoleId cannot be empty.", nameof(roleId));

        var assignment = new RoleAssignment(id, personId, roleId, assignedAt, null, true);
        assignment.AddDomainEvent(new RoleAssigned(assignment.Id, personId, roleId, roleName));
        return assignment;
    }

    public void Revoke(string roleName, DateTime revokedAt)
    {
        IsActive = false;
        RevokedAt = revokedAt;
        UpdatedAt = revokedAt;
        AddDomainEvent(new RoleRevoked(Id, PersonId, RoleId, roleName));
    }
}
