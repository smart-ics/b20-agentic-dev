using Cakra.Core;

namespace Cakra.Modules.WorkPackage.Domain.Events;

/// <summary>
/// Domain event published when a Work Package transitions to CLOSED status (Architecture §11, Domain §10).
/// </summary>
public sealed record WorkPackageClosed(
    Guid WorkPackageId,
    string Reason,
    WorkPackageStatus PreviousStatus,
    WorkPackageStatus NewStatus,
    DateTime OccurredAtUtc,
    Guid EventId) : IDomainEvent
{
    public WorkPackageClosed(
        Guid workPackageId,
        string reason,
        WorkPackageStatus previousStatus,
        WorkPackageStatus newStatus,
        DateTime occurredAtUtc)
        : this(workPackageId, reason, previousStatus, newStatus, occurredAtUtc, Guid.NewGuid())
    {
    }
}
