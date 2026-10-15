using Cakra.Modules.Request;
using Cakra.Modules.Request.Services;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Request;

/// <summary>
/// Unit tests for CR-034 §4 TD-001:
/// - Complexity to budget hours mapping (1=2h, 2=4h, 3=8h, 4=16h, 5=24h, fallback=2h)
/// - Budget burn percentage calculation with standard rounding
/// - IsOverBudget threshold determination (TotalInProgressHours >= BudgetHours)
/// - Zero divide-by-zero guard when budget hours is 0
/// - TaskWorkInProgressDto contract defaults
/// </summary>
public sealed class WorkInProgressBudgetBurnTests
{
    [Fact]
    public void TaskWorkInProgressDto_has_correct_defaults()
    {
        var dto = new TaskWorkInProgressDto();

        dto.Complexity.Should().Be(1);
        dto.BudgetHours.Should().Be(2.0);
        dto.BurnPercentage.Should().Be(0);
        dto.IsOverBudget.Should().BeFalse();
    }

    [Theory]
    [InlineData(1, 2.0)]
    [InlineData(2, 4.0)]
    [InlineData(3, 8.0)]
    [InlineData(4, 16.0)]
    [InlineData(5, 24.0)]
    [InlineData(0, 2.0)]
    [InlineData(-1, 2.0)]
    [InlineData(6, 2.0)]
    [InlineData(99, 2.0)]
    public void GetBudgetHoursForComplexity_maps_expected_hours(int complexity, double expectedHours)
    {
        var result = RequestQueryService.GetBudgetHoursForComplexity(complexity);
        result.Should().Be(expectedHours);
    }

    [Theory]
    [InlineData(0.0, 2.0, 0)]
    [InlineData(1.0, 2.0, 50)]
    [InlineData(2.0, 2.0, 100)]
    [InlineData(3.0, 2.0, 150)]
    [InlineData(2.0, 8.0, 25)]
    [InlineData(3.0, 8.0, 38)] // 37.5 rounds to 38
    [InlineData(4.0, 8.0, 50)]
    [InlineData(6.0, 8.0, 75)]
    [InlineData(8.0, 8.0, 100)]
    [InlineData(11.0, 8.0, 138)] // 137.5 rounds to 138
    [InlineData(24.0, 24.0, 100)]
    [InlineData(25.5, 24.0, 106)] // 106.25 rounds to 106
    public void CalculateBurnMetrics_computes_correct_burn_percentage(double totalHours, double budgetHours, int expectedBurnPercentage)
    {
        var (burnPercentage, _) = RequestQueryService.CalculateBurnMetrics(totalHours, budgetHours);
        burnPercentage.Should().Be(expectedBurnPercentage);
        burnPercentage.Should().Be((int)Math.Round((totalHours / budgetHours) * 100.0));
    }

    [Theory]
    [InlineData(1.99, 2.0, false)]
    [InlineData(2.0, 2.0, true)]
    [InlineData(2.01, 2.0, true)]
    [InlineData(7.99, 8.0, false)]
    [InlineData(8.0, 8.0, true)]
    [InlineData(8.01, 8.0, true)]
    [InlineData(23.99, 24.0, false)]
    [InlineData(24.0, 24.0, true)]
    [InlineData(24.01, 24.0, true)]
    public void CalculateBurnMetrics_determines_isOverBudget_accurately(double totalHours, double budgetHours, bool expectedIsOverBudget)
    {
        var (_, isOverBudget) = RequestQueryService.CalculateBurnMetrics(totalHours, budgetHours);
        isOverBudget.Should().Be(expectedIsOverBudget);
    }

    [Theory]
    [InlineData(5.0, 0.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(2.0, -1.0)]
    public void CalculateBurnMetrics_safely_handles_zero_or_negative_budget_hours(double totalHours, double budgetHours)
    {
        var act = () => RequestQueryService.CalculateBurnMetrics(totalHours, budgetHours);
        act.Should().NotThrow<DivideByZeroException>();

        var (burnPercentage, isOverBudget) = act();
        burnPercentage.Should().Be(0);
        isOverBudget.Should().Be(totalHours >= budgetHours);
    }
}
