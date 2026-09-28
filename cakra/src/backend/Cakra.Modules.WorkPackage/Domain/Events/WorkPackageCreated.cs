using Cakra.Core;

namespace Cakra.Modules.WorkPackage.Domain.Events;

/// <summary>
/// Domain event published when a Work Package is created in DRAFT status (Architecture §11, Domain §10).
/// </summary>
public sealed record WorkPackageCreated(
    Guid WorkPackageId,
    string Name,
    string Objective,
    Guid OwnerPersonId,
    Guid? CustomerId,
    Guid? ProductId,
    DateTime OccurredAtUtc,
    Guid EventId) : IDomainEvent
{
    public WorkPackageCreated(
        Guid workPackageId,
        string name,
        string objective,
        Guid ownerPersonId,
        Guid? customerId,
        Guid? productId,
        DateTime occurredAtUtc)
        : this(workPackageId, name, objective, ownerPersonId, customerId, productId, occurredAtUtc, Guid.NewGuid())
    {
    }
}
