namespace Cakra.Modules.Analytics;

/// <summary>
/// Read-only projection representing a row in <c>analytics.DailyWorkloadSnapshots</c>
/// (Architecture §6, §7, §13, §17).
///Once recorded, snapshot records are treated as immutable historical facts unless
/// explicitly recomputed via <c>ManagementAnalyticsService.RecomputeSnapshots</c>.
/// </summary>
public sealed record DailyWorkloadSnapshotDto
{
    /// <summary>Unique identifier of the snapshot row (<c>SnapshotId</c> primary key).</summary>
    public Guid SnapshotId { get; init; }

    /// <summary>Convenience alias for <see cref="SnapshotId"/>.</summary>
    public Guid Id
    {
        get => SnapshotId;
        init => SnapshotId = value;
    }

    /// <summary>Calendar date (UTC date component) of the end-of-day workload snapshot.</summary>
    public DateTime SnapshotDate { get; init; }

    /// <summary>Identifier of the organizational Person whose workload was captured.</summary>
    public Guid PersonId { get; init; }

    /// <summary>Optional resolved display name of the Person from OrganizationQueryService.</summary>
    public string? PersonName { get; init; }

    /// <summary>Count of active (non-closed) requests owned by the person as of the snapshot date.</summary>
    public int ActiveRequestsCount { get; init; }

    /// <summary>Count of requests owned by the person in <c>PAUSED</c> status as of the snapshot date.</summary>
    public int PausedRequestsCount { get; init; }

    /// <summary>Deprecated alias for <see cref="PausedRequestsCount"/>.</summary>
    [Obsolete("Use PausedRequestsCount instead.")]
    public int EscalatedRequestsCount
    {
        get => PausedRequestsCount;
        init => PausedRequestsCount = value;
    }

    /// <summary>Count of active requests owned by the person that have had no update for 72+ hours.</summary>
    public int StalledRequestsCount { get; init; }

    /// <summary>Count of requests owned by the person that transitioned to <c>COMPLETED</c> on the snapshot date.</summary>
    public int CompletedRequestsToday { get; init; }

    /// <summary>Average age in hours of active requests owned by the person as of the snapshot capture time.</summary>
    public decimal AvgAgeHours { get; init; }

    /// <summary>UTC timestamp when the snapshot row was captured.</summary>
    public DateTime CapturedAt { get; init; }
}

/// <summary>
/// Read-only projection representing a row in <c>analytics.MonthlyCustomerPerformanceSnapshots</c>
/// (Architecture §6, §7, §13, §17).
/// </summary>
public sealed record MonthlyCustomerPerformanceSnapshotDto
{
    /// <summary>Unique identifier of the snapshot row (<c>SnapshotId</c> primary key).</summary>
    public Guid SnapshotId { get; init; }

    /// <summary>Convenience alias for <see cref="SnapshotId"/>.</summary>
    public Guid Id
    {
        get => SnapshotId;
        init => SnapshotId = value;
    }

    /// <summary>Calendar year-month in <c>YYYY-MM</c> format (e.g. <c>2026-09</c>).</summary>
    public string YearMonth { get; init; } = string.Empty;

    /// <summary>Identifier of the Customer organization.</summary>
    public Guid CustomerId { get; init; }

    /// <summary>Optional resolved display name of the Customer from CustomerQueryService.</summary>
    public string? CustomerName { get; init; }

    /// <summary>Total requests created or resolved for the customer during the calendar month.</summary>
    public int TotalRequests { get; init; }

    /// <summary>Count of requests resolved (<c>COMPLETED</c>) for the customer during the calendar month.</summary>
    public int ResolvedRequestsCount { get; init; }

    /// <summary>Count of requests rejected (<c>REJECTED</c>) for the customer during the calendar month.</summary>
    public int RejectedRequestsCount { get; init; }

    /// <summary>Average resolution turnaround time in hours for requests completed during the calendar month.</summary>
    public decimal AvgResolutionHours { get; init; }

    /// <summary>Count of completed requests resolved within the target SLA threshold (72 hours).</summary>
    public int SlaMetCount { get; init; }

    /// <summary>Count of completed requests whose resolution time exceeded the target SLA threshold (72 hours).</summary>
    public int SlaBreachedCount { get; init; }

    /// <summary>UTC timestamp when the snapshot row was captured.</summary>
    public DateTime CapturedAt { get; init; }
}

/// <summary>
/// Summary result returned by <c>ManagementAnalyticsService.RecomputeSnapshots</c>
/// (Architecture §13 — On-Demand Recomputation).
/// </summary>
public sealed record SnapshotRecomputationResultDto
{
    /// <summary>Inclusive start date of the recomputed range (UTC date).</summary>
    public DateTime StartDate { get; init; }

    /// <summary>Inclusive end date of the recomputed range (UTC date).</summary>
    public DateTime EndDate { get; init; }

    /// <summary>Number of calendar days processed for daily workload snapshots.</summary>
    public int DaysProcessed { get; init; }

    /// <summary>Number of distinct <c>YYYY-MM</c> calendar months processed for monthly customer snapshots.</summary>
    public int MonthsProcessed { get; init; }

    /// <summary>Total <c>DailyWorkloadSnapshots</c> rows written during recomputation.</summary>
    public int DailySnapshotsWritten { get; init; }

    /// <summary>Total <c>MonthlyCustomerPerformanceSnapshots</c> rows written during recomputation.</summary>
    public int MonthlySnapshotsWritten { get; init; }

    /// <summary>Recomputed daily workload snapshot records across the date range.</summary>
    public IReadOnlyList<DailyWorkloadSnapshotDto> DailySnapshots { get; init; } = Array.Empty<DailyWorkloadSnapshotDto>();

    /// <summary>Recomputed monthly customer performance snapshot records across the spanned months.</summary>
    public IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto> MonthlySnapshots { get; init; } = Array.Empty<MonthlyCustomerPerformanceSnapshotDto>();
}

/// <summary>
/// Monthly aggregated performance row for a programmer across <c>DailyWorkloadSnapshots</c>
/// and closed <c>Requests</c> (supports <c>SCR-MGT-002</c>, FEAT-MGT-003, UC-MGT-003).
/// </summary>
public sealed record ProgrammerMonthlyPerformanceItemDto
{
    /// <summary>Calendar month in <c>YYYY-MM</c> format.</summary>
    public string YearMonth { get; init; } = string.Empty;

    /// <summary>Identifier of the programmer/implementator.</summary>
    public Guid PersonId { get; init; }

    /// <summary>Resolved full name of the programmer/implementator.</summary>
    public string? PersonName { get; init; }

    /// <summary>Total requests completed in the month (from daily snapshots / closed requests).</summary>
    public int CompletedRequestsCount { get; init; }

    /// <summary>Total requests rejected in the month.</summary>
    public int RejectedRequestsCount { get; init; }

    /// <summary>Peak or latest active request count observed in daily snapshots during the month.</summary>
    public int MaxActiveRequestsCount { get; init; }

    /// <summary>Total paused request snapshot observations during the month.</summary>
    public int PausedRequestsCount { get; init; }

    /// <summary>Deprecated alias for <see cref="PausedRequestsCount"/>.</summary>
    [Obsolete("Use PausedRequestsCount instead.")]
    public int EscalatedRequestsCount
    {
        get => PausedRequestsCount;
        init => PausedRequestsCount = value;
    }

    /// <summary>Average request age/resolution turnaround in hours during the month.</summary>
    public decimal AvgResolutionHours { get; init; }
}

/// <summary>
/// Historical programmer performance projection combining <c>DailyWorkloadSnapshots</c>,
/// <c>MonthlyCustomerPerformanceSnapshots</c>, and monthly aggregations for <c>SCR-MGT-002</c>
/// (Architecture §7, §8, §9, §13 — FEAT-MGT-003, UC-MGT-003).
/// </summary>
public sealed record ProgrammerPerformanceReportDto
{
    /// <summary>Optional filtered PersonId.</summary>
    public Guid? PersonId { get; init; }

    /// <summary>Optional resolved full name of the selected person.</summary>
    public string? PersonName { get; init; }

    /// <summary>Optional start month filter (<c>YYYY-MM</c>).</summary>
    public string? StartMonth { get; init; }

    /// <summary>Optional end month filter (<c>YYYY-MM</c>).</summary>
    public string? EndMonth { get; init; }

    /// <summary>Total completed requests across the selected period.</summary>
    public int TotalCompletedRequests { get; init; }

    /// <summary>Total rejected requests across the selected period.</summary>
    public int TotalRejectedRequests { get; init; }

    /// <summary>Average resolution turnaround time in hours across the selected period.</summary>
    public decimal AvgResolutionHours { get; init; }

    /// <summary>Monthly performance summary rows.</summary>
    public IReadOnlyList<ProgrammerMonthlyPerformanceItemDto> MonthlySeries { get; init; } = Array.Empty<ProgrammerMonthlyPerformanceItemDto>();

    /// <summary>Underlying daily workload snapshots matching the filter.</summary>
    public IReadOnlyList<DailyWorkloadSnapshotDto> DailyWorkloadSnapshots { get; init; } = Array.Empty<DailyWorkloadSnapshotDto>();

    /// <summary>Underlying monthly customer performance snapshots matching the month range.</summary>
    public IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto> MonthlyCustomerSnapshots { get; init; } = Array.Empty<MonthlyCustomerPerformanceSnapshotDto>();
}
