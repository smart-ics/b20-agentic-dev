using Cakra.Api.Infrastructure.Context;
using Cakra.Api.Infrastructure.Migrations;
using Cakra.Core;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Analytics;
using Cakra.Modules.Analytics.Services;
using Cakra.Modules.Customer.Services;
using Cakra.Modules.Organization.Commands;
using Cakra.Modules.Product.Services;
using Cakra.Modules.Request.Services;
using Dapper;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Respawn;
using Respawn.Graph;
using Xunit;

namespace Cakra.Tests.Integration.Analytics;

/// <summary>
/// Integration tests verifying Slice P7-S34: Analytics Module — Snapshot Tables &amp; Snapshot Job
/// (Architecture §13, §17, §19.3, §19.7, §19.8, §20):
/// - Migration <c>0010_analytics_tables.sql</c> creates <c>analytics.DailyWorkloadSnapshots</c> and
///   <c>analytics.MonthlyCustomerPerformanceSnapshots</c> with <c>UNIQUE(SnapshotDate, PersonId)</c>
///   and <c>UNIQUE(YearMonth, CustomerId)</c> and zero cross-schema foreign keys.
/// - <c>AnalyticsSnapshotJob</c> (registered as <c>IHostedService</c> / <c>BackgroundService</c>) triggers
///   daily workload snapshots and monthly customer performance snapshots with accurate Dapper metrics.
/// - Snapshot immutability is preserved on repeat daily/monthly runs, while
///   <c>ManagementAnalyticsService.RecomputeSnapshots(startDate, endDate)</c> performs idempotent backfill.
/// </summary>
public class AnalyticsSnapshotIntegrationTests : IAsyncLifetime
{
    private const string LocalDbFallback =
        "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb_Analytics;Integrated Security=true;TrustServerCertificate=True;";

    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private bool _sqlServerAvailable;

    public AnalyticsSnapshotIntegrationTests()
    {
        var baseConnection = DatabaseResetHelper.ResolveConnectionString() ?? LocalDbFallback;
        _connectionString = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = "CakraTestDb_Analytics"
        }.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var masterConnectionString = new SqlConnectionStringBuilder(_connectionString)
            {
                InitialCatalog = "master"
            }.ConnectionString;

            await using var probeConn = new SqlConnection(masterConnectionString);
            await probeConn.OpenAsync();
            _sqlServerAvailable = true;
        }
        catch
        {
            _sqlServerAvailable = false;
            return;
        }

        var runner = new DatabaseMigrationRunner(_connectionString, NullLogger<DatabaseMigrationRunner>.Instance);
        var migrationResult = runner.Run();
        migrationResult.Successful.Should().BeTrue("DbUp migrations including 0010_analytics_tables.sql must succeed");

        await using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
        }

        _factory = new CakraWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
                builder.UseSetting("ConnectionStrings:TestConnection", _connectionString);
            });

        await ResetTablesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_sqlServerAvailable)
        {
            await ResetTablesAsync();
        }

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private async Task ResetTablesAsync()
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = ["analytics", "request", "product", "customer", "organization"],
            TablesToIgnore = [new Table("dbo", "__SchemaVersions"), new Table("dbo", "SchemaVersions")]
        });

        await respawner.ResetAsync(conn);
    }

    [Fact]
    public async Task Migration_0010_creates_analytics_snapshot_tables_with_unique_constraints_and_zero_cross_schema_fks()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var tables = (await conn.QueryAsync<string>(
            """
            SELECT t.name
            FROM sys.tables t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = 'analytics'
            ORDER BY t.name;
            """)).ToList();

        tables.Should().Contain(new[]
        {
            "DailyWorkloadSnapshots",
            "MonthlyCustomerPerformanceSnapshots"
        });

        var crossSchemaFkCount = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(1)
            FROM sys.foreign_keys fk
            INNER JOIN sys.tables parentTable ON parentTable.object_id = fk.parent_object_id
            INNER JOIN sys.schemas parentSchema ON parentSchema.schema_id = parentTable.schema_id
            WHERE parentSchema.name = 'analytics';
            """);

        crossSchemaFkCount.Should().Be(0, "Architecture §20 prohibits cross-schema foreign keys on analytics tables");

        // Verify UNIQUE(SnapshotDate, PersonId) on analytics.DailyWorkloadSnapshots
        var personId = Guid.NewGuid();
        var snapshotDate = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);

        const string insertDailySql = """
            INSERT INTO [analytics].[DailyWorkloadSnapshots] (
                [SnapshotId], [SnapshotDate], [PersonId],
                [ActiveRequestsCount], [EscalatedRequestsCount], [StalledRequestsCount],
                [CompletedRequestsToday], [AvgAgeHours], [CapturedAt]
            )
            VALUES (
                @SnapshotId, @SnapshotDate, @PersonId,
                2, 1, 0, 1, 12.50, SYSUTCDATETIME()
            );
            """;

        await conn.ExecuteAsync(insertDailySql, new
        {
            SnapshotId = Guid.NewGuid(),
            SnapshotDate = snapshotDate,
            PersonId = personId
        });

        Func<Task> duplicateDailyAct = async () => await conn.ExecuteAsync(insertDailySql, new
        {
            SnapshotId = Guid.NewGuid(),
            SnapshotDate = snapshotDate,
            PersonId = personId
        });

        await duplicateDailyAct.Should().ThrowAsync<SqlException>(
            "UNIQUE(SnapshotDate, PersonId) must reject duplicate rows for the same date and person");

        // Verify UNIQUE(YearMonth, CustomerId) on analytics.MonthlyCustomerPerformanceSnapshots
        var customerId = Guid.NewGuid();
        const string yearMonth = "2026-09";

        const string insertMonthlySql = """
            INSERT INTO [analytics].[MonthlyCustomerPerformanceSnapshots] (
                [SnapshotId], [YearMonth], [CustomerId],
                [TotalRequests], [ResolvedRequestsCount], [RejectedRequestsCount],
                [AvgResolutionHours], [SlaMetCount], [SlaBreachedCount], [CapturedAt]
            )
            VALUES (
                @SnapshotId, @YearMonth, @CustomerId,
                5, 4, 1, 18.25, 3, 1, SYSUTCDATETIME()
            );
            """;

        await conn.ExecuteAsync(insertMonthlySql, new
        {
            SnapshotId = Guid.NewGuid(),
            YearMonth = yearMonth,
            CustomerId = customerId
        });

        Func<Task> duplicateMonthlyAct = async () => await conn.ExecuteAsync(insertMonthlySql, new
        {
            SnapshotId = Guid.NewGuid(),
            YearMonth = yearMonth,
            CustomerId = customerId
        });

        await duplicateMonthlyAct.Should().ThrowAsync<SqlException>(
            "UNIQUE(YearMonth, CustomerId) must reject duplicate rows for the same month and customer");
    }

    [Fact]
    public async Task AnalyticsSnapshotJob_daily_snapshot_inserts_DailyWorkloadSnapshots_with_correct_metrics_and_preserves_immutability()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var analyticsService = scope.ServiceProvider.GetRequiredService<IManagementAnalyticsService>();
        var snapshotJob = scope.ServiceProvider.GetRequiredService<AnalyticsSnapshotJob>();
        var hostedServices = scope.ServiceProvider.GetServices<IHostedService>();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        hostedServices.OfType<AnalyticsSnapshotJob>().Should().ContainSingle(
            "AnalyticsSnapshotJob must be registered as an ASP.NET Core IHostedService / BackgroundService");

        // Seed 2 active Persons, 1 Customer, and 1 Product
        var progA = await mediator.Send(new CreatePersonCommand(
            "Dian",
            "Kusuma",
            $"dian.{Guid.NewGuid():N}@cakra.id"));

        var progB = await mediator.Send(new CreatePersonCommand(
            "Fajar",
            "Hidayat",
            $"fajar.{Guid.NewGuid():N}@cakra.id"));

        var customer = await mediator.Send(new CreateCustomerCommand(
            $"CUST-{Guid.NewGuid():N}"[..15],
            "RSUP Dr. Sardjito",
            HasActiveMaintenanceContract: true));

        var product = await mediator.Send(new CreateProductCommand(
            $"PRD-{Guid.NewGuid():N}"[..14],
            "MyHospital Core",
            "Hospital Information System",
            progA.Id));

        currentContext?.Initialize(Guid.NewGuid(), progA.Id, new[] { "Programmer" });

        // Req 1 for progA: IN_PROGRESS (active, not stalled — created 10 hours ago, updated 2 hours ago)
        var req1 = await mediator.Send(new RecordRequestCommand(
            Title: "Active in-progress request",
            Description: "In progress fix for pharmacy dispensing",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "Bug",
            Priority: "HIGH"));
        await mediator.Send(new AssignRequestOwnerCommand(req1.Id, progA.Id));
        await mediator.Send(new AcceptRequestResponsibilityCommand(req1.Id, "Working on fix"));

        // Req 2 for progA: ESCALATED and stalled (created & last updated 80 hours ago >= 72h threshold)
        var req2 = await mediator.Send(new RecordRequestCommand(
            Title: "Escalated stalled request",
            Description: "Blocked on third-party HL7 gateway specification",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "Integration",
            Priority: "URGENT"));
        await mediator.Send(new AssignRequestOwnerCommand(req2.Id, progA.Id));

        // Req 3 for progA: COMPLETED today
        var req3 = await mediator.Send(new RecordRequestCommand(
            Title: "Completed request today",
            Description: "Resolved outpatient queue sorting issue",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "Bug",
            Priority: "NORMAL"));
        await mediator.Send(new AssignRequestOwnerCommand(req3.Id, progA.Id));
        await mediator.Send(new AcceptRequestResponsibilityCommand(req3.Id));
        await mediator.Send(new ReviewRequestCompletionCommand(req3.Id, "Fixed sorting order in query"));

        // Req 4 for progB: EVALUATING (active, not stalled)
        var req4 = await mediator.Send(new RecordRequestCommand(
            Title: "Evaluating request for progB",
            Description: "Triage laboratory barcode label alignment",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "Support",
            Priority: "NORMAL"));
        await mediator.Send(new AssignRequestOwnerCommand(req4.Id, progB.Id));

        var nowUtc = DateTime.UtcNow;
        var todayDate = nowUtc.Date;

        // Backdate req1 (10h old, updated 2h ago) and set req2 to ESCALATED + stalled (80h old)
        using (var db = connectionFactory.CreateConnection())
        {
            await db.ExecuteAsync(
                """
                UPDATE [request].[Requests]
                SET [CreatedAt] = @CreatedAt,
                    [UpdatedAt] = @UpdatedAt
                WHERE [Id] = @Id;
                """,
                new
                {
                    Id = req1.Id,
                    CreatedAt = nowUtc.AddHours(-10),
                    UpdatedAt = nowUtc.AddHours(-2)
                });

            await db.ExecuteAsync(
                """
                UPDATE [request].[Requests]
                SET [Status] = 'ESCALATED',
                    [EscalationReason] = 'Waiting for vendor spec',
                    [CreatedAt] = @StalledTimestamp,
                    [UpdatedAt] = @StalledTimestamp
                WHERE [Id] = @Id;
                """,
                new
                {
                    Id = req2.Id,
                    StalledTimestamp = nowUtc.AddHours(-80)
                });

            await db.ExecuteAsync(
                """
                UPDATE [request].[Requests]
                SET [CreatedAt] = @CreatedAt,
                    [UpdatedAt] = @UpdatedAt
                WHERE [Id] = @Id;
                """,
                new
                {
                    Id = req4.Id,
                    CreatedAt = nowUtc.AddHours(-4),
                    UpdatedAt = nowUtc.AddHours(-1)
                });
        }

        // Trigger Daily Snapshot Job
        var snapshots = await snapshotJob.TriggerDailySnapshotAsync(todayDate);
        snapshots.Should().HaveCount(2);

        var progASnapshot = snapshots.Single(s => s.PersonId == progA.Id);
        progASnapshot.SnapshotDate.Should().Be(todayDate);
        progASnapshot.PersonName.Should().Be("Dian Kusuma");
        progASnapshot.ActiveRequestsCount.Should().Be(2, "req1 (IN_PROGRESS) and req2 (ESCALATED) are active");
        progASnapshot.EscalatedRequestsCount.Should().Be(1, "req2 is in ESCALATED status");
        progASnapshot.StalledRequestsCount.Should().Be(1, "req2 has been un-updated for 80 hours (>= 72h)");
        progASnapshot.CompletedRequestsToday.Should().Be(1, "req3 was completed today");
        progASnapshot.AvgAgeHours.Should().BeGreaterThanOrEqualTo(44.5m);

        var progBSnapshot = snapshots.Single(s => s.PersonId == progB.Id);
        progBSnapshot.SnapshotDate.Should().Be(todayDate);
        progBSnapshot.PersonName.Should().Be("Fajar Hidayat");
        progBSnapshot.ActiveRequestsCount.Should().Be(1, "req4 (EVALUATING) is active");
        progBSnapshot.EscalatedRequestsCount.Should().Be(0);
        progBSnapshot.StalledRequestsCount.Should().Be(0);
        progBSnapshot.CompletedRequestsToday.Should().Be(0);
        progBSnapshot.AvgAgeHours.Should().BeGreaterThanOrEqualTo(3.5m);

        // Verify persisted rows via GetDailyWorkloadSnapshotsAsync
        var persistedRows = await analyticsService.GetDailyWorkloadSnapshotsAsync(
            startDate: todayDate,
            endDate: todayDate);
        persistedRows.Should().HaveCount(2);

        // Verify Immutability (Architecture §13): adding a new request and re-triggering the daily job
        // does NOT mutate already-captured historical snapshot rows
        var req5 = await mediator.Send(new RecordRequestCommand(
            Title: "Additional request after snapshot",
            Description: "Recorded after daily snapshot was already frozen",
            CustomerId: customer.Id,
            ProductId: product.Id));
        await mediator.Send(new AssignRequestOwnerCommand(req5.Id, progB.Id));

        var secondRunSnapshots = await snapshotJob.TriggerDailySnapshotAsync(todayDate);
        var progBSecondRun = secondRunSnapshots.Single(s => s.PersonId == progB.Id);

        progBSecondRun.SnapshotId.Should().Be(progBSnapshot.SnapshotId, "snapshot row must be immutable once inserted");
        progBSecondRun.ActiveRequestsCount.Should().Be(1, "existing snapshot count must not be mutated without explicit RecomputeSnapshots");
    }

    [Fact]
    public async Task Monthly_snapshot_and_RecomputeSnapshots_idempotently_backfill_daily_and_monthly_snapshots()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var analyticsService = scope.ServiceProvider.GetRequiredService<IManagementAnalyticsService>();
        var snapshotJob = scope.ServiceProvider.GetRequiredService<AnalyticsSnapshotJob>();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        var programmer = await mediator.Send(new CreatePersonCommand(
            "Budi",
            "Setiawan",
            $"budi.{Guid.NewGuid():N}@cakra.id"));

        var customer = await mediator.Send(new CreateCustomerCommand(
            $"CUST-{Guid.NewGuid():N}"[..15],
            "RSUD Pasar Minggu",
            HasActiveMaintenanceContract: true));

        var product = await mediator.Send(new CreateProductCommand(
            $"PRD-{Guid.NewGuid():N}"[..14],
            "BTrade3 Core",
            "Enterprise Billing Engine",
            programmer.Id));

        currentContext?.Initialize(Guid.NewGuid(), programmer.Id, new[] { "Programmer" });

        // Create 3 requests for customer in 2026-08:
        // 1. Completed in 12 hours (SLA Met <= 72h)
        var reqSlaMet = await mediator.Send(new RecordRequestCommand(
            Title: "August Request 1 - Fast Resolution",
            Description: "Resolved within 12 hours",
            CustomerId: customer.Id,
            ProductId: product.Id));
        await mediator.Send(new AssignRequestOwnerCommand(reqSlaMet.Id, programmer.Id));
        await mediator.Send(new AcceptRequestResponsibilityCommand(reqSlaMet.Id));
        await mediator.Send(new ReviewRequestCompletionCommand(reqSlaMet.Id, "Resolved in 12h"));

        // 2. Completed in 96 hours (SLA Breached > 72h)
        var reqSlaBreached = await mediator.Send(new RecordRequestCommand(
            Title: "August Request 2 - Slow Resolution",
            Description: "Resolved after 96 hours",
            CustomerId: customer.Id,
            ProductId: product.Id));
        await mediator.Send(new AssignRequestOwnerCommand(reqSlaBreached.Id, programmer.Id));
        await mediator.Send(new AcceptRequestResponsibilityCommand(reqSlaBreached.Id));
        await mediator.Send(new ReviewRequestCompletionCommand(reqSlaBreached.Id, "Resolved in 96h"));

        // 3. Rejected in August 2026
        var reqRejected = await mediator.Send(new RecordRequestCommand(
            Title: "August Request 3 - Rejected",
            Description: "Out of contract scope",
            CustomerId: customer.Id,
            ProductId: product.Id));
        await mediator.Send(new AssignRequestOwnerCommand(reqRejected.Id, programmer.Id));
        await mediator.Send(new RejectRequestCommand(reqRejected.Id, "Out of scope"));

        // Set authoritative timestamps in August 2026 (2026-08-10 and 2026-08-11)
        var aug10Morning = new DateTime(2026, 8, 10, 8, 0, 0, DateTimeKind.Utc);
        var aug10Evening = new DateTime(2026, 8, 10, 20, 0, 0, DateTimeKind.Utc); // +12h
        var aug06Morning = new DateTime(2026, 8, 6, 10, 0, 0, DateTimeKind.Utc);
        var aug10Noon = new DateTime(2026, 8, 10, 10, 0, 0, DateTimeKind.Utc);    // +96h
        var aug11Morning = new DateTime(2026, 8, 11, 9, 0, 0, DateTimeKind.Utc);
        var aug11Noon = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

        using (var db = connectionFactory.CreateConnection())
        {
            await db.ExecuteAsync(
                """
                UPDATE [request].[Requests]
                SET [CreatedAt] = @CreatedAt, [UpdatedAt] = @ResolvedAt
                WHERE [Id] = @RequestId;
                UPDATE [request].[RequestResolutions]
                SET [ResolvedAt] = @ResolvedAt, [CreatedAt] = @ResolvedAt
                WHERE [RequestId] = @RequestId;
                """,
                new { RequestId = reqSlaMet.Id, CreatedAt = aug10Morning, ResolvedAt = aug10Evening });

            await db.ExecuteAsync(
                """
                UPDATE [request].[Requests]
                SET [CreatedAt] = @CreatedAt, [UpdatedAt] = @ResolvedAt
                WHERE [Id] = @RequestId;
                UPDATE [request].[RequestResolutions]
                SET [ResolvedAt] = @ResolvedAt, [CreatedAt] = @ResolvedAt
                WHERE [RequestId] = @RequestId;
                """,
                new { RequestId = reqSlaBreached.Id, CreatedAt = aug06Morning, ResolvedAt = aug10Noon });

            await db.ExecuteAsync(
                """
                UPDATE [request].[Requests]
                SET [CreatedAt] = @CreatedAt, [UpdatedAt] = @ResolvedAt
                WHERE [Id] = @RequestId;
                UPDATE [request].[RequestResolutions]
                SET [ResolvedAt] = @ResolvedAt, [CreatedAt] = @ResolvedAt
                WHERE [RequestId] = @RequestId;
                """,
                new { RequestId = reqRejected.Id, CreatedAt = aug11Morning, ResolvedAt = aug11Noon });
        }

        // 1. Trigger Monthly Snapshot Job for "2026-08"
        var monthlySnapshots = await snapshotJob.TriggerMonthlySnapshotAsync("2026-08");
        var custMonthly = monthlySnapshots.Single(m => m.CustomerId == customer.Id);

        custMonthly.YearMonth.Should().Be("2026-08");
        custMonthly.CustomerName.Should().Be("RSUD Pasar Minggu");
        custMonthly.TotalRequests.Should().Be(3);
        custMonthly.ResolvedRequestsCount.Should().Be(2);
        custMonthly.RejectedRequestsCount.Should().Be(1);
        custMonthly.AvgResolutionHours.Should().Be(54.00m, "average of 12h and 96h is 54.00h");
        custMonthly.SlaMetCount.Should().Be(1);
        custMonthly.SlaBreachedCount.Should().Be(1);

        // 2. Add a 4th request resolved in 6 hours on 2026-08-11 (historical correction)
        var reqLateBackfill = await mediator.Send(new RecordRequestCommand(
            Title: "August Request 4 - Historical Backfill",
            Description: "Resolved in 6 hours on Aug 11",
            CustomerId: customer.Id,
            ProductId: product.Id));
        await mediator.Send(new AssignRequestOwnerCommand(reqLateBackfill.Id, programmer.Id));
        await mediator.Send(new AcceptRequestResponsibilityCommand(reqLateBackfill.Id));
        await mediator.Send(new ReviewRequestCompletionCommand(reqLateBackfill.Id, "Resolved in 6h"));

        using (var db = connectionFactory.CreateConnection())
        {
            await db.ExecuteAsync(
                """
                UPDATE [request].[Requests]
                SET [CreatedAt] = @CreatedAt, [UpdatedAt] = @ResolvedAt
                WHERE [Id] = @RequestId;
                UPDATE [request].[RequestResolutions]
                SET [ResolvedAt] = @ResolvedAt, [CreatedAt] = @ResolvedAt
                WHERE [RequestId] = @RequestId;
                """,
                new
                {
                    RequestId = reqLateBackfill.Id,
                    CreatedAt = new DateTime(2026, 8, 11, 10, 0, 0, DateTimeKind.Utc),
                    ResolvedAt = new DateTime(2026, 8, 11, 16, 0, 0, DateTimeKind.Utc)
                });
        }

        // 3. Execute ManagementAnalyticsService.RecomputeSnapshots(2026-08-10, 2026-08-11)
        var recomputeResult = await analyticsService.RecomputeSnapshots(
            new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc));

        recomputeResult.DaysProcessed.Should().Be(2);
        recomputeResult.MonthsProcessed.Should().Be(1);
        recomputeResult.DailySnapshotsWritten.Should().Be(2);
        recomputeResult.MonthlySnapshotsWritten.Should().Be(1);

        var recomputedMonthly = recomputeResult.MonthlySnapshots.Single(m => m.CustomerId == customer.Id);
        recomputedMonthly.TotalRequests.Should().Be(4);
        recomputedMonthly.ResolvedRequestsCount.Should().Be(3);
        recomputedMonthly.RejectedRequestsCount.Should().Be(1);
        recomputedMonthly.SlaMetCount.Should().Be(2);
        recomputedMonthly.SlaBreachedCount.Should().Be(1);
        recomputedMonthly.AvgResolutionHours.Should().Be(38.00m, "average of 12h, 96h, and 6h is 38.00h");

        // Re-running RecomputeSnapshots over the same range is idempotent
        var secondRecompute = await mediator.Send(new RecomputeSnapshotsCommand(
            new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)));
        secondRecompute.DailySnapshotsWritten.Should().Be(2);
        secondRecompute.MonthlySnapshotsWritten.Should().Be(1);

        var performanceReport = await analyticsService.GetProgrammerPerformanceAsync(
            programmer.Id,
            "2026-08",
            "2026-08");
        performanceReport.PersonId.Should().Be(programmer.Id);
        performanceReport.PersonName.Should().Be("Budi Setiawan");
        performanceReport.TotalCompletedRequests.Should().Be(3, "2 completed on Aug 10 + 1 completed on Aug 11");
        performanceReport.DailyWorkloadSnapshots.Should().HaveCount(2);
        performanceReport.MonthlyCustomerSnapshots.Should().ContainSingle();
    }

    [Fact]
    public void AnalyticsSnapshotJob_schedule_calculators_target_23_59_59_daily_and_1st_of_month_00_05_00_monthly()
    {
        var sampleNow = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

        var dailyDelay = AnalyticsSnapshotJob.CalculateDelayUntilNextDailyRun(sampleNow);
        dailyDelay.Should().Be(new TimeSpan(11, 59, 59));

        var afterDailyTarget = new DateTime(2026, 9, 28, 23, 59, 59, DateTimeKind.Utc);
        var nextDayDelay = AnalyticsSnapshotJob.CalculateDelayUntilNextDailyRun(afterDailyTarget);
        nextDayDelay.Should().Be(TimeSpan.FromDays(1));

        var beforeMonthlyTarget = new DateTime(2026, 10, 1, 0, 1, 0, DateTimeKind.Utc);
        var monthlyDelaySameDay = AnalyticsSnapshotJob.CalculateDelayUntilNextMonthlyRun(beforeMonthlyTarget);
        monthlyDelaySameDay.Should().Be(TimeSpan.FromMinutes(4));

        var afterMonthlyTarget = new DateTime(2026, 10, 1, 0, 5, 0, DateTimeKind.Utc);
        var nextMonthDelay = AnalyticsSnapshotJob.CalculateDelayUntilNextMonthlyRun(afterMonthlyTarget);
        nextMonthDelay.Should().Be(new DateTime(2026, 11, 1, 0, 5, 0, DateTimeKind.Utc) - afterMonthlyTarget);

        AnalyticsSnapshotJob.GetPrecedingYearMonth(new DateTime(2026, 10, 1, 0, 5, 0, DateTimeKind.Utc))
            .Should().Be("2026-09");
        AnalyticsSnapshotJob.GetPrecedingYearMonth(new DateTime(2026, 1, 1, 0, 5, 0, DateTimeKind.Utc))
            .Should().Be("2025-12");
    }
}
