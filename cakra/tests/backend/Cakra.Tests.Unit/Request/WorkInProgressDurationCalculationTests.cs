using Cakra.Modules.Request;
using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Services;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Request;

/// <summary>
/// Unit tests for Change Request CR-021 §4 TD-002:
/// - Cumulative in-progress duration calculation across start/pause cycles
/// - Dynamic real-time ongoing duration calculation for currently active tasks
/// - Formatted duration string generation ("Xh Ym") and decimal hours rounding
/// - Public API contracts and MediatR handlers for WIP overview
/// </summary>
public sealed class WorkInProgressDurationCalculationTests
{
    [Fact]
    public void Published_query_contracts_for_wip_are_publicly_accessible()
    {
        typeof(TaskWorkInProgressDto).IsPublic.Should().BeTrue();
        typeof(PersonWorkInProgressDto).IsPublic.Should().BeTrue();
        typeof(GetWorkInProgressOverviewQuery).IsPublic.Should().BeTrue();

        var method = typeof(IRequestQueryService).GetMethod(nameof(IRequestQueryService.GetWorkInProgressOverviewAsync));
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<IReadOnlyList<PersonWorkInProgressDto>>));
    }

    [Fact]
    public void CalculateInProgressDuration_returns_zero_when_no_assignments_for_paused_task()
    {
        var createdAt = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        var referenceUtc = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        var (totalSeconds, totalHours, formatted, lastStartedAt) = RequestQueryService.CalculateInProgressDuration(
            status: RequestStatusNames.Paused,
            createdAt: createdAt,
            updatedAt: updatedAt,
            assignments: null,
            referenceUtc: referenceUtc);

        totalSeconds.Should().Be(0);
        totalHours.Should().Be(0);
        formatted.Should().Be("0h 0m");
        lastStartedAt.Should().BeNull();
    }

    [Fact]
    public void CalculateInProgressDuration_fallback_for_in_progress_without_assignments()
    {
        var createdAt = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        var referenceUtc = new DateTime(2026, 10, 1, 9, 45, 0, DateTimeKind.Utc); // 45m after updatedAt

        var (totalSeconds, totalHours, formatted, lastStartedAt) = RequestQueryService.CalculateInProgressDuration(
            status: RequestStatusNames.InProgress,
            createdAt: createdAt,
            updatedAt: updatedAt,
            assignments: Array.Empty<RequestAssignmentDto>(),
            referenceUtc: referenceUtc);

        totalSeconds.Should().Be(2700);
        totalHours.Should().Be(0.75);
        formatted.Should().Be("0h 45m");
        lastStartedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void CalculateInProgressDuration_single_completed_interval_paused_task()
    {
        var reqId = Guid.NewGuid();
        var startTime = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
        var pauseTime = new DateTime(2026, 10, 1, 11, 30, 0, DateTimeKind.Utc); // 1.5h = 5400s
        var referenceUtc = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc);

        var assignments = new List<RequestAssignmentDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                RequestId = reqId,
                NewStatus = RequestStatusNames.InProgress,
                AssignedAtUtc = startTime
            },
            new()
            {
                Id = Guid.NewGuid(),
                RequestId = reqId,
                NewStatus = RequestStatusNames.Paused,
                AssignedAtUtc = pauseTime
            }
        };

        var (totalSeconds, totalHours, formatted, lastStartedAt) = RequestQueryService.CalculateInProgressDuration(
            status: RequestStatusNames.Paused,
            createdAt: startTime.AddHours(-1),
            updatedAt: pauseTime,
            assignments: assignments,
            referenceUtc: referenceUtc);

        totalSeconds.Should().Be(5400);
        totalHours.Should().Be(1.5);
        formatted.Should().Be("1h 30m");
        lastStartedAt.Should().Be(startTime);
    }

    [Fact]
    public void CalculateInProgressDuration_single_ongoing_active_interval()
    {
        var reqId = Guid.NewGuid();
        var startTime = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
        var referenceUtc = new DateTime(2026, 10, 1, 10, 45, 0, DateTimeKind.Utc); // 45m active = 2700s

        var assignments = new List<RequestAssignmentDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                RequestId = reqId,
                NewStatus = RequestStatusNames.InProgress,
                AssignedAtUtc = startTime
            }
        };

        var (totalSeconds, totalHours, formatted, lastStartedAt) = RequestQueryService.CalculateInProgressDuration(
            status: RequestStatusNames.InProgress,
            createdAt: startTime.AddHours(-1),
            updatedAt: startTime,
            assignments: assignments,
            referenceUtc: referenceUtc);

        totalSeconds.Should().Be(2700);
        totalHours.Should().Be(0.75);
        formatted.Should().Be("0h 45m");
        lastStartedAt.Should().Be(startTime);
    }

    [Fact]
    public void CalculateInProgressDuration_multi_cycle_start_pause_start_with_active_interval()
    {
        var reqId = Guid.NewGuid();
        var t1Start = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var t1Pause = new DateTime(2026, 10, 1, 9, 30, 0, DateTimeKind.Utc);  // 1.5h = 5400s

        var t2Start = new DateTime(2026, 10, 1, 10, 30, 0, DateTimeKind.Utc);
        var t2Pause = new DateTime(2026, 10, 1, 11, 30, 0, DateTimeKind.Utc); // 1.0h = 3600s

        var t3Start = new DateTime(2026, 10, 1, 13, 0, 0, DateTimeKind.Utc);
        var referenceUtc = new DateTime(2026, 10, 1, 13, 45, 0, DateTimeKind.Utc); // 0.75h = 2700s

        var assignments = new List<RequestAssignmentDto>
        {
            new() { Id = Guid.NewGuid(), RequestId = reqId, NewStatus = RequestStatusNames.InProgress, AssignedAtUtc = t1Start },
            new() { Id = Guid.NewGuid(), RequestId = reqId, NewStatus = RequestStatusNames.Paused, AssignedAtUtc = t1Pause },
            new() { Id = Guid.NewGuid(), RequestId = reqId, NewStatus = RequestStatusNames.InProgress, AssignedAtUtc = t2Start },
            new() { Id = Guid.NewGuid(), RequestId = reqId, NewStatus = RequestStatusNames.Paused, AssignedAtUtc = t2Pause },
            new() { Id = Guid.NewGuid(), RequestId = reqId, NewStatus = RequestStatusNames.InProgress, AssignedAtUtc = t3Start }
        };

        var (totalSeconds, totalHours, formatted, lastStartedAt) = RequestQueryService.CalculateInProgressDuration(
            status: RequestStatusNames.InProgress,
            createdAt: t1Start.AddHours(-2),
            updatedAt: t3Start,
            assignments: assignments,
            referenceUtc: referenceUtc);

        // 5400 + 3600 + 2700 = 11700s = 3.25h = 3h 15m
        totalSeconds.Should().Be(11700);
        totalHours.Should().Be(3.25);
        formatted.Should().Be("3h 15m");
        lastStartedAt.Should().Be(t3Start);
    }

    [Fact]
    public void CalculateInProgressDuration_handles_large_durations_exceeding_24_hours_without_wrapping()
    {
        var reqId = Guid.NewGuid();
        var startTime = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var referenceUtc = startTime.AddHours(26).AddMinutes(15); // 26h 15m = 94500s

        var assignments = new List<RequestAssignmentDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                RequestId = reqId,
                NewStatus = RequestStatusNames.InProgress,
                AssignedAtUtc = startTime
            }
        };

        var (totalSeconds, totalHours, formatted, lastStartedAt) = RequestQueryService.CalculateInProgressDuration(
            status: RequestStatusNames.InProgress,
            createdAt: startTime,
            updatedAt: startTime,
            assignments: assignments,
            referenceUtc: referenceUtc);

        totalSeconds.Should().Be(94500);
        totalHours.Should().Be(26.25);
        formatted.Should().Be("26h 15m");
        lastStartedAt.Should().Be(startTime);
    }

    [Fact]
    public void CalculateInProgressDuration_ignores_intervals_while_reassigned_to_assigned_state()
    {
        var reqId = Guid.NewGuid();
        var t1Start = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        var t1Reassign = new DateTime(2026, 10, 1, 9, 30, 0, DateTimeKind.Utc); // 30m in progress = 1800s

        var t2Start = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);   // 30m idle ASSIGNED not counted
        var t2Pause = new DateTime(2026, 10, 1, 11, 0, 0, DateTimeKind.Utc);   // 60m in progress = 3600s

        var assignments = new List<RequestAssignmentDto>
        {
            new() { Id = Guid.NewGuid(), RequestId = reqId, NewStatus = RequestStatusNames.InProgress, AssignedAtUtc = t1Start },
            new() { Id = Guid.NewGuid(), RequestId = reqId, NewStatus = RequestStatusNames.Assigned, AssignedAtUtc = t1Reassign },
            new() { Id = Guid.NewGuid(), RequestId = reqId, NewStatus = RequestStatusNames.InProgress, AssignedAtUtc = t2Start },
            new() { Id = Guid.NewGuid(), RequestId = reqId, NewStatus = RequestStatusNames.Paused, AssignedAtUtc = t2Pause }
        };

        var (totalSeconds, totalHours, formatted, lastStartedAt) = RequestQueryService.CalculateInProgressDuration(
            status: RequestStatusNames.Paused,
            createdAt: t1Start.AddHours(-1),
            updatedAt: t2Pause,
            assignments: assignments,
            referenceUtc: t2Pause.AddHours(2));

        // 1800 + 3600 = 5400s = 1.5h = 1h 30m
        totalSeconds.Should().Be(5400);
        totalHours.Should().Be(1.5);
        formatted.Should().Be("1h 30m");
        lastStartedAt.Should().Be(t2Start);
    }

    [Fact]
    public void CalculateInProgressDuration_guards_against_clock_skew_negative_deltas()
    {
        var reqId = Guid.NewGuid();
        var startTime = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
        var earlierEnd = new DateTime(2026, 10, 1, 9, 59, 0, DateTimeKind.Utc); // -1 minute anomaly

        var assignments = new List<RequestAssignmentDto>
        {
            new() { Id = Guid.NewGuid(), RequestId = reqId, NewStatus = RequestStatusNames.InProgress, AssignedAtUtc = startTime },
            new() { Id = Guid.NewGuid(), RequestId = reqId, NewStatus = RequestStatusNames.Paused, AssignedAtUtc = earlierEnd }
        };

        var (totalSeconds, totalHours, formatted, _) = RequestQueryService.CalculateInProgressDuration(
            status: RequestStatusNames.Paused,
            createdAt: startTime,
            updatedAt: earlierEnd,
            assignments: assignments,
            referenceUtc: startTime);

        totalSeconds.Should().Be(0);
        totalHours.Should().Be(0);
        formatted.Should().Be("0h 0m");
    }
}
