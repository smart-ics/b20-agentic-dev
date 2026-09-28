using Cakra.Core;

namespace Cakra.Modules.WorkPackage.Domain.Events;

/// <summary>
/// Domain event published when a Request is added to a Work Package (Architecture §11, Domain §10).
/// </summary>
public sealed record RequestAddedToWorkPackage(
    Guid WorkPackageId,
    Guid RequestId,
    DateTime OccurredAtUtc,
    Guid EventId) : IDomainEvent
{
    public RequestAddedToWorkPackage(
        Guid workPackageId,
        Guid requestId,
        DateTime occurredAtUtc)
        : this(workPackageId, requestId, occurredAtUtc, Guid.NewGuid())
    {
    }
}
