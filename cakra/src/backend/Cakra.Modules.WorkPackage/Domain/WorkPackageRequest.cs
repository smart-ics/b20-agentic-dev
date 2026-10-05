using Cakra.Core;
using Cakra.Modules.WorkPackage.Domain.Exceptions;

namespace Cakra.Modules.WorkPackage.Domain;

/// <summary>
/// Membership entity representing the association between a Work Package and a constituent Request
/// (Architecture §11, §17, Domain §5).
/// Preserves historical membership traceability (Business Rule 15).
/// </summary>
public sealed class WorkPackageRequest : EntityBase
{
    /// <summary>Identifier of the enclosing Work Package.</summary>
    public Guid WorkPackageId { get; private set; }

    /// <summary>Identifier of the constituent Request.</summary>
    public Guid RequestId { get; private set; }

    /// <summary>UTC timestamp when the request was added to the Work Package.</summary>
    public DateTime AddedAt { get; private set; }

    /// <summary>UTC timestamp when the request was removed from the Work Package, or <c>null</c> if active.</summary>
    public DateTime? RemovedAt { get; private set; }

    /// <summary>Relative display sequence / priority order of the request within the Work Package (Architecture §11, CR-015).</summary>
    public int SortOrder { get; private set; }

    /// <summary>Indicates whether this membership link is currently active.</summary>
    public bool IsActive => RemovedAt is null;

    /// <summary>
    /// Parameterless constructor for Dapper persistence hydration.
    /// </summary>
    private WorkPackageRequest()
    {
    }

    /// <summary>
    /// Internal constructor called by <see cref="WorkPackage.AddRequest"/>.
    /// </summary>
    internal WorkPackageRequest(Guid id, Guid workPackageId, Guid requestId, DateTime addedAt, int sortOrder = 0)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        }

        if (workPackageId == Guid.Empty)
        {
            throw new ArgumentException("WorkPackageId cannot be empty.", nameof(workPackageId));
        }

        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("RequestId cannot be empty.", nameof(requestId));
        }

        Id = id;
        WorkPackageId = workPackageId;
        RequestId = requestId;
        AddedAt = addedAt;
        CreatedAt = addedAt;
        UpdatedAt = null;
        RemovedAt = null;
        SortOrder = sortOrder;
    }

    /// <summary>
    /// Updates the sort order index of this request within the Work Package (Architecture CR-015 §4 TD-003).
    /// </summary>
    /// <param name="sortOrder">New zero-based sort order index.</param>
    /// <param name="updatedAt">Optional UTC timestamp when the reordering occurred.</param>
    internal void SetSortOrder(int sortOrder, DateTime? updatedAt = null)
    {
        SortOrder = sortOrder;
        UpdatedAt = updatedAt ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates this membership link and records the removal timestamp (Architecture §11).
    /// </summary>
    /// <param name="removedAt">UTC timestamp when the removal occurred.</param>
    internal void MarkRemoved(DateTime removedAt)
    {
        if (RemovedAt is not null)
        {
            throw new WorkPackageDomainException(
                $"Request '{RequestId}' is already removed from Work Package '{WorkPackageId}'.");
        }

        if (removedAt < AddedAt)
        {
            throw new WorkPackageDomainException(
                "Removal timestamp cannot be earlier than addition timestamp.");
        }

        RemovedAt = removedAt;
        UpdatedAt = removedAt;
    }

    /// <summary>
    /// Rehydrates a WorkPackageRequest membership entity from persistence storage without domain validation.
    /// </summary>
    public static WorkPackageRequest Rehydrate(
        Guid id,
        Guid workPackageId,
        Guid requestId,
        DateTime addedAt,
        DateTime? removedAt = null,
        DateTime? createdAt = null,
        DateTime? updatedAt = null,
        int sortOrder = 0)
    {
        return new WorkPackageRequest
        {
            Id = id,
            WorkPackageId = workPackageId,
            RequestId = requestId,
            AddedAt = addedAt,
            RemovedAt = removedAt,
            CreatedAt = createdAt ?? addedAt,
            UpdatedAt = updatedAt,
            SortOrder = sortOrder
        };
    }
}
