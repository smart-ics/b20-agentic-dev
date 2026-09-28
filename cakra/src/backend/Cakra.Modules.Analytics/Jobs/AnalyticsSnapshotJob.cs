using System.Globalization;
using Cakra.Core;
using Cakra.Modules.Analytics.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cakra.Modules.Analytics;

/// <summary>
/// Scheduled background worker implemented as an ASP.NET Core <see cref="BackgroundService"/>
/// (<see cref="IHostedService"/>) using <see cref="System.Threading.Timer"/> for scheduling
/// (Architecture §7, §13, §15, §19.7):
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Daily Snapshot (23:59:59 UTC)</b>: Computes end-of-day workload per active <c>Person</c>
///       from <c>request.Requests</c> using Dapper parameterized SQL and inserts into
///       <c>analytics.DailyWorkloadSnapshots</c>.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Monthly Snapshot (1st of month, 00:05:00 UTC)</b>: Aggregates preceding calendar month
///       metrics per <c>Customer</c> using Dapper parameterized SQL and inserts into
///       <c>analytics.MonthlyCustomerPerformanceSnapshots</c>.
///     </description>
///   </item>
/// </list>
/// </summary>
public sealed class AnalyticsSnapshotJob : BackgroundService
{
    private static readonly TimeSpan MaxTimerDueTime = TimeSpan.FromDays(45);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISystemClock? _systemClock;
    private readonly ILogger<AnalyticsSnapshotJob> _logger;
    private readonly object _timerLock = new();

    private Timer? _dailyTimer;
    private Timer? _monthlyTimer;
    private CancellationToken _stoppingToken;

    public AnalyticsSnapshotJob(
        IServiceScopeFactory scopeFactory,
        ISystemClock? systemClock = null,
        ILogger<AnalyticsSnapshotJob>? logger = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _systemClock = systemClock;
        _logger = logger ?? NullLogger<AnalyticsSnapshotJob>.Instance;
    }

    /// <summary>
    /// Computes the delay from <paramref name="utcNow"/> until the next daily snapshot execution
    /// at <c>23:59:59</c> UTC (Architecture §13, §19.7).
    /// </summary>
    public static TimeSpan CalculateDelayUntilNextDailyRun(DateTime utcNow)
    {
        var nextRun = new DateTime(
            utcNow.Year,
            utcNow.Month,
            utcNow.Day,
            23,
            59,
            59,
            DateTimeKind.Utc);

        if (utcNow >= nextRun)
        {
            nextRun = nextRun.AddDays(1);
        }

        var delay = nextRun - utcNow;
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
    }

    /// <summary>
    /// Computes the delay from <paramref name="utcNow"/> until the next monthly snapshot execution
    /// on the 1st of the month at <c>00:05:00</c> UTC (Architecture §13, §19.7).
    /// </summary>
    public static TimeSpan CalculateDelayUntilNextMonthlyRun(DateTime utcNow)
    {
        var nextRun = new DateTime(
            utcNow.Year,
            utcNow.Month,
            1,
            0,
            5,
            0,
            DateTimeKind.Utc);

        if (utcNow >= nextRun)
        {
            nextRun = nextRun.AddMonths(1);
        }

        var delay = nextRun - utcNow;
        if (delay < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return delay > MaxTimerDueTime ? MaxTimerDueTime : delay;
    }

    /// <summary>
    /// Returns the preceding calendar month in <c>YYYY-MM</c> format relative to <paramref name="referenceDateUtc"/>.
    /// </summary>
    public static string GetPrecedingYearMonth(DateTime referenceDateUtc)
    {
        var firstOfCurrentMonth = new DateTime(
            referenceDateUtc.Year,
            referenceDateUtc.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        return firstOfCurrentMonth.AddMonths(-1).ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Triggers the daily workload snapshot computation and persistence for the specified
    /// <paramref name="snapshotDate"/> (or today's UTC date when <c>null</c>).
    /// </summary>
    public async Task<IReadOnlyList<DailyWorkloadSnapshotDto>> TriggerDailySnapshotAsync(
        DateTime? snapshotDate = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveDate = (snapshotDate ?? GetUtcNow()).Date;

        _logger.LogInformation(
            "AnalyticsSnapshotJob executing daily workload snapshot for {SnapshotDate:yyyy-MM-dd}",
            effectiveDate);

        using var scope = _scopeFactory.CreateScope();
        var analyticsService = scope.ServiceProvider.GetRequiredService<IManagementAnalyticsService>();
        return await analyticsService.CaptureDailyWorkloadSnapshotAsync(effectiveDate, cancellationToken);
    }

    /// <summary>
    /// Convenience alias for <see cref="TriggerDailySnapshotAsync"/>.
    /// </summary>
    public Task<IReadOnlyList<DailyWorkloadSnapshotDto>> RunDailySnapshotAsync(
        DateTime? snapshotDate = null,
        CancellationToken cancellationToken = default)
        => TriggerDailySnapshotAsync(snapshotDate, cancellationToken);

    /// <summary>
    /// Triggers the monthly customer performance snapshot computation and persistence for the specified
    /// <paramref name="yearMonth"/> (<c>YYYY-MM</c>, or the preceding calendar month when <c>null</c>).
    /// </summary>
    public async Task<IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto>> TriggerMonthlySnapshotAsync(
        string? yearMonth = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveYearMonth = string.IsNullOrWhiteSpace(yearMonth)
            ? GetPrecedingYearMonth(GetUtcNow())
            : yearMonth.Trim();

        _logger.LogInformation(
            "AnalyticsSnapshotJob executing monthly customer performance snapshot for {YearMonth}",
            effectiveYearMonth);

        using var scope = _scopeFactory.CreateScope();
        var analyticsService = scope.ServiceProvider.GetRequiredService<IManagementAnalyticsService>();
        return await analyticsService.CaptureMonthlyCustomerPerformanceSnapshotAsync(effectiveYearMonth, cancellationToken);
    }

    /// <summary>
    /// Convenience alias for <see cref="TriggerMonthlySnapshotAsync"/>.
    /// </summary>
    public Task<IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto>> RunMonthlySnapshotAsync(
        string? yearMonth = null,
        CancellationToken cancellationToken = default)
        => TriggerMonthlySnapshotAsync(yearMonth, cancellationToken);

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        var now = GetUtcNow();
        var dailyDelay = CalculateDelayUntilNextDailyRun(now);
        var monthlyDelay = CalculateDelayUntilNextMonthlyRun(now);

        lock (_timerLock)
        {
            _dailyTimer = new Timer(
                OnDailyTimerElapsed,
                state: null,
                dueTime: dailyDelay,
                period: Timeout.InfiniteTimeSpan);

            _monthlyTimer = new Timer(
                OnMonthlyTimerElapsed,
                state: null,
                dueTime: monthlyDelay,
                period: Timeout.InfiniteTimeSpan);
        }

        stoppingToken.Register(StopTimers);

        _logger.LogInformation(
            "AnalyticsSnapshotJob started. Next daily snapshot in {DailyDelay}; next monthly snapshot in {MonthlyDelay}",
            dailyDelay,
            monthlyDelay);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        StopTimers();
        return base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        StopTimers();
        base.Dispose();
    }

    private async void OnDailyTimerElapsed(object? state)
    {
        if (_stoppingToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var snapshotDate = GetUtcNow().Date;
            await TriggerDailySnapshotAsync(snapshotDate, _stoppingToken);
        }
        catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error while executing scheduled daily workload snapshot");
        }
        finally
        {
            RescheduleDailyTimer();
        }
    }

    private async void OnMonthlyTimerElapsed(object? state)
    {
        if (_stoppingToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var precedingYearMonth = GetPrecedingYearMonth(GetUtcNow());
            await TriggerMonthlySnapshotAsync(precedingYearMonth, _stoppingToken);
        }
        catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error while executing scheduled monthly customer performance snapshot");
        }
        finally
        {
            RescheduleMonthlyTimer();
        }
    }

    private void RescheduleDailyTimer()
    {
        lock (_timerLock)
        {
            if (_stoppingToken.IsCancellationRequested || _dailyTimer is null)
            {
                return;
            }

            var delay = CalculateDelayUntilNextDailyRun(GetUtcNow().AddSeconds(1));
            _dailyTimer.Change(delay, Timeout.InfiniteTimeSpan);
        }
    }

    private void RescheduleMonthlyTimer()
    {
        lock (_timerLock)
        {
            if (_stoppingToken.IsCancellationRequested || _monthlyTimer is null)
            {
                return;
            }

            var delay = CalculateDelayUntilNextMonthlyRun(GetUtcNow().AddMinutes(1));
            _monthlyTimer.Change(delay, Timeout.InfiniteTimeSpan);
        }
    }

    private void StopTimers()
    {
        lock (_timerLock)
        {
            _dailyTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _dailyTimer?.Dispose();
            _dailyTimer = null;

            _monthlyTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _monthlyTimer?.Dispose();
            _monthlyTimer = null;
        }
    }

    private DateTime GetUtcNow() => _systemClock?.UtcNow ?? DateTime.UtcNow;
}
