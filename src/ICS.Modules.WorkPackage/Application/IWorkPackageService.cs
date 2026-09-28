namespace ICS.Modules.WorkPackage.Application;

/// <summary>
/// Application service contract for executing Work Package lifecycle commands.
/// Each command is dispatched through the MediatR pipeline and handled by a dedicated handler.
/// Architecture §11, §16, §19.2, §20.
/// </summary>
public interface IWorkPackageService
{
    /// <summary>
    /// Creates a new Work Package in DRAFT status. Validates the owner, customer, and product
    /// references against their owning modules. Emits WorkPackageCreated.
    /// </summary>
    Task<WorkPackageDto> CreateWorkPackageAsync(
        string name,
        string objective,
        Guid ownerPersonId,
        Guid? customerId = null,
        Guid? productId = null,
        Guid? workPackageId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the name and objective of a Work Package that is not closed.
    /// </summary>
    Task<WorkPackageDto> UpdateObjectiveAsync(
        Guid workPackageId,
        string name,
        string objective,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reassigns ownership to another active Person. Emits WorkPackageOwnerChanged.
    /// </summary>
    Task<WorkPackageDto> AssignOwnerAsync(
        Guid workPackageId,
        Guid newOwnerPersonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Associates a Request with a Work Package, enforcing Business Rule 9 across all active
    /// Work Packages. Emits RequestAddedToWorkPackage.
    /// </summary>
    Task<WorkPackageDto> AddRequestToWorkPackageAsync(
        Guid workPackageId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates the membership link for a Request, preserving the historical trace.
    /// Emits RequestRemovedFromWorkPackage.
    /// </summary>
    Task<WorkPackageDto> RemoveRequestFromWorkPackageAsync(
        Guid workPackageId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a Work Package from DRAFT to ACTIVE. Emits WorkPackageActivated.
    /// </summary>
    Task<WorkPackageDto> ActivateWorkPackageAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a Work Package to CLOSED. Constituent Request lifecycle states are not altered.
    /// Emits WorkPackageClosed.
    /// </summary>
    Task<WorkPackageDto> CloseWorkPackageAsync(
        Guid workPackageId,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
