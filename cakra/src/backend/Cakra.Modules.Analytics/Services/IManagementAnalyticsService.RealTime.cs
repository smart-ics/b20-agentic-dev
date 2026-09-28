namespace Cakra.Modules.Analytics.Services;

/// <summary>
/// Real-time operational projection queries on <see cref="IManagementAnalyticsService"/>
/// (Architecture §7, §8, §9, §13, §15 — Slice P7-S35, FEAT-MGT-002, FEAT-MGT-004).
/// </summary>
public partial interface IManagementAnalyticsService
{
    /// <summary>
    /// Dynamically aggregates active <c>Requests</c> grouped by <c>OwnerPersonId</c> and lifecycle sub-state
    /// (<c>CAPTURED</c>, <c>EVALUATING</c>, <c>ACCEPTED</c>, <c>IN_PROGRESS</c>, <c>ESCALATED</c>)
    /// using Dapper parameterized SQL, enriched with person identity from <c>IOrganizationQueryService</c>
    /// for <c>SCR-MGT-003: Programmer Workload Review</c> (Architecture §13, FEAT-MGT-004, UC-MGT-004).
    /// </summary>
    /// <param name="personId">Optional PersonId filter to inspect a single programmer's active workload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ProgrammerActiveWorkloadDto>> GetProgrammerActiveWorkloadAsync(
        Guid? personId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="GetProgrammerActiveWorkloadAsync"/> (Architecture §13).
    /// </summary>
    Task<IReadOnlyList<ProgrammerActiveWorkloadDto>> GetProgrammerActiveWorkload(
        Guid? personId = null,
        CancellationToken cancellationToken = default)
        => GetProgrammerActiveWorkloadAsync(personId, cancellationToken);

    /// <summary>
    /// Dynamically queries active requests, open blockers (<c>ESCALATED</c>), and recent completions
    /// (<c>COMPLETED</c>) for the specified <paramref name="customerId"/> using Dapper parameterized SQL,
    /// enriched with customer details and maintenance contract status via
    /// <c>ICustomerQueryService.GetCustomerWithContractStatusAsync</c> for
    /// <c>SCR-MGT-001: Customer Progress Review</c> (Architecture §13, FEAT-MGT-002, UC-MGT-002).
    /// </summary>
    /// <param name="customerId">Unique identifier of the Customer organization.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CustomerRequestPortfolioDto> GetCustomerRequestPortfolioAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience alias for <see cref="GetCustomerRequestPortfolioAsync"/> (Architecture §13).
    /// </summary>
    Task<CustomerRequestPortfolioDto> GetCustomerRequestPortfolio(
        Guid customerId,
        CancellationToken cancellationToken = default)
        => GetCustomerRequestPortfolioAsync(customerId, cancellationToken);
}
