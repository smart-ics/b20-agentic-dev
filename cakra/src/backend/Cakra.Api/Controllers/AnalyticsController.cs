using Cakra.Modules.Analytics;
using Cakra.Modules.Analytics.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Cakra.Api.Controllers;

/// <summary>
/// REST API controller for the Management Analytics module (<c>SCR-MGT-001..003</c>, <c>UC-MGT-002..004</c>,
/// <c>FEAT-MGT-002..004</c>; Architecture §7, §8, §9, §13, §14, §18, §19.5, §19.6).
/// Exposes <c>/api/v1/analytics/*</c> endpoints protected by RBAC (<c>[Authorize(Roles = "Management")]</c>).
/// </summary>
[Authorize(Roles = "Management")]
[Route("api/v1/analytics")]
public sealed class AnalyticsController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly IManagementAnalyticsService _analyticsService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        IMediator mediator,
        IManagementAnalyticsService analyticsService,
        ILogger<AnalyticsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves the real-time request portfolio (active requests, open blockers, and recent completions
    /// joined with maintenance contract status) for a customer
    /// (<c>ManagementAnalyticsService.GetCustomerRequestPortfolio</c>; Architecture §7, §8, §9, §13 —
    /// <c>UC-MGT-002</c>, <c>FEAT-MGT-002</c>, <c>SCR-MGT-001</c>).
    /// </summary>
    [HttpGet("customer-portfolio")]
    [ProducesResponseType(typeof(CustomerRequestPortfolioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CustomerRequestPortfolioDto>> GetCustomerRequestPortfolio(
        [FromQuery] Guid? customerId = null,
        [FromQuery] Guid? customer = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedCustomerId = customerId ?? customer ?? Guid.Empty;
        var result = await _mediator.Send(new GetCustomerRequestPortfolioQuery(resolvedCustomerId), cancellationToken);

        _logger.LogInformation(
            "Retrieved customer request portfolio for CustomerId '{CustomerId}' (Active={ActiveCount}, OpenBlockers={BlockersCount}, RecentCompletions={CompletedCount}).",
            result.CustomerId,
            result.ActiveRequestsCount,
            result.OpenBlockersCount,
            result.RecentCompletionsCount);

        return Ok(result);
    }

    /// <summary>
    /// Retrieves historical programmer performance metrics across <c>analytics.DailyWorkloadSnapshots</c>
    /// and <c>analytics.MonthlyCustomerPerformanceSnapshots</c>
    /// (<c>ManagementAnalyticsService.GetProgrammerPerformance</c>; Architecture §7, §8, §9, §13 —
    /// <c>UC-MGT-003</c>, <c>FEAT-MGT-003</c>, <c>SCR-MGT-002</c>).
    /// </summary>
    [HttpGet("programmer-performance")]
    [ProducesResponseType(typeof(ProgrammerPerformanceReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProgrammerPerformanceReportDto>> GetProgrammerPerformance(
        [FromQuery] Guid? personId = null,
        [FromQuery] Guid? person = null,
        [FromQuery] string? startMonth = null,
        [FromQuery] string? endMonth = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedPersonId = personId ?? person;
        var result = await _mediator.Send(
            new GetProgrammerPerformanceQuery(resolvedPersonId, startMonth, endMonth),
            cancellationToken);

        _logger.LogInformation(
            "Retrieved programmer performance report (PersonId='{PersonId}', StartMonth='{StartMonth}', EndMonth='{EndMonth}', MonthlySeriesCount={SeriesCount}).",
            resolvedPersonId,
            startMonth,
            endMonth,
            result.MonthlySeries.Count);

        return Ok(result);
    }

    /// <summary>
    /// Retrieves real-time active request workload aggregated per programmer/person and lifecycle sub-state
    /// (<c>ManagementAnalyticsService.GetProgrammerActiveWorkload</c>; Architecture §7, §8, §9, §13 —
    /// <c>UC-MGT-004</c>, <c>FEAT-MGT-004</c>, <c>SCR-MGT-003</c>).
    /// </summary>
    [HttpGet("programmer-workload")]
    [ProducesResponseType(typeof(IReadOnlyList<ProgrammerActiveWorkloadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ProgrammerActiveWorkloadDto>>> GetProgrammerActiveWorkload(
        [FromQuery] Guid? personId = null,
        [FromQuery] Guid? person = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedPersonId = personId ?? person;
        var result = await _mediator.Send(new GetProgrammerActiveWorkloadQuery(resolvedPersonId), cancellationToken);

        _logger.LogInformation(
            "Retrieved programmer active workload (PersonId='{PersonId}', ProgrammerCount={ProgrammerCount}).",
            resolvedPersonId,
            result.Count);

        return Ok(result);
    }
}
