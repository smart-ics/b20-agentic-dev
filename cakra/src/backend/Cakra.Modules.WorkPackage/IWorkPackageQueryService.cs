using Cakra.Modules.WorkPackage.Domain;

namespace Cakra.Modules.WorkPackage;

/// <summary>
/// Filter parameters for <see cref="IWorkPackageQueryService.ListWorkPackagesAsync(WorkPackageFilter?, CancellationToken)"/>
/// (Architecture §7, §11 — UC-WP-003, SCR-WP-001).
/// </summary>
public sealed record WorkPackageFilter
{
    /// <summary>Optional lifecycle status filter (<c>DRAFT</c>, <c>ACTIVE</c>, <c>CLOSED</c>).</summary>
    public string? Status { get; init; }

    /// <summary>Convenience alias for <see cref="Status"/>.</summary>
    public string? StatusFilter
    {
        get => Status;
        init => Status = value;
    }

    /// <summary>Optional Work Package Owner <c>PersonId</c> filter.</summary>
    public Guid? OwnerPersonId { get; init; }

    /// <summary>Optional <c>CustomerId</c> filter.</summary>
    public Guid? CustomerId { get; init; }

    /// <summary>Optional <c>ProductId</c> filter.</summary>
    public Guid? ProductId { get; init; }
}

/// <summary>
/// Published cross-module query contract for Work Package details, filtered package listings,
/// scope projections, and request-to-work-package associations (Architecture §7, §11, §15, §20, §21).
/// Internal repositories are not exposed outside the Work Package module boundary.
/// </summary>
public interface IWorkPackageQueryService
{
    /// <summary>
    /// Retrieves a Work Package by its unique identifier, enriched with owner, customer, product,
    /// and scope details, or <c>null</c> if not found (Architecture §11).
    /// </summary>
    Task<WorkPackageDto?> GetWorkPackageByIdAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="GetWorkPackageByIdAsync"/> (Architecture §11).
    /// </summary>
    Task<WorkPackageDto?> GetWorkPackageById(
        Guid workPackageId,
        CancellationToken cancellationToken = default)
        => GetWorkPackageByIdAsync(workPackageId, cancellationToken);

    /// <summary>
    /// Retrieves a filtered list of Work Packages matching the optional status, ownerPersonId,
    /// customerId, and productId criteria, ordered by creation timestamp descending (Architecture §11).
    /// </summary>
    Task<IReadOnlyList<WorkPackageDto>> ListWorkPackagesAsync(
        string? status = null,
        Guid? ownerPersonId = null,
        Guid? customerId = null,
        Guid? productId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="ListWorkPackagesAsync(string?, Guid?, Guid?, Guid?, CancellationToken)"/> (Architecture §11).
    /// </summary>
    Task<IReadOnlyList<WorkPackageDto>> ListWorkPackages(
        string? status = null,
        Guid? ownerPersonId = null,
        Guid? customerId = null,
        Guid? productId = null,
        CancellationToken cancellationToken = default)
        => ListWorkPackagesAsync(status, ownerPersonId, customerId, productId, cancellationToken);

    /// <summary>
    /// Convenience overload accepting a strongly typed <see cref="WorkPackageStatus"/> filter.
    /// </summary>
    Task<IReadOnlyList<WorkPackageDto>> ListWorkPackagesAsync(
        WorkPackageStatus? status,
        Guid? ownerPersonId = null,
        Guid? customerId = null,
        Guid? productId = null,
        CancellationToken cancellationToken = default)
        => ListWorkPackagesAsync(status?.ToName(), ownerPersonId, customerId, productId, cancellationToken);

    /// <summary>
    /// Convenience alias for <see cref="ListWorkPackagesAsync(WorkPackageStatus?, Guid?, Guid?, Guid?, CancellationToken)"/>.
    /// </summary>
    Task<IReadOnlyList<WorkPackageDto>> ListWorkPackages(
        WorkPackageStatus? status,
        Guid? ownerPersonId = null,
        Guid? customerId = null,
        Guid? productId = null,
        CancellationToken cancellationToken = default)
        => ListWorkPackagesAsync(status?.ToName(), ownerPersonId, customerId, productId, cancellationToken);

    /// <summary>
    /// Convenience overload accepting a <see cref="WorkPackageFilter"/> object.
    /// </summary>
    Task<IReadOnlyList<WorkPackageDto>> ListWorkPackagesAsync(
        WorkPackageFilter? filter,
        CancellationToken cancellationToken = default)
        => ListWorkPackagesAsync(
            filter?.Status,
            filter?.OwnerPersonId,
            filter?.CustomerId,
            filter?.ProductId,
            cancellationToken);

    /// <summary>
    /// Convenience alias for <see cref="ListWorkPackagesAsync(WorkPackageFilter?, CancellationToken)"/>.
    /// </summary>
    Task<IReadOnlyList<WorkPackageDto>> ListWorkPackages(
        WorkPackageFilter? filter,
        CancellationToken cancellationToken = default)
        => ListWorkPackagesAsync(filter, cancellationToken);

    /// <summary>
    /// Retrieves current and historical Requests included in the specified Work Package,
    /// enriched with authoritative Request details via <c>IRequestQueryService</c> (Architecture §11).
    /// </summary>
    Task<IReadOnlyList<WorkPackageScopeItemDto>> GetWorkPackageScopeAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="GetWorkPackageScopeAsync(Guid, CancellationToken)"/> (Architecture §11).
    /// </summary>
    Task<IReadOnlyList<WorkPackageScopeItemDto>> GetWorkPackageScope(
        Guid workPackageId,
        CancellationToken cancellationToken = default)
        => GetWorkPackageScopeAsync(workPackageId, cancellationToken);

    /// <summary>
    /// Resolves the active (non-closed) Work Package currently containing the specified Request,
    /// or <c>null</c> if the Request does not belong to an active Work Package (Architecture §11).
    /// </summary>
    Task<WorkPackageDto?> GetRequestWorkPackageAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="GetRequestWorkPackageAsync"/> (Architecture §11).
    /// </summary>
    Task<WorkPackageDto?> GetRequestWorkPackage(
        Guid requestId,
        CancellationToken cancellationToken = default)
        => GetRequestWorkPackageAsync(requestId, cancellationToken);

    /// <summary>
    /// Checks whether a Work Package with the specified identifier exists in <c>workpackage.WorkPackages</c>
    /// (used by Post and other modules for cross-module reference validation per Architecture §15).
    /// </summary>
    Task<bool> WorkPackageExistsAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous convenience overload for <see cref="WorkPackageExistsAsync"/>.
    /// </summary>
    bool WorkPackageExists(Guid workPackageId)
        => WorkPackageExistsAsync(workPackageId, CancellationToken.None).GetAwaiter().GetResult();
}
