namespace Cakra.Modules.Analytics;

/// <summary>
/// Read-only projection representing an individual Request item within a real-time
/// programmer active workload queue (<c>SCR-MGT-003</c>) or a customer request portfolio
/// (<c>SCR-MGT-001</c>) (Architecture §7, §8, §9, §13 — FEAT-MGT-002, FEAT-MGT-004).
/// </summary>
public record CustomerPortfolioRequestItemDto
{
    /// <summary>Unique identifier of the request (<c>request.Requests.Id</c>).</summary>
    public Guid RequestId { get; init; }

    /// <summary>Convenience alias for <see cref="RequestId"/>.</summary>
    public Guid Id
    {
        get => RequestId;
        init => RequestId = value;
    }

    /// <summary>Brief summary or subject of the request.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Detailed description of the request.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Classification of the request (e.g. GENERAL, Bug, Feature, Support).</summary>
    public string RequestType { get; init; } = "GENERAL";

    /// <summary>Current authoritative lifecycle status (CAPTURED, ASSIGNED, IN_PROGRESS, PAUSED, COMPLETED, CANCELLED).</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Priority level (LOW, NORMAL, HIGH, URGENT).</summary>
    public string Priority { get; init; } = "NORMAL";

    /// <summary>Assigned Request Owner PersonId in the Organization domain, or <c>null</c> if unassigned.</summary>
    public Guid? OwnerPersonId { get; init; }

    /// <summary>Convenience alias for <see cref="OwnerPersonId"/>.</summary>
    public Guid? AssigneePersonId
    {
        get => OwnerPersonId;
        init => OwnerPersonId = value;
    }

    /// <summary>Resolved full name of the assigned Request Owner from <c>IOrganizationQueryService</c>.</summary>
    public string? OwnerName { get; init; }

    /// <summary>Convenience alias for <see cref="OwnerName"/>.</summary>
    public string? AssigneeName
    {
        get => OwnerName;
        init => OwnerName = value;
    }

    /// <summary>Associated CustomerId.</summary>
    public Guid? CustomerId { get; init; }

    /// <summary>Resolved customer name from <c>ICustomerQueryService</c>.</summary>
    public string? CustomerName { get; init; }

    /// <summary>Resolved customer code from <c>ICustomerQueryService</c>.</summary>
    public string? CustomerCode { get; init; }

    /// <summary>Optional associated ProductId.</summary>
    public Guid? ProductId { get; init; }

    /// <summary>Optional associated WorkPackageId.</summary>
    public Guid? WorkPackageId { get; init; }

    /// <summary>Deprecated escalation reason retained for historical/backward-compatibility.</summary>
    public string? EscalationReason { get; init; }

    /// <summary>Resolution outcome when closed (<c>COMPLETED</c>, <c>RESOLVED</c>, or <c>CANCELLED</c>).</summary>
    public string? ResolutionOutcome { get; init; }

    /// <summary>Resolution summary description when closed.</summary>
    public string? ResolutionSummary { get; init; }

    /// <summary>Identifier of the Person who resolved the request, when closed.</summary>
    public Guid? ResolvedBy { get; init; }

    /// <summary>UTC timestamp when the request was resolved, when closed.</summary>
    public DateTime? ResolvedAt { get; init; }

    /// <summary>UTC timestamp when the request was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>UTC timestamp when the request was last updated.</summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>Effective last activity timestamp (<c>ResolvedAt ?? UpdatedAt ?? CreatedAt</c>).</summary>
    public DateTime LastUpdatedAt => ResolvedAt ?? UpdatedAt ?? CreatedAt;

    /// <summary>Indicates whether the request is currently paused (<c>Status == 'PAUSED'</c>).</summary>
    public bool IsPaused => string.Equals(Status, "PAUSED", StringComparison.OrdinalIgnoreCase);

    /// <summary>Indicates whether the request is currently blocked/paused (<c>Status == 'PAUSED'</c>).</summary>
    public bool IsBlocked => IsPaused;

    /// <summary>Indicates whether the request is in an active (non-closed) lifecycle state.</summary>
    public bool IsActive =>
        string.Equals(Status, "CAPTURED", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, "ASSIGNED", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, "IN_PROGRESS", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, "PAUSED", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, "EVALUATING", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, "ACCEPTED", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, "ESCALATED", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Alias for <see cref="CustomerPortfolioRequestItemDto"/> used in workload queue contexts.
/// </summary>
public sealed record RequestWorkloadItemDto : CustomerPortfolioRequestItemDto;

/// <summary>
/// Real-time operational projection representing active request workload aggregated per person
/// and lifecycle sub-state for <c>SCR-MGT-003: Programmer Workload Review</c>
/// (Architecture §7, §8, §9, §13 — UC-MGT-004, FEAT-MGT-004).
/// </summary>
public record ProgrammerActiveWorkloadDto
{
    /// <summary>Default active request threshold at or above which a programmer is flagged as overloaded.</summary>
    public const int DefaultOverloadThreshold = 5;

    /// <summary>Unique identifier of the Person in the Organization domain.</summary>
    public Guid PersonId { get; init; }

    /// <summary>Convenience alias for <see cref="PersonId"/> matching <c>request.Requests.OwnerPersonId</c>.</summary>
    public Guid OwnerPersonId
    {
        get => PersonId;
        init => PersonId = value;
    }

    /// <summary>Authoritative full name of the Person resolved via <c>IOrganizationQueryService</c>.</summary>
    public string PersonName { get; init; } = string.Empty;

    /// <summary>Convenience alias for <see cref="PersonName"/>.</summary>
    public string FullName
    {
        get => PersonName;
        init => PersonName = value;
    }

    /// <summary>Optional email address of the Person resolved via <c>IOrganizationQueryService</c>.</summary>
    public string? Email { get; init; }

    /// <summary>Count of owned requests currently in <c>CAPTURED</c> status.</summary>
    public int CapturedCount { get; init; }

    /// <summary>Count of owned requests currently in <c>ASSIGNED</c> status.</summary>
    public int AssignedCount { get; init; }

    /// <summary>Deprecated count of owned requests currently in <c>EVALUATING</c> status.</summary>
    public int EvaluatingCount { get; init; }

    /// <summary>Deprecated count of owned requests currently in <c>ACCEPTED</c> status.</summary>
    public int AcceptedCount { get; init; }

    /// <summary>Count of owned requests currently in <c>IN_PROGRESS</c> status.</summary>
    public int InProgressCount { get; init; }

    /// <summary>Count of owned requests currently in <c>PAUSED</c> status.</summary>
    public int PausedCount { get; init; }

    /// <summary>Deprecated alias for <see cref="PausedCount"/>.</summary>
    [Obsolete("Use PausedCount instead.")]
    public int EscalatedCount
    {
        get => PausedCount;
        init => PausedCount = value;
    }

    /// <summary>Total count of active requests (<c>CAPTURED + ASSIGNED + IN_PROGRESS + PAUSED</c>).</summary>
    public int TotalActiveCount { get; init; }

    /// <summary>Convenience alias for <see cref="TotalActiveCount"/>.</summary>
    public int ActiveRequestsCount
    {
        get => TotalActiveCount;
        init => TotalActiveCount = value;
    }

    /// <summary>Count of active requests owned by the person that have not been updated for 72+ hours.</summary>
    public int StalledRequestsCount { get; init; }

    /// <summary>Indicates whether the programmer's current workload warrants a management alert badge on <c>SCR-MGT-003</c>.</summary>
    public bool IsOverloaded => TotalActiveCount >= DefaultOverloadThreshold || PausedCount > 0;

    /// <summary>Breakdown of active request counts keyed by canonical sub-state (<c>CAPTURED</c>, <c>ASSIGNED</c>, <c>IN_PROGRESS</c>, <c>PAUSED</c>).</summary>
    public IReadOnlyDictionary<string, int> SubStateCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Detailed active request queue assigned to this person for drill-down on <c>SCR-MGT-003</c>.</summary>
    public IReadOnlyList<CustomerPortfolioRequestItemDto> ActiveRequests { get; init; } =
        Array.Empty<CustomerPortfolioRequestItemDto>();
}

/// <summary>
/// Alias for <see cref="ProgrammerActiveWorkloadDto"/> representing programmer workload summary.
/// </summary>
public sealed record ProgrammerWorkloadSummaryDto : ProgrammerActiveWorkloadDto;

/// <summary>
/// Real-time operational projection representing a customer's request portfolio, including
/// active requests, open blockers (<c>PAUSED</c>), recent completions (<c>COMPLETED</c>),
/// and authoritative maintenance contract status for <c>SCR-MGT-001: Customer Progress Review</c>
/// (Architecture §7, §8, §9, §13, §15 — UC-MGT-002, FEAT-MGT-002).
/// </summary>
public record CustomerRequestPortfolioDto
{
    /// <summary>Unique identifier of the Customer organization.</summary>
    public Guid CustomerId { get; init; }

    /// <summary>Unique business code of the Customer resolved via <c>ICustomerQueryService</c>.</summary>
    public string CustomerCode { get; init; } = string.Empty;

    /// <summary>Authoritative name of the Customer resolved via <c>ICustomerQueryService</c>.</summary>
    public string CustomerName { get; init; } = string.Empty;

    /// <summary>Lifecycle status of the Customer (<c>ACTIVE</c> or <c>INACTIVE</c>).</summary>
    public string CustomerStatus { get; init; } = "ACTIVE";

    /// <summary>Authoritative maintenance contract flag from <c>ICustomerQueryService.GetCustomerWithContractStatusAsync</c>.</summary>
    public bool HasActiveMaintenanceContract { get; init; }

    /// <summary>Human-readable maintenance contract status (<c>ACTIVE</c> or <c>NONE</c>).</summary>
    public string ContractStatus { get; init; } = "NONE";

    /// <summary>Total count of active (non-closed) requests for the customer (<c>CAPTURED</c>, <c>ASSIGNED</c>, <c>IN_PROGRESS</c>, <c>PAUSED</c>).</summary>
    public int ActiveRequestsCount { get; init; }

    /// <summary>Count of paused requests in <c>PAUSED</c> status requiring attention.</summary>
    public int PausedRequestsCount { get; init; }

    /// <summary>Count of open blocker requests in <c>PAUSED</c> status requiring attention.</summary>
    public int OpenBlockersCount
    {
        get => PausedRequestsCount;
        init => PausedRequestsCount = value;
    }

    /// <summary>Convenience alias for <see cref="PausedRequestsCount"/>.</summary>
    public int BlockedRequestsCount => PausedRequestsCount;

    /// <summary>Deprecated alias for <see cref="PausedRequestsCount"/>.</summary>
    [Obsolete("Use PausedRequestsCount instead.")]
    public int EscalatedRequestsCount
    {
        get => PausedRequestsCount;
        init => PausedRequestsCount = value;
    }

    /// <summary>Count of completed (<c>COMPLETED</c>) requests for the customer.</summary>
    public int RecentCompletionsCount { get; init; }

    /// <summary>Convenience alias for <see cref="RecentCompletionsCount"/>.</summary>
    public int ResolvedRequestsCount => RecentCompletionsCount;

    /// <summary>Convenience alias for <see cref="RecentCompletionsCount"/>.</summary>
    public int CompletedRequestsCount => RecentCompletionsCount;

    /// <summary>Count of rejected/cancelled requests for the customer.</summary>
    public int RejectedRequestsCount { get; init; }

    /// <summary>Total count of all requests recorded for the customer across all statuses.</summary>
    public int TotalRequestsCount { get; init; }

    /// <summary>Active (non-closed) requests for the customer.</summary>
    public IReadOnlyList<CustomerPortfolioRequestItemDto> ActiveRequests { get; init; } =
        Array.Empty<CustomerPortfolioRequestItemDto>();

    /// <summary>Open blocker / paused requests (<c>Status = 'PAUSED'</c>) for the customer.</summary>
    public IReadOnlyList<CustomerPortfolioRequestItemDto> OpenBlockers { get; init; } =
        Array.Empty<CustomerPortfolioRequestItemDto>();

    /// <summary>Convenience alias for <see cref="OpenBlockers"/>.</summary>
    public IReadOnlyList<CustomerPortfolioRequestItemDto> BlockedRequests => OpenBlockers;

    /// <summary>Convenience alias for <see cref="OpenBlockers"/>.</summary>
    public IReadOnlyList<CustomerPortfolioRequestItemDto> PausedRequests => OpenBlockers;

    /// <summary>Recently completed requests (<c>Status = 'COMPLETED'</c>) for the customer, ordered by resolution time descending.</summary>
    public IReadOnlyList<CustomerPortfolioRequestItemDto> RecentCompletions { get; init; } =
        Array.Empty<CustomerPortfolioRequestItemDto>();

    /// <summary>Complete customer request grid items for <c>SCR-MGT-001</c>.</summary>
    public IReadOnlyList<CustomerPortfolioRequestItemDto> Requests { get; init; } =
        Array.Empty<CustomerPortfolioRequestItemDto>();
}

/// <summary>
/// Alias for <see cref="CustomerRequestPortfolioDto"/> representing customer portfolio summary.
/// </summary>
public sealed record CustomerPortfolioSummaryDto : CustomerRequestPortfolioDto;
