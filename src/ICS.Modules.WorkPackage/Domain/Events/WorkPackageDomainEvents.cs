namespace ICS.Modules.WorkPackage.Domain.Events;

using ICS.Core.Domain;

/// <summary>
/// Domain event published when a new Work Package is created in DRAFT state.
/// Architecture §11; work-package-domain.md §10.
/// </summary>
public sealed record WorkPackageCreated(
    Guid WorkPackageId,
    string Name,
    string Objective,
    Guid OwnerPersonId,
    Guid? CustomerId,
    Guid? ProductId) : DomainEvent;

/// <summary>
/// Domain event published when a Work Package transitions from DRAFT to ACTIVE state.
/// Architecture §11; work-package-domain.md §10.
/// </summary>
public sealed record WorkPackageActivated(
    Guid WorkPackageId,
    DateTime ActivatedAt) : DomainEvent;

/// <summary>
/// Domain event published when a Work Package is closed.
/// Architecture §11; work-package-domain.md §10.
/// </summary>
public sealed record WorkPackageClosed(
    Guid WorkPackageId,
    string? Reason,
    DateTime ClosedAt) : DomainEvent;

/// <summary>
/// Domain event published when ownership of a Work Package is reassigned to another Person.
/// Architecture §11; work-package-domain.md §10.
/// </summary>
public sealed record WorkPackageOwnerChanged(
    Guid WorkPackageId,
    Guid PreviousOwnerPersonId,
    Guid NewOwnerPersonId,
    DateTime ChangedAt) : DomainEvent;

/// <summary>
/// Domain event published when an operational Request is associated with a Work Package.
/// Architecture §11; work-package-domain.md §10.
/// </summary>
public sealed record RequestAddedToWorkPackage(
    Guid WorkPackageId,
    Guid RequestId,
    DateTime AddedAt) : DomainEvent;

/// <summary>
/// Domain event published when a Request membership link in a Work Package is deactivated.
/// Architecture §11; work-package-domain.md §10.
/// </summary>
public sealed record RequestRemovedFromWorkPackage(
    Guid WorkPackageId,
    Guid RequestId,
    DateTime RemovedAt) : DomainEvent;
