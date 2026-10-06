using Cakra.Core;
using Cakra.Modules.Request.Domain;

namespace Cakra.Modules.Request.Persistence;

/// <summary>
/// Internal Dapper repository contract for the <see cref="Domain.Request"/> aggregate root,
/// <see cref="RequestResolution"/>, and <see cref="RequestAssignment"/> audit records
/// in the <c>request.*</c> schema (Architecture §6, §17, §18, §19.3, §20, §21).
/// </summary>
internal interface IRequestRepository : IRepository<Domain.Request>
{
    /// <summary>
    /// Records an ownership or lifecycle state transition audit entry into <c>request.RequestAssignments</c>
    /// using explicit parameterized SQL (Architecture §18 Audit Logging).
    /// Reusable across P4-S18, P4-S19, and P4-S20.
    /// </summary>
    Task AddAssignmentAsync(RequestAssignment assignment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all audit assignment records for the specified request ordered chronologically.
    /// </summary>
    Task<IReadOnlyList<RequestAssignment>> GetAssignmentsByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a resolution outcome into <c>request.RequestResolutions</c> using explicit parameterized SQL.
    /// </summary>
    Task AddResolutionAsync(RequestResolution resolution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the resolution record for the specified request, or <c>null</c> if unresolved.
    /// </summary>
    Task<RequestResolution?> GetResolutionByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the active request currently in <c>IN_PROGRESS</c> status for the specified owner person,
    /// or <c>null</c> if the person has no active in-progress task (Architecture §4 TD-001, CR-021).
    /// </summary>
    Task<Domain.Request?> GetActiveInProgressByOwnerAsync(
        Guid ownerPersonId,
        CancellationToken cancellationToken = default);
}
