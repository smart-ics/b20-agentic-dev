namespace Cakra.Modules.WorkPackage.Domain;

/// <summary>
/// Domain-level contract to verify whether a Request is already associated with an active Work Package.
/// Enforces Business Rule 9: A request may belong to at most one active Work Package.
/// </summary>
public interface IActiveWorkPackageChecker
{
    /// <summary>
    /// Checks whether the specified request is currently assigned to any active Work Package.
    /// </summary>
    /// <param name="requestId">Unique identifier of the request.</param>
    /// <param name="excludingWorkPackageId">Optional WorkPackageId to exclude from the check (e.g. current package).</param>
    /// <returns><c>true</c> if the request is already in an active work package; otherwise, <c>false</c>.</returns>
    bool IsRequestInActiveWorkPackage(Guid requestId, Guid? excludingWorkPackageId = null);

    /// <summary>
    /// Asynchronously checks whether the specified request is currently assigned to any active Work Package.
    /// </summary>
    Task<bool> IsRequestInActiveWorkPackageAsync(
        Guid requestId,
        Guid? excludingWorkPackageId = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(IsRequestInActiveWorkPackage(requestId, excludingWorkPackageId));
}
