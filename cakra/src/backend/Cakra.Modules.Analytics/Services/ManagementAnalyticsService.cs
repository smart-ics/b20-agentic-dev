using System.Globalization;
using Cakra.Core;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Cakra.Modules.Request;
using Dapper;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cakra.Modules.Analytics.Services;

/// <summary>
/// Application service for Management Analytics (Architecture §7, §8, §9, §13, §15, §19.3, §19.7, §20, §21).
/// Marked <c>partial</c> so P7-S34 implements snapshot generation, on-demand recomputation, and snapshot queries,
/// and P7-S35 extends it with real-time dynamic workload and customer portfolio projections.
/// All persistence reads and writes use Dapper with explicit parameterized SQL; Entity Framework is never used.
/// </summary>
public partial class ManagementAnalyticsService :
    IManagementAnalyticsService,
    IRequestHandler<CaptureDailyWorkloadSnapshotCommand, IReadOnlyList<DailyWorkloadSnapshotDto>>,
    IRequestHandler<CaptureMonthlyCustomerPerformanceSnapshotCommand, IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto>>,
    IRequestHandler<RecomputeSnapshotsCommand, SnapshotRecomputationResultDto>,
    IRequestHandler<GetDailyWorkloadSnapshotsQuery, IReadOnlyList<DailyWorkloadSnapshotDto>>,
    IRequestHandler<GetMonthlyCustomerPerformanceSnapshotsQuery, IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto>>,
    IRequestHandler<GetProgrammerPerformanceQuery, ProgrammerPerformanceReportDto>
{
    /// <summary>
    /// Threshold in hours after which an un-updated active request is counted as stalled in daily snapshots.
    /// </summary>
    public const int StalledThresholdHours = 72;

    /// <summary>
    /// Default target resolution SLA threshold in hours for monthly customer performance snapshots.
    /// </summary>
    public const int DefaultSlaThresholdHours = 72;

    private const int StalledThresholdSeconds = StalledThresholdHours * 3600;
    private const int DefaultSlaThresholdSeconds = DefaultSlaThresholdHours * 3600;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IRequestQueryService _requestQueryService;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly ISystemClock? _systemClock;
    private readonly ILogger<ManagementAnalyticsService> _logger;

    public ManagementAnalyticsService(
        IDbConnectionFactory connectionFactory,
        IRequestQueryService requestQueryService,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        ISystemClock? systemClock = null,
        ILogger<ManagementAnalyticsService>? logger = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _requestQueryService = requestQueryService ?? throw new ArgumentNullException(nameof(requestQueryService));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _systemClock = systemClock;
        _logger = logger ?? NullLogger<ManagementAnalyticsService>.Instance;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DailyWorkloadSnapshotDto>> CaptureDailyWorkloadSnapshotAsync(
        DateTime snapshotDate,
        CancellationToken cancellationToken = default)
        => ComputeAndPersistDailySnapshotsAsync(snapshotDate, overwriteExisting: false, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto>> CaptureMonthlyCustomerPerformanceSnapshotAsync(
        string yearMonth,
        CancellationToken cancellationToken = default)
        => ComputeAndPersistMonthlySnapshotsAsync(yearMonth, overwriteExisting: false, cancellationToken);

    /// <inheritdoc />
    public async Task<SnapshotRecomputationResultDto> RecomputeSnapshotsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var normalizedStart = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
        var normalizedEnd = DateTime.SpecifyKind(endDate.Date, DateTimeKind.Utc);

        if (normalizedEnd < normalizedStart)
        {
            throw new ArgumentException("endDate must be greater than or equal to startDate.", nameof(endDate));
        }

        _logger.LogInformation(
            "Recomputing analytics snapshots from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
            normalizedStart,
            normalizedEnd);

        var dailyResults = new List<DailyWorkloadSnapshotDto>();
        var monthlyResults = new List<MonthlyCustomerPerformanceSnapshotDto>();
        var spannedMonths = new SortedSet<string>(StringComparer.Ordinal);
        var daysProcessed = 0;

        for (var day = normalizedStart; day <= normalizedEnd; day = day.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            spannedMonths.Add(day.ToString("yyyy-MM", CultureInfo.InvariantCulture));

            var daySnapshots = await ComputeAndPersistDailySnapshotsAsync(
                day,
                overwriteExisting: true,
                cancellationToken);

            dailyResults.AddRange(daySnapshots);
            daysProcessed++;
        }

        foreach (var yearMonth in spannedMonths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var monthSnapshots = await ComputeAndPersistMonthlySnapshotsAsync(
                yearMonth,
                overwriteExisting: true,
                cancellationToken);

            monthlyResults.AddRange(monthSnapshots);
        }

        _logger.LogInformation(
            "Completed analytics snapshot recomputation: {DaysProcessed} days ({DailyCount} rows), {MonthsProcessed} months ({MonthlyCount} rows)",
            daysProcessed,
            dailyResults.Count,
            spannedMonths.Count,
            monthlyResults.Count);

        return new SnapshotRecomputationResultDto
        {
            StartDate = normalizedStart,
            EndDate = normalizedEnd,
            DaysProcessed = daysProcessed,
            MonthsProcessed = spannedMonths.Count,
            DailySnapshotsWritten = dailyResults.Count,
            MonthlySnapshotsWritten = monthlyResults.Count,
            DailySnapshots = dailyResults,
            MonthlySnapshots = monthlyResults
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DailyWorkloadSnapshotDto>> GetDailyWorkloadSnapshotsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        Guid? personId = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                s.[SnapshotId],
                s.[SnapshotDate],
                s.[PersonId],
                s.[ActiveRequestsCount],
                s.[EscalatedRequestsCount],
                s.[StalledRequestsCount],
                s.[CompletedRequestsToday],
                s.[AvgAgeHours],
                s.[CapturedAt]
            FROM [analytics].[DailyWorkloadSnapshots] s
            WHERE (@StartDate IS NULL OR s.[SnapshotDate] >= @StartDate)
              AND (@EndDate IS NULL OR s.[SnapshotDate] <= @EndDate)
              AND (@PersonId IS NULL OR s.[PersonId] = @PersonId)
            ORDER BY s.[SnapshotDate] DESC, s.[PersonId] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<DailyWorkloadSnapshotDto>(
            new CommandDefinition(
                sql,
                new
                {
                    StartDate = startDate?.Date,
                    EndDate = endDate?.Date,
                    PersonId = personId
                },
                cancellationToken: cancellationToken))).AsList();

        if (rows.Count == 0)
        {
            return rows;
        }

        var personNames = new Dictionary<Guid, string?>();
        var enriched = new List<DailyWorkloadSnapshotDto>(rows.Count);

        foreach (var row in rows)
        {
            if (!personNames.TryGetValue(row.PersonId, out var personName))
            {
                var person = await _organizationQueryService.GetPersonByIdAsync(row.PersonId, cancellationToken);
                personName = person?.FullName;
                personNames[row.PersonId] = personName;
            }

            enriched.Add(row with { PersonName = personName });
        }

        return enriched;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto>> GetMonthlyCustomerPerformanceSnapshotsAsync(
        string? startMonth = null,
        string? endMonth = null,
        Guid? customerId = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                s.[SnapshotId],
                s.[YearMonth],
                s.[CustomerId],
                s.[TotalRequests],
                s.[ResolvedRequestsCount],
                s.[RejectedRequestsCount],
                s.[AvgResolutionHours],
                s.[SlaMetCount],
                s.[SlaBreachedCount],
                s.[CapturedAt]
            FROM [analytics].[MonthlyCustomerPerformanceSnapshots] s
            WHERE (@StartMonth IS NULL OR s.[YearMonth] >= @StartMonth)
              AND (@EndMonth IS NULL OR s.[YearMonth] <= @EndMonth)
              AND (@CustomerId IS NULL OR s.[CustomerId] = @CustomerId)
            ORDER BY s.[YearMonth] DESC, s.[CustomerId] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<MonthlyCustomerPerformanceSnapshotDto>(
            new CommandDefinition(
                sql,
                new
                {
                    StartMonth = string.IsNullOrWhiteSpace(startMonth) ? null : startMonth.Trim(),
                    EndMonth = string.IsNullOrWhiteSpace(endMonth) ? null : endMonth.Trim(),
                    CustomerId = customerId
                },
                cancellationToken: cancellationToken))).AsList();

        if (rows.Count == 0)
        {
            return rows;
        }

        var customerNames = new Dictionary<Guid, string?>();
        var enriched = new List<MonthlyCustomerPerformanceSnapshotDto>(rows.Count);

        foreach (var row in rows)
        {
            if (!customerNames.TryGetValue(row.CustomerId, out var customerName))
            {
                var customer = await _customerQueryService.GetCustomerByIdAsync(row.CustomerId, cancellationToken);
                customerName = customer?.CustomerName;
                customerNames[row.CustomerId] = customerName;
            }

            enriched.Add(row with { CustomerName = customerName });
        }

        return enriched;
    }

    /// <inheritdoc />
    public async Task<ProgrammerPerformanceReportDto> GetProgrammerPerformanceAsync(
        Guid? personId = null,
        string? startMonth = null,
        string? endMonth = null,
        CancellationToken cancellationToken = default)
    {
        DateTime? startDate = null;
        DateTime? endDate = null;

        if (!string.IsNullOrWhiteSpace(startMonth) && TryParseYearMonth(startMonth, out var startYear, out var startMon))
        {
            startDate = new DateTime(startYear, startMon, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        if (!string.IsNullOrWhiteSpace(endMonth) && TryParseYearMonth(endMonth, out var endYear, out var endMon))
        {
            endDate = new DateTime(endYear, endMon, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1).AddDays(-1);
        }

        var dailySnapshots = await GetDailyWorkloadSnapshotsAsync(startDate, endDate, personId, cancellationToken);
        var monthlyCustomerSnapshots = await GetMonthlyCustomerPerformanceSnapshotsAsync(startMonth, endMonth, null, cancellationToken);

        string? selectedPersonName = null;
        if (personId.HasValue && personId.Value != Guid.Empty)
        {
            var person = await _organizationQueryService.GetPersonByIdAsync(personId.Value, cancellationToken);
            selectedPersonName = person?.FullName;
        }

        var monthlySeries = dailySnapshots
            .GroupBy(s => new
            {
                YearMonth = s.SnapshotDate.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                s.PersonId,
                s.PersonName
            })
            .OrderByDescending(g => g.Key.YearMonth)
            .ThenBy(g => g.Key.PersonName)
            .Select(g => new ProgrammerMonthlyPerformanceItemDto
            {
                YearMonth = g.Key.YearMonth,
                PersonId = g.Key.PersonId,
                PersonName = g.Key.PersonName ?? selectedPersonName,
                CompletedRequestsCount = g.Sum(x => x.CompletedRequestsToday),
                RejectedRequestsCount = 0,
                MaxActiveRequestsCount = g.Max(x => x.ActiveRequestsCount),
                EscalatedRequestsCount = g.Max(x => x.EscalatedRequestsCount),
                AvgResolutionHours = Math.Round(g.Average(x => x.AvgAgeHours), 2)
            })
            .ToList();

        var totalCompleted = dailySnapshots.Sum(x => x.CompletedRequestsToday);
        var avgHours = dailySnapshots.Count > 0
            ? Math.Round(dailySnapshots.Average(x => x.AvgAgeHours), 2)
            : 0m;

        return new ProgrammerPerformanceReportDto
        {
            PersonId = personId,
            PersonName = selectedPersonName,
            StartMonth = startMonth,
            EndMonth = endMonth,
            TotalCompletedRequests = totalCompleted,
            TotalRejectedRequests = 0,
            AvgResolutionHours = avgHours,
            MonthlySeries = monthlySeries,
            DailyWorkloadSnapshots = dailySnapshots,
            MonthlyCustomerSnapshots = monthlyCustomerSnapshots
        };
    }

    // -------------------------------------------------------------------------
    // MediatR Request Handlers
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public Task<IReadOnlyList<DailyWorkloadSnapshotDto>> Handle(
        CaptureDailyWorkloadSnapshotCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CaptureDailyWorkloadSnapshotAsync(request.SnapshotDate, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto>> Handle(
        CaptureMonthlyCustomerPerformanceSnapshotCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CaptureMonthlyCustomerPerformanceSnapshotAsync(request.YearMonth, cancellationToken);
    }

    /// <inheritdoc />
    public Task<SnapshotRecomputationResultDto> Handle(
        RecomputeSnapshotsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RecomputeSnapshotsAsync(request.StartDate, request.EndDate, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DailyWorkloadSnapshotDto>> Handle(
        GetDailyWorkloadSnapshotsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetDailyWorkloadSnapshotsAsync(request.StartDate, request.EndDate, request.PersonId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto>> Handle(
        GetMonthlyCustomerPerformanceSnapshotsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetMonthlyCustomerPerformanceSnapshotsAsync(request.StartMonth, request.EndMonth, request.CustomerId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ProgrammerPerformanceReportDto> Handle(
        GetProgrammerPerformanceQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetProgrammerPerformanceAsync(request.PersonId, request.StartMonth, request.EndMonth, cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Core Dapper Snapshot Computation & Persistence
    // -------------------------------------------------------------------------

    private async Task<IReadOnlyList<DailyWorkloadSnapshotDto>> ComputeAndPersistDailySnapshotsAsync(
        DateTime snapshotDate,
        bool overwriteExisting,
        CancellationToken cancellationToken)
    {
        if (snapshotDate == default)
        {
            throw new ArgumentException("snapshotDate must be a valid date.", nameof(snapshotDate));
        }

        var targetDate = DateTime.SpecifyKind(snapshotDate.Date, DateTimeKind.Utc);
        var dayStartUtc = targetDate;
        var nextDayStartUtc = dayStartUtc.AddDays(1);
        var endOfDayUtc = nextDayStartUtc.AddTicks(-1);
        var capturedAtUtc = _systemClock?.UtcNow ?? DateTime.UtcNow;
        var referenceUtc = targetDate == capturedAtUtc.Date ? capturedAtUtc : endOfDayUtc;

        // Read active persons via published OrganizationQueryService (Architecture §15, §20)
        var activePersons = await _organizationQueryService.ListActivePersonsAsync(cancellationToken);
        if (activePersons.Count == 0)
        {
            _logger.LogInformation(
                "No active persons found for daily workload snapshot on {SnapshotDate:yyyy-MM-dd}",
                targetDate);
            return Array.Empty<DailyWorkloadSnapshotDto>();
        }

        const string selectExistingSql = """
            SELECT
                [SnapshotId],
                [SnapshotDate],
                [PersonId],
                [ActiveRequestsCount],
                [EscalatedRequestsCount],
                [StalledRequestsCount],
                [CompletedRequestsToday],
                [AvgAgeHours],
                [CapturedAt]
            FROM [analytics].[DailyWorkloadSnapshots]
            WHERE [SnapshotDate] = @SnapshotDate
              AND [PersonId] = @PersonId;
            """;

        const string computeMetricsSql = """
            SELECT
                COUNT(CASE
                    WHEN r.[Status] IN ('CAPTURED', 'EVALUATING', 'ACCEPTED', 'IN_PROGRESS', 'ESCALATED')
                         OR (
                             r.[Status] IN ('COMPLETED', 'REJECTED')
                             AND COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt]) >= @NextDayStartUtc
                         )
                    THEN 1
                END) AS [ActiveRequestsCount],
                COUNT(CASE
                    WHEN r.[Status] = 'ESCALATED'
                    THEN 1
                END) AS [EscalatedRequestsCount],
                COUNT(CASE
                    WHEN (
                             r.[Status] IN ('CAPTURED', 'EVALUATING', 'ACCEPTED', 'IN_PROGRESS', 'ESCALATED')
                             OR (
                                 r.[Status] IN ('COMPLETED', 'REJECTED')
                                 AND COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt]) >= @NextDayStartUtc
                             )
                         )
                         AND DATEDIFF(SECOND, COALESCE(r.[UpdatedAt], r.[CreatedAt]), @ReferenceUtc) >= @StalledThresholdSeconds
                    THEN 1
                END) AS [StalledRequestsCount],
                COUNT(CASE
                    WHEN r.[Status] = 'COMPLETED'
                         AND COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt]) >= @DayStartUtc
                         AND COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt]) < @NextDayStartUtc
                    THEN 1
                END) AS [CompletedRequestsToday],
                CAST(COALESCE(AVG(CASE
                    WHEN r.[Status] IN ('CAPTURED', 'EVALUATING', 'ACCEPTED', 'IN_PROGRESS', 'ESCALATED')
                         OR (
                             r.[Status] IN ('COMPLETED', 'REJECTED')
                             AND COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt]) >= @NextDayStartUtc
                         )
                    THEN CASE
                        WHEN @ReferenceUtc > r.[CreatedAt]
                        THEN DATEDIFF(SECOND, r.[CreatedAt], @ReferenceUtc) / 3600.0
                        ELSE 0.0
                    END
                END), 0.0) AS DECIMAL(10, 2)) AS [AvgAgeHours]
            FROM [request].[Requests] r
            LEFT JOIN [request].[RequestResolutions] res ON res.[RequestId] = r.[Id]
            WHERE r.[OwnerPersonId] = @PersonId
              AND r.[CreatedAt] < @NextDayStartUtc;
            """;

        const string deleteExistingSql = """
            DELETE FROM [analytics].[DailyWorkloadSnapshots]
            WHERE [SnapshotDate] = @SnapshotDate
              AND [PersonId] = @PersonId;
            """;

        const string insertSnapshotSql = """
            INSERT INTO [analytics].[DailyWorkloadSnapshots] (
                [SnapshotId],
                [SnapshotDate],
                [PersonId],
                [ActiveRequestsCount],
                [EscalatedRequestsCount],
                [StalledRequestsCount],
                [CompletedRequestsToday],
                [AvgAgeHours],
                [CapturedAt]
            )
            VALUES (
                @SnapshotId,
                @SnapshotDate,
                @PersonId,
                @ActiveRequestsCount,
                @EscalatedRequestsCount,
                @StalledRequestsCount,
                @CompletedRequestsToday,
                @AvgAgeHours,
                @CapturedAt
            );
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = new List<DailyWorkloadSnapshotDto>(activePersons.Count);

        foreach (var person in activePersons)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!overwriteExisting)
            {
                // Architecture §13 — Immutability: Once recorded, snapshot records are treated as immutable historical facts.
                var existing = await connection.QuerySingleOrDefaultAsync<DailyWorkloadSnapshotDto>(
                    new CommandDefinition(
                        selectExistingSql,
                        new { SnapshotDate = targetDate, PersonId = person.Id },
                        cancellationToken: cancellationToken));

                if (existing is not null)
                {
                    results.Add(existing with { PersonName = person.FullName });
                    continue;
                }
            }
            else
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        deleteExistingSql,
                        new { SnapshotDate = targetDate, PersonId = person.Id },
                        cancellationToken: cancellationToken));
            }

            var metrics = await connection.QuerySingleAsync<DailyMetricsRow>(
                new CommandDefinition(
                    computeMetricsSql,
                    new
                    {
                        PersonId = person.Id,
                        DayStartUtc = dayStartUtc,
                        NextDayStartUtc = nextDayStartUtc,
                        ReferenceUtc = referenceUtc,
                        StalledThresholdSeconds
                    },
                    cancellationToken: cancellationToken));

            var snapshot = new DailyWorkloadSnapshotDto
            {
                SnapshotId = Guid.NewGuid(),
                SnapshotDate = targetDate,
                PersonId = person.Id,
                PersonName = person.FullName,
                ActiveRequestsCount = metrics.ActiveRequestsCount,
                EscalatedRequestsCount = metrics.EscalatedRequestsCount,
                StalledRequestsCount = metrics.StalledRequestsCount,
                CompletedRequestsToday = metrics.CompletedRequestsToday,
                AvgAgeHours = metrics.AvgAgeHours,
                CapturedAt = capturedAtUtc
            };

            await connection.ExecuteAsync(
                new CommandDefinition(
                    insertSnapshotSql,
                    new
                    {
                        snapshot.SnapshotId,
                        snapshot.SnapshotDate,
                        snapshot.PersonId,
                        snapshot.ActiveRequestsCount,
                        snapshot.EscalatedRequestsCount,
                        snapshot.StalledRequestsCount,
                        snapshot.CompletedRequestsToday,
                        snapshot.AvgAgeHours,
                        snapshot.CapturedAt
                    },
                    cancellationToken: cancellationToken));

            results.Add(snapshot);
        }

        _logger.LogInformation(
            "Captured {SnapshotCount} daily workload snapshots for {SnapshotDate:yyyy-MM-dd} (Overwrite={Overwrite})",
            results.Count,
            targetDate,
            overwriteExisting);

        return results;
    }

    private async Task<IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto>> ComputeAndPersistMonthlySnapshotsAsync(
        string yearMonth,
        bool overwriteExisting,
        CancellationToken cancellationToken)
    {
        if (!TryParseYearMonth(yearMonth, out var year, out var month))
        {
            throw new ArgumentException("yearMonth must be in 'YYYY-MM' format (e.g. '2026-09').", nameof(yearMonth));
        }

        var normalizedYearMonth = $"{year:D4}-{month:D2}";
        var monthStartUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthStartUtc = monthStartUtc.AddMonths(1);
        var capturedAtUtc = _systemClock?.UtcNow ?? DateTime.UtcNow;

        // Read active customers via published CustomerQueryService (Architecture §15, §20)
        var customers = await _customerQueryService.ListActiveCustomersAsync(cancellationToken);
        if (customers.Count == 0)
        {
            _logger.LogInformation(
                "No active customers found for monthly performance snapshot {YearMonth}",
                normalizedYearMonth);
            return Array.Empty<MonthlyCustomerPerformanceSnapshotDto>();
        }

        const string selectExistingSql = """
            SELECT
                [SnapshotId],
                [YearMonth],
                [CustomerId],
                [TotalRequests],
                [ResolvedRequestsCount],
                [RejectedRequestsCount],
                [AvgResolutionHours],
                [SlaMetCount],
                [SlaBreachedCount],
                [CapturedAt]
            FROM [analytics].[MonthlyCustomerPerformanceSnapshots]
            WHERE [YearMonth] = @YearMonth
              AND [CustomerId] = @CustomerId;
            """;

        const string computeMonthlySql = """
            SELECT
                COUNT(1) AS [TotalRequests],
                COUNT(CASE
                    WHEN r.[Status] = 'COMPLETED'
                         AND COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt]) >= @MonthStartUtc
                         AND COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt]) < @NextMonthStartUtc
                    THEN 1
                END) AS [ResolvedRequestsCount],
                COUNT(CASE
                    WHEN r.[Status] = 'REJECTED'
                         AND COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt]) >= @MonthStartUtc
                         AND COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt]) < @NextMonthStartUtc
                    THEN 1
                END) AS [RejectedRequestsCount],
                CAST(COALESCE(AVG(CASE
                    WHEN r.[Status] = 'COMPLETED'
                         AND COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt]) >= @MonthStartUtc
                         AND COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt]) < @NextMonthStartUtc
                    THEN CASE
                        WHEN COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt]) > r.[CreatedAt]
                        THEN DATEDIFF(SECOND, r.[CreatedAt], COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt])) / 3600.0
                        ELSE 0.0
                    END
                END), 0.0) AS DECIMAL(10, 2)) AS [AvgResolutionHours],
                COUNT(CASE
                    WHEN r.[Status] = 'COMPLETED'
                         AND COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt]) >= @MonthStartUtc
                         AND COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt]) < @NextMonthStartUtc
                         AND DATEDIFF(SECOND, r.[CreatedAt], COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt])) <= @SlaThresholdSeconds
                    THEN 1
                END) AS [SlaMetCount],
                COUNT(CASE
                    WHEN r.[Status] = 'COMPLETED'
                         AND COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt]) >= @MonthStartUtc
                         AND COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt]) < @NextMonthStartUtc
                         AND DATEDIFF(SECOND, r.[CreatedAt], COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt])) > @SlaThresholdSeconds
                    THEN 1
                END) AS [SlaBreachedCount]
            FROM [request].[Requests] r
            LEFT JOIN [request].[RequestResolutions] res ON res.[RequestId] = r.[Id]
            WHERE r.[CustomerId] = @CustomerId
              AND (
                  (r.[CreatedAt] >= @MonthStartUtc AND r.[CreatedAt] < @NextMonthStartUtc)
                  OR (res.[ResolvedAt] >= @MonthStartUtc AND res.[ResolvedAt] < @NextMonthStartUtc)
              );
            """;

        const string deleteExistingSql = """
            DELETE FROM [analytics].[MonthlyCustomerPerformanceSnapshots]
            WHERE [YearMonth] = @YearMonth
              AND [CustomerId] = @CustomerId;
            """;

        const string insertMonthlySql = """
            INSERT INTO [analytics].[MonthlyCustomerPerformanceSnapshots] (
                [SnapshotId],
                [YearMonth],
                [CustomerId],
                [TotalRequests],
                [ResolvedRequestsCount],
                [RejectedRequestsCount],
                [AvgResolutionHours],
                [SlaMetCount],
                [SlaBreachedCount],
                [CapturedAt]
            )
            VALUES (
                @SnapshotId,
                @YearMonth,
                @CustomerId,
                @TotalRequests,
                @ResolvedRequestsCount,
                @RejectedRequestsCount,
                @AvgResolutionHours,
                @SlaMetCount,
                @SlaBreachedCount,
                @CapturedAt
            );
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = new List<MonthlyCustomerPerformanceSnapshotDto>(customers.Count);

        foreach (var customer in customers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!overwriteExisting)
            {
                // Architecture §13 — Immutability: Once recorded, snapshot records are treated as immutable historical facts.
                var existing = await connection.QuerySingleOrDefaultAsync<MonthlyCustomerPerformanceSnapshotDto>(
                    new CommandDefinition(
                        selectExistingSql,
                        new { YearMonth = normalizedYearMonth, CustomerId = customer.Id },
                        cancellationToken: cancellationToken));

                if (existing is not null)
                {
                    results.Add(existing with { CustomerName = customer.CustomerName });
                    continue;
                }
            }
            else
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        deleteExistingSql,
                        new { YearMonth = normalizedYearMonth, CustomerId = customer.Id },
                        cancellationToken: cancellationToken));
            }

            var metrics = await connection.QuerySingleAsync<MonthlyMetricsRow>(
                new CommandDefinition(
                    computeMonthlySql,
                    new
                    {
                        CustomerId = customer.Id,
                        MonthStartUtc = monthStartUtc,
                        NextMonthStartUtc = nextMonthStartUtc,
                        SlaThresholdSeconds = DefaultSlaThresholdSeconds
                    },
                    cancellationToken: cancellationToken));

            var snapshot = new MonthlyCustomerPerformanceSnapshotDto
            {
                SnapshotId = Guid.NewGuid(),
                YearMonth = normalizedYearMonth,
                CustomerId = customer.Id,
                CustomerName = customer.CustomerName,
                TotalRequests = metrics.TotalRequests,
                ResolvedRequestsCount = metrics.ResolvedRequestsCount,
                RejectedRequestsCount = metrics.RejectedRequestsCount,
                AvgResolutionHours = metrics.AvgResolutionHours,
                SlaMetCount = metrics.SlaMetCount,
                SlaBreachedCount = metrics.SlaBreachedCount,
                CapturedAt = capturedAtUtc
            };

            await connection.ExecuteAsync(
                new CommandDefinition(
                    insertMonthlySql,
                    new
                    {
                        snapshot.SnapshotId,
                        snapshot.YearMonth,
                        snapshot.CustomerId,
                        snapshot.TotalRequests,
                        snapshot.ResolvedRequestsCount,
                        snapshot.RejectedRequestsCount,
                        snapshot.AvgResolutionHours,
                        snapshot.SlaMetCount,
                        snapshot.SlaBreachedCount,
                        snapshot.CapturedAt
                    },
                    cancellationToken: cancellationToken));

            results.Add(snapshot);
        }

        _logger.LogInformation(
            "Captured {SnapshotCount} monthly customer performance snapshots for {YearMonth} (Overwrite={Overwrite})",
            results.Count,
            normalizedYearMonth,
            overwriteExisting);

        return results;
    }

    private static bool TryParseYearMonth(string? yearMonth, out int year, out int month)
    {
        year = 0;
        month = 0;

        if (string.IsNullOrWhiteSpace(yearMonth))
        {
            return false;
        }

        var trimmed = yearMonth.Trim();
        if (trimmed.Length != 7 || trimmed[4] != '-')
        {
            return false;
        }

        return int.TryParse(trimmed.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out year)
            && int.TryParse(trimmed.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out month)
            && year is >= 1900 and <= 9999
            && month is >= 1 and <= 12;
    }

    private sealed class DailyMetricsRow
    {
        public int ActiveRequestsCount { get; init; }
        public int EscalatedRequestsCount { get; init; }
        public int StalledRequestsCount { get; init; }
        public int CompletedRequestsToday { get; init; }
        public decimal AvgAgeHours { get; init; }
    }

    private sealed class MonthlyMetricsRow
    {
        public int TotalRequests { get; init; }
        public int ResolvedRequestsCount { get; init; }
        public int RejectedRequestsCount { get; init; }
        public decimal AvgResolutionHours { get; init; }
        public int SlaMetCount { get; init; }
        public int SlaBreachedCount { get; init; }
    }
}
