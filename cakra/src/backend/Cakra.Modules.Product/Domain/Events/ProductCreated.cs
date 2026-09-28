using Cakra.Core;

namespace Cakra.Modules.Product.Domain.Events;

/// <summary>
/// Published when a new <see cref="Product"/> is created in the catalog (Architecture §10; Product Domain §10).
/// </summary>
public sealed record ProductCreated(
    Guid ProductId,
    string Code,
    string Name,
    string? Description,
    Guid OwnerPersonId,
    Guid EventId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public ProductCreated(
        Guid productId,
        string code,
        string name,
        string? description,
        Guid ownerPersonId)
        : this(productId, code, name, description, ownerPersonId, Guid.NewGuid(), DateTime.UtcNow)
    {
    }
}
