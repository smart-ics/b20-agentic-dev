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
    internal WorkPackageRequest(Guid id, Guid workPackageId, Guid requestId, DateTime addedAt)
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
        DateTime? updatedAt = null)
    {
        return new WorkPackageRequest
        {
            Id = id,
            WorkPackageId = workPackageId,
            RequestId = requestId,
            AddedAt = addedAt,
            RemovedAt = removedAt,
            CreatedAt = createdAt ?? addedAt,
            UpdatedAt = updatedAt
        };
    }
}
