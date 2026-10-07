namespace Cakra.Modules.WorkPackage.Models;

/// <summary>
/// Canonical string constants for Work Package Scope Pressure tiers benchmarked against C_org
/// (Architecture CR-025 §4 TD-002).
/// </summary>
public static class WorkPackagePressureTiers
{
    public const string Unplanned = "UNPLANNED";
    public const string Nominal = "NOMINAL";
    public const string Elevated = "ELEVATED";
    public const string Critical = "CRITICAL";
    public const string Impossible = "IMPOSSIBLE";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Unplanned,
        Nominal,
        Elevated,
        Critical,
        Impossible
    };
}

/// <summary>
/// Canonical string constants for Observable Operational Health Invariants
/// (Architecture CR-025 §4 TD-003).
/// Ordered from highest severity to nominal flowing state.
/// </summary>
public static class WorkPackageHealthStates
{
    public const string DeadlineBreached = "DEADLINE_BREACHED";
    public const string ActiveBlockers = "ACTIVE_BLOCKERS";
    public const string Dormant = "DORMANT";
    public const string WipStagnant = "WIP_STAGNANT";
    public const string Flowing = "FLOWING";

    public static readonly IReadOnlyList<string> All = new[]
    {
        DeadlineBreached,
        ActiveBlockers,
        Dormant,
        WipStagnant,
        Flowing
    };
}

/// <summary>
/// Comprehensive factual telemetry read model for a single Work Package (Architecture CR-025 §4 TD-005).
/// Encompasses demand density (Scope Pressure), observable health invariant evaluations,
/// flow inventory counters, and 14-day discrete event pulse.
/// </summary>
public sealed record WorkPackageTelemetryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Objective { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid OwnerPersonId { get; init; }
    public string? OwnerName { get; init; }
    public Guid? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public Guid? ProductId { get; init; }
    public string? ProductName { get; init; }
    public DateTime? Deadline { get; init; }

    // Scope & Pressure
    public int TotalComplexity { get; init; }
    public int CompletedComplexity { get; init; }
    public int RemainingComplexity { get; init; }
    public int TotalRequestsCount { get; init; }
    public int RemainingRequestsCount { get; init; }
    public int? WorkingDaysRemaining { get; init; }
    public double? RequiredDailyBurn { get; init; }
    public double? OrgCapacityShare { get; init; }
    public string PressureTier { get; init; } = WorkPackagePressureTiers.Unplanned;

    // Operational Health Invariants
    public string HealthState { get; init; } = WorkPackageHealthStates.Flowing;
    public bool DeadlineBreached { get; init; }
    public int BlockedRequestsCount { get; init; }
    public double DormantDays { get; init; }
    public bool IsDormant { get; init; }
    public int ActiveWipCount { get; init; }
    public double OldestActiveWipDays { get; init; }
    public bool IsWipStagnant { get; init; }
    public int Outflow14dCount { get; init; }

    // 14-day flow barcode
    public IReadOnlyList<FlowBarcodeDayDto> FlowBarcode { get; init; } = Array.Empty<FlowBarcodeDayDto>();
}

/// <summary>
/// Represents a single day bucket within the 14-day Flow Activity Barcode (Architecture CR-025 §4 TD-004, TD-005).
/// </summary>
public sealed record FlowBarcodeDayDto
{
    /// <summary>Calendar date formatted as YYYY-MM-DD.</summary>
    public string Date { get; init; } = string.Empty;

    /// <summary>Number of requests transitioned to COMPLETED on this date.</summary>
    public int ClosedCount { get; init; }

    /// <summary>Number of requests updated or state-transitioned on this date.</summary>
    public int StateMutationCount { get; init; }

    /// <summary>Number of requests paused or blocked on this date.</summary>
    public int BlockedCount { get; init; }
}
