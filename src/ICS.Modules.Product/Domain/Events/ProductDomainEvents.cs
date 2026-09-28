namespace ICS.Modules.Product.Domain.Events;

using ICS.Core.Domain;

/// <summary>
/// Domain event published when a new Product is created.
/// Architecture §10, §16, §18; product-domain.md.
/// </summary>
public sealed record ProductCreated(
    Guid ProductId,
    string Code,
    string Name,
    string? Description,
    Guid OwnerPersonId) : DomainEvent;

/// <summary>
/// Domain event published when a Product's owner is changed.
/// Architecture §10, §16, §18; product-domain.md.
/// </summary>
public sealed record ProductOwnerChanged(
    Guid ProductId,
    Guid PreviousOwnerPersonId,
    Guid NewOwnerPersonId) : DomainEvent;

/// <summary>
/// Domain event published when a Product is activated.
/// Architecture §10, §16, §18; product-domain.md.
/// </summary>
public sealed record ProductActivated(
    Guid ProductId,
    string Code,
    string Name) : DomainEvent;

/// <summary>
/// Domain event published when a Product is deactivated.
/// Architecture §10, §16, §18; product-domain.md.
/// </summary>
public sealed record ProductDeactivated(
    Guid ProductId,
    string Code,
    string Name) : DomainEvent;
