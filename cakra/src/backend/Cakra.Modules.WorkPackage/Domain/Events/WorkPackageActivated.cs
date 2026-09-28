using Cakra.Core;

namespace Cakra.Modules.WorkPackage.Domain.Events;

/// <summary>
/// Domain event published when a Work Package transitions from DRAFT to ACTIVE (Architecture §11, Domain §10).
/// </summary>
public sealed record WorkPackageActivated(
    Guid WorkPackageId,
    WorkPackageStatus PreviousStatus,
    WorkPackageStatus NewStatus,
    DateTime OccurredAtUtc,
    Guid EventId) : IDomainEvent
{
    public WorkPackageActivated(
        Guid workPackageId,
        WorkPackageStatus previousStatus,
        WorkPackageStatus newStatus,
        DateTime occurredAtUtc)
        : this(workPackageId, previousStatus, newStatus, occurredAtUtc, Guid.NewGuid())
    {
    }
}
