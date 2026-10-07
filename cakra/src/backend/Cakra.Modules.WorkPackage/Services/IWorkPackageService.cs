namespace Cakra.Modules.WorkPackage.Services;

/// <summary>
/// Application command service contract for Work Package lifecycle and scope operations
/// (Architecture §6, §7, §8, §11, §15).
/// </summary>
public interface IWorkPackageService
{
    /// <summary>
    /// Creates a new Work Package in <c>DRAFT</c> state, validates the owner via <c>IOrganizationQueryService</c>,
    /// validates optional customer/product references via <c>ICustomerQueryService</c> and <c>IProductQueryService</c>,
    /// and emits <c>WorkPackageCreated</c> (Architecture §11 — UC-WP-001).
    /// </summary>
    Task<WorkPackageDto> CreateWorkPackageAsync(
        string name,
        string objective,
        Guid ownerPersonId,
        Guid? customerId = null,
        Guid? productId = null,
        DateTime? deadline = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="CreateWorkPackageAsync"/> (Architecture §11).
    /// </summary>
    Task<WorkPackageDto> CreateWorkPackage(
        string name,
        string objective,
        Guid ownerPersonId,
        Guid? customerId = null,
        Guid? productId = null,
        DateTime? deadline = null,
        CancellationToken cancellationToken = default)
        => CreateWorkPackageAsync(name, objective, ownerPersonId, customerId, productId, deadline, cancellationToken);

    /// <summary>
    /// Updates the descriptive title and objective of an existing non-closed Work Package (Architecture §11 — UC-WP-001).
    /// </summary>
    Task<WorkPackageDto> UpdateObjectiveAsync(
        Guid workPackageId,
        string name,
        string objective,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="UpdateObjectiveAsync"/> (Architecture §11).
    /// </summary>
    Task<WorkPackageDto> UpdateObjective(
        Guid workPackageId,
        string name,
        string objective,
        CancellationToken cancellationToken = default)
        => UpdateObjectiveAsync(workPackageId, name, objective, cancellationToken);

    /// <summary>
    /// Reassigns ownership of a Work Package to an active Person in Organization and emits
    /// <c>WorkPackageOwnerChanged</c> (Architecture §11 — UC-WP-001).
    /// </summary>
    Task<WorkPackageDto> AssignOwnerAsync(
        Guid workPackageId,
        Guid newOwnerPersonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="AssignOwnerAsync"/> (Architecture §11).
    /// </summary>
    Task<WorkPackageDto> AssignOwner(
        Guid workPackageId,
        Guid newOwnerPersonId,
        CancellationToken cancellationToken = default)
        => AssignOwnerAsync(workPackageId, newOwnerPersonId, cancellationToken);

    /// <summary>
    /// Associates an existing Request with a Work Package after validating via <c>IRequestQueryService</c>
    /// that the Request exists and enforcing Business Rule 9 that the Request is not already in another
    /// active Work Package. Emits <c>RequestAddedToWorkPackage</c> (Architecture §11 — UC-WP-002).
    /// </summary>
    Task<WorkPackageDto> AddRequestToWorkPackageAsync(
        Guid workPackageId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="AddRequestToWorkPackageAsync"/> (Architecture §11).
    /// </summary>
    Task<WorkPackageDto> AddRequestToWorkPackage(
        Guid workPackageId,
        Guid requestId,
        CancellationToken cancellationToken = default)
        => AddRequestToWorkPackageAsync(workPackageId, requestId, cancellationToken);

    /// <summary>
    /// Removes a Request from a Work Package by deactivating its membership link and recording
    /// the removal timestamp. Emits <c>RequestRemovedFromWorkPackage</c> (Architecture §11 — UC-WP-002).
    /// </summary>
    Task<WorkPackageDto> RemoveRequestFromWorkPackageAsync(
        Guid workPackageId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="RemoveRequestFromWorkPackageAsync"/> (Architecture §11).
    /// </summary>
    Task<WorkPackageDto> RemoveRequestFromWorkPackage(
        Guid workPackageId,
        Guid requestId,
        CancellationToken cancellationToken = default)
        => RemoveRequestFromWorkPackageAsync(workPackageId, requestId, cancellationToken);

    /// <summary>
    /// Transitions a Work Package from <c>DRAFT</c> to <c>ACTIVE</c> state and emits
    /// <c>WorkPackageActivated</c> (Architecture §11 — UC-WP-001).
    /// </summary>
    Task<WorkPackageDto> ActivateWorkPackageAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="ActivateWorkPackageAsync"/> (Architecture §11).
    /// </summary>
    Task<WorkPackageDto> ActivateWorkPackage(
        Guid workPackageId,
        CancellationToken cancellationToken = default)
        => ActivateWorkPackageAsync(workPackageId, cancellationToken);

    /// <summary>
    /// Transitions a Work Package from <c>ACTIVE</c> or <c>DRAFT</c> to <c>CLOSED</c> state,
    /// recording the close reason and timestamp without altering constituent Request lifecycle states.
    /// Emits <c>WorkPackageClosed</c> (Architecture §11 — UC-WP-001).
    /// </summary>
    Task<WorkPackageDto> CloseWorkPackageAsync(
        Guid workPackageId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="CloseWorkPackageAsync"/> (Architecture §11).
    /// </summary>
    Task<WorkPackageDto> CloseWorkPackage(
        Guid workPackageId,
        string reason,
        CancellationToken cancellationToken = default)
        => CloseWorkPackageAsync(workPackageId, reason, cancellationToken);

    /// <summary>
    /// Reorders the active constituent requests of a Work Package according to the specified list of Request IDs
    /// (CR-015; Architecture §4 TD-002, TD-003).
    /// </summary>
    Task<WorkPackageDto> ReorderRequestsAsync(
        Guid workPackageId,
        IReadOnlyList<Guid> orderedRequestIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="ReorderRequestsAsync"/> (CR-015; Architecture §4).
    /// </summary>
    Task<WorkPackageDto> ReorderRequests(
        Guid workPackageId,
        IReadOnlyList<Guid> orderedRequestIds,
        CancellationToken cancellationToken = default)
        => ReorderRequestsAsync(workPackageId, orderedRequestIds, cancellationToken);

    /// <summary>
    /// Updates or clears the target deadline date of an existing non-closed Work Package (Architecture CR-023 §4 TD-002, TD-004).
    /// </summary>
    Task<WorkPackageDto> UpdateDeadlineAsync(
        Guid workPackageId,
        DateTime? deadline,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="UpdateDeadlineAsync"/> (Architecture CR-023 §4).
    /// </summary>
    Task<WorkPackageDto> UpdateDeadline(
        Guid workPackageId,
        DateTime? deadline,
        CancellationToken cancellationToken = default)
        => UpdateDeadlineAsync(workPackageId, deadline, cancellationToken);
}

