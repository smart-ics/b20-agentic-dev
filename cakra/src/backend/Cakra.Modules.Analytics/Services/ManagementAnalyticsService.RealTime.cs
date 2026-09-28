using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Dapper;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cakra.Modules.Analytics.Services;

/// <summary>
/// Real-time dynamic query projections on <see cref="ManagementAnalyticsService"/>
/// (Architecture §7, §8, §9, §13, §15, §19.3, §20 — Slice P7-S35, FEAT-MGT-002, FEAT-MGT-004).
/// Executes read-only Dapper parameterized SQL queries against <c>request.*</c> and enriches
/// cross-domain attributes via published query contracts (<see cref="IOrganizationQueryService"/>,
/// <see cref="ICustomerQueryService"/>). Zero cross-module writes are performed.
/// </summary>
public sealed partial class ManagementAnalyticsService :
    IRequestHandler<GetProgrammerActiveWorkloadQuery, IReadOnlyList<ProgrammerActiveWorkloadDto>>,
    IRequestHandler<GetCustomerRequestPortfolioQuery, CustomerRequestPortfolioDto>
{
    /// <summary>
    /// Maximum number of recent completed requests returned in <see cref="CustomerRequestPortfolioDto.RecentCompletions"/>.
    /// </summary>
    public const int DefaultRecentCompletionsLimit = 20;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProgrammerActiveWorkloadDto>> GetProgrammerActiveWorkloadAsync(
        Guid? personId = null,
        CancellationToken cancellationToken = default)
    {
        if (personId.HasValue && personId.Value == Guid.Empty)
        {
            throw new ArgumentException("personId must not be an empty GUID when specified.", nameof(personId));
        }

        var referenceUtc = _systemClock?.UtcNow ?? DateTime.UtcNow;

        // Architecture §13 (Real-Time Operational Projections), §20 (Parameterization Requirement):
        // Dynamically aggregate Requests in active lifecycle states grouped by OwnerPersonId and sub-state.
        const string aggregateBySubStateSql = """
            SELECT
                r.[OwnerPersonId] AS [PersonId],
                r.[Status] AS [SubState],
                COUNT(1) AS [RequestCount],
                COUNT(CASE
                    WHEN DATEDIFF(SECOND, COALESCE(r.[UpdatedAt], r.[CreatedAt]), @ReferenceUtc) >= @StalledThresholdSeconds
                    THEN 1
                END) AS [StalledCount]
            FROM [request].[Requests] r
            WHERE r.[OwnerPersonId] IS NOT NULL
              AND r.[Status] IN ('CAPTURED', 'EVALUATING', 'ACCEPTED', 'IN_PROGRESS', 'ESCALATED')
              AND (@PersonId IS NULL OR r.[OwnerPersonId] = @PersonId)
            GROUP BY r.[OwnerPersonId], r.[Status]
            ORDER BY r.[OwnerPersonId] ASC, r.[Status] ASC;
            """;

        const string activeQueueSql = """
            SELECT
                r.[Id] AS [RequestId],
                r.[Title],
                r.[Description],
                r.[RequestType],
                r.[Status],
                r.[Priority],
                r.[OwnerPersonId],
                r.[CustomerId],
                r.[ProductId],
                r.[WorkPackageId],
                r.[EscalationReason],
                r.[CreatedAt],
                r.[UpdatedAt]
            FROM [request].[Requests] r
            WHERE r.[OwnerPersonId] IS NOT NULL
              AND r.[Status] IN ('CAPTURED', 'EVALUATING', 'ACCEPTED', 'IN_PROGRESS', 'ESCALATED')
              AND (@PersonId IS NULL OR r.[OwnerPersonId] = @PersonId)
            ORDER BY
                CASE r.[Status]
                    WHEN 'ESCALATED' THEN 1
                    WHEN 'IN_PROGRESS' THEN 2
                    WHEN 'ACCEPTED' THEN 3
                    WHEN 'EVALUATING' THEN 4
                    WHEN 'CAPTURED' THEN 5
                    ELSE 6
                END ASC,
                COALESCE(r.[UpdatedAt], r.[CreatedAt]) DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();

        var subStateRows = (await connection.QueryAsync<WorkloadSubStateRow>(
            new CommandDefinition(
                aggregateBySubStateSql,
                new
                {
                    PersonId = personId,
                    ReferenceUtc = referenceUtc,
                    StalledThresholdSeconds
                },
                cancellationToken: cancellationToken))).AsList();

        var activeQueueRows = (await connection.QueryAsync<CustomerPortfolioRequestItemDto>(
            new CommandDefinition(
                activeQueueSql,
                new { PersonId = personId },
                cancellationToken: cancellationToken))).AsList();

        // Resolve Person identities via IOrganizationQueryService (Architecture §15, §20)
        var personsById = new Dictionary<Guid, PersonDto>();

        if (personId.HasValue)
        {
            var singlePerson = await _organizationQueryService.GetPersonByIdAsync(personId.Value, cancellationToken);
            if (singlePerson is not null)
            {
                personsById[singlePerson.Id] = singlePerson;
            }
        }
        else
        {
            var activePersons = await _organizationQueryService.ListActivePersonsAsync(cancellationToken);
            foreach (var person in activePersons)
            {
                personsById[person.Id] = person;
            }
        }

        // Ensure any owner present in active requests is also resolved
        foreach (var row in subStateRows)
        {
            if (!personsById.ContainsKey(row.PersonId))
            {
                var owner = await _organizationQueryService.GetPersonByIdAsync(row.PersonId, cancellationToken);
                if (owner is not null)
                {
                    personsById[row.PersonId] = owner;
                }
            }
        }

        // Resolve customer names for active queue items via ICustomerQueryService
        var customersById = new Dictionary<Guid, CustomerDto?>();
        var enrichedQueueByOwner = new Dictionary<Guid, List<CustomerPortfolioRequestItemDto>>();

        foreach (var item in activeQueueRows)
        {
            if (!item.OwnerPersonId.HasValue)
            {
                continue;
            }

            var ownerId = item.OwnerPersonId.Value;
            var ownerName = personsById.TryGetValue(ownerId, out var ownerDto) ? ownerDto.FullName : null;

            string? customerName = null;
            string? customerCode = null;
            if (item.CustomerId.HasValue)
            {
                var custId = item.CustomerId.Value;
                if (!customersById.TryGetValue(custId, out var custDto))
                {
                    custDto = await _customerQueryService.GetCustomerByIdAsync(custId, cancellationToken);
                    customersById[custId] = custDto;
                }

                customerName = custDto?.CustomerName;
                customerCode = custDto?.CustomerCode;
            }

            var enrichedItem = item with
            {
                OwnerName = ownerName,
                CustomerName = customerName,
                CustomerCode = customerCode
            };

            if (!enrichedQueueByOwner.TryGetValue(ownerId, out var ownerQueue))
            {
                ownerQueue = new List<CustomerPortfolioRequestItemDto>();
                enrichedQueueByOwner[ownerId] = ownerQueue;
            }

            ownerQueue.Add(enrichedItem);
        }

        // Combine all target PersonIds (from active persons + any owners in subStateRows)
        var targetPersonIds = new HashSet<Guid>(personsById.Keys);
        foreach (var row in subStateRows)
        {
            targetPersonIds.Add(row.PersonId);
        }

        var groupedSubStates = subStateRows
            .GroupBy(r => r.PersonId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var results = new List<ProgrammerActiveWorkloadDto>(targetPersonIds.Count);

        foreach (var targetPersonId in targetPersonIds)
        {
            personsById.TryGetValue(targetPersonId, out var personDto);
            groupedSubStates.TryGetValue(targetPersonId, out var personSubStates);

            var capturedCount = 0;
            var evaluatingCount = 0;
            var acceptedCount = 0;
            var inProgressCount = 0;
            var escalatedCount = 0;
            var stalledCount = 0;

            if (personSubStates is not null)
            {
                foreach (var stateRow in personSubStates)
                {
                    stalledCount += stateRow.StalledCount;
                    var normalizedState = stateRow.SubState?.Trim().ToUpperInvariant();
                    switch (normalizedState)
                    {
                        case "CAPTURED":
                            capturedCount += stateRow.RequestCount;
                            break;
                        case "EVALUATING":
                            evaluatingCount += stateRow.RequestCount;
                            break;
                        case "ACCEPTED":
                            acceptedCount += stateRow.RequestCount;
                            break;
                        case "IN_PROGRESS":
                            inProgressCount += stateRow.RequestCount;
                            break;
                        case "ESCALATED":
                            escalatedCount += stateRow.RequestCount;
                            break;
                    }
                }
            }

            var totalActive = capturedCount + evaluatingCount + acceptedCount + inProgressCount + escalatedCount;
            var subStateCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["CAPTURED"] = capturedCount,
                ["EVALUATING"] = evaluatingCount,
                ["ACCEPTED"] = acceptedCount,
                ["IN_PROGRESS"] = inProgressCount,
                ["ESCALATED"] = escalatedCount
            };

            var activeQueue = enrichedQueueByOwner.TryGetValue(targetPersonId, out var queueList)
                ? (IReadOnlyList<CustomerPortfolioRequestItemDto>)queueList
                : Array.Empty<CustomerPortfolioRequestItemDto>();

            results.Add(new ProgrammerActiveWorkloadDto
            {
                PersonId = targetPersonId,
                PersonName = personDto?.FullName ?? string.Empty,
                Email = personDto?.Email,
                CapturedCount = capturedCount,
                EvaluatingCount = evaluatingCount,
                AcceptedCount = acceptedCount,
                InProgressCount = inProgressCount,
                EscalatedCount = escalatedCount,
                TotalActiveCount = totalActive,
                StalledRequestsCount = stalledCount,
                SubStateCounts = subStateCounts,
                ActiveRequests = activeQueue
            });
        }

        var orderedResults = results
            .OrderByDescending(x => x.TotalActiveCount)
            .ThenByDescending(x => x.EscalatedCount)
            .ThenBy(x => x.PersonName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation(
            "Computed real-time programmer active workload for {PersonFilter}: {PersonCount} programmer(s) returned",
            personId?.ToString() ?? "ALL",
            orderedResults.Count);

        return orderedResults;
    }

    /// <inheritdoc />
    public async Task<CustomerRequestPortfolioDto> GetCustomerRequestPortfolioAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("customerId must not be an empty GUID.", nameof(customerId));
        }

        // Enrich customer details and maintenance contract status via published CustomerQueryService (Architecture §13, §15, §20)
        var customer = await _customerQueryService.GetCustomerWithContractStatusAsync(customerId, cancellationToken);

        const string summaryCountsSql = """
            SELECT
                COUNT(1) AS [TotalRequestsCount],
                COUNT(CASE
                    WHEN r.[Status] IN ('CAPTURED', 'EVALUATING', 'ACCEPTED', 'IN_PROGRESS', 'ESCALATED')
                    THEN 1
                END) AS [ActiveRequestsCount],
                COUNT(CASE
                    WHEN r.[Status] = 'ESCALATED'
                    THEN 1
                END) AS [OpenBlockersCount],
                COUNT(CASE
                    WHEN r.[Status] = 'COMPLETED'
                    THEN 1
                END) AS [RecentCompletionsCount],
                COUNT(CASE
                    WHEN r.[Status] = 'REJECTED'
                    THEN 1
                END) AS [RejectedRequestsCount]
            FROM [request].[Requests] r
            WHERE r.[CustomerId] = @CustomerId;
            """;

        const string portfolioRequestsSql = """
            SELECT
                r.[Id] AS [RequestId],
                r.[Title],
                r.[Description],
                r.[RequestType],
                r.[Status],
                r.[Priority],
                r.[OwnerPersonId],
                r.[CustomerId],
                r.[ProductId],
                r.[WorkPackageId],
                r.[EscalationReason],
                res.[Outcome] AS [ResolutionOutcome],
                res.[Description] AS [ResolutionSummary],
                res.[ResolvedBy],
                res.[ResolvedAt],
                r.[CreatedAt],
                r.[UpdatedAt]
            FROM [request].[Requests] r
            LEFT JOIN [request].[RequestResolutions] res ON res.[RequestId] = r.[Id]
            WHERE r.[CustomerId] = @CustomerId
            ORDER BY
                CASE
                    WHEN r.[Status] = 'ESCALATED' THEN 1
                    WHEN r.[Status] IN ('CAPTURED', 'EVALUATING', 'ACCEPTED', 'IN_PROGRESS') THEN 2
                    WHEN r.[Status] = 'COMPLETED' THEN 3
                    ELSE 4
                END ASC,
                COALESCE(res.[ResolvedAt], r.[UpdatedAt], r.[CreatedAt]) DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();

        var summary = await connection.QuerySingleAsync<CustomerPortfolioSummaryRow>(
            new CommandDefinition(
                summaryCountsSql,
                new { CustomerId = customerId },
                cancellationToken: cancellationToken));

        var rawRequests = (await connection.QueryAsync<CustomerPortfolioRequestItemDto>(
            new CommandDefinition(
                portfolioRequestsSql,
                new { CustomerId = customerId },
                cancellationToken: cancellationToken))).AsList();

        // Enrich OwnerName via IOrganizationQueryService
        var ownerNamesById = new Dictionary<Guid, string?>();
        var enrichedRequests = new List<CustomerPortfolioRequestItemDto>(rawRequests.Count);

        foreach (var req in rawRequests)
        {
            string? ownerName = null;
            if (req.OwnerPersonId.HasValue)
            {
                var ownerId = req.OwnerPersonId.Value;
                if (!ownerNamesById.TryGetValue(ownerId, out ownerName))
                {
                    var owner = await _organizationQueryService.GetPersonByIdAsync(ownerId, cancellationToken);
                    ownerName = owner?.FullName;
                    ownerNamesById[ownerId] = ownerName;
                }
            }

            enrichedRequests.Add(req with
            {
                OwnerName = ownerName,
                CustomerName = customer?.CustomerName,
                CustomerCode = customer?.CustomerCode
            });
        }

        var activeRequests = enrichedRequests
            .Where(r => r.IsActive)
            .ToList();

        var openBlockers = enrichedRequests
            .Where(r => r.IsBlocked)
            .ToList();

        var recentCompletions = enrichedRequests
            .Where(r => string.Equals(r.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.ResolvedAt ?? r.UpdatedAt ?? r.CreatedAt)
            .Take(DefaultRecentCompletionsLimit)
            .ToList();

        _logger.LogInformation(
            "Computed real-time customer request portfolio for CustomerId {CustomerId}: Active={ActiveCount}, OpenBlockers={BlockersCount}, Completed={CompletedCount}",
            customerId,
            summary.ActiveRequestsCount,
            summary.OpenBlockersCount,
            summary.RecentCompletionsCount);

        return new CustomerRequestPortfolioDto
        {
            CustomerId = customerId,
            CustomerCode = customer?.CustomerCode ?? string.Empty,
            CustomerName = customer?.CustomerName ?? string.Empty,
            CustomerStatus = customer?.Status ?? "UNKNOWN",
            HasActiveMaintenanceContract = customer?.HasActiveMaintenanceContract ?? false,
            ContractStatus = customer?.ContractStatus ?? "NONE",
            ActiveRequestsCount = summary.ActiveRequestsCount,
            OpenBlockersCount = summary.OpenBlockersCount,
            RecentCompletionsCount = summary.RecentCompletionsCount,
            RejectedRequestsCount = summary.RejectedRequestsCount,
            TotalRequestsCount = summary.TotalRequestsCount,
            ActiveRequests = activeRequests,
            OpenBlockers = openBlockers,
            RecentCompletions = recentCompletions,
            Requests = enrichedRequests
        };
    }

    // -------------------------------------------------------------------------
    // MediatR Request Handlers (Real-Time Operational Projections)
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public Task<IReadOnlyList<ProgrammerActiveWorkloadDto>> Handle(
        GetProgrammerActiveWorkloadQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetProgrammerActiveWorkloadAsync(request.PersonId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CustomerRequestPortfolioDto> Handle(
        GetCustomerRequestPortfolioQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetCustomerRequestPortfolioAsync(request.CustomerId, cancellationToken);
    }

    private sealed class WorkloadSubStateRow
    {
        public Guid PersonId { get; init; }
        public string SubState { get; init; } = string.Empty;
        public int RequestCount { get; init; }
        public int StalledCount { get; init; }
    }

    private sealed class CustomerPortfolioSummaryRow
    {
        public int TotalRequestsCount { get; init; }
        public int ActiveRequestsCount { get; init; }
        public int OpenBlockersCount { get; init; }
        public int RecentCompletionsCount { get; init; }
        public int RejectedRequestsCount { get; init; }
    }
}
