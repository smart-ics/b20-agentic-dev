namespace ICS.Modules.WorkPackage.Domain;

using ICS.Core.Domain;

/// <summary>
/// Domain membership entity representing the association between a Work Package and an operational Request.
/// Preserves historical membership trace per Architecture §11 and work-package-domain.md §5, §8 (Rule 15).
/// </summary>
public class WorkPackageRequest : Entity
{
    public Guid WorkPackageId { get; private set; }
    public Guid RequestId { get; private set; }
    public DateTime AddedAt { get; private set; }
    public DateTime? RemovedAt { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Parameterless constructor for Dapper persistence mapping.
    /// </summary>
    protected WorkPackageRequest() { }

    /// <summary>
    /// Full constructor for creating or hydrating the membership entity.
    /// </summary>
    public WorkPackageRequest(
        Guid id,
        Guid workPackageId,
        Guid requestId,
        DateTime addedAt,
        DateTime? removedAt = null,
        bool isActive = true)
        : base(id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Membership ID cannot be empty.", nameof(id));
        if (workPackageId == Guid.Empty)
            throw new ArgumentException("WorkPackageId cannot be empty.", nameof(workPackageId));
        if (requestId == Guid.Empty)
            throw new ArgumentException("RequestId cannot be empty.", nameof(requestId));

        WorkPackageId = workPackageId;
        RequestId = requestId;
        AddedAt = addedAt;
        RemovedAt = removedAt;
        IsActive = isActive;
        CreatedAt = addedAt;
        UpdatedAt = removedAt;
    }

    /// <summary>
    /// Deactivates this membership link when the Request is removed from the Work Package,
    /// recording the removal timestamp and maintaining historical auditability.
    /// </summary>
    public void Deactivate(DateTime removedAt)
    {
        IsActive = false;
        RemovedAt = removedAt;
        UpdatedAt = removedAt;
    }
}
