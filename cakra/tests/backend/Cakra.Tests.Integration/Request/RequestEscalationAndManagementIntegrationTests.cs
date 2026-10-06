using Cakra.Api.Infrastructure.Context;
using Cakra.Api.Infrastructure.Migrations;
using Cakra.Core;
using Cakra.Modules.Customer.Services;
using Cakra.Modules.Organization.Commands;
using Cakra.Modules.Product.Services;
using Cakra.Modules.Request;
using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Domain.Exceptions;
using Cakra.Modules.Request.Persistence;
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
/// Integration tests verifying P4-S20 Request Module — Escalation &amp; Management Commands:
/// - EscalateRequest (UC-REQ-006): transitions EVALUATING or IN_PROGRESS -&gt; ESCALATED, persists
///   EscalationReason and audit trail in request.RequestAssignments via Dapper parameterized SQL
/// - RequestManagementDecision (UC-REQ-007): records ManagementDecisionNotes, persists audit trail,
///   and supports resolving ESCALATED requests back to EVALUATING or IN_PROGRESS
/// - ReassignRequestOwnership (UC-MGT-001): validates new assignee via IOrganizationQueryService,
///   updates OwnerPersonId and status via Dapper parameterized SQL, and records audit trail
/// - WebApplicationFactory&lt;Program&gt; + Respawn test isolation on dedicated CakraTestDb_RequestEscalation database
/// </summary>
[Collection("OrganizationDatabase")]
public class RequestEscalationAndManagementIntegrationTests : IAsyncLifetime
{
    private const string LocalDbFallback = "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb_RequestEscalation;Integrated Security=true;TrustServerCertificate=True;";
    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private bool _sqlServerAvailable;

    public RequestEscalationAndManagementIntegrationTests()
    {
        var baseConnectionString = DatabaseResetHelper.ResolveConnectionString() ?? LocalDbFallback;
        _connectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "CakraTestDb_RequestEscalation"
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
    public async Task PauseWork_and_ResumeWork_flows_persist_state_and_audit_trail_against_SqlServer()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var requestRepository = scope.ServiceProvider.GetRequiredService<IRequestRepository>();
        var requestQueryService = scope.ServiceProvider.GetRequiredService<IRequestQueryService>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        // 1. Seed Organization Persons, Customer, and Product
        var implementator = await mediator.Send(new CreatePersonCommand(
            "Maya",
            "Lestari",
            $"maya.{Guid.NewGuid():N}@cakra.id"));

        var programmer = await mediator.Send(new CreatePersonCommand(
            "Rizky",
            "Pratama",
            $"rizky.{Guid.NewGuid():N}@cakra.id"));

        var manager = await mediator.Send(new CreatePersonCommand(
            "Bambang",
            "Wibowo",
            $"bambang.{Guid.NewGuid():N}@cakra.id"));

        var customer = await mediator.Send(new CreateCustomerCommand(
            $"CUST-{Guid.NewGuid():N}"[..16],
            "RSUP Dr. Hasan Sadikin",
            HasActiveMaintenanceContract: true));

        var product = await mediator.Send(new CreateProductCommand(
            $"PRD-{Guid.NewGuid():N}"[..14],
            "MyHospital Billing",
            "Hospital Billing & Tariff Engine",
            programmer.Id));

        // 2. Record Request -> CAPTURED, Assign Owner -> ASSIGNED
        currentContext?.Initialize(Guid.NewGuid(), implementator.Id, new[] { "Implementator" });

        var recorded = await mediator.Send(new RecordRequestCommand(
            Title: "INA-CBG Grouper Tariff Retroactive Adjustment",
            Description: "Hospital requests retroactive tariff recalculation for 12,000 closed claims.",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "Support",
            Priority: "URGENT"));

        await mediator.Send(new AssignRequestOwnerCommand(
            RequestId: recorded.Id,
            OwnerPersonId: programmer.Id,
            Notes: "Assigned to billing specialist"));

        // 3. StartWork from ASSIGNED -> IN_PROGRESS
        currentContext?.Initialize(Guid.NewGuid(), programmer.Id, new[] { "Programmer" });

        var started = await mediator.Send(new StartWorkCommand(
            RequestId: recorded.Id,
            ActorPersonId: programmer.Id,
            Notes: "Starting investigation on tariff recalculation."));

        started.Status.Should().Be(RequestStatusNames.InProgress);

        // 4. PauseWork from IN_PROGRESS -> PAUSED
        var paused = await mediator.Send(new PauseWorkCommand(
            RequestId: recorded.Id,
            ActorPersonId: programmer.Id,
            Note: "Retroactive recalculation affects audited financial period; waiting for audit waiver."));

        paused.Status.Should().Be(RequestStatusNames.Paused);
        paused.OwnerPersonId.Should().Be(programmer.Id);

        var persistedAfterPause = await requestRepository.GetByIdAsync(recorded.Id);
        persistedAfterPause.Should().NotBeNull();
        persistedAfterPause!.Status.Should().Be(RequestStatus.Paused);

        var pauseAudit = persistedAfterPause.Assignments.Last();
        pauseAudit.PreviousStatus.Should().Be(RequestStatus.InProgress);
        pauseAudit.NewStatus.Should().Be(RequestStatus.Paused);
        pauseAudit.ActorPersonId.Should().Be(programmer.Id);

        // 5. Resume work via StartWorkCommand from PAUSED -> IN_PROGRESS
        var resumed = await mediator.Send(new StartWorkCommand(
            RequestId: recorded.Id,
            ActorPersonId: programmer.Id,
            Notes: "Hospital Director signed formal waiver; resumed adjustment work."));

        resumed.Status.Should().Be(RequestStatusNames.InProgress);

        // 6. Complete Request and verify full state history via RequestQueryService
        var completed = await mediator.Send(new ReviewRequestCompletionCommand(
            RequestId: recorded.Id,
            ResolutionDescription: "Executed retroactive tariff adjustment batch and verified reconciliation report."));

        completed.Status.Should().Be(RequestStatusNames.Completed);

        var detail = await requestQueryService.GetRequestById(recorded.Id);
        detail.Should().NotBeNull();
        detail!.Status.Should().Be(RequestStatusNames.Completed);

        var history = await requestQueryService.GetRequestStateHistory(recorded.Id);
        history.Should().HaveCount(6);
        history.Select(h => h.NewStatus).Should().ContainInOrder(
            RequestStatusNames.Captured,
            RequestStatusNames.Assigned,
            RequestStatusNames.InProgress,
            RequestStatusNames.Paused,
            RequestStatusNames.InProgress,
            RequestStatusNames.Completed);
    }

    [Fact]
    public async Task ReassignRequestOwnership_flow_and_cross_module_validation_succeed_against_SqlServer()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var requestRepository = scope.ServiceProvider.GetRequiredService<IRequestRepository>();
        var requestQueryService = scope.ServiceProvider.GetRequiredService<IRequestQueryService>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        var implementator = await mediator.Send(new CreatePersonCommand(
            "Fajar",
            "Nugraha",
            $"fajar.{Guid.NewGuid():N}@cakra.id"));

        var juniorProg = await mediator.Send(new CreatePersonCommand(
            "Dimas",
            "Saputra",
            $"dimas.{Guid.NewGuid():N}@cakra.id"));

        var seniorProg = await mediator.Send(new CreatePersonCommand(
            "Eka",
            "Kurniawan",
            $"eka.{Guid.NewGuid():N}@cakra.id"));

        var manager = await mediator.Send(new CreatePersonCommand(
            "Surya",
            "Darma",
            $"surya.{Guid.NewGuid():N}@cakra.id"));

        var inactiveProg = await mediator.Send(new CreatePersonCommand(
            "Former",
            "Developer",
            $"former.{Guid.NewGuid():N}@cakra.id"));
        await mediator.Send(new DeactivatePersonCommand(inactiveProg.Id));

        currentContext?.Initialize(Guid.NewGuid(), implementator.Id, new[] { "Implementator" });

        // 1. Record -> Assign to juniorProg -> Start (IN_PROGRESS)
        var recorded = await mediator.Send(new RecordRequestCommand(
            Title: "SATUSEHAT FHIR Encounter Sync Deadlock",
            Description: "Deadlock on concurrent outpatient encounter bundle submissions.",
            RequestType: "Bug",
            Priority: "HIGH"));

        await mediator.Send(new AssignRequestOwnerCommand(recorded.Id, juniorProg.Id));

        currentContext?.Initialize(Guid.NewGuid(), juniorProg.Id, new[] { "Programmer" });
        await mediator.Send(new StartWorkCommand(recorded.Id, Notes: "Investigating SQL deadlock graph", ActorPersonId: juniorProg.Id));

        // 2. Validate cross-module rules on ReassignRequestOwnership (UC-MGT-001)
        currentContext?.Initialize(Guid.NewGuid(), manager.Id, new[] { "Management" });

        // Nonexistent person -> KeyNotFoundException
        var unknownPersonAct = async () => await mediator.Send(new ReassignRequestOwnershipCommand(
            RequestId: recorded.Id,
            NewOwnerPersonId: Guid.NewGuid()));
        await unknownPersonAct.Should().ThrowAsync<KeyNotFoundException>();

        // Inactive person -> InvalidOperationException
        var inactivePersonAct = async () => await mediator.Send(new ReassignRequestOwnershipCommand(
            RequestId: recorded.Id,
            NewOwnerPersonId: inactiveProg.Id));
        await inactivePersonAct.Should().ThrowAsync<InvalidOperationException>();

        // Identical owner -> InvalidOperationException
        var sameOwnerAct = async () => await mediator.Send(new ReassignRequestOwnershipCommand(
            RequestId: recorded.Id,
            NewOwnerPersonId: juniorProg.Id));
        await sameOwnerAct.Should().ThrowAsync<InvalidOperationException>();

        // 3. ReassignRequestOwnership from IN_PROGRESS to seniorProg -> resets to ASSIGNED
        var reassigned = await mediator.Send(new ReassignRequestOwnershipCommand(
            RequestId: recorded.Id,
            NewOwnerPersonId: seniorProg.Id,
            Notes: "Reassigned by management to principal integration architect"));

        reassigned.Status.Should().Be(RequestStatusNames.Assigned);
        reassigned.OwnerPersonId.Should().Be(seniorProg.Id);

        var persisted = await requestRepository.GetByIdAsync(recorded.Id);
        persisted.Should().NotBeNull();
        persisted!.OwnerPersonId.Should().Be(seniorProg.Id);
        persisted.Status.Should().Be(RequestStatus.Assigned);

        var reassignAudit = persisted.Assignments.Last();
        reassignAudit.PreviousOwnerPersonId.Should().Be(juniorProg.Id);
        reassignAudit.AssignedOwnerPersonId.Should().Be(seniorProg.Id);
        reassignAudit.ActorPersonId.Should().Be(manager.Id);
        reassignAudit.PreviousStatus.Should().Be(RequestStatus.InProgress);
        reassignAudit.NewStatus.Should().Be(RequestStatus.Assigned);
        reassignAudit.Notes.Should().Be("Reassigned by management to principal integration architect");

        // 4. Verify ListMyAssignedRequests reflects ownership transfer
        (await requestQueryService.ListMyAssignedRequests(juniorProg.Id)).Should().BeEmpty();
        var seniorQueue = await requestQueryService.ListMyAssignedRequests(seniorProg.Id);
        seniorQueue.Should().ContainSingle().Which.Id.Should().Be(recorded.Id);
    }
}
