using Cakra.Core;

namespace Cakra.Modules.WorkPackage.Domain.Events;

/// <summary>
/// Domain event published when the Customer or Product context association of a Work Package changes (CR-024).
/// </summary>
public sealed record WorkPackageContextChanged(
    Guid WorkPackageId,
    Guid? PreviousCustomerId,
    Guid? NewCustomerId,
    Guid? PreviousProductId,
    Guid? NewProductId,
    DateTime OccurredAtUtc,
    Guid EventId) : IDomainEvent
{
    public WorkPackageContextChanged(
        Guid workPackageId,
        Guid? previousCustomerId,
        Guid? newCustomerId,
        Guid? previousProductId,
        Guid? newProductId,
        DateTime occurredAtUtc)
        : this(workPackageId, previousCustomerId, newCustomerId, previousProductId, newProductId, occurredAtUtc, Guid.NewGuid())
    {
    }
}
