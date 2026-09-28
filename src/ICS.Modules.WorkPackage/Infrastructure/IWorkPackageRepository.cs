namespace ICS.Modules.WorkPackage.Infrastructure;

using ICS.Modules.WorkPackage.Application;
using ICS.Modules.WorkPackage.Domain;

/// <summary>
/// Repository contract for the Work Package aggregate using explicit parameterized Dapper SQL.
/// Public only so the module's public <see cref="IWorkPackageQueryService"/> can consume it;
/// it is a module implementation detail and is not a cross-module contract — other modules
/// must use <see cref="IWorkPackageQueryService"/> instead.
/// Architecture §11, §16, §17, §19.3, §20.
/// </summary>
public interface IWorkPackageRepository
{
    Task<WorkPackage?> GetByIdAsync(Guid workPackageId, CancellationToken cancellationToken = default);

    Task AddAsync(WorkPackage workPackage, CancellationToken cancellationToken = default);

    Task UpdateAsync(WorkPackage workPackage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a request is currently an active member of any Work Package other than
    /// <paramref name="excludingWorkPackageId"/>. Backs the cross-aggregate half of Business Rule 9.
    /// </summary>
    Task<bool> IsRequestInActiveWorkPackageAsync(
        Guid requestId,
        Guid? excludingWorkPackageId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a newly added active membership link. Returns 0 when an active link already exists.
    /// </summary>
    Task<int> AddRequestMembershipAsync(
        WorkPackage workPackage,
        WorkPackageRequest membership,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates the active membership link for a request. Returns the number of rows affected.
    /// </summary>
    Task<int> DeactivateRequestMembershipAsync(
        Guid workPackageId,
        Guid requestId,
        DateTime removedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a Work Package state transition for audit purposes (Architecture §18).
    /// </summary>
    Task AddStateHistoryAsync(
        Guid workPackageId,
        string? fromStatus,
        string toStatus,
        Guid actorPersonId,
        string? reason,
        DateTime changedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkPackage>> ListAsync(WorkPackageGridFilterDto filter, CancellationToken cancellationToken = default);

    Task<(int TotalCount, IReadOnlyList<WorkPackage> Items)> GetFilteredGridAsync(
        WorkPackageGridFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkPackageRequest>> GetMembershipsAsync(Guid workPackageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the chronological state transition audit trail for a Work Package.
    /// </summary>
    Task<IReadOnlyList<WorkPackageStateHistoryDto>> GetStateHistoryAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the active Work Package membership of a request together with the status of the
    /// Work Package holding it, or null when the request has no active membership.
    /// The holder status lets callers apply the Business Rule 9 semantics precisely: a CLOSED
    /// Work Package releases the request, so it must not block reuse.
    /// </summary>
    Task<(WorkPackageRequest Membership, string HolderStatus)?> GetActiveMembershipForRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);
}
