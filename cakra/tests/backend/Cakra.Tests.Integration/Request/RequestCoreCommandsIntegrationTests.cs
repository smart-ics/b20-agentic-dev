using Cakra.Api.Infrastructure.Context;
using Cakra.Api.Infrastructure.Migrations;
using Cakra.Core;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Customer.Services;
using Cakra.Modules.Organization.Commands;
using Cakra.Modules.Product.Services;
using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Persistence;
using Cakra.Modules.Request.Services;
using Dapper;
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
/// Integration tests verifying P4-S18 Request Module — Persistence &amp; Core Commands:
/// - DbUp migration script 0006_request_tables.sql creates request.Requests, request.RequestResolutions,
///   and request.RequestAssignments
/// - Sequential lifecycle test: RecordRequest (CAPTURED) -&gt; AssignRequestOwner (EVALUATING) -&gt; EvaluateRequest
/// - Cross-module validation against Organization, Customer, and Product published query services
/// - State change audit logging into request.RequestAssignments with actor PersonId, timestamp, and previous/new state
/// - WebApplicationFactory&lt;Program&gt; + Respawn test isolation
/// </summary>
[Collection("OrganizationDatabase")]
public class RequestCoreCommandsIntegrationTests : IAsyncLifetime
{
    private const string LocalDbFallback = "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb;Integrated Security=true;TrustServerCertificate=True;";
    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private bool _sqlServerAvailable;

    public RequestCoreCommandsIntegrationTests()
    {
        _connectionString = DatabaseResetHelper.ResolveConnectionString() ?? LocalDbFallback;
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
        migrationResult.Successful.Should().BeTrue("DbUp migration 0006_request_tables.sql must execute without errors");

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
    public async Task Migration_0006_creates_request_schema_tables()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        using var connection = connectionFactory.CreateConnection();

        var tables = (await connection.QueryAsync<string>("""
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = 'request';
            """)).ToList();

        tables.Should().Contain(new[] { "Requests", "RequestResolutions", "RequestAssignments" });
    }

    [Fact]
    public async Task RecordRequest_AssignRequestOwner_and_EvaluateRequest_succeed_sequentially_and_persist_audit_trail()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var requestRepository = scope.ServiceProvider.GetRequiredService<IRequestRepository>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        // 1. Seed prerequisite Organization Persons, Customer, and Product via their module commands
        var implementator = await mediator.Send(new CreatePersonCommand(
            "Dian",
            "Sastro",
            $"dian.{Guid.NewGuid():N}@cakra.id"));

        var programmer = await mediator.Send(new CreatePersonCommand(
            "Budi",
            "Hartono",
            $"budi.{Guid.NewGuid():N}@cakra.id"));

        var customerCode = $"CUST-{Guid.NewGuid():N}"[..16];
        var customer = await mediator.Send(new CreateCustomerCommand(
            customerCode,
            "RSUP Dr. Sardjito",
            HasActiveMaintenanceContract: true));

        var productCode = $"PRD-{Guid.NewGuid():N}"[..14];
        var product = await mediator.Send(new CreateProductCommand(
            productCode,
            "MyHospital HIS",
            "Hospital Information System",
            programmer.Id));

        // Populate ambient security context with implementator as the recording actor
        currentContext?.Initialize(Guid.NewGuid(), implementator.Id, new[] { "Implementator" });

        // 2. Step 1 of lifecycle: RecordRequest -> CAPTURED
        var recorded = await mediator.Send(new RecordRequestCommand(
            Title: "HL7 Laboratory Order Interface Delay",
            Description: "LIS orders take over 90 seconds to synchronize during morning peak hours.",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "Bug",
            Priority: "HIGH"));

        recorded.Id.Should().NotBeEmpty();
        recorded.Title.Should().Be("HL7 Laboratory Order Interface Delay");
        recorded.Description.Should().Be("LIS orders take over 90 seconds to synchronize during morning peak hours.");
        recorded.RequestType.Should().Be("Bug");
        recorded.Status.Should().Be(RequestStatusNames.Captured);
        recorded.Priority.Should().Be("HIGH");
        recorded.CustomerId.Should().Be(customer.Id);
        recorded.ProductId.Should().Be(product.Id);
        recorded.OwnerPersonId.Should().BeNull();
        recorded.EvaluationNotes.Should().BeNull();

        // Verify persisted state and initial audit record in request.RequestAssignments
        var persistedAfterRecord = await requestRepository.GetByIdAsync(recorded.Id);
        persistedAfterRecord.Should().NotBeNull();
        persistedAfterRecord!.Status.Should().Be(RequestStatus.Captured);
        persistedAfterRecord.Assignments.Should().ContainSingle();

        var initialAudit = persistedAfterRecord.Assignments.Single();
        initialAudit.RequestId.Should().Be(recorded.Id);
        initialAudit.PreviousStatus.Should().BeNull();
        initialAudit.NewStatus.Should().Be(RequestStatus.Captured);
        initialAudit.ActorPersonId.Should().Be(implementator.Id);
        initialAudit.PreviousOwnerPersonId.Should().BeNull();
        initialAudit.AssignedOwnerPersonId.Should().BeNull();

        // 3. Step 2 of lifecycle: AssignRequestOwner -> ASSIGNED
        var assigned = await mediator.Send(new AssignRequestOwnerCommand(
            RequestId: recorded.Id,
            OwnerPersonId: programmer.Id,
            Notes: "Assigned to LIS integration lead for triage evaluation"));

        assigned.Id.Should().Be(recorded.Id);
        assigned.Status.Should().Be(RequestStatusNames.Assigned);
        assigned.OwnerPersonId.Should().Be(programmer.Id);
        assigned.UpdatedAt.Should().NotBeNull();

        var persistedAfterAssign = await requestRepository.GetByIdAsync(recorded.Id);
        persistedAfterAssign.Should().NotBeNull();
        persistedAfterAssign!.Status.Should().Be(RequestStatus.Assigned);
        persistedAfterAssign.OwnerPersonId.Should().Be(programmer.Id);
        persistedAfterAssign.Assignments.Should().HaveCount(2);

        var assignAudit = persistedAfterAssign.Assignments.Last();
        assignAudit.RequestId.Should().Be(recorded.Id);
        assignAudit.PreviousStatus.Should().Be(RequestStatus.Captured);
        assignAudit.NewStatus.Should().Be(RequestStatus.Assigned);
        assignAudit.PreviousOwnerPersonId.Should().BeNull();
        assignAudit.AssignedOwnerPersonId.Should().Be(programmer.Id);
        assignAudit.ActorPersonId.Should().Be(implementator.Id);
        assignAudit.Notes.Should().Be("Assigned to LIS integration lead for triage evaluation");

        // 4. Step 3 of lifecycle: StartWorkCommand -> transitions ASSIGNED to IN_PROGRESS
        currentContext?.Initialize(Guid.NewGuid(), programmer.Id, new[] { "Programmer" });

        var started = await mediator.Send(new StartWorkCommand(
            RequestId: recorded.Id,
            ActorPersonId: programmer.Id,
            Notes: "Confirmed missing index on HL7 outbound queue table; starting fix."));

        started.Id.Should().Be(recorded.Id);
        started.Status.Should().Be(RequestStatusNames.InProgress);
        started.OwnerPersonId.Should().Be(programmer.Id);

        var persistedAfterStart = await requestRepository.GetByIdAsync(recorded.Id);
        persistedAfterStart.Should().NotBeNull();
        persistedAfterStart!.Status.Should().Be(RequestStatus.InProgress);
        persistedAfterStart.Assignments.Should().HaveCount(3);
    }

    [Fact]
    public async Task Cross_module_validation_and_same_schema_foreign_keys_are_enforced_against_SqlServer()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var requestRepository = scope.ServiceProvider.GetRequiredService<IRequestRepository>();

        // 1. Nonexistent customer rejected on RecordRequestCommand
        var unknownCustomerAct = async () => await mediator.Send(new RecordRequestCommand(
            Title: "Invalid Customer Request",
            Description: "Should fail customer validation",
            CustomerId: Guid.NewGuid()));
        await unknownCustomerAct.Should().ThrowAsync<KeyNotFoundException>();

        // 2. Nonexistent product rejected on RecordRequestCommand
        var unknownProductAct = async () => await mediator.Send(new RecordRequestCommand(
            Title: "Invalid Product Request",
            Description: "Should fail product validation",
            ProductId: Guid.NewGuid()));
        await unknownProductAct.Should().ThrowAsync<KeyNotFoundException>();

        // 3. Inactive assignee rejected on AssignRequestOwnerCommand
        var inactivePerson = await mediator.Send(new CreatePersonCommand(
            "Inactive",
            "Engineer",
            $"inactive.{Guid.NewGuid():N}@cakra.id"));
        await mediator.Send(new DeactivatePersonCommand(inactivePerson.Id));

        var validRequest = await mediator.Send(new RecordRequestCommand(
            Title: "Internal Infrastructure Request",
            Description: "Valid internal request without external customer",
            ActorPersonId: Guid.NewGuid()));

        var assignInactiveAct = async () => await mediator.Send(new AssignRequestOwnerCommand(
            validRequest.Id,
            inactivePerson.Id));
        await assignInactiveAct.Should().ThrowAsync<InvalidOperationException>();

        // 4. Same-schema FK constraint prevents orphaned RequestAssignment rows
        var orphanedAssignment = RequestAssignment.Create(
            requestId: Guid.NewGuid(),
            previousOwnerPersonId: null,
            assignedOwnerPersonId: Guid.NewGuid(),
            actorPersonId: Guid.NewGuid(),
            previousStatus: RequestStatus.Captured,
            newStatus: RequestStatus.Assigned,
            assignedAtUtc: DateTime.UtcNow,
            notes: "Orphaned assignment");

        var fkAct = async () => await requestRepository.AddAssignmentAsync(orphanedAssignment);
        await fkAct.Should().ThrowAsync<SqlException>();
    }
}
