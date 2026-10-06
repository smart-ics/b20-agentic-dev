using Cakra.Api.Infrastructure.Context;
using Cakra.Api.Infrastructure.Migrations;
using Cakra.Core;
using Cakra.Modules.Customer.Services;
using Cakra.Modules.Organization.Commands;
using Cakra.Modules.Product.Services;
using Cakra.Modules.Request;
using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Services;
using Cakra.Modules.WorkPackage;
using Cakra.Modules.WorkPackage.Domain;
using Cakra.Modules.WorkPackage.Domain.Exceptions;
using Cakra.Modules.WorkPackage.Services;
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

namespace Cakra.Tests.Integration.WorkPackage;

/// <summary>
/// Integration tests verifying P5-S25 Work Package Module — Application Services &amp; Persistence:
/// - DbUp migration script <c>0007_workpackage_tables.sql</c> creates <c>workpackage.WorkPackages</c>
///   and <c>workpackage.WorkPackageRequests</c> with intra-schema FK only and zero cross-schema FKs.
/// - Full Work Package lifecycle (<c>CreateWorkPackage</c> -&gt; <c>UpdateObjective</c> -&gt;
///   <c>AssignOwner</c> -&gt; <c>AddRequestToWorkPackage</c> -&gt; <c>ActivateWorkPackage</c> -&gt;
///   <c>RemoveRequestFromWorkPackage</c> -&gt; <c>CloseWorkPackage</c>) via MediatR and Dapper.
/// - <c>WorkPackageQueryService</c> queries (<c>GetWorkPackageById</c>, <c>ListWorkPackages</c>,
///   <c>GetWorkPackageScope</c>, <c>GetRequestWorkPackage</c>) with cross-module enrichment.
/// - Business Rule 9 enforcement across active Work Packages.
/// - Closing a Work Package does not alter constituent Request lifecycle states or owners.
/// - Isolated SQL Server test database (<c>CakraTestDb_WorkPackage</c>) + Respawn cleanup.
/// </summary>
public sealed class WorkPackageModuleIntegrationTests : IAsyncLifetime
{
    private const string LocalDbFallback = "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb_WorkPackage;Integrated Security=true;TrustServerCertificate=True;";
    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private bool _sqlServerAvailable;

    public WorkPackageModuleIntegrationTests()
    {
        var baseConnectionString = DatabaseResetHelper.ResolveConnectionString() ?? LocalDbFallback;
        _connectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "CakraTestDb_WorkPackage"
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
            SchemasToInclude = ["workpackage", "request", "product", "customer", "organization"],
            TablesToIgnore = [new Table("dbo", "__SchemaVersions"), new Table("dbo", "SchemaVersions")]
        });

        await respawner.ResetAsync(conn);
    }

    [Fact]
    public async Task Migration_0007_creates_workpackage_schema_tables_with_intra_schema_fk_only()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string tablesSql = """
            SELECT t.name
            FROM sys.tables t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = N'workpackage'
            ORDER BY t.name;
            """;

        var tables = (await conn.QueryAsync<string>(tablesSql)).ToList();
        tables.Should().BeEquivalentTo(new[] { "WorkPackageRequests", "WorkPackages" });

        const string foreignKeysSql = """
            SELECT
                fk.name AS ForeignKeyName,
                SCHEMA_NAME(tp.schema_id) AS ParentSchema,
                tp.name AS ParentTable,
                SCHEMA_NAME(tr.schema_id) AS ReferencedSchema,
                tr.name AS ReferencedTable
            FROM sys.foreign_keys fk
            INNER JOIN sys.tables tp ON tp.object_id = fk.parent_object_id
            INNER JOIN sys.tables tr ON tr.object_id = fk.referenced_object_id
            WHERE SCHEMA_NAME(tp.schema_id) = N'workpackage';
            """;

        var fks = (await conn.QueryAsync<ForeignKeyInfo>(foreignKeysSql)).ToList();
        fks.Should().ContainSingle();
        fks[0].ForeignKeyName.Should().Be("FK_WorkPackageRequests_WorkPackages");
        fks[0].ParentSchema.Should().Be("workpackage");
        fks[0].ParentTable.Should().Be("WorkPackageRequests");
        fks[0].ReferencedSchema.Should().Be("workpackage");
        fks[0].ReferencedTable.Should().Be("WorkPackages");
    }

    [Fact]
    public async Task Full_WorkPackage_lifecycle_request_membership_and_queries_succeed_against_SqlServer()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var workPackageQueryService = scope.ServiceProvider.GetRequiredService<IWorkPackageQueryService>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        // 1. Seed Organization Persons, Customers, Products, and Requests
        var owner1 = await mediator.Send(new CreatePersonCommand(
            "Dewi",
            "Lestari",
            $"dewi.{Guid.NewGuid():N}@cakra.id"));

        var owner2 = await mediator.Send(new CreatePersonCommand(
            "Fajar",
            "Pratama",
            $"fajar.{Guid.NewGuid():N}@cakra.id"));

        var programmer = await mediator.Send(new CreatePersonCommand(
            "Rizky",
            "Hidayat",
            $"rizky.{Guid.NewGuid():N}@cakra.id"));

        var customer1 = await mediator.Send(new CreateCustomerCommand(
            $"CUST1-{Guid.NewGuid():N}"[..15],
            "RSUP Dr. Sardjito",
            HasActiveMaintenanceContract: true));

        var customer2 = await mediator.Send(new CreateCustomerCommand(
            $"CUST2-{Guid.NewGuid():N}"[..15],
            "RSUD Kota Yogyakarta",
            HasActiveMaintenanceContract: true));

        var product1 = await mediator.Send(new CreateProductCommand(
            $"PRD1-{Guid.NewGuid():N}"[..14],
            "MyHospital Billing",
            "Hospital Billing & INA-CBGs Integration",
            programmer.Id));

        var product2 = await mediator.Send(new CreateProductCommand(
            $"PRD2-{Guid.NewGuid():N}"[..14],
            "PenaEl Pharmacy",
            "Pharmacy Inventory & e-Prescription",
            programmer.Id));

        currentContext?.Initialize(Guid.NewGuid(), owner1.Id, new[] { "Management", "Implementator" });

        var req1 = await mediator.Send(new RecordRequestCommand(
            Title: "INA-CBGs tariff grouping validation fix",
            Description: "Ensure inpatient procedure codes map to updated tariff table.",
            CustomerId: customer1.Id,
            ProductId: product1.Id,
            RequestType: "Bug",
            Priority: "HIGH"));

        var req2 = await mediator.Send(new RecordRequestCommand(
            Title: "Discharge summary claim export PDF header",
            Description: "Include hospital accreditation badge on claim summary export.",
            CustomerId: customer1.Id,
            ProductId: product1.Id,
            RequestType: "Feature",
            Priority: "NORMAL"));

        var req3 = await mediator.Send(new RecordRequestCommand(
            Title: "BPJS SEP bridging retry timeout adjustment",
            Description: "Increase bridging timeout threshold during peak morning hours.",
            CustomerId: customer1.Id,
            ProductId: product1.Id,
            RequestType: "Support",
            Priority: "URGENT"));

        // Advance req1 to IN_PROGRESS and req2 to ASSIGNED
        await mediator.Send(new AssignRequestOwnerCommand(req1.Id, programmer.Id));
        currentContext?.Initialize(Guid.NewGuid(), programmer.Id, new[] { "Programmer" });
        await mediator.Send(new StartWorkCommand(req1.Id, Notes: "Implementing tariff fix", ActorPersonId: programmer.Id));

        await mediator.Send(new AssignRequestOwnerCommand(req2.Id, programmer.Id));

        // 2. Step 1: CreateWorkPackage -> DRAFT
        currentContext?.Initialize(Guid.NewGuid(), owner1.Id, new[] { "Management" });

        var createdWp = await mediator.Send(new CreateWorkPackageCommand(
            Name: "RSUP Sardjito Q4 Billing Stabilization",
            Objective: "Deliver claim and billing stabilization items prior to monthly closing.",
            OwnerPersonId: owner1.Id,
            CustomerId: customer1.Id,
            ProductId: product1.Id));

        createdWp.Id.Should().NotBeEmpty();
        createdWp.Name.Should().Be("RSUP Sardjito Q4 Billing Stabilization");
        createdWp.Status.Should().Be(WorkPackageStatusNames.Draft);
        createdWp.OwnerPersonId.Should().Be(owner1.Id);
        createdWp.CustomerId.Should().Be(customer1.Id);
        createdWp.ProductId.Should().Be(product1.Id);

        // Create a second work package for list filtering verification
        var secondWp = await mediator.Send(new CreateWorkPackageCommand(
            Name: "RSUD Jogja Pharmacy Rollout",
            Objective: "Coordinate pharmacy module rollout tasks.",
            OwnerPersonId: owner1.Id,
            CustomerId: customer2.Id,
            ProductId: product2.Id));

        // 3. Step 2: UpdateObjective
        var updatedWp = await mediator.Send(new UpdateObjectiveCommand(
            WorkPackageId: createdWp.Id,
            Name: "RSUP Sardjito Billing & Bridging Stabilization",
            Objective: "Deliver INA-CBGs and BPJS SEP bridging stabilization prior to monthly closing."));

        updatedWp.Name.Should().Be("RSUP Sardjito Billing & Bridging Stabilization");
        updatedWp.Objective.Should().Be("Deliver INA-CBGs and BPJS SEP bridging stabilization prior to monthly closing.");

        // 4. Step 3: AssignOwner -> owner2
        var reassignedWp = await mediator.Send(new AssignOwnerCommand(
            WorkPackageId: createdWp.Id,
            NewOwnerPersonId: owner2.Id));

        reassignedWp.OwnerPersonId.Should().Be(owner2.Id);

        // 5. Step 4: AddRequestToWorkPackage (req1 and req2)
        var wpWithReqs = await mediator.Send(new AddRequestToWorkPackageCommand(createdWp.Id, req1.Id));
        wpWithReqs.ActiveRequests.Should().ContainSingle().Which.RequestId.Should().Be(req1.Id);

        wpWithReqs = await mediator.Send(new AddRequestToWorkPackageCommand(createdWp.Id, req2.Id));
        wpWithReqs.ActiveRequests.Should().HaveCount(2);

        // 6. Step 5: ActivateWorkPackage -> ACTIVE
        var activatedWp = await mediator.Send(new ActivateWorkPackageCommand(createdWp.Id));
        activatedWp.Status.Should().Be(WorkPackageStatusNames.Active);

        // 7. Step 6: RemoveRequestFromWorkPackage (remove req2) and add req3
        var afterRemoveWp = await mediator.Send(new RemoveRequestFromWorkPackageCommand(createdWp.Id, req2.Id));
        afterRemoveWp.ActiveRequests.Should().ContainSingle().Which.RequestId.Should().Be(req1.Id);
        afterRemoveWp.Requests.Should().HaveCount(2);

        var afterAddReq3Wp = await mediator.Send(new AddRequestToWorkPackageCommand(createdWp.Id, req3.Id));
        afterAddReq3Wp.ActiveRequests.Select(r => r.RequestId).Should().BeEquivalentTo(new[] { req1.Id, req3.Id });

        // 8. Verify GetWorkPackageById (both direct and via MediatR)
        var fetchedWp = await workPackageQueryService.GetWorkPackageById(createdWp.Id);
        fetchedWp.Should().NotBeNull();
        fetchedWp!.Id.Should().Be(createdWp.Id);
        fetchedWp.Name.Should().Be("RSUP Sardjito Billing & Bridging Stabilization");
        fetchedWp.Objective.Should().Be("Deliver INA-CBGs and BPJS SEP bridging stabilization prior to monthly closing.");
        fetchedWp.Status.Should().Be(WorkPackageStatusNames.Active);
        fetchedWp.OwnerPersonId.Should().Be(owner2.Id);
        fetchedWp.OwnerName.Should().Be("Fajar Pratama");
        fetchedWp.CustomerId.Should().Be(customer1.Id);
        fetchedWp.CustomerName.Should().Be("RSUP Dr. Sardjito");
        fetchedWp.ProductId.Should().Be(product1.Id);
        fetchedWp.ProductName.Should().Be("MyHospital Billing");
        fetchedWp.ActiveRequestCount.Should().Be(2);
        fetchedWp.TotalRequestCount.Should().Be(3);

        var fetchedViaMediator = await mediator.Send(new GetWorkPackageByIdQuery(createdWp.Id));
        fetchedViaMediator.Should().NotBeNull();
        fetchedViaMediator!.OwnerName.Should().Be("Fajar Pratama");

        // 9. Verify GetWorkPackageScope returns active and historical requests enriched via IRequestQueryService
        var scopeItems = await workPackageQueryService.GetWorkPackageScope(createdWp.Id);
        scopeItems.Should().HaveCount(3);

        var scopeReq1 = scopeItems.Single(s => s.RequestId == req1.Id);
        scopeReq1.IsActive.Should().BeTrue();
        scopeReq1.RemovedAt.Should().BeNull();
        scopeReq1.Title.Should().Be("INA-CBGs tariff grouping validation fix");
        scopeReq1.Status.Should().Be(RequestStatusNames.InProgress);
        scopeReq1.Priority.Should().Be("HIGH");
        scopeReq1.OwnerPersonId.Should().Be(programmer.Id);
        scopeReq1.OwnerName.Should().Be("Rizky Hidayat");
        scopeReq1.CustomerName.Should().Be("RSUP Dr. Sardjito");
        scopeReq1.ProductName.Should().Be("MyHospital Billing");
        scopeReq1.Request.Should().NotBeNull();

        var scopeReq2 = scopeItems.Single(s => s.RequestId == req2.Id);
        scopeReq2.IsActive.Should().BeFalse();
        scopeReq2.RemovedAt.Should().NotBeNull();
        scopeReq2.Title.Should().Be("Discharge summary claim export PDF header");
        scopeReq2.Status.Should().Be(RequestStatusNames.Assigned);

        var scopeReq3 = scopeItems.Single(s => s.RequestId == req3.Id);
        scopeReq3.IsActive.Should().BeTrue();
        scopeReq3.Title.Should().Be("BPJS SEP bridging retry timeout adjustment");
        scopeReq3.Status.Should().Be(RequestStatusNames.Captured);
        scopeReq3.Priority.Should().Be("URGENT");

        // 10. Verify GetRequestWorkPackage
        var req1Package = await workPackageQueryService.GetRequestWorkPackage(req1.Id);
        req1Package.Should().NotBeNull();
        req1Package!.Id.Should().Be(createdWp.Id);

        var req3Package = await mediator.Send(new GetRequestWorkPackageQuery(req3.Id));
        req3Package.Should().NotBeNull();
        req3Package!.Id.Should().Be(createdWp.Id);

        var removedReq2Package = await workPackageQueryService.GetRequestWorkPackage(req2.Id);
        removedReq2Package.Should().BeNull("removed requests do not belong to an active work package");

        // 11. Verify ListWorkPackages filters (status, ownerPersonId, customerId, productId)
        var allPackages = await workPackageQueryService.ListWorkPackages();
        allPackages.Should().HaveCount(2);

        var activePackages = await workPackageQueryService.ListWorkPackages(status: WorkPackageStatusNames.Active);
        activePackages.Should().ContainSingle().Which.Id.Should().Be(createdWp.Id);

        var draftPackages = await workPackageQueryService.ListWorkPackages(status: WorkPackageStatusNames.Draft);
        draftPackages.Should().ContainSingle().Which.Id.Should().Be(secondWp.Id);

        var owner2Packages = await workPackageQueryService.ListWorkPackages(ownerPersonId: owner2.Id);
        owner2Packages.Should().ContainSingle().Which.Id.Should().Be(createdWp.Id);

        var customer2Packages = await workPackageQueryService.ListWorkPackages(customerId: customer2.Id);
        customer2Packages.Should().ContainSingle().Which.Id.Should().Be(secondWp.Id);

        var product1Packages = await mediator.Send(new ListWorkPackagesQuery(ProductId: product1.Id));
        product1Packages.Should().ContainSingle().Which.Id.Should().Be(createdWp.Id);

        // 12. Step 7: CloseWorkPackage -> CLOSED
        var closedWp = await mediator.Send(new CloseWorkPackageCommand(
            WorkPackageId: createdWp.Id,
            Reason: "Monthly billing stabilization completed and signed off."));

        closedWp.Status.Should().Be(WorkPackageStatusNames.Closed);
        closedWp.ClosedReason.Should().Be("Monthly billing stabilization completed and signed off.");
        closedWp.ClosedAt.Should().NotBeNull();

        // After closing, GetRequestWorkPackage returns null because the WorkPackage is no longer active
        var req1PackageAfterClose = await workPackageQueryService.GetRequestWorkPackage(req1.Id);
        req1PackageAfterClose.Should().BeNull();
    }

    [Fact]
    public async Task Business_Rule_9_prevents_adding_request_already_in_another_active_WorkPackage()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        var owner = await mediator.Send(new CreatePersonCommand(
            "Hendra",
            "Wijaya",
            $"hendra.{Guid.NewGuid():N}@cakra.id"));

        currentContext?.Initialize(Guid.NewGuid(), owner.Id, new[] { "Management" });

        var request = await mediator.Send(new RecordRequestCommand(
            Title: "Critical database deadlock in lab results posting",
            Description: "Investigate lock escalation on LabOrderResults.",
            RequestType: "Bug",
            Priority: "URGENT"));

        var wp1 = await mediator.Send(new CreateWorkPackageCommand(
            Name: "Lab Stabilization Package A",
            Objective: "Resolve lab performance bottlenecks",
            OwnerPersonId: owner.Id));

        var wp2 = await mediator.Send(new CreateWorkPackageCommand(
            Name: "Lab Stabilization Package B",
            Objective: "Secondary lab improvements",
            OwnerPersonId: owner.Id));

        await mediator.Send(new ActivateWorkPackageCommand(wp1.Id));
        await mediator.Send(new ActivateWorkPackageCommand(wp2.Id));

        // Add request to wp1
        await mediator.Send(new AddRequestToWorkPackageCommand(wp1.Id, request.Id));

        // Adding the same request to wp1 again must fail with BusinessRuleViolationException (Rule 9)
        var duplicateSamePackageAct = async () =>
            await mediator.Send(new AddRequestToWorkPackageCommand(wp1.Id, request.Id));

        var samePkgEx = (await duplicateSamePackageAct.Should().ThrowAsync<BusinessRuleViolationException>()).Which;
        samePkgEx.RuleNumber.Should().Be(9);

        // Adding the same request to wp2 while active in wp1 must fail with BusinessRuleViolationException (Rule 9)
        var duplicateOtherPackageAct = async () =>
            await mediator.Send(new AddRequestToWorkPackageCommand(wp2.Id, request.Id));

        var otherPkgEx = (await duplicateOtherPackageAct.Should().ThrowAsync<BusinessRuleViolationException>()).Which;
        otherPkgEx.RuleNumber.Should().Be(9);

        // Once wp1 is closed, the request is no longer in an active Work Package and can be added to wp2
        await mediator.Send(new CloseWorkPackageCommand(wp1.Id, "Closed initial package"));

        var addedToWp2 = await mediator.Send(new AddRequestToWorkPackageCommand(wp2.Id, request.Id));
        addedToWp2.ActiveRequests.Should().ContainSingle().Which.RequestId.Should().Be(request.Id);
    }

    [Fact]
    public async Task Closing_WorkPackage_does_not_alter_constituent_Request_lifecycle_states_or_owners()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var requestQueryService = scope.ServiceProvider.GetRequiredService<IRequestQueryService>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        var wpOwner = await mediator.Send(new CreatePersonCommand(
            "Maya",
            "Kusuma",
            $"maya.{Guid.NewGuid():N}@cakra.id"));

        var reqOwner = await mediator.Send(new CreatePersonCommand(
            "Dimas",
            "Saputra",
            $"dimas.{Guid.NewGuid():N}@cakra.id"));

        currentContext?.Initialize(Guid.NewGuid(), wpOwner.Id, new[] { "Management" });

        // Create 3 requests in different states: CAPTURED, EVALUATING, IN_PROGRESS
        var capturedReq = await mediator.Send(new RecordRequestCommand(
            Title: "Captured Request",
            Description: "Remains in CAPTURED state"));

        var assignedReq = await mediator.Send(new RecordRequestCommand(
            Title: "Assigned Request",
            Description: "Remains in ASSIGNED state"));
        await mediator.Send(new AssignRequestOwnerCommand(assignedReq.Id, reqOwner.Id));

        var inProgressReq = await mediator.Send(new RecordRequestCommand(
            Title: "InProgress Request",
            Description: "Remains in IN_PROGRESS state"));
        await mediator.Send(new AssignRequestOwnerCommand(inProgressReq.Id, reqOwner.Id));
        currentContext?.Initialize(Guid.NewGuid(), reqOwner.Id, new[] { "Programmer" });
        await mediator.Send(new StartWorkCommand(inProgressReq.Id, ActorPersonId: reqOwner.Id));

        // Record state history counts before Work Package operations
        var capturedHistoryBefore = await requestQueryService.GetRequestStateHistory(capturedReq.Id);
        var assignedHistoryBefore = await requestQueryService.GetRequestStateHistory(assignedReq.Id);
        var inProgressHistoryBefore = await requestQueryService.GetRequestStateHistory(inProgressReq.Id);

        // Create, populate, activate, and close a Work Package containing all 3 requests
        currentContext?.Initialize(Guid.NewGuid(), wpOwner.Id, new[] { "Management" });
        var wp = await mediator.Send(new CreateWorkPackageCommand(
            Name: "Cross-State Grouping Package",
            Objective: "Verify zero side effects on Request lifecycle states",
            OwnerPersonId: wpOwner.Id));

        await mediator.Send(new AddRequestToWorkPackageCommand(wp.Id, capturedReq.Id));
        await mediator.Send(new AddRequestToWorkPackageCommand(wp.Id, assignedReq.Id));
        await mediator.Send(new AddRequestToWorkPackageCommand(wp.Id, inProgressReq.Id));

        await mediator.Send(new ActivateWorkPackageCommand(wp.Id));
        await mediator.Send(new RemoveRequestFromWorkPackageCommand(wp.Id, assignedReq.Id));
        await mediator.Send(new CloseWorkPackageCommand(wp.Id, "Closing package while requests are still open"));

        // Verify constituent Requests have identical lifecycle states, owners, and state histories
        var capturedAfter = await requestQueryService.GetRequestById(capturedReq.Id);
        capturedAfter.Should().NotBeNull();
        capturedAfter!.Status.Should().Be(RequestStatusNames.Captured);
        capturedAfter.OwnerPersonId.Should().BeNull();
        (await requestQueryService.GetRequestStateHistory(capturedReq.Id))
            .Should().HaveCount(capturedHistoryBefore.Count);

        var assignedAfter = await requestQueryService.GetRequestById(assignedReq.Id);
        assignedAfter.Should().NotBeNull();
        assignedAfter!.Status.Should().Be(RequestStatusNames.Assigned);
        assignedAfter.OwnerPersonId.Should().Be(reqOwner.Id);
        (await requestQueryService.GetRequestStateHistory(assignedReq.Id))
            .Should().HaveCount(assignedHistoryBefore.Count);

        var inProgressAfter = await requestQueryService.GetRequestById(inProgressReq.Id);
        inProgressAfter.Should().NotBeNull();
        inProgressAfter!.Status.Should().Be(RequestStatusNames.InProgress);
        inProgressAfter.OwnerPersonId.Should().Be(reqOwner.Id);
        (await requestQueryService.GetRequestStateHistory(inProgressReq.Id))
            .Should().HaveCount(inProgressHistoryBefore.Count);
    }

    private sealed class ForeignKeyInfo
    {
        public string ForeignKeyName { get; init; } = string.Empty;
        public string ParentSchema { get; init; } = string.Empty;
        public string ParentTable { get; init; } = string.Empty;
        public string ReferencedSchema { get; init; } = string.Empty;
        public string ReferencedTable { get; init; } = string.Empty;
    }
}
