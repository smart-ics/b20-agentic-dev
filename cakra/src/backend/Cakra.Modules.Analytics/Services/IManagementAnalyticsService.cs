namespace Cakra.Modules.Analytics.Services;

/// <summary>
/// Published application service contract for Management Analytics (Architecture §7, §8, §9, §13, §15, §19.7).
/// Marked <c>partial</c> so P7-S34 defines snapshot capture, on-demand recomputation, and snapshot queries,
/// and P7-S35 extends it with real-time dynamic workload and customer portfolio queries.
/// </summary>
public partial interface IManagementAnalyticsService
{
    /// <summary>
    /// Captures end-of-day workload snapshot records into <c>analytics.DailyWorkloadSnapshots</c>
    /// for all active <c>Person</c> records on the specified <paramref name="snapshotDate"/>
    /// using Dapper parameterized SQL (Architecture §13, §19.7).
    /// Existing snapshot records for <c>(SnapshotDate, PersonId)</c> are preserved as immutable
    /// historical facts unless recomputed via <see cref="RecomputeSnapshotsAsync"/>.
    /// </summary>
    Task<IReadOnlyList<DailyWorkloadSnapshotDto>> CaptureDailyWorkloadSnapshotAsync(
        DateTime snapshotDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="CaptureDailyWorkloadSnapshotAsync"/>.
    /// </summary>
    Task<IReadOnlyList<DailyWorkloadSnapshotDto>> CaptureDailyWorkloadSnapshot(
        DateTime snapshotDate,
        CancellationToken cancellationToken = default)
        => CaptureDailyWorkloadSnapshotAsync(snapshotDate, cancellationToken);

    /// <summary>
    /// Captures monthly customer performance snapshot records into
    /// <c>analytics.MonthlyCustomerPerformanceSnapshots</c> for all active <c>Customer</c> records
    /// for the specified <paramref name="yearMonth"/> (<c>YYYY-MM</c>) using Dapper parameterized SQL
    /// (Architecture §13, §19.7).
    /// Existing snapshot records for <c>(YearMonth, CustomerId)</c> are preserved as immutable
    /// historical facts unless recomputed via <see cref="RecomputeSnapshotsAsync"/>.
    /// </summary>
    Task<IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto>> CaptureMonthlyCustomerPerformanceSnapshotAsync(
        string yearMonth,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="CaptureMonthlyCustomerPerformanceSnapshotAsync(string, CancellationToken)"/>.
    /// </summary>
    Task<IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto>> CaptureMonthlyCustomerPerformanceSnapshot(
        string yearMonth,
        CancellationToken cancellationToken = default)
        => CaptureMonthlyCustomerPerformanceSnapshotAsync(yearMonth, cancellationToken);

    /// <summary>
    /// Convenience overload capturing the monthly customer performance snapshot for the calendar month
    /// of <paramref name="monthDate"/> (<c>yyyy-MM</c>).
    /// </summary>
    Task<IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto>> CaptureMonthlyCustomerPerformanceSnapshotAsync(
        DateTime monthDate,
        CancellationToken cancellationToken = default)
        => CaptureMonthlyCustomerPerformanceSnapshotAsync(monthDate.ToString("yyyy-MM"), cancellationToken);

    /// <summary>
    /// Idempotently backfills and recomputes <c>analytics.DailyWorkloadSnapshots</c> and
    /// <c>analytics.MonthlyCustomerPerformanceSnapshots</c> across the inclusive date range
    /// <c>[startDate, endDate]</c> using Dapper parameterized SQL (Architecture §13 — On-Demand Recomputation).
    /// </summary>
    Task<SnapshotRecomputationResultDto> RecomputeSnapshotsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="RecomputeSnapshotsAsync"/> (Architecture §13).
    /// </summary>
    Task<SnapshotRecomputationResultDto> RecomputeSnapshots(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
        => RecomputeSnapshotsAsync(startDate, endDate, cancellationToken);

    /// <summary>
    /// Queries <c>analytics.DailyWorkloadSnapshots</c> filtered by optional date range and <c>PersonId</c>
    /// using Dapper parameterized SQL (Architecture §13).
    /// </summary>
    Task<IReadOnlyList<DailyWorkloadSnapshotDto>> GetDailyWorkloadSnapshotsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        Guid? personId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="GetDailyWorkloadSnapshotsAsync"/>.
    /// </summary>
    Task<IReadOnlyList<DailyWorkloadSnapshotDto>> GetDailyWorkloadSnapshots(
        DateTime? startDate = null,
        DateTime? endDate = null,
        Guid? personId = null,
        CancellationToken cancellationToken = default)
        => GetDailyWorkloadSnapshotsAsync(startDate, endDate, personId, cancellationToken);

    /// <summary>
    /// Queries <c>analytics.MonthlyCustomerPerformanceSnapshots</c> filtered by optional month range
    /// (<c>YYYY-MM</c>) and <c>CustomerId</c> using Dapper parameterized SQL (Architecture §13).
    /// </summary>
    Task<IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto>> GetMonthlyCustomerPerformanceSnapshotsAsync(
        string? startMonth = null,
        string? endMonth = null,
        Guid? customerId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="GetMonthlyCustomerPerformanceSnapshotsAsync"/>.
    /// </summary>
    Task<IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto>> GetMonthlyCustomerPerformanceSnapshots(
        string? startMonth = null,
        string? endMonth = null,
        Guid? customerId = null,
        CancellationToken cancellationToken = default)
        => GetMonthlyCustomerPerformanceSnapshotsAsync(startMonth, endMonth, customerId, cancellationToken);

    /// <summary>
    /// Queries historical programmer performance metrics across <c>analytics.DailyWorkloadSnapshots</c>
    /// and <c>analytics.MonthlyCustomerPerformanceSnapshots</c> for <c>SCR-MGT-002</c>
    /// (Architecture §7, §8, §9, §13 — UC-MGT-003, FEAT-MGT-003).
    /// </summary>
    Task<ProgrammerPerformanceReportDto> GetProgrammerPerformanceAsync(
        Guid? personId = null,
        string? startMonth = null,
        string? endMonth = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="GetProgrammerPerformanceAsync"/>.
    /// </summary>
    Task<ProgrammerPerformanceReportDto> GetProgrammerPerformance(
        Guid? personId = null,
        string? startMonth = null,
        string? endMonth = null,
        CancellationToken cancellationToken = default)
        => GetProgrammerPerformanceAsync(personId, startMonth, endMonth, cancellationToken);
}
