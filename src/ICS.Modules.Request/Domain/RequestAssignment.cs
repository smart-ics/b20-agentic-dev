namespace ICS.Modules.Request.Domain;

using ICS.Core.Domain;

/// <summary>
/// Domain entity representing a Request ownership assignment record preserving audit history.
/// Architecture §6, §16, §17; request-domain.md §5, §8.
/// </summary>
public class RequestAssignment : Entity
{
    public Guid RequestId { get; private set; }
    public Guid OwnerPersonId { get; private set; }
    public Guid AssignedByPersonId { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public string? Note { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Parameterless constructor for Dapper persistence mapping.
    /// </summary>
    protected RequestAssignment() { }

    public RequestAssignment(
        Guid id,
        Guid requestId,
        Guid ownerPersonId,
        Guid assignedByPersonId,
        DateTime assignedAt,
        string? note = null,
        bool isActive = true)
        : base(id)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("RequestId cannot be empty.", nameof(requestId));
        if (ownerPersonId == Guid.Empty)
            throw new ArgumentException("OwnerPersonId cannot be empty.", nameof(ownerPersonId));
        if (assignedByPersonId == Guid.Empty)
            throw new ArgumentException("AssignedByPersonId cannot be empty.", nameof(assignedByPersonId));

        RequestId = requestId;
        OwnerPersonId = ownerPersonId;
        AssignedByPersonId = assignedByPersonId;
        AssignedAt = assignedAt;
        Note = note?.Trim();
        IsActive = isActive;
        CreatedAt = assignedAt;
    }

    /// <summary>
    /// Deactivates this assignment record when ownership is reassigned to another person.
    /// </summary>
    public void Deactivate(DateTime deactivationTime)
    {
        IsActive = false;
        UpdatedAt = deactivationTime;
    }
}
