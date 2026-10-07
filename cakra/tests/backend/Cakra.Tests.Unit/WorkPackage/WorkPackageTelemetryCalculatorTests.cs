using Cakra.Modules.Request;
using Cakra.Modules.Request.Domain;
using Cakra.Modules.WorkPackage;
using Cakra.Modules.WorkPackage.Domain;
using Cakra.Modules.WorkPackage.Models;
using Cakra.Modules.WorkPackage.Services;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.WorkPackage;

public sealed class WorkPackageTelemetryCalculatorTests
{
    private readonly WorkPackageTelemetryCalculator _calculator = new();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    [Fact]
    public void ComputeWorkingDays_WhenTargetIsSameOrBeforeStart_ReturnsZero()
    {
        // Arrange
        var today = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc); // Wednesday
        var sameDay = new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);
        var pastDay = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        // Act & Assert
        WorkPackageTelemetryCalculator.ComputeWorkingDays(today, sameDay).Should().Be(0);
        WorkPackageTelemetryCalculator.ComputeWorkingDays(today, pastDay).Should().Be(0);
    }

    [Fact]
    public void ComputeWorkingDays_AcrossWeekdays_ExcludesWeekendsCorrectly()
    {
        // Wednesday Oct 7 to Wednesday Oct 14 (1 week = 5 working days)
        var start = new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);
        var target1Week = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc);
        WorkPackageTelemetryCalculator.ComputeWorkingDays(start, target1Week).Should().Be(5);

        // Friday Oct 9 to Monday Oct 12 (skips Sat & Sun = 1 working day)
        var friday = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
        var monday = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);
        WorkPackageTelemetryCalculator.ComputeWorkingDays(friday, monday).Should().Be(1);

        // Saturday Oct 10 to Sunday Oct 11 (weekend = 0 working days)
        var saturday = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        var sunday = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);
        WorkPackageTelemetryCalculator.ComputeWorkingDays(saturday, sunday).Should().Be(0);

        // Friday Oct 9 to Friday Oct 16 (5 working days)
        var nextFriday = new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);
        WorkPackageTelemetryCalculator.ComputeWorkingDays(friday, nextFriday).Should().Be(5);
    }

    [Fact]
    public void ComputeBusinessDays_CalculatesElapsedBusinessDays_IncludingFractions()
    {
        // Thursday Sep 24 00:00:00 to Wednesday Oct 7 12:00:00 = exactly 9.5 business days
        var start = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

        var businessDays = WorkPackageTelemetryCalculator.ComputeBusinessDays(start, end);
        Math.Round(businessDays, 1).Should().Be(9.5);

        // Start >= End returns 0
        WorkPackageTelemetryCalculator.ComputeBusinessDays(end, start).Should().Be(0.0);
    }

    [Theory]
    [InlineData(null, true, WorkPackagePressureTiers.Unplanned)]
    [InlineData(10.0, false, WorkPackagePressureTiers.Unplanned)]
    [InlineData(0.0, true, WorkPackagePressureTiers.Nominal)]
    [InlineData(19.9, true, WorkPackagePressureTiers.Nominal)]
    [InlineData(20.0, true, WorkPackagePressureTiers.Elevated)]
    [InlineData(35.0, true, WorkPackagePressureTiers.Elevated)]
    [InlineData(50.0, true, WorkPackagePressureTiers.Elevated)]
    [InlineData(50.1, true, WorkPackagePressureTiers.Critical)]
    [InlineData(75.0, true, WorkPackagePressureTiers.Critical)]
    [InlineData(100.0, true, WorkPackagePressureTiers.Critical)]
    [InlineData(100.1, true, WorkPackagePressureTiers.Impossible)]
    [InlineData(150.0, true, WorkPackagePressureTiers.Impossible)]
    public void DeterminePressureTier_MapsCapacityShareToCorrectTiers(
        double? capacityShare,
        bool hasDeadline,
        string expectedTier)
    {
        var tier = WorkPackageTelemetryCalculator.DeterminePressureTier(capacityShare, hasDeadline);
        tier.Should().Be(expectedTier);
    }

    [Fact]
    public void DetermineHealthState_EnforcesSeverityPrecedence()
    {
        // 1. DeadlineBreached overrides all
        WorkPackageTelemetryCalculator.DetermineHealthState(
            deadlineBreached: true,
            hasActiveBlockers: true,
            isDormant: true,
            isWipStagnant: true).Should().Be(WorkPackageHealthStates.DeadlineBreached);

        // 2. ActiveBlockers overrides dormant and wip stagnant
        WorkPackageTelemetryCalculator.DetermineHealthState(
            deadlineBreached: false,
            hasActiveBlockers: true,
            isDormant: true,
            isWipStagnant: true).Should().Be(WorkPackageHealthStates.ActiveBlockers);

        // 3. Dormant overrides wip stagnant
        WorkPackageTelemetryCalculator.DetermineHealthState(
            deadlineBreached: false,
            hasActiveBlockers: false,
            isDormant: true,
            isWipStagnant: true).Should().Be(WorkPackageHealthStates.Dormant);

        // 4. WipStagnant when only stagnancy violated
        WorkPackageTelemetryCalculator.DetermineHealthState(
            deadlineBreached: false,
            hasActiveBlockers: false,
            isDormant: false,
            isWipStagnant: true).Should().Be(WorkPackageHealthStates.WipStagnant);

        // 5. Flowing when no invariant violated
        WorkPackageTelemetryCalculator.DetermineHealthState(
            deadlineBreached: false,
            hasActiveBlockers: false,
            isDormant: false,
            isWipStagnant: false).Should().Be(WorkPackageHealthStates.Flowing);
    }

    [Fact]
    public void CalculateTelemetry_WhenNoDeadline_AssignsUnplannedPressureTier()
    {
        // Arrange
        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Unplanned Package",
            objective: "No target deadline set",
            ownerPersonId: _ownerId,
            customerId: _customerId,
            productId: _productId,
            deadline: null);

        var requests = new List<RequestDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Req 1", Complexity = 3, Status = RequestStatusNames.InProgress },
            new() { Id = Guid.NewGuid(), Title = "Req 2", Complexity = 2, Status = RequestStatusNames.Captured }
        };

        // Act
        var telemetry = _calculator.CalculateTelemetry(package, requests, cOrg: 5.0);

        // Assert
        telemetry.Deadline.Should().BeNull();
        telemetry.WorkingDaysRemaining.Should().BeNull();
        telemetry.RequiredDailyBurn.Should().BeNull();
        telemetry.OrgCapacityShare.Should().BeNull();
        telemetry.PressureTier.Should().Be(WorkPackagePressureTiers.Unplanned);
        telemetry.TotalComplexity.Should().Be(5);
        telemetry.RemainingComplexity.Should().Be(5);
        telemetry.DeadlineBreached.Should().BeFalse();
    }

    [Fact]
    public void CalculateTelemetry_WhenDeadlinePresent_CalculatesBurnAndCapacityShare()
    {
        // Arrange
        var today = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc); // Wednesday
        var deadline = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc); // 5 working days remaining

        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "RSUD Billing Engine",
            objective: "Deliver billing module",
            ownerPersonId: _ownerId,
            customerId: _customerId,
            productId: _productId,
            deadline: deadline);

        // 32 total: 4 completed, 28 remaining (7 requests)
        var requests = new List<RequestDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Req 1", Complexity = 4, Status = RequestStatusNames.Completed, UpdatedAt = today },
            new() { Id = Guid.NewGuid(), Title = "Req 2", Complexity = 5, Status = RequestStatusNames.InProgress, UpdatedAt = today },
            new() { Id = Guid.NewGuid(), Title = "Req 3", Complexity = 5, Status = RequestStatusNames.InProgress, UpdatedAt = today },
            new() { Id = Guid.NewGuid(), Title = "Req 4", Complexity = 5, Status = RequestStatusNames.InProgress, UpdatedAt = today },
            new() { Id = Guid.NewGuid(), Title = "Req 5", Complexity = 3, Status = RequestStatusNames.InProgress, UpdatedAt = today },
            new() { Id = Guid.NewGuid(), Title = "Req 6", Complexity = 5, Status = RequestStatusNames.Captured, UpdatedAt = today },
            new() { Id = Guid.NewGuid(), Title = "Req 7", Complexity = 5, Status = RequestStatusNames.Captured, UpdatedAt = today }
        };

        // Act (cOrg = 5.0 pt/day)
        var telemetry = _calculator.CalculateTelemetry(package, requests, cOrg: 5.0, asOfDateUtc: today);

        // Assert
        telemetry.TotalComplexity.Should().Be(32);
        telemetry.CompletedComplexity.Should().Be(4);
        telemetry.RemainingComplexity.Should().Be(28);
        telemetry.TotalRequestsCount.Should().Be(7);
        telemetry.RemainingRequestsCount.Should().Be(6); // 1 completed, 6 remaining (28 complexity)
        telemetry.WorkingDaysRemaining.Should().Be(5);

        // Daily Burn: 28 / 5 = 5.6 pts/day
        telemetry.RequiredDailyBurn.Should().Be(5.6);

        // Org Capacity Share: (5.6 / 5.0) * 100 = 112% -> IMPOSSIBLE
        telemetry.OrgCapacityShare.Should().Be(112.0);
        telemetry.PressureTier.Should().Be(WorkPackagePressureTiers.Impossible);
    }

    [Fact]
    public void CalculateTelemetry_WhenDeadlineBreached_SetsDeadlineBreachedInvariant()
    {
        // Arrange: deadline was yesterday
        var today = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
        var pastDeadline = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Breached Package",
            objective: "Missed deadline",
            ownerPersonId: _ownerId,
            deadline: pastDeadline);

        var requests = new List<RequestDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Incomplete", Complexity = 4, Status = RequestStatusNames.InProgress, UpdatedAt = today }
        };

        // Act
        var telemetry = _calculator.CalculateTelemetry(package, requests, cOrg: 2.0, asOfDateUtc: today);

        // Assert
        telemetry.WorkingDaysRemaining.Should().Be(0);
        telemetry.DeadlineBreached.Should().BeTrue();
        telemetry.HealthState.Should().Be(WorkPackageHealthStates.DeadlineBreached);
        // Daily burn equals remaining complexity when days <= 0
        telemetry.RequiredDailyBurn.Should().Be(4.0);
    }

    [Fact]
    public void CalculateTelemetry_WhenDeadlineInPast_ButAllRequestsCompleted_NotBreached()
    {
        // Arrange
        var today = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
        var pastDeadline = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Finished Package",
            objective: "All completed",
            ownerPersonId: _ownerId,
            deadline: pastDeadline);

        var requests = new List<RequestDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Done", Complexity = 3, Status = RequestStatusNames.Completed, UpdatedAt = today }
        };

        // Act
        var telemetry = _calculator.CalculateTelemetry(package, requests, cOrg: 2.0, asOfDateUtc: today);

        // Assert
        telemetry.RemainingRequestsCount.Should().Be(0);
        telemetry.DeadlineBreached.Should().BeFalse();
        telemetry.HealthState.Should().Be(WorkPackageHealthStates.Flowing);
    }

    [Fact]
    public void CalculateTelemetry_WhenRequestsPaused_IdentifiesActiveBlockers()
    {
        // Arrange
        var today = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
        var futureDeadline = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Blocked Package",
            objective: "Paused request impediment",
            ownerPersonId: _ownerId,
            deadline: futureDeadline);

        var requests = new List<RequestDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Paused Req", Complexity = 2, Status = RequestStatusNames.Paused, UpdatedAt = today },
            new() { Id = Guid.NewGuid(), Title = "Active Req", Complexity = 3, Status = RequestStatusNames.InProgress, UpdatedAt = today }
        };

        // Act
        var telemetry = _calculator.CalculateTelemetry(package, requests, cOrg: 5.0, asOfDateUtc: today);

        // Assert
        telemetry.BlockedRequestsCount.Should().Be(1);
        telemetry.HealthState.Should().Be(WorkPackageHealthStates.ActiveBlockers);
    }

    [Fact]
    public void CalculateTelemetry_WhenIdleMoreThanFiveBusinessDays_IdentifiesDormant()
    {
        // Arrange: Today is Oct 7, latest event was Sep 24 (9.5 business days ago)
        var today = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        var oldEventDate = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Dormant Package",
            objective: "No events for 9.5 business days",
            ownerPersonId: _ownerId,
            createdAtUtc: oldEventDate,
            deadline: new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));

        var requests = new List<RequestDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Old Req", Complexity = 2, Status = RequestStatusNames.Captured, CreatedAt = oldEventDate, UpdatedAt = oldEventDate }
        };

        // Act
        var telemetry = _calculator.CalculateTelemetry(package, requests, cOrg: 5.0, asOfDateUtc: today);

        // Assert
        telemetry.IsDormant.Should().BeTrue();
        telemetry.DormantDays.Should().Be(9.5);
        telemetry.HealthState.Should().Be(WorkPackageHealthStates.Dormant);
    }

    [Fact]
    public void CalculateTelemetry_WhenInProgressLongerThanFourteenDays_IdentifiesWipStagnant()
    {
        // Arrange: Today is Oct 20, request was started on Oct 1 (19 calendar days ago)
        var today = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);
        var startedDate = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

        var package = Cakra.Modules.WorkPackage.Domain.WorkPackage.Create(
            name: "Stagnant WIP Package",
            objective: "In progress for 19 days",
            ownerPersonId: _ownerId,
            createdAtUtc: today,
            deadline: new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc));

        var requests = new List<RequestDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Long In-Progress", Complexity = 3, Status = RequestStatusNames.InProgress, CreatedAt = startedDate, UpdatedAt = startedDate }
        };

        // Act
        var telemetry = _calculator.CalculateTelemetry(package, requests, cOrg: 5.0, asOfDateUtc: today);

        // Assert
        telemetry.ActiveWipCount.Should().Be(1);
        telemetry.OldestActiveWipDays.Should().Be(19.0);
        telemetry.IsWipStagnant.Should().BeTrue();
        telemetry.HealthState.Should().Be(WorkPackageHealthStates.WipStagnant);
    }

    [Fact]
    public void Generate14DayBarcode_ProducesFourteenConsecutiveDays_WithEventCounts()
    {
        // Arrange
        var today = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
        var twoDaysAgo = today.Date.AddDays(-2);

        var requests = new List<RequestDto>
        {
            new() { Id = Guid.NewGuid(), Status = RequestStatusNames.Completed, UpdatedAt = today },
            new() { Id = Guid.NewGuid(), Status = RequestStatusNames.Paused, UpdatedAt = twoDaysAgo }
        };

        // Act
        var barcode = WorkPackageTelemetryCalculator.Generate14DayBarcode(requests, today);

        // Assert
        barcode.Should().HaveCount(14);
        barcode.Last().Date.Should().Be("2026-10-07");
        barcode.Last().ClosedCount.Should().Be(1);
        barcode.Last().StateMutationCount.Should().Be(1);

        var dayTwoDaysAgo = barcode.First(b => b.Date == twoDaysAgo.ToString("yyyy-MM-dd"));
        dayTwoDaysAgo.BlockedCount.Should().Be(1);
        dayTwoDaysAgo.StateMutationCount.Should().Be(1);
    }

    [Fact]
    public void CalculateTelemetry_FromWorkPackageDto_ProducesConsistentModel()
    {
        // Arrange
        var today = new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);
        var dto = new WorkPackageDto
        {
            Id = Guid.NewGuid(),
            Name = "DTO Package",
            Objective = "From DTO",
            Status = "ACTIVE",
            OwnerPersonId = _ownerId,
            OwnerName = "Budi Sutrisno",
            CustomerId = _customerId,
            CustomerName = "RSUD Gambir",
            ProductId = _productId,
            ProductName = "Billing Core",
            Deadline = today.AddDays(7),
            CreatedAt = today.AddDays(-5),
            Requests = new List<WorkPackageScopeItemDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    RequestId = Guid.NewGuid(),
                    Title = "Scope Item 1",
                    Status = RequestStatusNames.InProgress,
                    CreatedAt = today.AddDays(-2),
                    UpdatedAt = today,
                    Request = new RequestDto
                    {
                        Id = Guid.NewGuid(),
                        Title = "Scope Item 1",
                        Complexity = 3,
                        Status = RequestStatusNames.InProgress,
                        UpdatedAt = today
                    }
                }
            }
        };

        // Act
        var telemetry = _calculator.CalculateTelemetry(dto, cOrg: 3.0, asOfDateUtc: today);

        // Assert
        telemetry.Id.Should().Be(dto.Id);
        telemetry.Name.Should().Be("DTO Package");
        telemetry.OwnerName.Should().Be("Budi Sutrisno");
        telemetry.CustomerName.Should().Be("RSUD Gambir");
        telemetry.ProductName.Should().Be("Billing Core");
        telemetry.TotalComplexity.Should().Be(3);
        telemetry.RemainingComplexity.Should().Be(3);
        telemetry.PressureTier.Should().Be(WorkPackagePressureTiers.Elevated);
    }

    [Fact]
    public void BuildMatrix_AggregatesCountsAcrossTiersAndHealthStates()
    {
        // Arrange
        var packages = new List<WorkPackageTelemetryDto>
        {
            new() { Id = Guid.NewGuid(), HealthState = WorkPackageHealthStates.DeadlineBreached, PressureTier = WorkPackagePressureTiers.Impossible },
            new() { Id = Guid.NewGuid(), HealthState = WorkPackageHealthStates.ActiveBlockers, PressureTier = WorkPackagePressureTiers.Elevated },
            new() { Id = Guid.NewGuid(), HealthState = WorkPackageHealthStates.ActiveBlockers, PressureTier = WorkPackagePressureTiers.Unplanned },
            new() { Id = Guid.NewGuid(), HealthState = WorkPackageHealthStates.Flowing, PressureTier = WorkPackagePressureTiers.Nominal },
            new() { Id = Guid.NewGuid(), HealthState = WorkPackageHealthStates.Flowing, PressureTier = WorkPackagePressureTiers.Nominal }
        };

        // Act
        var matrix = _calculator.BuildMatrix(packages);

        // Assert
        matrix.Cells[WorkPackageHealthStates.DeadlineBreached][WorkPackagePressureTiers.Impossible].Should().Be(1);
        matrix.Cells[WorkPackageHealthStates.ActiveBlockers][WorkPackagePressureTiers.Elevated].Should().Be(1);
        matrix.Cells[WorkPackageHealthStates.ActiveBlockers][WorkPackagePressureTiers.Unplanned].Should().Be(1);
        matrix.Cells[WorkPackageHealthStates.Flowing][WorkPackagePressureTiers.Nominal].Should().Be(2);

        matrix.RowTotals[WorkPackageHealthStates.Flowing].Should().Be(2);
        matrix.RowTotals[WorkPackageHealthStates.ActiveBlockers].Should().Be(2);
        matrix.RowTotals[WorkPackageHealthStates.DeadlineBreached].Should().Be(1);
        matrix.RowTotals[WorkPackageHealthStates.Dormant].Should().Be(0);

        matrix.ColumnTotals[WorkPackagePressureTiers.Nominal].Should().Be(2);
        matrix.ColumnTotals[WorkPackagePressureTiers.Elevated].Should().Be(1);
        matrix.ColumnTotals[WorkPackagePressureTiers.Impossible].Should().Be(1);
        matrix.ColumnTotals[WorkPackagePressureTiers.Unplanned].Should().Be(1);
        matrix.ColumnTotals[WorkPackagePressureTiers.Critical].Should().Be(0);
    }

    [Fact]
    public void CalculatePortfolioMetrics_CalculatesAggregateLoadAndCounts()
    {
        // Arrange
        var packages = new List<WorkPackageTelemetryDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Deadline = DateTime.UtcNow.AddDays(5),
                RequiredDailyBurn = 3.0,
                HealthState = WorkPackageHealthStates.Flowing
            },
            new()
            {
                Id = Guid.NewGuid(),
                Deadline = DateTime.UtcNow.AddDays(5),
                RequiredDailyBurn = 2.0,
                HealthState = WorkPackageHealthStates.ActiveBlockers
            },
            new()
            {
                Id = Guid.NewGuid(),
                Deadline = null,
                RequiredDailyBurn = null,
                HealthState = WorkPackageHealthStates.Flowing
            }
        };

        // Act (cOrg = 4.0)
        var metrics = _calculator.CalculatePortfolioMetrics(packages, cOrg: 4.0);

        // Assert
        metrics.TotalActiveWorkPackages.Should().Be(3);
        metrics.WithDeadlineCount.Should().Be(2);
        metrics.WithoutDeadlineCount.Should().Be(1);
        metrics.OrgDailyThroughput.Should().Be(4.0);
        // Total burn = 3.0 + 2.0 = 5.0. Load % = (5.0 / 4.0) * 100 = 125.0%
        metrics.PortfolioAggregateLoadPercentage.Should().Be(125.0);
        metrics.TotalInvariantViolationsCount.Should().Be(1);
    }
}
