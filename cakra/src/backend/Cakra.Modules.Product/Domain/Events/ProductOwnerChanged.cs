using Cakra.Core;

namespace Cakra.Modules.Product.Domain.Events;

/// <summary>
/// Published when ownership of a <see cref="Product"/> is assigned to a different person (Architecture §10; Product Domain §10).
/// </summary>
public sealed record ProductOwnerChanged(
    Guid ProductId,
    Guid PreviousOwnerPersonId,
    Guid NewOwnerPersonId,
    Guid EventId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public ProductOwnerChanged(
        Guid productId,
        Guid previousOwnerPersonId,
        Guid newOwnerPersonId)
        : this(productId, previousOwnerPersonId, newOwnerPersonId, Guid.NewGuid(), DateTime.UtcNow)
    {
    }
}
