namespace ICS.Modules.Customer.Domain.Events;

using ICS.Core.Domain;

/// <summary>
/// Domain event published when a new Customer is created.
/// Architecture §6, §7, §16, §18; customer-domain.md.
/// </summary>
public sealed record CustomerCreated(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    bool HasActiveMaintenanceContract) : DomainEvent;

/// <summary>
/// Domain event published when a Customer is deactivated.
/// Architecture §6, §7, §16, §18; customer-domain.md.
/// </summary>
public sealed record CustomerDeactivated(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName) : DomainEvent;

/// <summary>
/// Domain event published when a Customer is re-activated.
/// Architecture §6, §7, §16, §18; customer-domain.md.
/// </summary>
public sealed record CustomerActivated(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName) : DomainEvent;

/// <summary>
/// Domain event published when Customer master data is updated.
/// Architecture §6, §7, §16, §18; customer-domain.md.
/// </summary>
public sealed record CustomerMasterDataUpdated(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    bool HasActiveMaintenanceContract) : DomainEvent;

/// <summary>
/// Domain event published when a CustomerContact is added to a Customer.
/// Architecture §6, §7, §16, §18; customer-domain.md.
/// </summary>
public sealed record CustomerContactAdded(
    Guid ContactId,
    Guid CustomerId,
    string Name,
    string? Email) : DomainEvent;

/// <summary>
/// Domain event published when a CustomerContact is updated.
/// Architecture §6, §7, §16, §18; customer-domain.md.
/// </summary>
public sealed record CustomerContactUpdated(
    Guid ContactId,
    Guid CustomerId,
    string Name,
    string? Email) : DomainEvent;

/// <summary>
/// Domain event published when a CustomerContact is deactivated.
/// Architecture §6, §7, §16, §18; customer-domain.md.
/// </summary>
public sealed record CustomerContactDeactivated(
    Guid ContactId,
    Guid CustomerId,
    string Name) : DomainEvent;

/// <summary>
/// Domain event published when a CustomerContact is re-activated.
/// Architecture §6, §7, §16, §18; customer-domain.md.
/// </summary>
public sealed record CustomerContactActivated(
    Guid ContactId,
    Guid CustomerId,
    string Name) : DomainEvent;
