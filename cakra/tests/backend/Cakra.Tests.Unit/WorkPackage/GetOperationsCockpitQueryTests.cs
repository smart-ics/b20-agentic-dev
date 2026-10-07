using Cakra.Core;
using Cakra.Modules.Request;
using Cakra.Modules.Request.Domain;
using Cakra.Modules.WorkPackage;
using Cakra.Modules.WorkPackage.Domain;
using Cakra.Modules.WorkPackage.Models;
using Cakra.Modules.WorkPackage.Services;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.WorkPackage;

public sealed class GetOperationsCockpitQueryTests
{
    private readonly FakeWorkPackageQueryService _wpQueryService = new();
    private readonly FakeRequestQueryService _reqQueryService = new();
    private readonly WorkPackageTelemetryCalculator _telemetryCalculator = new();
    private readonly FakeSystemClock _clock = new();
    private readonly GetOperationsCockpitQueryHandler _handler;

    public GetOperationsCockpitQueryTests()
    {
        _handler = new GetOperationsCockpitQueryHandler(
            _wpQueryService,
            _reqQueryService,
            _telemetryCalculator,
            _clock);
    }

    [Fact]
    public async Task Handle_WhenNoActiveWorkPackages_ReturnsZeroMetricsAndZeroMatrixCells()
    {
        // Arrange
        _reqQueryService.DailyThroughput = 4.5;

        // Act
        var result = await _handler.Handle(new GetOperationsCockpitQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Packages.Should().BeEmpty();
        result.PortfolioMetrics.TotalActiveWorkPackages.Should().Be(0);
        result.PortfolioMetrics.WithDeadlineCount.Should().Be(0);
        result.PortfolioMetrics.WithoutDeadlineCount.Should().Be(0);
        result.PortfolioMetrics.OrgDailyThroughput.Should().Be(4.5);
        result.PortfolioMetrics.PortfolioAggregateLoadPercentage.Should().Be(0.0);
        result.PortfolioMetrics.TotalInvariantViolationsCount.Should().Be(0);

        // Verify matrix contains all rows & cols initialized to 0
        foreach (var row in WorkPackageHealthStates.All)
        {
            result.Matrix.RowTotals[row].Should().Be(0);
            foreach (var col in WorkPackagePressureTiers.All)
            {
                result.Matrix.Cells[row][col].Should().Be(0);
            }
        }

        foreach (var col in WorkPackagePressureTiers.All)
        {
            result.Matrix.ColumnTotals[col].Should().Be(0);
        }
    }

    [Fact]
    public async Task Handle_WithDiverseActiveWorkPackages_AssemblesCorrectTelemetryMatrixAndPortfolioMetrics()
    {
        // Arrange: Wednesday Oct 7, 2026
        _clock.UtcNow = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
        _reqQueryService.DailyThroughput = 5.0;

        // WP 1: Wednesday Oct 14 deadline (5 working days). 2 active requests with complexity 5 each (total 10).
        // Daily Burn = 10 / 5 = 2.0. OrgCapacityShare = (2.0 / 5.0) * 100 = 40.0% (ELEVATED). FLOWING.
        var wp1 = CreateWorkPackageDto("WP-1", "Objective 1", WorkPackageStatusNames.Active,
            deadline: new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc),
            requests: new[]
            {
                CreateScopeItem("Req-1", RequestStatusNames.InProgress, complexity: 5),
                CreateScopeItem("Req-2", RequestStatusNames.Captured, complexity: 5)
            });

        // WP 2: Thursday Oct 8 deadline (1 working day). 1 active request with complexity 6.
        // Daily Burn = 6 / 1 = 6.0. OrgCapacityShare = (6.0 / 5.0) * 100 = 120.0% (IMPOSSIBLE).
        // Request status = PAUSED -> ACTIVE_BLOCKERS.
        var wp2 = CreateWorkPackageDto("WP-2", "Objective 2", WorkPackageStatusNames.Active,
            deadline: new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
            requests: new[]
            {
                CreateScopeItem("Req-3", RequestStatusNames.Paused, complexity: 6)
            });

        // WP 3: No deadline. 1 active request with complexity 3.
        // PressureTier = UNPLANNED. HealthState = FLOWING.
        var wp3 = CreateWorkPackageDto("WP-3", "Objective 3", WorkPackageStatusNames.Active,
            deadline: null,
            requests: new[]
            {
                CreateScopeItem("Req-4", RequestStatusNames.InProgress, complexity: 3)
            });

        // WP 4: Tuesday Oct 6 deadline (in the past: 0 working days). Remaining complexity = 4.
        // Daily Burn = 4.0. OrgCapacityShare = (4.0 / 5.0) * 100 = 80.0% (CRITICAL).
        // HealthState = DEADLINE_BREACHED.
        var wp4 = CreateWorkPackageDto("WP-4", "Objective 4", WorkPackageStatusNames.Active,
            deadline: new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc),
            requests: new[]
            {
                CreateScopeItem("Req-5", RequestStatusNames.InProgress, complexity: 4)
            });

        _wpQueryService.Packages.AddRange(new[] { wp1, wp2, wp3, wp4 });

        // Act
        var result = await _handler.Handle(new GetOperationsCockpitQuery(), CancellationToken.None);

        // Assert
        result.Packages.Should().HaveCount(4);

        var p1 = result.Packages.Single(p => p.Name == "WP-1");
        p1.PressureTier.Should().Be(WorkPackagePressureTiers.Elevated);
        p1.HealthState.Should().Be(WorkPackageHealthStates.Flowing);
        p1.RequiredDailyBurn.Should().Be(2.0);
        p1.OrgCapacityShare.Should().Be(40.0);

        var p2 = result.Packages.Single(p => p.Name == "WP-2");
        p2.PressureTier.Should().Be(WorkPackagePressureTiers.Impossible);
        p2.HealthState.Should().Be(WorkPackageHealthStates.ActiveBlockers);
        p2.RequiredDailyBurn.Should().Be(6.0);
        p2.OrgCapacityShare.Should().Be(120.0);

        var p3 = result.Packages.Single(p => p.Name == "WP-3");
        p3.PressureTier.Should().Be(WorkPackagePressureTiers.Unplanned);
        p3.HealthState.Should().Be(WorkPackageHealthStates.Flowing);
        p3.RequiredDailyBurn.Should().BeNull();
        p3.OrgCapacityShare.Should().BeNull();

        var p4 = result.Packages.Single(p => p.Name == "WP-4");
        p4.PressureTier.Should().Be(WorkPackagePressureTiers.Critical);
        p4.HealthState.Should().Be(WorkPackageHealthStates.DeadlineBreached);
        p4.RequiredDailyBurn.Should().Be(4.0);
        p4.OrgCapacityShare.Should().Be(80.0);

        // Macro Metrics
        result.PortfolioMetrics.TotalActiveWorkPackages.Should().Be(4);
        result.PortfolioMetrics.WithDeadlineCount.Should().Be(3);
        result.PortfolioMetrics.WithoutDeadlineCount.Should().Be(1);
        result.PortfolioMetrics.OrgDailyThroughput.Should().Be(5.0);
        // Burn sum = 2.0 + 6.0 + 4.0 = 12.0. Portfolio load = (12.0 / 5.0) * 100 = 240.0%
        result.PortfolioMetrics.PortfolioAggregateLoadPercentage.Should().Be(240.0);
        result.PortfolioMetrics.TotalInvariantViolationsCount.Should().Be(2); // WP-2 and WP-4

        // 2D Matrix distribution
        result.Matrix.Cells[WorkPackageHealthStates.Flowing][WorkPackagePressureTiers.Elevated].Should().Be(1);
        result.Matrix.Cells[WorkPackageHealthStates.ActiveBlockers][WorkPackagePressureTiers.Impossible].Should().Be(1);
        result.Matrix.Cells[WorkPackageHealthStates.Flowing][WorkPackagePressureTiers.Unplanned].Should().Be(1);
        result.Matrix.Cells[WorkPackageHealthStates.DeadlineBreached][WorkPackagePressureTiers.Critical].Should().Be(1);

        result.Matrix.RowTotals[WorkPackageHealthStates.Flowing].Should().Be(2);
        result.Matrix.RowTotals[WorkPackageHealthStates.ActiveBlockers].Should().Be(1);
        result.Matrix.RowTotals[WorkPackageHealthStates.DeadlineBreached].Should().Be(1);
        result.Matrix.RowTotals[WorkPackageHealthStates.Dormant].Should().Be(0);
        result.Matrix.RowTotals[WorkPackageHealthStates.WipStagnant].Should().Be(0);

        result.Matrix.ColumnTotals[WorkPackagePressureTiers.Elevated].Should().Be(1);
        result.Matrix.ColumnTotals[WorkPackagePressureTiers.Impossible].Should().Be(1);
        result.Matrix.ColumnTotals[WorkPackagePressureTiers.Unplanned].Should().Be(1);
        result.Matrix.ColumnTotals[WorkPackagePressureTiers.Critical].Should().Be(1);
        result.Matrix.ColumnTotals[WorkPackagePressureTiers.Nominal].Should().Be(0);
    }

    [Fact]
    public async Task Handle_IgnoresRemovedOrInactiveRequestsInTelemetry()
    {
        // Arrange
        _clock.UtcNow = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
        _reqQueryService.DailyThroughput = 5.0;

        var activeScopeItem = CreateScopeItem("Active Req", RequestStatusNames.InProgress, complexity: 5);
        var removedScopeItem = new WorkPackageScopeItemDto
        {
            Id = Guid.NewGuid(),
            RequestId = Guid.NewGuid(),
            Title = "Removed Req",
            Status = RequestStatusNames.InProgress,
            RemovedAt = _clock.UtcNow.AddDays(-1), // Removed!
            Request = new RequestDto
            {
                Id = Guid.NewGuid(),
                Title = "Removed Req",
                Status = RequestStatusNames.InProgress,
                Complexity = 10
            }
        };

        var wp = CreateWorkPackageDto("WP-Active", "Objective", WorkPackageStatusNames.Active,
            deadline: new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc),
            requests: new[] { activeScopeItem, removedScopeItem });

        _wpQueryService.Packages.Add(wp);

        // Act
        var result = await _handler.Handle(new GetOperationsCockpitQuery(), CancellationToken.None);

        // Assert
        result.Packages.Should().HaveCount(1);
        var pkg = result.Packages[0];
        pkg.TotalComplexity.Should().Be(5);
        pkg.RemainingComplexity.Should().Be(5);
        pkg.TotalRequestsCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_IgnoresNonActiveWorkPackages()
    {
        // Arrange
        var activeWp = CreateWorkPackageDto("WP-Active", "Active", WorkPackageStatusNames.Active);
        var draftWp = CreateWorkPackageDto("WP-Draft", "Draft", WorkPackageStatusNames.Draft);
        var closedWp = CreateWorkPackageDto("WP-Closed", "Closed", WorkPackageStatusNames.Closed);

        _wpQueryService.Packages.AddRange(new[] { activeWp, draftWp, closedWp });

        // Act
        var result = await _handler.Handle(new GetOperationsCockpitQuery(), CancellationToken.None);

        // Assert
        result.Packages.Should().HaveCount(1);
        result.Packages[0].Name.Should().Be("WP-Active");
        result.PortfolioMetrics.TotalActiveWorkPackages.Should().Be(1);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    public async Task Handle_WhenThroughputIsZeroOrNegative_UsesSafetyFloor(double invalidThroughput)
    {
        // Arrange
        _reqQueryService.DailyThroughput = invalidThroughput;
        var wp = CreateWorkPackageDto("WP", "Obj", WorkPackageStatusNames.Active);
        _wpQueryService.Packages.Add(wp);

        // Act
        var result = await _handler.Handle(new GetOperationsCockpitQuery(), CancellationToken.None);

        // Assert
        result.PortfolioMetrics.OrgDailyThroughput.Should().Be(1.0);
    }

    [Fact]
    public async Task Handle_WhenRequestQueryServiceThrows_FallsBackToSafetyFloor()
    {
        // Arrange
        var throwingQueryService = new ThrowingRequestQueryService();
        var handler = new GetOperationsCockpitQueryHandler(
            _wpQueryService,
            throwingQueryService,
            _telemetryCalculator,
            _clock);

        var wp = CreateWorkPackageDto("WP", "Obj", WorkPackageStatusNames.Active);
        _wpQueryService.Packages.Add(wp);

        // Act
        var result = await handler.Handle(new GetOperationsCockpitQuery(), CancellationToken.None);

        // Assert
        result.PortfolioMetrics.OrgDailyThroughput.Should().Be(1.0);
    }

    [Fact]
    public async Task Handle_WhenRequestQueryServiceIsNull_UsesSafetyFloor()
    {
        // Arrange
        var handler = new GetOperationsCockpitQueryHandler(
            _wpQueryService,
            requestQueryService: null,
            telemetryCalculator: _telemetryCalculator,
            systemClock: _clock);

        var wp = CreateWorkPackageDto("WP", "Obj", WorkPackageStatusNames.Active);
        _wpQueryService.Packages.Add(wp);

        // Act
        var result = await handler.Handle(new GetOperationsCockpitQuery(), CancellationToken.None);

        // Assert
        result.PortfolioMetrics.OrgDailyThroughput.Should().Be(1.0);
    }

    [Fact]
    public async Task Handle_WhenExplicitAsOfDateSpecifiedInQuery_UsesSpecifiedDate()
    {
        // Arrange: System clock is Oct 1, but query specifies Oct 7
        _clock.UtcNow = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var queryAsOf = new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);

        // Deadline is Oct 8 (1 working day from Oct 7, but 5 working days from Oct 1)
        var wp = CreateWorkPackageDto("WP", "Obj", WorkPackageStatusNames.Active,
            deadline: new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
            requests: new[] { CreateScopeItem("R1", RequestStatusNames.InProgress, complexity: 5) });
        _wpQueryService.Packages.Add(wp);

        // Act
        var result = await _handler.Handle(new GetOperationsCockpitQuery(queryAsOf), CancellationToken.None);

        // Assert
        var pkg = result.Packages[0];
        pkg.WorkingDaysRemaining.Should().Be(1); // computed from Oct 7, not Oct 1
    }

    [Fact]
    public async Task Handle_WithScopeItemLackingRequestDto_MapsFallbackAttributesGracefully()
    {
        // Arrange
        var scopeItem = new WorkPackageScopeItemDto
        {
            Id = Guid.NewGuid(),
            RequestId = Guid.NewGuid(),
            Title = "Un-enriched Request",
            Description = "Description",
            Status = RequestStatusNames.InProgress,
            Request = null // No enriched RequestDto
        };

        var wp = CreateWorkPackageDto("WP", "Obj", WorkPackageStatusNames.Active,
            requests: new[] { scopeItem });
        _wpQueryService.Packages.Add(wp);

        // Act
        var result = await _handler.Handle(new GetOperationsCockpitQuery(), CancellationToken.None);

        // Assert
        result.Packages.Should().HaveCount(1);
        result.Packages[0].TotalRequestsCount.Should().Be(1);
        result.Packages[0].ActiveWipCount.Should().Be(1);
    }

    private static WorkPackageDto CreateWorkPackageDto(
        string name,
        string objective,
        string status,
        DateTime? deadline = null,
        IReadOnlyList<WorkPackageScopeItemDto>? requests = null)
    {
        var wpId = Guid.NewGuid();
        return new WorkPackageDto
        {
            Id = wpId,
            Name = name,
            Objective = objective,
            Status = status,
            OwnerPersonId = Guid.NewGuid(),
            OwnerName = "Jane Doe",
            CustomerId = Guid.NewGuid(),
            CustomerName = "Acme Corp",
            ProductId = Guid.NewGuid(),
            ProductName = "Platform",
            Deadline = deadline,
            CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc),
            Requests = requests ?? Array.Empty<WorkPackageScopeItemDto>()
        };
    }

    private static WorkPackageScopeItemDto CreateScopeItem(
        string title,
        string status,
        int complexity = 1)
    {
        var reqId = Guid.NewGuid();
        return new WorkPackageScopeItemDto
        {
            Id = Guid.NewGuid(),
            RequestId = reqId,
            Title = title,
            Status = status,
            Request = new RequestDto
            {
                Id = reqId,
                Title = title,
                Status = status,
                Complexity = complexity,
                CreatedAt = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc)
            }
        };
    }

    private sealed class FakeWorkPackageQueryService : IWorkPackageQueryService
    {
        public List<WorkPackageDto> Packages { get; } = new();

        public Task<IReadOnlyList<WorkPackageDto>> ListWorkPackagesAsync(
            string? status = null,
            Guid? ownerPersonId = null,
            Guid? customerId = null,
            Guid? productId = null,
            CancellationToken cancellationToken = default)
        {
            var query = Packages.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(p => string.Equals(p.Status, status, StringComparison.OrdinalIgnoreCase));
            }
            if (ownerPersonId.HasValue && ownerPersonId.Value != Guid.Empty)
            {
                query = query.Where(p => p.OwnerPersonId == ownerPersonId.Value);
            }
            if (customerId.HasValue && customerId.Value != Guid.Empty)
            {
                query = query.Where(p => p.CustomerId == customerId.Value);
            }
            if (productId.HasValue && productId.Value != Guid.Empty)
            {
                query = query.Where(p => p.ProductId == productId.Value);
            }
            return Task.FromResult<IReadOnlyList<WorkPackageDto>>(query.ToList());
        }

        public Task<WorkPackageDto?> GetWorkPackageByIdAsync(Guid workPackageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Packages.FirstOrDefault(p => p.Id == workPackageId));

        public Task<IReadOnlyList<WorkPackageScopeItemDto>> GetWorkPackageScopeAsync(Guid workPackageId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkPackageScopeItemDto>>(
                Packages.FirstOrDefault(p => p.Id == workPackageId)?.Requests ?? Array.Empty<WorkPackageScopeItemDto>());

        public Task<WorkPackageDto?> GetRequestWorkPackageAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Packages.FirstOrDefault(p => p.Requests.Any(r => r.RequestId == requestId)));

        public Task<bool> WorkPackageExistsAsync(Guid workPackageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Packages.Any(p => p.Id == workPackageId));
    }

    private sealed class FakeRequestQueryService : IRequestQueryService
    {
        public double DailyThroughput { get; set; } = 5.0;

        public Task<double> GetOrgDemonstratedDailyThroughputAsync(int windowDays = 30, CancellationToken cancellationToken = default) =>
            Task.FromResult(DailyThroughput);

        public Task<RequestDto?> GetRequestByIdAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<RequestDto?>(null);

        public Task<IReadOnlyList<RequestAssignmentDto>> GetRequestStateHistoryAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestAssignmentDto>>(Array.Empty<RequestAssignmentDto>());

        public Task<IReadOnlyList<RequestDto>> ListMyAssignedRequestsAsync(Guid? personId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestDto>>(Array.Empty<RequestDto>());

        public Task<IReadOnlyList<RequestDto>> GetRequestsWithAssignedSubTasksAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestDto>>(Array.Empty<RequestDto>());

        public Task<PagedRequestGridResult> GetFilteredRequestGridAsync(RequestGridFilter? filter = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedRequestGridResult());

        public Task<bool> RequestExistsAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<IReadOnlyList<RequestDto>> GetRequestsByIdsAsync(IEnumerable<Guid> requestIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestDto>>(Array.Empty<RequestDto>());

        public Task<IReadOnlyList<PersonWorkInProgressDto>> GetWorkInProgressOverviewAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PersonWorkInProgressDto>>(Array.Empty<PersonWorkInProgressDto>());
    }

    private sealed class ThrowingRequestQueryService : IRequestQueryService
    {
        public Task<double> GetOrgDemonstratedDailyThroughputAsync(int windowDays = 30, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated database failure");

        public Task<RequestDto?> GetRequestByIdAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<RequestDto?>(null);

        public Task<IReadOnlyList<RequestAssignmentDto>> GetRequestStateHistoryAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestAssignmentDto>>(Array.Empty<RequestAssignmentDto>());

        public Task<IReadOnlyList<RequestDto>> ListMyAssignedRequestsAsync(Guid? personId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestDto>>(Array.Empty<RequestDto>());

        public Task<IReadOnlyList<RequestDto>> GetRequestsWithAssignedSubTasksAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestDto>>(Array.Empty<RequestDto>());

        public Task<PagedRequestGridResult> GetFilteredRequestGridAsync(RequestGridFilter? filter = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedRequestGridResult());

        public Task<bool> RequestExistsAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<IReadOnlyList<RequestDto>> GetRequestsByIdsAsync(IEnumerable<Guid> requestIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestDto>>(Array.Empty<RequestDto>());

        public Task<IReadOnlyList<PersonWorkInProgressDto>> GetWorkInProgressOverviewAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PersonWorkInProgressDto>>(Array.Empty<PersonWorkInProgressDto>());
    }

    private sealed class FakeSystemClock : ISystemClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
    }
}
