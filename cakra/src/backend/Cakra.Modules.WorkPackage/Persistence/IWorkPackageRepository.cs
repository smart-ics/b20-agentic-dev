using Cakra.Core;
using Cakra.Modules.WorkPackage.Domain;

namespace Cakra.Modules.WorkPackage.Persistence;

/// <summary>
/// Internal Dapper repository contract for the <see cref="Domain.WorkPackage"/> aggregate root
/// and <see cref="WorkPackageRequest"/> membership links in the <c>workpackage.*</c> schema
/// (Architecture §6, §11, §17, §19.3, §20, §21).
/// </summary>
internal interface IWorkPackageRepository : IRepository<Domain.WorkPackage>, IActiveWorkPackageChecker
{
    /// <summary>
    /// Inserts a new <see cref="WorkPackageRequest"/> membership record into <c>workpackage.WorkPackageRequests</c>
    /// using explicit parameterized SQL.
    /// </summary>
    Task AddRequestMembershipAsync(
        WorkPackageRequest membership,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing <see cref="WorkPackageRequest"/> membership record (e.g. recording <c>RemovedAt</c>)
    /// in <c>workpackage.WorkPackageRequests</c> using explicit parameterized SQL.
    /// </summary>
    Task UpdateRequestMembershipAsync(
        WorkPackageRequest membership,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all current and historical <see cref="WorkPackageRequest"/> membership records
    /// for the specified Work Package ordered chronologically.
    /// </summary>
    Task<IReadOnlyList<WorkPackageRequest>> GetMembershipsByWorkPackageIdAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the Work Package aggregate root and all its constituent request memberships
    /// within a single atomic database transaction (Architecture CR-015 §4 TD-002, TD-003).
    /// </summary>
    Task SaveAsync(
        Domain.WorkPackage entity,
        CancellationToken cancellationToken = default);
}
