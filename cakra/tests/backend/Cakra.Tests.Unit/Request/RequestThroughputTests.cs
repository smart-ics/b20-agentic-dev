using Cakra.Core;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Request;
using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Persistence;
using Cakra.Modules.Request.Services;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Request;

/// <summary>
/// Unit tests for Organization Demonstrated Daily Throughput Service (C_org)
/// per Architecture CR-025 §4 TD-001 and Plan Slice P1-S01:
/// - Contract compliance on IRequestQueryService and RequestQueryService
/// - Complexity sum division by rolling window days
/// - Safety floor behavior (minimum 1.0 pt/day when throughput <= 0.0)
/// - Timestamp boundary calculation (UtcNow - windowDays)
/// - Parameter edge cases (null, negative, or zero window)
/// </summary>
public sealed class RequestThroughputTests
{
    [Fact]
    public void Contract_method_and_convenience_alias_exist_on_IRequestQueryService_and_RequestQueryService()
    {
        var interfaceMethod = typeof(IRequestQueryService).GetMethod(nameof(IRequestQueryService.GetOrgDemonstratedDailyThroughputAsync));
        interfaceMethod.Should().NotBeNull();
        interfaceMethod!.ReturnType.Should().Be(typeof(Task<double>));

        var interfaceAlias = typeof(IRequestQueryService).GetMethod(nameof(IRequestQueryService.GetOrgDemonstratedDailyThroughput));
        interfaceAlias.Should().NotBeNull();
        interfaceAlias!.ReturnType.Should().Be(typeof(Task<double>));

        var serviceMethod = typeof(RequestQueryService).GetMethod(nameof(RequestQueryService.GetOrgDemonstratedDailyThroughputAsync));
        serviceMethod.Should().NotBeNull();
        serviceMethod!.ReturnType.Should().Be(typeof(Task<double>));

        var serviceAlias = typeof(RequestQueryService).GetMethod(nameof(RequestQueryService.GetOrgDemonstratedDailyThroughput));
        serviceAlias.Should().NotBeNull();
        serviceAlias!.ReturnType.Should().Be(typeof(Task<double>));
    }

    [Fact]
    public async Task Calculates_daily_burn_rate_correctly_for_standard_30_day_window()
    {
        var repo = new FakeThroughputRequestRepository { ComplexitySumToReturn = 60 };
        var clock = new FakeSystemClock(new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc));
        var sut = CreateService(repo, clock);

        var throughput = await sut.GetOrgDemonstratedDailyThroughputAsync(30);

        throughput.Should().Be(2.0); // 60 pts / 30 days = 2.0 pts/day
        repo.RecordedSinceUtc.Should().Be(clock.UtcNow.AddDays(-30));
    }

    [Fact]
    public async Task Calculates_daily_burn_rate_correctly_for_custom_window_days()
    {
        var repo = new FakeThroughputRequestRepository { ComplexitySumToReturn = 45 };
        var clock = new FakeSystemClock(new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc));
        var sut = CreateService(repo, clock);

        var throughput = await sut.GetOrgDemonstratedDailyThroughputAsync(15);

        throughput.Should().Be(3.0); // 45 pts / 15 days = 3.0 pts/day
        repo.RecordedSinceUtc.Should().Be(clock.UtcNow.AddDays(-15));
    }

    [Fact]
    public async Task Applies_safety_floor_of_1_point_per_day_when_completed_complexity_is_zero()
    {
        var repo = new FakeThroughputRequestRepository { ComplexitySumToReturn = 0 };
        var clock = new FakeSystemClock(new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc));
        var sut = CreateService(repo, clock);

        var throughput = await sut.GetOrgDemonstratedDailyThroughputAsync(30);

        throughput.Should().Be(1.0); // Safety floor returned to avoid division by zero downstream
    }

    [Fact]
    public async Task Applies_safety_floor_of_1_point_per_day_when_completed_complexity_is_negative()
    {
        var repo = new FakeThroughputRequestRepository { ComplexitySumToReturn = -10 };
        var clock = new FakeSystemClock(new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc));
        var sut = CreateService(repo, clock);

        var throughput = await sut.GetOrgDemonstratedDailyThroughputAsync(30);

        throughput.Should().Be(1.0); // Throughput <= 0 triggers safety floor
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-30)]
    public async Task Returns_safety_floor_when_window_days_is_zero_or_negative(int invalidWindowDays)
    {
        var repo = new FakeThroughputRequestRepository { ComplexitySumToReturn = 100 };
        var clock = new FakeSystemClock(new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc));
        var sut = CreateService(repo, clock);

        var throughput = await sut.GetOrgDemonstratedDailyThroughputAsync(invalidWindowDays);

        throughput.Should().Be(1.0);
        repo.RecordedSinceUtc.Should().BeNull("repository query should not execute for non-positive window days");
    }

    [Fact]
    public async Task Default_parameters_use_30_day_rolling_window()
    {
        var repo = new FakeThroughputRequestRepository { ComplexitySumToReturn = 90 };
        var clock = new FakeSystemClock(new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc));
        var sut = CreateService(repo, clock);

        var throughput = await sut.GetOrgDemonstratedDailyThroughputAsync();

        throughput.Should().Be(3.0); // 90 / 30 = 3.0
        repo.RecordedSinceUtc.Should().Be(clock.UtcNow.AddDays(-30));
    }

    [Fact]
    public async Task Synchronous_convenience_alias_returns_identical_result()
    {
        var repo = new FakeThroughputRequestRepository { ComplexitySumToReturn = 75 };
        var clock = new FakeSystemClock(new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc));
        var sut = CreateService(repo, clock);

        var asyncResult = await sut.GetOrgDemonstratedDailyThroughputAsync(25);
        var aliasResult = await sut.GetOrgDemonstratedDailyThroughput(25);

        asyncResult.Should().Be(3.0);
        aliasResult.Should().Be(asyncResult);
    }

    private static RequestQueryService CreateService(IRequestRepository repo, ISystemClock clock)
    {
        var fakeConnFactory = new FakeConnectionFactory();
        return new RequestQueryService(
            connectionFactory: fakeConnFactory,
            currentContextProvider: null,
            organizationQueryService: null,
            customerQueryService: null,
            productQueryService: null,
            requestRepository: repo,
            clock: clock);
    }

    private sealed class FakeThroughputRequestRepository : IRequestRepository
    {
        public int ComplexitySumToReturn { get; set; }
        public DateTime? RecordedSinceUtc { get; private set; }

        public Task<int> GetCompletedComplexitySumSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
        {
            RecordedSinceUtc = sinceUtc;
            return Task.FromResult(ComplexitySumToReturn);
        }

        public Task<Cakra.Modules.Request.Domain.Request?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Cakra.Modules.Request.Domain.Request?>(null);

        public Task<IReadOnlyList<Cakra.Modules.Request.Domain.Request>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Cakra.Modules.Request.Domain.Request>>(Array.Empty<Cakra.Modules.Request.Domain.Request>());

        public Task AddAsync(Cakra.Modules.Request.Domain.Request entity, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(Cakra.Modules.Request.Domain.Request entity, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AddAssignmentAsync(RequestAssignment assignment, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<RequestAssignment>> GetAssignmentsByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestAssignment>>(Array.Empty<RequestAssignment>());

        public Task AddResolutionAsync(RequestResolution resolution, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<RequestResolution?> GetResolutionByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<RequestResolution?>(null);

        public Task<Cakra.Modules.Request.Domain.Request?> GetActiveInProgressByOwnerAsync(Guid ownerPersonId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Cakra.Modules.Request.Domain.Request?>(null);
    }

    private sealed class FakeSystemClock : ISystemClock
    {
        public DateTime UtcNow { get; }

        public FakeSystemClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }
    }

    private sealed class FakeConnectionFactory : IDbConnectionFactory
    {
        public System.Data.IDbConnection CreateConnection() => throw new NotSupportedException();
    }
}
