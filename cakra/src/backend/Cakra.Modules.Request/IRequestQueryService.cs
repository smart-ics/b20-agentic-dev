namespace Cakra.Modules.Request;

/// <summary>
/// Filter and pagination parameters for <see cref="IRequestQueryService.GetFilteredRequestGridAsync(RequestGridFilter?, CancellationToken)"/>
/// (Architecture §7, §8 — UC-COL-002..004, SCR-REQ-001, SCR-REQ-005).
/// </summary>
public sealed record RequestGridFilter
{
    /// <summary>Optional lifecycle status filter (e.g. CAPTURED, EVALUATING, ACCEPTED, REJECTED, IN_PROGRESS, ESCALATED, COMPLETED).</summary>
    public string? Status { get; init; }

    /// <summary>Optional assigned Request Owner PersonId filter.</summary>
    public Guid? OwnerPersonId { get; init; }

    /// <summary>Convenience alias for <see cref="OwnerPersonId"/>.</summary>
    public Guid? AssigneePersonId
    {
        get => OwnerPersonId;
        init => OwnerPersonId = value;
    }

    /// <summary>Convenience alias for <see cref="OwnerPersonId"/>.</summary>
    public Guid? AssigneeId
    {
        get => OwnerPersonId;
        init => OwnerPersonId = value;
    }

    /// <summary>Optional CustomerId filter.</summary>
    public Guid? CustomerId { get; init; }

    /// <summary>Optional ProductId filter.</summary>
    public Guid? ProductId { get; init; }

    /// <summary>Optional WorkPackageId filter.</summary>
    public Guid? WorkPackageId { get; init; }

    /// <summary>Optional search term matched against Title or Description.</summary>
    public string? SearchTerm { get; init; }

    /// <summary>1-based page number (default 1). Ignored when <see cref="Offset"/> is explicitly specified.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Number of items per page (default 20, clamped to 1..200).</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>Optional explicit 0-based row offset. When null, computed from <see cref="Page"/> and <see cref="PageSize"/>.</summary>
    public int? Offset { get; init; }
}

/// <summary>
/// Paginated result set returned by <see cref="IRequestQueryService.GetFilteredRequestGridAsync(RequestGridFilter?, CancellationToken)"/>
/// (Architecture §7, §8).
/// </summary>
public sealed record PagedRequestGridResult
{
    /// <summary>Current page of matching request records.</summary>
    public IReadOnlyList<RequestDto> Items { get; init; } = Array.Empty<RequestDto>();

    /// <summary>Convenience alias for <see cref="Items"/>.</summary>
    public IReadOnlyList<RequestDto> Requests => Items;

    /// <summary>Total count of matching requests across all pages.</summary>
    public int TotalCount { get; init; }

    /// <summary>1-based page index.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Page size used for the query.</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>0-based row offset used for the query.</summary>
    public int Offset { get; init; }

    /// <summary>Total number of pages available.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

/// <summary>
/// Published cross-module query contract for Request details, state history, assigned queues,
/// and filtered request grids (Architecture §7, §8, §15, §20, §21).
/// Internal repositories are not exposed outside the Request module boundary.
/// </summary>
public interface IRequestQueryService
{
    /// <summary>
    /// Retrieves a complete Request record by its unique identifier, including resolution outcome
    /// and ordered state assignment history, or <c>null</c> if not found (Architecture §7, §8).
    /// </summary>
    Task<RequestDto?> GetRequestByIdAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="GetRequestByIdAsync"/> (Architecture §7).
    /// </summary>
    Task<RequestDto?> GetRequestById(Guid requestId, CancellationToken cancellationToken = default)
        => GetRequestByIdAsync(requestId, cancellationToken);

    /// <summary>
    /// Retrieves the chronological state transition and ownership audit history for a Request
    /// from <c>request.RequestAssignments</c> (Architecture §7, §8, §18 — UC-COL-002, UC-COL-003).
    /// </summary>
    Task<IReadOnlyList<RequestAssignmentDto>> GetRequestStateHistoryAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="GetRequestStateHistoryAsync"/> (Architecture §7).
    /// </summary>
    Task<IReadOnlyList<RequestAssignmentDto>> GetRequestStateHistory(
        Guid requestId,
        CancellationToken cancellationToken = default)
        => GetRequestStateHistoryAsync(requestId, cancellationToken);

    /// <summary>
    /// Retrieves all requests assigned to the current authenticated person (from <c>ICurrentContextProvider.CurrentPersonId</c>)
    /// or the explicitly supplied <paramref name="personId"/>, ordered by creation timestamp descending
    /// (Architecture §7, §8 — UC-COL-004, SCR-REQ-004).
    /// </summary>
    Task<IReadOnlyList<RequestDto>> ListMyAssignedRequestsAsync(
        Guid? personId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="ListMyAssignedRequestsAsync(Guid?, CancellationToken)"/> (Architecture §7).
    /// </summary>
    Task<IReadOnlyList<RequestDto>> ListMyAssignedRequests(
        Guid? personId = null,
        CancellationToken cancellationToken = default)
        => ListMyAssignedRequestsAsync(personId, cancellationToken);

    /// <summary>
    /// Convenience overload for <see cref="ListMyAssignedRequestsAsync(Guid?, CancellationToken)"/>
    /// using ambient <c>CurrentContextProvider.CurrentPersonId</c>.
    /// </summary>
    Task<IReadOnlyList<RequestDto>> ListMyAssignedRequests(CancellationToken cancellationToken)
        => ListMyAssignedRequestsAsync(null, cancellationToken);

    /// <summary>
    /// Retrieves active requests containing sub-tasks assigned to <paramref name="personId"/> where IsCompleted = 0
    /// (Architecture CR-006 §4 TD-009).
    /// </summary>
    Task<IReadOnlyList<RequestDto>> GetRequestsWithAssignedSubTasksAsync(
        Guid personId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="GetRequestsWithAssignedSubTasksAsync"/>.
    /// </summary>
    Task<IReadOnlyList<RequestDto>> GetRequestsWithAssignedSubTasks(
        Guid personId,
        CancellationToken cancellationToken = default)
        => GetRequestsWithAssignedSubTasksAsync(personId, cancellationToken);

    /// <summary>
    /// Executes a filtered and paginated query over <c>request.Requests</c> filtering by status,
    /// assignee, customer, product, or work package (Architecture §7, §8 — UC-COL-002..004, SCR-REQ-001, SCR-REQ-005).
    /// </summary>
    Task<PagedRequestGridResult> GetFilteredRequestGridAsync(
        RequestGridFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="GetFilteredRequestGridAsync(RequestGridFilter?, CancellationToken)"/> (Architecture §7).
    /// </summary>
    Task<PagedRequestGridResult> GetFilteredRequestGrid(
        RequestGridFilter? filter = null,
        CancellationToken cancellationToken = default)
        => GetFilteredRequestGridAsync(filter, cancellationToken);

    /// <summary>
    /// Convenience overload for <see cref="GetFilteredRequestGridAsync(RequestGridFilter?, CancellationToken)"/>
    /// accepting individual filter and pagination parameters.
    /// </summary>
    Task<PagedRequestGridResult> GetFilteredRequestGridAsync(
        string? status,
        Guid? assigneePersonId = null,
        Guid? customerId = null,
        Guid? productId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
        => GetFilteredRequestGridAsync(
            new RequestGridFilter
            {
                Status = status,
                AssigneePersonId = assigneePersonId,
                CustomerId = customerId,
                ProductId = productId,
                Page = page,
                PageSize = pageSize
            },
            cancellationToken);

    /// <summary>
    /// Convenience alias for <see cref="GetFilteredRequestGridAsync(string?, Guid?, Guid?, Guid?, int, int, CancellationToken)"/>.
    /// </summary>
    Task<PagedRequestGridResult> GetFilteredRequestGrid(
        string? status,
        Guid? assigneePersonId = null,
        Guid? customerId = null,
        Guid? productId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
        => GetFilteredRequestGridAsync(
            status,
            assigneePersonId,
            customerId,
            productId,
            page,
            pageSize,
            cancellationToken);

    /// <summary>
    /// Checks whether a Request with the specified identifier exists in <c>request.Requests</c>
    /// (used by Work Package and Post modules for cross-module reference validation per Architecture §15).
    /// </summary>
    Task<bool> RequestExistsAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous convenience overload for <see cref="RequestExistsAsync"/>.
    /// </summary>
    bool RequestExists(Guid requestId)
        => RequestExistsAsync(requestId, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Retrieves multiple Request records by their identifiers (used by Work Package scope projections
    /// and Feed/Analytics read models per Architecture §11, §15).
    /// </summary>
    Task<IReadOnlyList<RequestDto>> GetRequestsByIdsAsync(
        IEnumerable<Guid> requestIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="GetRequestsByIdsAsync"/>.
    /// </summary>
    Task<IReadOnlyList<RequestDto>> GetRequestsByIds(
        IEnumerable<Guid> requestIds,
        CancellationToken cancellationToken = default)
        => GetRequestsByIdsAsync(requestIds, cancellationToken);

    /// <summary>
    /// Retrieves the real-time Work in Progress (WIP) overview across all persons with active
    /// or paused requests, including cumulative elapsed IN_PROGRESS hours per task (Architecture CR-021 §4 TD-002, TD-005, §5, §11).
    /// </summary>
    Task<IReadOnlyList<PersonWorkInProgressDto>> GetWorkInProgressOverviewAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PersonWorkInProgressDto>>(Array.Empty<PersonWorkInProgressDto>());

    /// <summary>
    /// Convenience alias for <see cref="GetWorkInProgressOverviewAsync"/> (Architecture CR-021 §4 TD-002).
    /// </summary>
    Task<IReadOnlyList<PersonWorkInProgressDto>> GetWorkInProgressOverview(
        CancellationToken cancellationToken = default)
        => GetWorkInProgressOverviewAsync(cancellationToken);

    /// <summary>
    /// Computes the empirical organization-wide daily throughput (C_org) across all completed requests
    /// within the specified rolling window (Architecture CR-025 TD-001).
    /// </summary>
    /// <param name="windowDays">Rolling window in calendar days (defaults to 30).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Demonstrated daily complexity burn rate (minimum safety floor of 1.0).</returns>
    Task<double> GetOrgDemonstratedDailyThroughputAsync(
        int windowDays = 30,
        CancellationToken cancellationToken = default)
        => Task.FromResult(1.0);

    /// <summary>
    /// Convenience alias for <see cref="GetOrgDemonstratedDailyThroughputAsync"/> (Architecture CR-025 TD-001).
    /// </summary>
    Task<double> GetOrgDemonstratedDailyThroughput(
        int windowDays = 30,
        CancellationToken cancellationToken = default)
        => GetOrgDemonstratedDailyThroughputAsync(windowDays, cancellationToken);
}

