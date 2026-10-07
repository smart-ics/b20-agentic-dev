using Cakra.Core;
using Cakra.Modules.Request;
using Cakra.Modules.WorkPackage.Domain;
using Cakra.Modules.WorkPackage.Models;
using MediatR;

namespace Cakra.Modules.WorkPackage.Services;

/// <summary>
/// MediatR query handler for <see cref="GetOperationsCockpitQuery"/> loading active Work Packages,
/// querying rolling 30-day C_org throughput, computing demand density and factual operational health telemetry,
/// and assembling the portfolio Operations Cockpit read model (Architecture CR-025 §4 TD-005).
/// </summary>
public sealed class GetOperationsCockpitQueryHandler : IRequestHandler<GetOperationsCockpitQuery, OperationsCockpitDto>
{
    private readonly IWorkPackageQueryService _workPackageQueryService;
    private readonly IRequestQueryService? _requestQueryService;
    private readonly IWorkPackageTelemetryCalculator _telemetryCalculator;
    private readonly ISystemClock? _systemClock;

    public GetOperationsCockpitQueryHandler(
        IWorkPackageQueryService workPackageQueryService,
        IRequestQueryService? requestQueryService = null,
        IWorkPackageTelemetryCalculator? telemetryCalculator = null,
        ISystemClock? systemClock = null)
    {
        _workPackageQueryService = workPackageQueryService ?? throw new ArgumentNullException(nameof(workPackageQueryService));
        _requestQueryService = requestQueryService;
        _telemetryCalculator = telemetryCalculator ?? new WorkPackageTelemetryCalculator();
        _systemClock = systemClock;
    }

    public async Task<OperationsCockpitDto> Handle(
        GetOperationsCockpitQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asOfDateUtc = request.AsOfDateUtc ?? _systemClock?.UtcNow ?? DateTime.UtcNow;

        // 1. Query empirical organization demonstrated daily throughput C_org (rolling 30 days)
        double cOrg = 1.0;
        if (_requestQueryService is not null)
        {
            try
            {
                cOrg = await _requestQueryService.GetOrgDemonstratedDailyThroughputAsync(30, cancellationToken);
            }
            catch
            {
                cOrg = 1.0;
            }
        }

        if (cOrg <= 0.0)
        {
            cOrg = 1.0;
        }

        // 2. Query all active Work Packages
        var activePackages = await _workPackageQueryService.ListWorkPackagesAsync(
            WorkPackageStatusNames.Active,
            cancellationToken: cancellationToken);

        if (activePackages.Count == 0)
        {
            return new OperationsCockpitDto
            {
                PortfolioMetrics = _telemetryCalculator.CalculatePortfolioMetrics(Array.Empty<WorkPackageTelemetryDto>(), cOrg),
                Matrix = _telemetryCalculator.BuildMatrix(Array.Empty<WorkPackageTelemetryDto>()),
                Packages = Array.Empty<WorkPackageTelemetryDto>()
            };
        }

        // 3. For each active Work Package, resolve constituent active requests and compute telemetry
        var telemetryList = new List<WorkPackageTelemetryDto>(activePackages.Count);
        foreach (var wp in activePackages)
        {
            var activeRequests = (wp.Requests ?? Array.Empty<WorkPackageScopeItemDto>())
                .Where(s => s.IsActive)
                .Select(MapScopeItemToRequest)
                .ToList();

            var telemetry = _telemetryCalculator.ComputeTelemetry(wp, activeRequests, cOrg, asOfDateUtc);
            telemetryList.Add(telemetry);
        }

        // 4. Build 2D Pressure x Health triage matrix and portfolio aggregate metrics
        var matrix = _telemetryCalculator.BuildMatrix(telemetryList);
        var portfolioMetrics = _telemetryCalculator.CalculatePortfolioMetrics(telemetryList, cOrg);

        // 5. Assemble and return OperationsCockpitDto
        return new OperationsCockpitDto
        {
            PortfolioMetrics = portfolioMetrics,
            Matrix = matrix,
            Packages = telemetryList
        };
    }

    private static RequestDto MapScopeItemToRequest(WorkPackageScopeItemDto scopeItem)
    {
        if (scopeItem.Request is not null)
        {
            return scopeItem.Request;
        }

        return new RequestDto
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
        };
    }
}
