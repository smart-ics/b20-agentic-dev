using Cakra.Api.Infrastructure.Context;
using Cakra.Api.Infrastructure.Migrations;
using Cakra.Core;
using Cakra.Modules.Customer.Services;
using Cakra.Modules.Organization.Commands;
using Cakra.Modules.Product.Services;
using Cakra.Modules.Request;
using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Services;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Respawn;
using Respawn.Graph;
using Xunit;

namespace Cakra.Tests.Integration.Request;

/// <summary>
/// Integration tests verifying P4-S19 Request Module — Lifecycle Completion Commands &amp; Queries:
/// - Full request lifecycle (Record -&gt; Assign -&gt; Evaluate -&gt; Accept -&gt; Complete) against SQL Server
/// - Rejection flow (Record -&gt; Assign -&gt; Evaluate -&gt; Reject) against SQL Server
/// - RequestQueryService Dapper parameterized SQL queries:
///   GetRequestById, GetRequestStateHistory, ListMyAssignedRequests, GetFilteredRequestGrid,
///   RequestExists, and GetRequestsByIds
/// - WebApplicationFactory&lt;Program&gt; + Respawn test isolation
/// </summary>
[Collection("OrganizationDatabase")]
public class RequestCompletionAndQueriesIntegrationTests : IAsyncLifetime
{
    private const string LocalDbFallback = "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb_RequestCompletion;Integrated Security=true;TrustServerCertificate=True;";
    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private bool _sqlServerAvailable;

    public RequestCompletionAndQueriesIntegrationTests()
    {
        var baseConnectionString = DatabaseResetHelper.ResolveConnectionString() ?? LocalDbFallback;
        _connectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "CakraTestDb_RequestCompletion"
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
        migrationResult.Successful.Should().BeTrue("DbUp migrations must execute without errors");

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
            SchemasToInclude = ["request", "product", "customer", "organization"],
            TablesToIgnore = [new Table("dbo", "__SchemaVersions"), new Table("dbo", "SchemaVersions")]
        });

        await respawner.ResetAsync(conn);
    }

    [Fact]
    public async Task Full_request_lifecycle_Record_Assign_Evaluate_Accept_Complete_and_queries_succeed_against_SqlServer()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var requestQueryService = scope.ServiceProvider.GetRequiredService<IRequestQueryService>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        // 1. Seed Organization Persons, Customer, and Product
        var implementator = await mediator.Send(new CreatePersonCommand(
            "Rina",
            "Wijaya",
            $"rina.{Guid.NewGuid():N}@cakra.id"));

        var programmer = await mediator.Send(new CreatePersonCommand(
            "Agus",
            "Santoso",
            $"agus.{Guid.NewGuid():N}@cakra.id"));

        var customer = await mediator.Send(new CreateCustomerCommand(
            $"CUST-{Guid.NewGuid():N}"[..16],
            "RSUD Dr. Soetomo",
            HasActiveMaintenanceContract: true));

        var product = await mediator.Send(new CreateProductCommand(
            $"PRD-{Guid.NewGuid():N}"[..14],
            "PenaElEMR",
            "Electronic Medical Record System",
            programmer.Id));

        // 2. Step 1: RecordRequest -> CAPTURED
        currentContext?.Initialize(Guid.NewGuid(), implementator.Id, new[] { "Implementator" });

        var recorded = await mediator.Send(new RecordRequestCommand(
            Title: "Outpatient e-Prescription stock deduction race condition",
            Description: "Concurrent prescription dispensing occasionally allows negative batch stock.",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "Bug",
            Priority: "URGENT"));

        recorded.Status.Should().Be(RequestStatusNames.Captured);

        // 3. Step 2: AssignRequestOwner -> EVALUATING
        var assigned = await mediator.Send(new AssignRequestOwnerCommand(
            RequestId: recorded.Id,
            OwnerPersonId: programmer.Id,
            Notes: "Assigned to pharmacy module owner"));

        assigned.Status.Should().Be(RequestStatusNames.Evaluating);
        assigned.OwnerPersonId.Should().Be(programmer.Id);

        // 4. Step 3: EvaluateRequest -> EVALUATING with evaluation notes
        currentContext?.Initialize(Guid.NewGuid(), programmer.Id, new[] { "Programmer" });

        var evaluated = await mediator.Send(new EvaluateRequestCommand(
            RequestId: recorded.Id,
            EvaluationNotes: "Missing UPDLOCK hint in inventory batch reservation query."));

        evaluated.Status.Should().Be(RequestStatusNames.Evaluating);
        evaluated.EvaluationNotes.Should().Be("Missing UPDLOCK hint in inventory batch reservation query.");

        // 5. Step 4: AcceptRequestResponsibility -> IN_PROGRESS
        var accepted = await mediator.Send(new AcceptRequestResponsibilityCommand(
            RequestId: recorded.Id,
            Notes: "Accepted responsibility and implementing row-level lock fix"));

        accepted.Status.Should().Be(RequestStatusNames.InProgress);
        accepted.OwnerPersonId.Should().Be(programmer.Id);

        // 6. Step 5: ReviewRequestCompletion -> COMPLETED
        var completed = await mediator.Send(new ReviewRequestCompletionCommand(
            RequestId: recorded.Id,
            ResolutionDescription: "Added UPDLOCK, ROWLOCK hints and optimistic concurrency check on stock deduction."));

        completed.Status.Should().Be(RequestStatusNames.Completed);
        completed.Resolution.Should().NotBeNull();
        completed.Resolution!.Outcome.Should().Be(ResolutionOutcomeNames.Completed);
        completed.Resolution.Description.Should().Be("Added UPDLOCK, ROWLOCK hints and optimistic concurrency check on stock deduction.");
        completed.Resolution.ResolvedBy.Should().Be(programmer.Id);

        // 7. Verify GetRequestById via IRequestQueryService and MediatR query
        var queriedById = await requestQueryService.GetRequestById(recorded.Id);
        queriedById.Should().NotBeNull();
        queriedById!.Id.Should().Be(recorded.Id);
        queriedById.Title.Should().Be("Outpatient e-Prescription stock deduction race condition");
        queriedById.Status.Should().Be(RequestStatusNames.Completed);
        queriedById.Priority.Should().Be("URGENT");
        queriedById.OwnerPersonId.Should().Be(programmer.Id);
        queriedById.OwnerName.Should().Be("Agus Santoso");
        queriedById.CustomerId.Should().Be(customer.Id);
        queriedById.CustomerName.Should().Be("RSUD Dr. Soetomo");
        queriedById.ProductId.Should().Be(product.Id);
        queriedById.ProductName.Should().Be("PenaElEMR");
        queriedById.EvaluationNotes.Should().Be("Missing UPDLOCK hint in inventory batch reservation query.");
        queriedById.Resolution.Should().NotBeNull();
        queriedById.Resolution!.Outcome.Should().Be(ResolutionOutcomeNames.Completed);
        queriedById.Resolution.ResolvedBy.Should().Be(programmer.Id);
        queriedById.Resolution.ResolvedByName.Should().Be("Agus Santoso");

        var queriedViaMediator = await mediator.Send(new GetRequestByIdQuery(recorded.Id));
        queriedViaMediator.Should().NotBeNull();
        queriedViaMediator!.Status.Should().Be(RequestStatusNames.Completed);

        // 8. Verify GetRequestStateHistory returns ordered audit trail
        var history = await requestQueryService.GetRequestStateHistory(recorded.Id);
        history.Should().HaveCount(5);

        history[0].PreviousStatus.Should().BeNull();
        history[0].NewStatus.Should().Be(RequestStatusNames.Captured);
        history[0].ActorPersonId.Should().Be(implementator.Id);
        history[0].ActorName.Should().Be("Rina Wijaya");

        history[1].PreviousStatus.Should().Be(RequestStatusNames.Captured);
        history[1].NewStatus.Should().Be(RequestStatusNames.Evaluating);
        history[1].AssignedOwnerPersonId.Should().Be(programmer.Id);
        history[1].AssignedOwnerName.Should().Be("Agus Santoso");

        history[2].PreviousStatus.Should().Be(RequestStatusNames.Evaluating);
        history[2].NewStatus.Should().Be(RequestStatusNames.Accepted);
        history[2].ActorPersonId.Should().Be(programmer.Id);

        history[3].PreviousStatus.Should().Be(RequestStatusNames.Accepted);
        history[3].NewStatus.Should().Be(RequestStatusNames.InProgress);
        history[3].ActorPersonId.Should().Be(programmer.Id);

        history[4].PreviousStatus.Should().Be(RequestStatusNames.InProgress);
        history[4].NewStatus.Should().Be(RequestStatusNames.Completed);
        history[4].ActorPersonId.Should().Be(programmer.Id);
    }

    [Fact]
    public async Task RejectRequest_flow_persists_rejected_resolution_and_state_history_against_SqlServer()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var requestQueryService = scope.ServiceProvider.GetRequiredService<IRequestQueryService>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        var implementator = await mediator.Send(new CreatePersonCommand(
            "Siti",
            "Aminah",
            $"siti.{Guid.NewGuid():N}@cakra.id"));

        var programmer = await mediator.Send(new CreatePersonCommand(
            "Hendra",
            "Gunawan",
            $"hendra.{Guid.NewGuid():N}@cakra.id"));

        currentContext?.Initialize(Guid.NewGuid(), implementator.Id, new[] { "Implementator" });

        var recorded = await mediator.Send(new RecordRequestCommand(
            Title: "Duplicate legacy report request",
            Description: "Request already covered by standard financial export.",
            RequestType: "Support",
            Priority: "LOW"));

        await mediator.Send(new AssignRequestOwnerCommand(recorded.Id, programmer.Id));

        currentContext?.Initialize(Guid.NewGuid(), programmer.Id, new[] { "Programmer" });
        await mediator.Send(new EvaluateRequestCommand(recorded.Id, "Verified duplicate of existing standard report."));

        var rejected = await mediator.Send(new RejectRequestCommand(
            RequestId: recorded.Id,
            Reason: "Duplicate of standard report RPT-FIN-004; no code change required."));

        rejected.Status.Should().Be(RequestStatusNames.Rejected);
        rejected.Resolution.Should().NotBeNull();
        rejected.Resolution!.Outcome.Should().Be(ResolutionOutcomeNames.Rejected);

        var detail = await requestQueryService.GetRequestById(recorded.Id);
        detail.Should().NotBeNull();
        detail!.Status.Should().Be(RequestStatusNames.Rejected);
        detail.Resolution.Should().NotBeNull();
        detail.Resolution!.Outcome.Should().Be(ResolutionOutcomeNames.Rejected);
        detail.Resolution.Description.Should().Be("Duplicate of standard report RPT-FIN-004; no code change required.");
        detail.Resolution.ResolvedBy.Should().Be(programmer.Id);

        var history = await mediator.Send(new GetRequestStateHistoryQuery(recorded.Id));
        history.Should().HaveCount(3);
        history.Last().PreviousStatus.Should().Be(RequestStatusNames.Evaluating);
        history.Last().NewStatus.Should().Be(RequestStatusNames.Rejected);
        history.Last().ActorPersonId.Should().Be(programmer.Id);
    }

    [Fact]
    public async Task ListMyAssignedRequests_and_GetFilteredRequestGrid_return_accurate_filtered_and_paginated_results()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var requestQueryService = scope.ServiceProvider.GetRequiredService<IRequestQueryService>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        // Seed 2 programmers, 2 customers, 2 products
        var progA = await mediator.Send(new CreatePersonCommand("Adi", "Pratama", $"adi.{Guid.NewGuid():N}@cakra.id"));
        var progB = await mediator.Send(new CreatePersonCommand("Bayu", "Nugroho", $"bayu.{Guid.NewGuid():N}@cakra.id"));

        var cust1 = await mediator.Send(new CreateCustomerCommand($"C1-{Guid.NewGuid():N}"[..14], "RS Harapan Kita", true));
        var cust2 = await mediator.Send(new CreateCustomerCommand($"C2-{Guid.NewGuid():N}"[..14], "RS Fatmawati", true));

        var prod1 = await mediator.Send(new CreateProductCommand($"P1-{Guid.NewGuid():N}"[..12], "MyHospital Core", "Core HIS", progA.Id));
        var prod2 = await mediator.Send(new CreateProductCommand($"P2-{Guid.NewGuid():N}"[..12], "BTrade3 Billing", "Billing Engine", progB.Id));

        currentContext?.Initialize(Guid.NewGuid(), progA.Id, new[] { "Programmer" });

        // Req 1: Cust1 + Prod1, assigned to progA -> IN_PROGRESS
        var req1 = await mediator.Send(new RecordRequestCommand(
            Title: "Req 1 - InProgress for ProgA",
            Description: "First request",
            CustomerId: cust1.Id,
            ProductId: prod1.Id,
            RequestType: "Bug",
            Priority: "HIGH"));
        await mediator.Send(new AssignRequestOwnerCommand(req1.Id, progA.Id));
        await mediator.Send(new AcceptRequestResponsibilityCommand(req1.Id, "Working on Req 1"));

        // Req 2: Cust1 + Prod2, assigned to progA -> COMPLETED
        var req2 = await mediator.Send(new RecordRequestCommand(
            Title: "Req 2 - Completed for ProgA",
            Description: "Second request",
            CustomerId: cust1.Id,
            ProductId: prod2.Id,
            RequestType: "Feature",
            Priority: "NORMAL"));
        await mediator.Send(new AssignRequestOwnerCommand(req2.Id, progA.Id));
        await mediator.Send(new AcceptRequestResponsibilityCommand(req2.Id));
        await mediator.Send(new ReviewRequestCompletionCommand(req2.Id, "Completed Req 2"));

        // Req 3: Cust2 + Prod1, assigned to progB -> EVALUATING
        var req3 = await mediator.Send(new RecordRequestCommand(
            Title: "Req 3 - Evaluating for ProgB",
            Description: "Third request",
            CustomerId: cust2.Id,
            ProductId: prod1.Id,
            RequestType: "Support",
            Priority: "LOW"));
        await mediator.Send(new AssignRequestOwnerCommand(req3.Id, progB.Id));

        // Req 4: Cust2 + Prod2, unassigned -> CAPTURED
        var req4 = await mediator.Send(new RecordRequestCommand(
            Title: "Req 4 - Unassigned Captured",
            Description: "Fourth request",
            CustomerId: cust2.Id,
            ProductId: prod2.Id,
            RequestType: "Bug",
            Priority: "URGENT"));

        // 1. Verify ListMyAssignedRequests uses CurrentContextProvider.CurrentPersonId (progA)
        var myAssignedForProgA = await requestQueryService.ListMyAssignedRequests();
        myAssignedForProgA.Should().HaveCount(2);
        myAssignedForProgA.Select(r => r.Id).Should().BeEquivalentTo(new[] { req1.Id, req2.Id });
        myAssignedForProgA.Should().OnlyContain(r => r.OwnerPersonId == progA.Id && r.OwnerName == "Adi Pratama");

        // Switch ambient context to progB and verify via MediatR query
        currentContext?.Initialize(Guid.NewGuid(), progB.Id, new[] { "Programmer" });
        var myAssignedForProgB = await mediator.Send(new ListMyAssignedRequestsQuery());
        myAssignedForProgB.Should().ContainSingle().Which.Id.Should().Be(req3.Id);

        // 2. Verify GetFilteredRequestGrid with no filters (returns all 4)
        var allGrid = await requestQueryService.GetFilteredRequestGrid();
        allGrid.TotalCount.Should().Be(4);
        allGrid.Items.Should().HaveCount(4);

        // 3. Filter by Status = IN_PROGRESS
        var inProgressGrid = await requestQueryService.GetFilteredRequestGrid(status: RequestStatusNames.InProgress);
        inProgressGrid.TotalCount.Should().Be(1);
        inProgressGrid.Items.Should().ContainSingle().Which.Id.Should().Be(req1.Id);

        // 4. Filter by Assignee = progA
        var progAGrid = await requestQueryService.GetFilteredRequestGrid(
            status: null,
            assigneePersonId: progA.Id);
        progAGrid.TotalCount.Should().Be(2);
        progAGrid.Items.Select(r => r.Id).Should().BeEquivalentTo(new[] { req1.Id, req2.Id });

        // 5. Filter by Customer = cust1
        var cust1Grid = await requestQueryService.GetFilteredRequestGrid(
            status: null,
            assigneePersonId: null,
            customerId: cust1.Id);
        cust1Grid.TotalCount.Should().Be(2);
        cust1Grid.Items.Select(r => r.Id).Should().BeEquivalentTo(new[] { req1.Id, req2.Id });

        // 6. Filter by Product = prod2
        var prod2Grid = await requestQueryService.GetFilteredRequestGrid(
            status: null,
            assigneePersonId: null,
            customerId: null,
            productId: prod2.Id);
        prod2Grid.TotalCount.Should().Be(2);
        prod2Grid.Items.Select(r => r.Id).Should().BeEquivalentTo(new[] { req2.Id, req4.Id });

        // 7. Combined filter: Customer = cust1 AND Product = prod2 AND Status = COMPLETED
        var combinedGrid = await mediator.Send(new GetFilteredRequestGridQuery(
            Status: RequestStatusNames.Completed,
            AssigneePersonId: progA.Id,
            CustomerId: cust1.Id,
            ProductId: prod2.Id));
        combinedGrid.TotalCount.Should().Be(1);
        combinedGrid.Items.Should().ContainSingle().Which.Id.Should().Be(req2.Id);

        // 8. Pagination: PageSize = 2, Page = 1 and Page = 2
        var page1 = await requestQueryService.GetFilteredRequestGrid(new RequestGridFilter
        {
            Page = 1,
            PageSize = 2
        });
        var page2 = await requestQueryService.GetFilteredRequestGrid(new RequestGridFilter
        {
            Page = 2,
            PageSize = 2
        });

        page1.TotalCount.Should().Be(4);
        page1.TotalPages.Should().Be(2);
        page1.Items.Should().HaveCount(2);

        page2.TotalCount.Should().Be(4);
        page2.TotalPages.Should().Be(2);
        page2.Items.Should().HaveCount(2);

        page1.Items.Select(x => x.Id).Concat(page2.Items.Select(x => x.Id))
            .Should().BeEquivalentTo(new[] { req1.Id, req2.Id, req3.Id, req4.Id });

        // 9. Verify RequestExists and GetRequestsByIds helper methods
        (await requestQueryService.RequestExistsAsync(req1.Id)).Should().BeTrue();
        (await requestQueryService.RequestExistsAsync(Guid.NewGuid())).Should().BeFalse();

        var batch = await requestQueryService.GetRequestsByIdsAsync(new[] { req1.Id, req3.Id });
        batch.Should().HaveCount(2);
        batch.Select(r => r.Id).Should().BeEquivalentTo(new[] { req1.Id, req3.Id });
    }
}
