using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;

namespace Cakra.Modules.Analytics.Services;

/// <summary>
/// MediatR command to capture end-of-day workload snapshots into <c>analytics.DailyWorkloadSnapshots</c>
/// for the specified <paramref name="SnapshotDate"/> (Architecture §13, §19.7).
/// </summary>
public sealed record CaptureDailyWorkloadSnapshotCommand(
    DateTime SnapshotDate) : IRequest<IReadOnlyList<DailyWorkloadSnapshotDto>>;

/// <summary>
/// FluentValidation validator for <see cref="CaptureDailyWorkloadSnapshotCommand"/>.
/// </summary>
public sealed class CaptureDailyWorkloadSnapshotCommandValidator : AbstractValidator<CaptureDailyWorkloadSnapshotCommand>
{
    public CaptureDailyWorkloadSnapshotCommandValidator()
    {
        RuleFor(x => x.SnapshotDate)
            .Must(d => d != default)
            .WithMessage("SnapshotDate must be a valid calendar date.");
    }
}

/// <summary>
/// MediatR command to capture monthly customer performance snapshots into
/// <c>analytics.MonthlyCustomerPerformanceSnapshots</c> for the specified <paramref name="YearMonth"/>
/// in <c>YYYY-MM</c> format (Architecture §13, §19.7).
/// </summary>
public sealed record CaptureMonthlyCustomerPerformanceSnapshotCommand(
    string YearMonth) : IRequest<IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto>>;

/// <summary>
/// FluentValidation validator for <see cref="CaptureMonthlyCustomerPerformanceSnapshotCommand"/>.
/// </summary>
public sealed class CaptureMonthlyCustomerPerformanceSnapshotCommandValidator : AbstractValidator<CaptureMonthlyCustomerPerformanceSnapshotCommand>
{
    private static readonly Regex YearMonthPattern = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    public CaptureMonthlyCustomerPerformanceSnapshotCommandValidator()
    {
        RuleFor(x => x.YearMonth)
            .NotEmpty()
            .WithMessage("YearMonth is required.")
            .Must(ym => !string.IsNullOrWhiteSpace(ym) && YearMonthPattern.IsMatch(ym.Trim()))
            .WithMessage("YearMonth must be in 'YYYY-MM' format (e.g. '2026-09').");
    }
}

/// <summary>
/// MediatR command to idempotently backfill and recompute daily and monthly analytics snapshots
/// across <c>[StartDate, EndDate]</c> (Architecture §13 — On-Demand Recomputation).
/// </summary>
public sealed record RecomputeSnapshotsCommand(
    DateTime StartDate,
    DateTime EndDate) : IRequest<SnapshotRecomputationResultDto>;

/// <summary>
/// FluentValidation validator for <see cref="RecomputeSnapshotsCommand"/>.
/// </summary>
public sealed class RecomputeSnapshotsCommandValidator : AbstractValidator<RecomputeSnapshotsCommand>
{
    public RecomputeSnapshotsCommandValidator()
    {
        RuleFor(x => x.StartDate)
            .Must(d => d != default)
            .WithMessage("StartDate must be a valid date.");

        RuleFor(x => x.EndDate)
            .Must(d => d != default)
            .WithMessage("EndDate must be a valid date.")
            .Must((cmd, endDate) => endDate.Date >= cmd.StartDate.Date)
            .WithMessage("EndDate must be greater than or equal to StartDate.");
    }
}

/// <summary>
/// MediatR query to retrieve <c>analytics.DailyWorkloadSnapshots</c> filtered by optional date range
/// and <c>PersonId</c> (Architecture §13).
/// </summary>
public sealed record GetDailyWorkloadSnapshotsQuery(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    Guid? PersonId = null) : IRequest<IReadOnlyList<DailyWorkloadSnapshotDto>>;

/// <summary>
/// MediatR query to retrieve <c>analytics.MonthlyCustomerPerformanceSnapshots</c> filtered by optional
/// month range (<c>YYYY-MM</c>) and <c>CustomerId</c> (Architecture §13).
/// </summary>
public sealed record GetMonthlyCustomerPerformanceSnapshotsQuery(
    string? StartMonth = null,
    string? EndMonth = null,
    Guid? CustomerId = null) : IRequest<IReadOnlyList<MonthlyCustomerPerformanceSnapshotDto>>;

/// <summary>
/// MediatR query to retrieve historical programmer performance metrics for <c>SCR-MGT-002</c>
/// (Architecture §7, §8, §9, §13 — UC-MGT-003, FEAT-MGT-003).
/// </summary>
public sealed record GetProgrammerPerformanceQuery(
    Guid? PersonId = null,
    string? StartMonth = null,
    string? EndMonth = null) : IRequest<ProgrammerPerformanceReportDto>;
