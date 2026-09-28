using Cakra.Core;

namespace Cakra.Modules.Product.Domain.Events;

/// <summary>
/// Published when a <see cref="Product"/> transitions to <c>ACTIVE</c> status (Architecture §10; Product Domain §10).
/// </summary>
public sealed record ProductActivated(
    Guid ProductId,
    Guid EventId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public ProductActivated(Guid productId)
        : this(productId, Guid.NewGuid(), DateTime.UtcNow)
    {
    }
}
