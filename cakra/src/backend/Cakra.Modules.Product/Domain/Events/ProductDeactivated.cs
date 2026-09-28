using Cakra.Core;

namespace Cakra.Modules.Product.Domain.Events;

/// <summary>
/// Published when a <see cref="Product"/> transitions to <c>INACTIVE</c> status (Architecture §10; Product Domain §10).
/// </summary>
public sealed record ProductDeactivated(
    Guid ProductId,
    Guid EventId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public ProductDeactivated(Guid productId)
        : this(productId, Guid.NewGuid(), DateTime.UtcNow)
    {
    }
}
