using Cakra.Modules.Request;
using Cakra.Modules.WorkPackage.Models;

namespace Cakra.Modules.WorkPackage.Services;

/// <summary>
/// Telemetry calculation engine evaluating Scope Pressure, Org Capacity Share tiers,
/// Observable Operational Health Invariants, Flow Inventory counters, and 14-day Activity Barcodes
/// (Architecture CR-025 §4 TD-002, TD-003, TD-004, TD-005).
/// </summary>
public interface IWorkPackageTelemetryCalculator
{
    /// <summary>
    /// Calculates complete telemetry for a Work Package aggregate and its constituent requests.
    /// </summary>
    WorkPackageTelemetryDto CalculateTelemetry(
        Domain.WorkPackage workPackage,
        IReadOnlyCollection<RequestDto> requests,
        double cOrg,
        DateTime? asOfDateUtc = null,
        string? ownerName = null,
        string? customerName = null,
        string? productName = null);

    /// <summary>
    /// Calculates complete telemetry for an enriched WorkPackageDto.
    /// </summary>
    WorkPackageTelemetryDto CalculateTelemetry(
        WorkPackageDto workPackage,
        double cOrg,
        DateTime? asOfDateUtc = null);

    /// <summary>
    /// Calculates complete telemetry for an enriched WorkPackageDto and its constituent requests.
    /// </summary>
    WorkPackageTelemetryDto CalculateTelemetry(
        WorkPackageDto workPackage,
        IReadOnlyCollection<RequestDto> requests,
        double cOrg,
        DateTime? asOfDateUtc = null);

    /// <summary>
    /// Convenience alias for <see cref="CalculateTelemetry(Domain.WorkPackage, IReadOnlyCollection{RequestDto}, double, DateTime?, string?, string?, string?)"/>.
    /// </summary>
    WorkPackageTelemetryDto ComputeTelemetry(
        Domain.WorkPackage workPackage,
        IReadOnlyCollection<RequestDto> requests,
        double cOrg,
        DateTime? asOfDateUtc = null,
        string? ownerName = null,
        string? customerName = null,
        string? productName = null)
        => CalculateTelemetry(workPackage, requests, cOrg, asOfDateUtc, ownerName, customerName, productName);

    /// <summary>
    /// Convenience alias for <see cref="CalculateTelemetry(WorkPackageDto, double, DateTime?)"/>.
    /// </summary>
    WorkPackageTelemetryDto ComputeTelemetry(
        WorkPackageDto workPackage,
        double cOrg,
        DateTime? asOfDateUtc = null)
        => CalculateTelemetry(workPackage, cOrg, asOfDateUtc);

    /// <summary>
    /// Convenience alias for <see cref="CalculateTelemetry(WorkPackageDto, IReadOnlyCollection{RequestDto}, double, DateTime?)"/>.
    /// </summary>
    WorkPackageTelemetryDto ComputeTelemetry(
        WorkPackageDto workPackage,
        IReadOnlyCollection<RequestDto> requests,
        double cOrg,
        DateTime? asOfDateUtc = null)
        => CalculateTelemetry(workPackage, requests, cOrg, asOfDateUtc);

    /// <summary>
    /// Aggregates a 2D Pressure × Health triage matrix distribution across a set of telemetry packages.
    /// </summary>
    PressureHealthMatrixDto BuildMatrix(IEnumerable<WorkPackageTelemetryDto> packages);

    /// <summary>
    /// Computes high-level portfolio macro metrics across a set of telemetry packages benchmarked to C_org.
    /// </summary>
    PortfolioMetricsDto CalculatePortfolioMetrics(IEnumerable<WorkPackageTelemetryDto> packages, double cOrg);
}
