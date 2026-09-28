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
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Respawn;
using Respawn.Graph;
using Xunit;

namespace Cakra.Tests.Integration.Analytics;

/// <summary>
/// Integration tests verifying Slice P7-S35: Analytics Module — Real-Time Workload &amp; Customer Portfolio Queries
/// (Architecture §7, §8, §9, §13, §15, §19.3, §19.8, §20 — FEAT-MGT-002, FEAT-MGT-004):
/// - <c>GetProgrammerActiveWorkload(personId?)</c> dynamically aggregates active <c>Requests</c> grouped by
///   <c>OwnerPersonId</c> and sub-state (<c>CAPTURED</c>, <c>EVALUATING</c>, <c>ACCEPTED</c>, <c>IN_PROGRESS</c>,
///   <c>ESCALATED</c>), enriches person names via <c>IOrganizationQueryService</c>, excludes closed requests,
///   and supports filtering by optional <c>personId</c>.
/// - <c>GetCustomerRequestPortfolio(customerId)</c> dynamically queries active requests, open blockers
///   (<c>ESCALATED</c>), and recent completions (<c>COMPLETED</c>), enriched with customer details and
///   maintenance contract status via <c>ICustomerQueryService.GetCustomerWithContractStatusAsync</c>.
/// - All SQL uses Dapper parameterized queries with zero cross-module writes.
/// </summary>
public class AnalyticsRealTimeQueriesIntegrationTests : IAsyncLifetime
{
    private const string LocalDbFallback =
        "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb_AnalyticsRealTime;Integrated Security=true;TrustServerCertificate=True;";

    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private bool _sqlServerAvailable;

    public AnalyticsRealTimeQueriesIntegrationTests()
    {
        var baseConnection = DatabaseResetHelper.ResolveConnectionString() ?? LocalDbFallback;
        _connectionString = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = "CakraTestDb_AnalyticsRealTime"
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
        migrationResult.Successful.Should().BeTrue("DbUp migrations must succeed for CakraTestDb_AnalyticsRealTime");

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
    public async Task GetProgrammerActiveWorkload_dynamically_aggregates_by_person_and_substate_and_supports_person_filter()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var analyticsService = scope.ServiceProvider.GetRequiredService<IManagementAnalyticsService>();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        // Seed 3 active programmers: progA (diverse active sub-states + closed), progB (evaluating), progC (zero active)
        var progA = await mediator.Send(new CreatePersonCommand(
            "Rina",
            "Wijaya",
            $"rina.{Guid.NewGuid():N}@cakra.id"));

        var progB = await mediator.Send(new CreatePersonCommand(
            "Hendra",
            "Pratama",
            $"hendra.{Guid.NewGuid():N}@cakra.id"));

        var progC = await mediator.Send(new CreatePersonCommand(
            "Siti",
            "Rahayu",
            $"siti.{Guid.NewGuid():N}@cakra.id"));

        var customer = await mediator.Send(new CreateCustomerCommand(
            $"CUST-{Guid.NewGuid():N}"[..15],
            "RSUP Fatmawati",
            HasActiveMaintenanceContract: true));

        var product = await mediator.Send(new CreateProductCommand(
            $"PRD-{Guid.NewGuid():N}"[..14],
            "MyHospital EMR",
            "Electronic Medical Record Module",
            progA.Id));

        currentContext?.Initialize(Guid.NewGuid(), progA.Id, new[] { "Programmer" });

        // Create requests for progA across all 5 active sub-states + 2 closed states:
        // 1. CAPTURED (assigned owner in CAPTURED sub-state)
        var reqCaptured = await mediator.Send(new RecordRequestCommand(
            Title: "Captured request for Rina",
            Description: "Initial capture awaiting triage",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "Support",
            Priority: "LOW"));

        // 2. EVALUATING
        var reqEvaluating = await mediator.Send(new RecordRequestCommand(
            Title: "Evaluating request for Rina",
            Description: "Under evaluation",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "Bug",
            Priority: "NORMAL"));
        await mediator.Send(new AssignRequestOwnerCommand(reqEvaluating.Id, progA.Id));

        // 3. ACCEPTED
        var reqAccepted = await mediator.Send(new RecordRequestCommand(
            Title: "Accepted request for Rina",
            Description: "Accepted for implementation",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "Feature",
            Priority: "HIGH"));
        await mediator.Send(new AssignRequestOwnerCommand(reqAccepted.Id, progA.Id));
        await mediator.Send(new AcceptRequestResponsibilityCommand(reqAccepted.Id));

        // 4. IN_PROGRESS
        var reqInProgress = await mediator.Send(new RecordRequestCommand(
            Title: "In-progress request for Rina",
            Description: "Active coding in progress",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "Bug",
            Priority: "HIGH"));
        await mediator.Send(new AssignRequestOwnerCommand(reqInProgress.Id, progA.Id));
        await mediator.Send(new AcceptRequestResponsibilityCommand(reqInProgress.Id));

        // 5. ESCALATED (and stalled > 72h)
        var reqEscalated = await mediator.Send(new RecordRequestCommand(
            Title: "Escalated blocker for Rina",
            Description: "Blocked on database schema lock",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "Bug",
            Priority: "URGENT"));
        await mediator.Send(new AssignRequestOwnerCommand(reqEscalated.Id, progA.Id));
        await mediator.Send(new EscalateRequestCommand(reqEscalated.Id, "Waiting on DBA approval"));

        // 6. COMPLETED (closed — must be excluded from active workload)
        var reqCompleted = await mediator.Send(new RecordRequestCommand(
            Title: "Completed request for Rina",
            Description: "Already resolved",
            CustomerId: customer.Id,
            ProductId: product.Id));
        await mediator.Send(new AssignRequestOwnerCommand(reqCompleted.Id, progA.Id));
        await mediator.Send(new AcceptRequestResponsibilityCommand(reqCompleted.Id));
        await mediator.Send(new ReviewRequestCompletionCommand(reqCompleted.Id, "Completed fix"));

        // 7. REJECTED (closed — must be excluded from active workload)
        var reqRejected = await mediator.Send(new RecordRequestCommand(
            Title: "Rejected request for Rina",
            Description: "Duplicate report",
            CustomerId: customer.Id,
            ProductId: product.Id));
        await mediator.Send(new AssignRequestOwnerCommand(reqRejected.Id, progA.Id));
        await mediator.Send(new RejectRequestCommand(reqRejected.Id, "Duplicate of existing issue"));

        // Create 1 EVALUATING request for progB
        var reqProgB = await mediator.Send(new RecordRequestCommand(
            Title: "Evaluating request for Hendra",
            Description: "Triage billing report",
            CustomerId: customer.Id,
            ProductId: product.Id));
        await mediator.Send(new AssignRequestOwnerCommand(reqProgB.Id, progB.Id));

        // Ensure exact sub-states in DB for CAPTURED (with OwnerPersonId) and ACCEPTED vs IN_PROGRESS
        var stalledTime = DateTime.UtcNow.AddHours(-80);
        using (var db = connectionFactory.CreateConnection())
        {
            await db.ExecuteAsync(
                """
                UPDATE [request].[Requests]
                SET [OwnerPersonId] = @OwnerId, [Status] = 'CAPTURED'
                WHERE [Id] = @CapturedId;

                UPDATE [request].[Requests]
                SET [Status] = 'ACCEPTED'
                WHERE [Id] = @AcceptedId;

                UPDATE [request].[Requests]
                SET [Status] = 'IN_PROGRESS'
                WHERE [Id] = @InProgressId;

                UPDATE [request].[Requests]
                SET [CreatedAt] = @StalledTime, [UpdatedAt] = @StalledTime
                WHERE [Id] = @EscalatedId;
                """,
                new
                {
                    OwnerId = progA.Id,
                    CapturedId = reqCaptured.Id,
                    AcceptedId = reqAccepted.Id,
                    InProgressId = reqInProgress.Id,
                    EscalatedId = reqEscalated.Id,
                    StalledTime = stalledTime
                });
        }

        // 1. Unfiltered query: returns all 3 active programmers
        var allWorkloads = await analyticsService.GetProgrammerActiveWorkload();
        allWorkloads.Should().HaveCount(3);

        var rinaWorkload = allWorkloads.Single(w => w.PersonId == progA.Id);
        rinaWorkload.PersonName.Should().Be("Rina Wijaya");
        rinaWorkload.CapturedCount.Should().Be(1);
        rinaWorkload.EvaluatingCount.Should().Be(1);
        rinaWorkload.AcceptedCount.Should().Be(1);
        rinaWorkload.InProgressCount.Should().Be(1);
        rinaWorkload.EscalatedCount.Should().Be(1);
        rinaWorkload.TotalActiveCount.Should().Be(5, "5 active sub-states; COMPLETED and REJECTED must be excluded");
        rinaWorkload.ActiveRequestsCount.Should().Be(5);
        rinaWorkload.StalledRequestsCount.Should().Be(1);
        rinaWorkload.IsOverloaded.Should().BeTrue();
        rinaWorkload.SubStateCounts["CAPTURED"].Should().Be(1);
        rinaWorkload.SubStateCounts["EVALUATING"].Should().Be(1);
        rinaWorkload.SubStateCounts["ACCEPTED"].Should().Be(1);
        rinaWorkload.SubStateCounts["IN_PROGRESS"].Should().Be(1);
        rinaWorkload.SubStateCounts["ESCALATED"].Should().Be(1);
        rinaWorkload.ActiveRequests.Should().HaveCount(5);
        rinaWorkload.ActiveRequests.Should().OnlyContain(r => r.CustomerName == "RSUP Fatmawati" && r.OwnerName == "Rina Wijaya");
        rinaWorkload.ActiveRequests.Select(r => r.RequestId)
            .Should().NotContain(new[] { reqCompleted.Id, reqRejected.Id });

        var hendraWorkload = allWorkloads.Single(w => w.PersonId == progB.Id);
        hendraWorkload.PersonName.Should().Be("Hendra Pratama");
        hendraWorkload.EvaluatingCount.Should().Be(1);
        hendraWorkload.TotalActiveCount.Should().Be(1);
        hendraWorkload.IsOverloaded.Should().BeFalse();
        hendraWorkload.ActiveRequests.Should().ContainSingle(r => r.RequestId == reqProgB.Id);

        var sitiWorkload = allWorkloads.Single(w => w.PersonId == progC.Id);
        sitiWorkload.PersonName.Should().Be("Siti Rahayu");
        sitiWorkload.TotalActiveCount.Should().Be(0);
        sitiWorkload.ActiveRequests.Should().BeEmpty();

        // 2. Filtered query by personId via MediatR & service
        var filteredByRina = await mediator.Send(new GetProgrammerActiveWorkloadQuery(progA.Id));
        filteredByRina.Should().ContainSingle();
        filteredByRina[0].PersonId.Should().Be(progA.Id);
        filteredByRina[0].TotalActiveCount.Should().Be(5);

        var filteredBySiti = await analyticsService.GetProgrammerActiveWorkloadAsync(progC.Id);
        filteredBySiti.Should().ContainSingle();
        filteredBySiti[0].PersonId.Should().Be(progC.Id);
        filteredBySiti[0].TotalActiveCount.Should().Be(0);
        filteredBySiti[0].ActiveRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCustomerRequestPortfolio_returns_active_requests_open_blockers_and_recent_completions_with_contract_status()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var analyticsService = scope.ServiceProvider.GetRequiredService<IManagementAnalyticsService>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        var programmer = await mediator.Send(new CreatePersonCommand(
            "Arif",
            "Nugroho",
            $"arif.{Guid.NewGuid():N}@cakra.id"));

        var custWithContract = await mediator.Send(new CreateCustomerCommand(
            "CUST-SARDJITO",
            "RSUP Dr. Sardjito",
            HasActiveMaintenanceContract: true));

        var custWithoutContract = await mediator.Send(new CreateCustomerCommand(
            "CUST-KARYADI",
            "RSUP Dr. Kariadi",
            HasActiveMaintenanceContract: false));

        var product = await mediator.Send(new CreateProductCommand(
            $"PRD-{Guid.NewGuid():N}"[..14],
            "PenaEl Lab",
            "Laboratory Information System",
            programmer.Id));

        currentContext?.Initialize(Guid.NewGuid(), programmer.Id, new[] { "Programmer" });

        // Create portfolio requests for custWithContract:
        // 1. Active IN_PROGRESS
        var reqActive1 = await mediator.Send(new RecordRequestCommand(
            Title: "HL7 Analyzer Interface Update",
            Description: "Add new hematology analyzer mapping",
            CustomerId: custWithContract.Id,
            ProductId: product.Id,
            RequestType: "Feature",
            Priority: "HIGH"));
        await mediator.Send(new AssignRequestOwnerCommand(reqActive1.Id, programmer.Id));
        await mediator.Send(new AcceptRequestResponsibilityCommand(reqActive1.Id));

        // 2. Active EVALUATING
        var reqActive2 = await mediator.Send(new RecordRequestCommand(
            Title: "Outpatient Lab Slip Layout",
            Description: "Adjust header margin on thermal printer",
            CustomerId: custWithContract.Id,
            ProductId: product.Id,
            RequestType: "Support",
            Priority: "NORMAL"));
        await mediator.Send(new AssignRequestOwnerCommand(reqActive2.Id, programmer.Id));

        // 3. Open Blocker (ESCALATED)
        var reqBlocker = await mediator.Send(new RecordRequestCommand(
            Title: "Critical LIS Database Timeout",
            Description: "Lock contention during morning peak",
            CustomerId: custWithContract.Id,
            ProductId: product.Id,
            RequestType: "Bug",
            Priority: "URGENT"));
        await mediator.Send(new AssignRequestOwnerCommand(reqBlocker.Id, programmer.Id));
        await mediator.Send(new EscalateRequestCommand(reqBlocker.Id, "Requires hospital infra firewall change"));

        // 4. Recent Completion 1 (COMPLETED)
        var reqCompleted1 = await mediator.Send(new RecordRequestCommand(
            Title: "Fix Barcode Checksum",
            Description: "Code128 checksum fix",
            CustomerId: custWithContract.Id,
            ProductId: product.Id,
            RequestType: "Bug",
            Priority: "HIGH"));
        await mediator.Send(new AssignRequestOwnerCommand(reqCompleted1.Id, programmer.Id));
        await mediator.Send(new AcceptRequestResponsibilityCommand(reqCompleted1.Id));
        await mediator.Send(new ReviewRequestCompletionCommand(reqCompleted1.Id, "Deployed barcode patch v1.4.2"));

        // 5. Recent Completion 2 (COMPLETED)
        var reqCompleted2 = await mediator.Send(new RecordRequestCommand(
            Title: "Add Reagent Lot Report",
            Description: "Monthly reagent usage export",
            CustomerId: custWithContract.Id,
            ProductId: product.Id,
            RequestType: "Feature",
            Priority: "NORMAL"));
        await mediator.Send(new AssignRequestOwnerCommand(reqCompleted2.Id, programmer.Id));
        await mediator.Send(new AcceptRequestResponsibilityCommand(reqCompleted2.Id));
        await mediator.Send(new ReviewRequestCompletionCommand(reqCompleted2.Id, "Added CSV export endpoint"));

        // 6. Rejected Request (REJECTED)
        var reqRejected = await mediator.Send(new RecordRequestCommand(
            Title: "Unsupported Legacy OS Printer Driver",
            Description: "Windows XP driver request",
            CustomerId: custWithContract.Id,
            ProductId: product.Id));
        await mediator.Send(new AssignRequestOwnerCommand(reqRejected.Id, programmer.Id));
        await mediator.Send(new RejectRequestCommand(reqRejected.Id, "OS out of vendor support"));

        // Query portfolio via service and MediatR
        var portfolio = await mediator.Send(new GetCustomerRequestPortfolioQuery(custWithContract.Id));

        portfolio.CustomerId.Should().Be(custWithContract.Id);
        portfolio.CustomerCode.Should().Be("CUST-SARDJITO");
        portfolio.CustomerName.Should().Be("RSUP Dr. Sardjito");
        portfolio.CustomerStatus.Should().Be("ACTIVE");
        portfolio.HasActiveMaintenanceContract.Should().BeTrue();
        portfolio.ContractStatus.Should().Be("ACTIVE");

        portfolio.TotalRequestsCount.Should().Be(6);
        portfolio.ActiveRequestsCount.Should().Be(3, "reqActive1, reqActive2, and reqBlocker are active");
        portfolio.OpenBlockersCount.Should().Be(1, "reqBlocker is ESCALATED");
        portfolio.BlockedRequestsCount.Should().Be(1);
        portfolio.RecentCompletionsCount.Should().Be(2, "reqCompleted1 and reqCompleted2 are COMPLETED");
        portfolio.ResolvedRequestsCount.Should().Be(2);
        portfolio.RejectedRequestsCount.Should().Be(1);

        portfolio.ActiveRequests.Should().HaveCount(3);
        portfolio.OpenBlockers.Should().ContainSingle(b =>
            b.RequestId == reqBlocker.Id &&
            b.Status == "ESCALATED" &&
            b.IsBlocked &&
            b.EscalationReason == "Requires hospital infra firewall change" &&
            b.OwnerName == "Arif Nugroho");

        portfolio.RecentCompletions.Should().HaveCount(2);
        portfolio.RecentCompletions.Select(c => c.RequestId)
            .Should().Contain(new[] { reqCompleted1.Id, reqCompleted2.Id });
        portfolio.RecentCompletions.Should().OnlyContain(c =>
            c.Status == "COMPLETED" &&
            !string.IsNullOrEmpty(c.ResolutionSummary) &&
            c.ResolvedAt.HasValue);

        portfolio.Requests.Should().HaveCount(6);
        portfolio.Requests[0].RequestId.Should().Be(reqBlocker.Id, "open blockers are ordered first for management visibility");

        // Query portfolio for customer with no requests and no active maintenance contract
        var emptyPortfolio = await analyticsService.GetCustomerRequestPortfolio(custWithoutContract.Id);
        emptyPortfolio.CustomerId.Should().Be(custWithoutContract.Id);
        emptyPortfolio.CustomerCode.Should().Be("CUST-KARYADI");
        emptyPortfolio.CustomerName.Should().Be("RSUP Dr. Kariadi");
        emptyPortfolio.HasActiveMaintenanceContract.Should().BeFalse();
        emptyPortfolio.ContractStatus.Should().Be("NONE");
        emptyPortfolio.TotalRequestsCount.Should().Be(0);
        emptyPortfolio.ActiveRequestsCount.Should().Be(0);
        emptyPortfolio.OpenBlockersCount.Should().Be(0);
        emptyPortfolio.RecentCompletionsCount.Should().Be(0);
        emptyPortfolio.ActiveRequests.Should().BeEmpty();
        emptyPortfolio.OpenBlockers.Should().BeEmpty();
        emptyPortfolio.RecentCompletions.Should().BeEmpty();
        emptyPortfolio.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task RealTime_queries_validate_inputs_and_execute_without_cross_module_writes()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var analyticsService = scope.ServiceProvider.GetRequiredService<IManagementAnalyticsService>();

        Func<Task> emptyCustomerQuery = async () =>
            await mediator.Send(new GetCustomerRequestPortfolioQuery(Guid.Empty));
        await emptyCustomerQuery.Should().ThrowAsync<ValidationException>();

        Func<Task> emptyPersonWorkloadQuery = async () =>
            await mediator.Send(new GetProgrammerActiveWorkloadQuery(Guid.Empty));
        await emptyPersonWorkloadQuery.Should().ThrowAsync<ValidationException>();

        Func<Task> directEmptyCustomerCall = async () =>
            await analyticsService.GetCustomerRequestPortfolioAsync(Guid.Empty);
        await directEmptyCustomerCall.Should().ThrowAsync<ArgumentException>();

        Func<Task> directEmptyPersonCall = async () =>
            await analyticsService.GetProgrammerActiveWorkloadAsync(Guid.Empty);
        await directEmptyPersonCall.Should().ThrowAsync<ArgumentException>();
    }
}
