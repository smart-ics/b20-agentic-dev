using Cakra.Modules.Request;
using Cakra.Modules.Request.Domain;
using Cakra.Modules.WorkPackage.Domain;
using Cakra.Modules.WorkPackage.Models;

namespace Cakra.Modules.WorkPackage.Services;

/// <summary>
/// Domain calculation engine for Work Package operational telemetry, demand density,
/// health invariants, flow inventory, and portfolio aggregations
/// (Architecture CR-025 §4 TD-002, TD-003, TD-004, TD-005).
/// </summary>
public sealed class WorkPackageTelemetryCalculator : IWorkPackageTelemetryCalculator
{
    private const double DefaultOrgCapacityFloor = 1.0;
    private const double DormantThresholdDays = 5.0;
    private const double WipStagnantThresholdDays = 14.0;
    private const int OutflowWindowDays = 14;

    /// <inheritdoc />
    public WorkPackageTelemetryDto CalculateTelemetry(
        Domain.WorkPackage workPackage,
        IReadOnlyCollection<RequestDto> requests,
        double cOrg,
        DateTime? asOfDateUtc = null,
        string? ownerName = null,
        string? customerName = null,
        string? productName = null)
    {
        ArgumentNullException.ThrowIfNull(workPackage);
        var reqList = requests ?? Array.Empty<RequestDto>();
        var asOf = asOfDateUtc ?? DateTime.UtcNow;

        return ComputeCoreTelemetry(
            id: workPackage.Id,
            name: workPackage.Name,
            objective: workPackage.Objective,
            status: workPackage.Status.ToName(),
            ownerPersonId: workPackage.OwnerPersonId,
            ownerName: ownerName,
            customerId: workPackage.CustomerId,
            customerName: customerName,
            productId: workPackage.ProductId,
            productName: productName,
            deadline: workPackage.Deadline,
            createdAt: workPackage.CreatedAt,
            updatedAt: workPackage.UpdatedAt,
            requests: reqList,
            cOrg: cOrg,
            asOfDateUtc: asOf);
    }

    /// <inheritdoc />
    public WorkPackageTelemetryDto CalculateTelemetry(
        WorkPackageDto workPackage,
        double cOrg,
        DateTime? asOfDateUtc = null)
    {
        ArgumentNullException.ThrowIfNull(workPackage);
        var asOf = asOfDateUtc ?? DateTime.UtcNow;

        var mappedRequests = (workPackage.Requests ?? Array.Empty<WorkPackageScopeItemDto>())
            .Where(scopeItem => scopeItem.IsActive)
            .Select(scopeItem => scopeItem.Request ?? new RequestDto
            {
                Id = scopeItem.RequestId,
                Title = scopeItem.Title,
                Description = scopeItem.Description,
                RequestType = scopeItem.RequestType,
                Status = scopeItem.Status,
                Priority = scopeItem.Priority,
                Complexity = 1,
                OwnerPersonId = scopeItem.OwnerPersonId,
                OwnerName = scopeItem.OwnerName,
                CustomerId = scopeItem.CustomerId,
                CustomerName = scopeItem.CustomerName,
                ProductId = scopeItem.ProductId,
                ProductName = scopeItem.ProductName,
                WorkPackageId = scopeItem.WorkPackageId,
                CreatedAt = scopeItem.CreatedAt,
                UpdatedAt = scopeItem.UpdatedAt
            })
            .ToList();

        return CalculateTelemetry(workPackage, mappedRequests, cOrg, asOf);
    }

    /// <inheritdoc />
    public WorkPackageTelemetryDto CalculateTelemetry(
        WorkPackageDto workPackage,
        IReadOnlyCollection<RequestDto> requests,
        double cOrg,
        DateTime? asOfDateUtc = null)
    {
        ArgumentNullException.ThrowIfNull(workPackage);
        var asOf = asOfDateUtc ?? DateTime.UtcNow;
        var reqList = requests ?? Array.Empty<RequestDto>();

        return ComputeCoreTelemetry(
            id: workPackage.Id,
            name: workPackage.Name,
            objective: workPackage.Objective,
            status: workPackage.Status,
            ownerPersonId: workPackage.OwnerPersonId,
            ownerName: workPackage.OwnerName,
            customerId: workPackage.CustomerId,
            customerName: workPackage.CustomerName,
            productId: workPackage.ProductId,
            productName: workPackage.ProductName,
            deadline: workPackage.Deadline,
            createdAt: workPackage.CreatedAt,
            updatedAt: workPackage.UpdatedAt,
            requests: reqList,
            cOrg: cOrg,
            asOfDateUtc: asOf);
    }

    /// <inheritdoc />
    public PressureHealthMatrixDto BuildMatrix(IEnumerable<WorkPackageTelemetryDto> packages)
    {
        var packageList = packages?.ToList() ?? new List<WorkPackageTelemetryDto>();
        var cells = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        var rowTotals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var colTotals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Pre-initialize rows & columns with zero to ensure stable full matrix
        foreach (var health in WorkPackageHealthStates.All)
        {
            cells[health] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            rowTotals[health] = 0;
            foreach (var tier in WorkPackagePressureTiers.All)
            {
                cells[health][tier] = 0;
            }
        }

        foreach (var tier in WorkPackagePressureTiers.All)
        {
            colTotals[tier] = 0;
        }

        foreach (var pkg in packageList)
        {
            var row = WorkPackageHealthStates.All.Contains(pkg.HealthState, StringComparer.OrdinalIgnoreCase)
                ? pkg.HealthState
                : WorkPackageHealthStates.Flowing;

            var col = WorkPackagePressureTiers.All.Contains(pkg.PressureTier, StringComparer.OrdinalIgnoreCase)
                ? pkg.PressureTier
                : WorkPackagePressureTiers.Unplanned;

            cells[row][col]++;
            rowTotals[row]++;
            colTotals[col]++;
        }

        return new PressureHealthMatrixDto
        {
            Cells = cells,
            RowTotals = rowTotals,
            ColumnTotals = colTotals
        };
    }

    /// <inheritdoc />
    public PortfolioMetricsDto CalculatePortfolioMetrics(IEnumerable<WorkPackageTelemetryDto> packages, double cOrg)
    {
        var packageList = packages?.ToList() ?? new List<WorkPackageTelemetryDto>();
        var effectiveCOrg = cOrg > 0.0 ? cOrg : DefaultOrgCapacityFloor;

        var totalActive = packageList.Count;
        var withDeadline = packageList.Count(p => p.Deadline.HasValue);
        var withoutDeadline = totalActive - withDeadline;

        var sumDailyBurn = packageList
            .Where(p => p.RequiredDailyBurn.HasValue)
            .Sum(p => p.RequiredDailyBurn!.Value);

        var portfolioAggregateLoad = Math.Round((sumDailyBurn / effectiveCOrg) * 100.0, 2);

        var totalViolations = packageList.Count(p =>
            !string.Equals(p.HealthState, WorkPackageHealthStates.Flowing, StringComparison.OrdinalIgnoreCase));

        return new PortfolioMetricsDto
        {
            TotalActiveWorkPackages = totalActive,
            WithDeadlineCount = withDeadline,
            WithoutDeadlineCount = withoutDeadline,
            OrgDailyThroughput = effectiveCOrg,
            PortfolioAggregateLoadPercentage = portfolioAggregateLoad,
            TotalInvariantViolationsCount = totalViolations
        };
    }

    /// <summary>
    /// Computes calendar working days between two UTC dates, excluding Saturdays and Sundays
    /// (Architecture CR-025 §4 TD-002, ASM-002).
    /// </summary>
    /// <param name="fromUtc">Reference start date (e.g., Today UTC).</param>
    /// <param name="toUtc">Target date (e.g., Deadline UTC).</param>
    /// <returns>Integer count of working days remaining; 0 if target date is today or in the past.</returns>
    public static int ComputeWorkingDays(DateTime fromUtc, DateTime toUtc)
    {
        var startDate = fromUtc.Date;
        var targetDate = toUtc.Date;

        if (targetDate <= startDate)
        {
            return 0;
        }

        int workingDays = 0;
        var current = startDate.AddDays(1);
        while (current <= targetDate)
        {
            if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
            {
                workingDays++;
            }
            current = current.AddDays(1);
        }

        return workingDays;
    }

    /// <summary>
    /// Computes elapsed business days between two timestamps, excluding Saturdays and Sundays
    /// (Architecture CR-025 §4 TD-003, ASM-004).
    /// Supports fractional days for exact dormancy evaluation.
    /// </summary>
    public static double ComputeBusinessDays(DateTime fromUtc, DateTime toUtc)
    {
        if (fromUtc >= toUtc)
        {
            return 0.0;
        }

        if (fromUtc.Date == toUtc.Date)
        {
            if (fromUtc.DayOfWeek == DayOfWeek.Saturday || fromUtc.DayOfWeek == DayOfWeek.Sunday)
            {
                return 0.0;
            }
            return (toUtc - fromUtc).TotalDays;
        }

        double days = 0.0;

        // Partial day for start date (if weekday)
        if (fromUtc.DayOfWeek != DayOfWeek.Saturday && fromUtc.DayOfWeek != DayOfWeek.Sunday)
        {
            var endOfStartDay = fromUtc.Date.AddDays(1);
            days += (endOfStartDay - fromUtc).TotalDays;
        }

        // Full days in between
        var current = fromUtc.Date.AddDays(1);
        while (current < toUtc.Date)
        {
            if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
            {
                days += 1.0;
            }
            current = current.AddDays(1);
        }

        // Partial day for end date (if weekday)
        if (toUtc.DayOfWeek != DayOfWeek.Saturday && toUtc.DayOfWeek != DayOfWeek.Sunday)
        {
            days += (toUtc - toUtc.Date).TotalDays;
        }

        return days;
    }

    /// <summary>
    /// Evaluates Org Capacity Share percentage against benchmark tiers (Architecture CR-025 §4 TD-002).
    /// </summary>
    public static string DeterminePressureTier(double? orgCapacityShare, bool hasDeadline)
    {
        if (!hasDeadline || !orgCapacityShare.HasValue)
        {
            return WorkPackagePressureTiers.Unplanned;
        }

        var share = orgCapacityShare.Value;

        if (share < 20.0)
        {
            return WorkPackagePressureTiers.Nominal;
        }

        if (share <= 50.0)
        {
            return WorkPackagePressureTiers.Elevated;
        }

        if (share <= 100.0)
        {
            return WorkPackagePressureTiers.Critical;
        }

        return WorkPackagePressureTiers.Impossible;
    }

    /// <summary>
    /// Assigns the primary Operational Health state based on strict invariant precedence
    /// (Architecture CR-025 §4 TD-003):
    /// 1. DEADLINE_BREACHED
    /// 2. ACTIVE_BLOCKERS
    /// 3. DORMANT
    /// 4. WIP_STAGNANT
    /// 5. FLOWING
    /// </summary>
    public static string DetermineHealthState(
        bool deadlineBreached,
        bool hasActiveBlockers,
        bool isDormant,
        bool isWipStagnant)
    {
        if (deadlineBreached)
        {
            return WorkPackageHealthStates.DeadlineBreached;
        }

        if (hasActiveBlockers)
        {
            return WorkPackageHealthStates.ActiveBlockers;
        }

        if (isDormant)
        {
            return WorkPackageHealthStates.Dormant;
        }

        if (isWipStagnant)
        {
            return WorkPackageHealthStates.WipStagnant;
        }

        return WorkPackageHealthStates.Flowing;
    }

    /// <summary>
    /// Generates the 14-day discrete activity barcode array ending on the reference date
    /// (Architecture CR-025 §4 TD-004).
    /// </summary>
    public static IReadOnlyList<FlowBarcodeDayDto> Generate14DayBarcode(
        IReadOnlyCollection<RequestDto> requests,
        DateTime asOfDateUtc)
    {
        var result = new List<FlowBarcodeDayDto>(OutflowWindowDays);
        var baseDate = asOfDateUtc.Date;

        for (int i = OutflowWindowDays - 1; i >= 0; i--)
        {
            var dayDate = baseDate.AddDays(-i);
            var dateStr = dayDate.ToString("yyyy-MM-dd");

            int closed = 0;
            int blocked = 0;
            int mutations = 0;

            foreach (var req in requests)
            {
                var reqTimestamp = (req.UpdatedAt ?? req.CreatedAt).Date;
                if (reqTimestamp == dayDate)
                {
                    mutations++;

                    if (string.Equals(req.Status, RequestStatusNames.Completed, StringComparison.OrdinalIgnoreCase))
                    {
                        closed++;
                    }
                    else if (IsRequestBlocked(req.Status))
                    {
                        blocked++;
                    }
                }
            }

            result.Add(new FlowBarcodeDayDto
            {
                Date = dateStr,
                ClosedCount = closed,
                StateMutationCount = mutations,
                BlockedCount = blocked
            });
        }

        return result;
    }

    private static WorkPackageTelemetryDto ComputeCoreTelemetry(
        Guid id,
        string name,
        string objective,
        string status,
        Guid ownerPersonId,
        string? ownerName,
        Guid? customerId,
        string? customerName,
        Guid? productId,
        string? productName,
        DateTime? deadline,
        DateTime createdAt,
        DateTime? updatedAt,
        IReadOnlyCollection<RequestDto> requests,
        double cOrg,
        DateTime asOfDateUtc)
    {
        var completedRequests = requests.Where(r =>
            string.Equals(r.Status, RequestStatusNames.Completed, StringComparison.OrdinalIgnoreCase)).ToList();

        var remainingRequests = requests.Where(r =>
            !string.Equals(r.Status, RequestStatusNames.Completed, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(r.Status, RequestStatusNames.Cancelled, StringComparison.OrdinalIgnoreCase)).ToList();

        int totalComplexity = requests.Sum(r => r.Complexity);
        int completedComplexity = completedRequests.Sum(r => r.Complexity);
        int remainingComplexity = remainingRequests.Sum(r => r.Complexity);
        int totalRequestsCount = requests.Count;
        int remainingRequestsCount = remainingRequests.Count;

        // Scope Pressure calculation (TD-002)
        int? workingDaysRemaining = null;
        double? requiredDailyBurn = null;
        double? orgCapacityShare = null;
        string pressureTier;

        if (deadline.HasValue)
        {
            var daysRemaining = ComputeWorkingDays(asOfDateUtc, deadline.Value.Date);
            workingDaysRemaining = daysRemaining;

            if (daysRemaining <= 0)
            {
                requiredDailyBurn = (double)remainingComplexity;
            }
            else
            {
                requiredDailyBurn = Math.Round((double)remainingComplexity / daysRemaining, 2);
            }

            var effectiveCOrg = cOrg > 0.0 ? cOrg : DefaultOrgCapacityFloor;
            orgCapacityShare = Math.Round((requiredDailyBurn.Value / effectiveCOrg) * 100.0, 2);
            pressureTier = DeterminePressureTier(orgCapacityShare, hasDeadline: true);
        }
        else
        {
            pressureTier = WorkPackagePressureTiers.Unplanned;
        }

        // Operational Health Invariants evaluation (TD-003)
        bool deadlineBreached = deadline.HasValue && (workingDaysRemaining ?? 0) <= 0 && remainingRequestsCount > 0;

        var blockedRequests = requests.Where(r => IsRequestBlocked(r.Status)).ToList();
        int blockedRequestsCount = blockedRequests.Count;
        bool hasActiveBlockers = blockedRequestsCount > 0;

        // Dormancy: elapsed business days since latest (WP.UpdatedAt ?? WP.CreatedAt, max(Requests.UpdatedAt ?? Requests.CreatedAt))
        var latestEventUtc = updatedAt ?? createdAt;
        foreach (var req in requests)
        {
            var reqTime = req.UpdatedAt ?? req.CreatedAt;
            if (reqTime > latestEventUtc)
            {
                latestEventUtc = reqTime;
            }
        }
        double dormantDays = Math.Round(ComputeBusinessDays(latestEventUtc, asOfDateUtc), 1);
        bool isDormant = dormantDays > DormantThresholdDays;

        // WIP Stagnant: active IN_PROGRESS requests with age > 14 calendar days
        var activeWipRequests = requests.Where(r =>
            string.Equals(r.Status, RequestStatusNames.InProgress, StringComparison.OrdinalIgnoreCase)).ToList();
        int activeWipCount = activeWipRequests.Count;

        double oldestActiveWipDays = 0.0;
        if (activeWipRequests.Count > 0)
        {
            oldestActiveWipDays = Math.Round(
                activeWipRequests.Max(r => Math.Max(0.0, (asOfDateUtc - (r.UpdatedAt ?? r.CreatedAt)).TotalDays)), 1);
        }
        bool isWipStagnant = oldestActiveWipDays > WipStagnantThresholdDays;

        string healthState = DetermineHealthState(deadlineBreached, hasActiveBlockers, isDormant, isWipStagnant);

        // Flow Inventory (TD-004)
        var outflowThresholdDate = asOfDateUtc.Date.AddDays(-(OutflowWindowDays - 1));
        int outflow14dCount = requests.Count(r =>
            string.Equals(r.Status, RequestStatusNames.Completed, StringComparison.OrdinalIgnoreCase) &&
            (r.UpdatedAt ?? r.CreatedAt).Date >= outflowThresholdDate &&
            (r.UpdatedAt ?? r.CreatedAt).Date <= asOfDateUtc.Date);

        var flowBarcode = Generate14DayBarcode(requests, asOfDateUtc);

        return new WorkPackageTelemetryDto
        {
            Id = id,
            Name = name,
            Objective = objective,
            Status = status,
            OwnerPersonId = ownerPersonId,
            OwnerName = ownerName,
            CustomerId = customerId,
            CustomerName = customerName,
            ProductId = productId,
            ProductName = productName,
            Deadline = deadline,

            TotalComplexity = totalComplexity,
            CompletedComplexity = completedComplexity,
            RemainingComplexity = remainingComplexity,
            TotalRequestsCount = totalRequestsCount,
            RemainingRequestsCount = remainingRequestsCount,
            WorkingDaysRemaining = workingDaysRemaining,
            RequiredDailyBurn = requiredDailyBurn,
            OrgCapacityShare = orgCapacityShare,
            PressureTier = pressureTier,

            HealthState = healthState,
            DeadlineBreached = deadlineBreached,
            BlockedRequestsCount = blockedRequestsCount,
            DormantDays = dormantDays,
            IsDormant = isDormant,
            ActiveWipCount = activeWipCount,
            OldestActiveWipDays = oldestActiveWipDays,
            IsWipStagnant = isWipStagnant,
            Outflow14dCount = outflow14dCount,

            FlowBarcode = flowBarcode
        };
    }

    private static bool IsRequestBlocked(string status) =>
        string.Equals(status, RequestStatusNames.Paused, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "BLOCKED", StringComparison.OrdinalIgnoreCase);
}
