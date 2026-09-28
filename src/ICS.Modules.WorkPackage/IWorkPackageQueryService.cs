namespace ICS.Modules.WorkPackage;

using ICS.Modules.WorkPackage.Application;

/// <summary>
/// In-process query contract exposed by the Work Package module for cross-module integration and UI queries.
/// Architecture §11, §15, §16, §20.
/// </summary>
public interface IWorkPackageQueryService
{
    /// <summary>
    /// Gets a single Work Package with its request memberships, or null when it does not exist.
    /// </summary>
    Task<WorkPackageDto?> GetWorkPackageByIdAsync(Guid workPackageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Work Packages with optional filtering and paging.
    /// </summary>
    Task<WorkPackageGridResultDto> ListWorkPackagesAsync(
        WorkPackageGridFilterDto? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full scope of a Work Package: the package, its resolved references, and its memberships.
    /// Returns null when the Work Package does not exist.
    /// </summary>
    Task<WorkPackageScopeDto?> GetWorkPackageScopeAsync(Guid workPackageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the active Work Package membership of a request, or null when the request has none.
    /// </summary>
    Task<WorkPackageRequestMembershipDto?> GetRequestWorkPackageAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the state transition audit trail of a Work Package.
    /// </summary>
    Task<IReadOnlyList<WorkPackageStateHistoryDto>> GetWorkPackageStateHistoryAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a request is currently an active member of a Work Package, enforcing
    /// Business Rule 9 for callers outside this module.
    /// </summary>
    Task<bool> IsRequestInActiveWorkPackageAsync(
        Guid requestId,
        Guid? excludingWorkPackageId = null,
        CancellationToken cancellationToken = default);
}
