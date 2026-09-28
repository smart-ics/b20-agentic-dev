using Cakra.Core;

namespace Cakra.Modules.WorkPackage.Domain.Events;

/// <summary>
/// Domain event published when the ownership of a Work Package is assigned to a different Person (Architecture §11, Domain §10).
/// </summary>
public sealed record WorkPackageOwnerChanged(
    Guid WorkPackageId,
    Guid PreviousOwnerPersonId,
    Guid NewOwnerPersonId,
    DateTime OccurredAtUtc,
    Guid EventId) : IDomainEvent
{
    public WorkPackageOwnerChanged(
        Guid workPackageId,
        Guid previousOwnerPersonId,
        Guid newOwnerPersonId,
        DateTime occurredAtUtc)
        : this(workPackageId, previousOwnerPersonId, newOwnerPersonId, occurredAtUtc, Guid.NewGuid())
    {
    }
}
