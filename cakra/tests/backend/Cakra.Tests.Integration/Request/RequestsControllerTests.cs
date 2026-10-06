using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cakra.Api.Infrastructure.Authentication;
using Cakra.Api.Infrastructure.Migrations;
using Cakra.Modules.Customer.Services;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Organization.Commands;
using Cakra.Modules.Product.Services;
using Cakra.Modules.Request.Domain;
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
/// Integration tests verifying P4-S21 (Request API Controller at <c>/api/v1/requests/*</c>
/// and Customer lookup controller at <c>/api/v1/customers/*</c>):
/// - All endpoints protected by <c>[Authorize]</c> (401 Unauthorized when unauthenticated)
/// - Lookup endpoints for dropdowns: <c>GET /api/v1/customers/active</c>, <c>GET /api/v1/products/active</c>, <c>GET /api/v1/organization/persons/active</c>
/// - <c>POST /api/v1/requests</c> (<c>RecordRequest</c> -> 201 Created)
/// - <c>POST /api/v1/requests/{id}/assign</c> (<c>AssignRequestOwner</c> -> 200 OK)
/// - <c>POST /api/v1/requests/{id}/evaluate</c> (<c>EvaluateRequest</c> -> 200 OK)
/// - <c>POST /api/v1/requests/{id}/accept</c> (<c>AcceptRequestResponsibility</c> -> 200 OK)
/// - <c>POST /api/v1/requests/{id}/reject</c> (<c>RejectRequest</c> -> 200 OK)
/// - <c>POST /api/v1/requests/{id}/escalate</c> (<c>EscalateRequest</c> -> 200 OK)
/// - <c>POST /api/v1/requests/{id}/management-decision</c> (<c>RequestManagementDecision</c> -> 200 OK)
/// - <c>POST /api/v1/requests/{id}/complete</c> (<c>ReviewRequestCompletion</c> -> 200 OK)
/// - <c>POST /api/v1/requests/{id}/reassign</c> (<c>ReassignRequestOwnership</c> -> 200 OK)
/// - <c>GET /api/v1/requests</c> (<c>GetFilteredRequestGrid</c> -> 200 OK)
/// - <c>GET /api/v1/requests/{id}</c> (<c>GetRequestById</c> -> 200 OK / 404 NotFound)
/// - <c>GET /api/v1/requests/{id}/history</c> (<c>GetRequestStateHistory</c> -> 200 OK with audit trail)
/// - <c>GET /api/v1/requests/my</c> (<c>ListMyAssignedRequests</c> -> 200 OK)
/// </summary>
[Collection("OrganizationDatabase")]
public class RequestsControllerTests : IAsyncLifetime
{
    private const string LocalDbFallback =
        "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb_RequestsController;Integrated Security=true;TrustServerCertificate=True;";

    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private bool _sqlServerAvailable;

    public RequestsControllerTests()
    {
        var baseConnectionString = DatabaseResetHelper.ResolveConnectionString() ?? LocalDbFallback;
        _connectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "CakraTestDb_RequestsController"
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
        migrationResult.Successful.Should().BeTrue("DbUp migrations must execute cleanly before integration tests");

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
            SchemasToInclude = ["request", "product", "customer", "organization", "identity"],
            TablesToIgnore = [new Table("dbo", "__SchemaVersions"), new Table("dbo", "SchemaVersions")]
        });

        await respawner.ResetAsync(conn);
    }

    [Fact]
    public async Task Unauthenticated_requests_to_requests_and_customers_endpoints_return_401_Unauthorized()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var randomId = Guid.NewGuid();

        (await client.GetAsync("/api/v1/requests")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync($"/api/v1/requests/{randomId}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync($"/api/v1/requests/{randomId}/history")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/requests/my")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PostAsJsonAsync("/api/v1/requests", new { title = "T", description = "D" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync($"/api/v1/requests/{randomId}/assign", new { ownerPersonId = randomId }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync($"/api/v1/requests/{randomId}/start", new { notes = "Start" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync($"/api/v1/requests/{randomId}/pause", new { note = "Pause" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync($"/api/v1/requests/{randomId}/cancel", new { reason = "Cancel" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync($"/api/v1/requests/{randomId}/complete", new { resolutionDescription = "Done" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync($"/api/v1/requests/{randomId}/reassign", new { newOwnerPersonId = randomId }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PatchAsJsonAsync($"/api/v1/requests/{randomId}/complexity", new { complexity = 3 }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync($"/api/v1/requests/{randomId}/subtasks", new { title = "Sub-task" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync($"/api/v1/requests/{randomId}/subtasks/{randomId}/complete", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync($"/api/v1/requests/{randomId}/subtasks/{randomId}/reopen", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.DeleteAsync($"/api/v1/requests/{randomId}/subtasks/{randomId}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/requests/assigned-subtasks"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.GetAsync("/api/v1/customers/active")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dropdown_lookup_endpoints_for_active_customers_products_and_persons_return_200_OK()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        var ctx = await SeedOperationalActorsAndMasterDataAsync();

        // 1. GET /api/v1/customers/active
        var customersResp = await ctx.Client.GetAsync("/api/v1/customers/active");
        customersResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var customersJson = await customersResp.Content.ReadFromJsonAsync<JsonElement>();
        var customerIds = customersJson.EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToList();
        customerIds.Should().Contain(ctx.Customer1Id);
        customerIds.Should().Contain(ctx.Customer2Id);
        customerIds.Should().NotContain(ctx.InactiveCustomerId);

        // 2. GET /api/v1/products/active
        var productsResp = await ctx.Client.GetAsync("/api/v1/products/active");
        productsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var productsJson = await productsResp.Content.ReadFromJsonAsync<JsonElement>();
        var productIds = productsJson.EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToList();
        productIds.Should().Contain(ctx.Product1Id);
        productIds.Should().Contain(ctx.Product2Id);

        // 3. GET /api/v1/organization/persons/active
        var personsResp = await ctx.Client.GetAsync("/api/v1/organization/persons/active");
        personsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var personsJson = await personsResp.Content.ReadFromJsonAsync<JsonElement>();
        var personIds = personsJson.EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToList();
        personIds.Should().Contain(ctx.Programmer1Id);
        personIds.Should().Contain(ctx.Programmer2Id);
    }

    [Fact]
    public async Task Full_request_lifecycle_HTTP_endpoints_create_assign_evaluate_accept_complete_detail_and_history_succeed()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        var ctx = await SeedOperationalActorsAndMasterDataAsync();

        // 1. POST /api/v1/requests -> 201 Created (CAPTURED)
        var createResp = await ctx.Client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "BPJS VClaim Bridging Timeout on SEP Generation",
            description = "Emergency registration desk experiences 30s timeout when generating SEP.",
            customerId = ctx.Customer1Id,
            productId = ctx.Product1Id,
            requestType = "Bug",
            priority = "URGENT"
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        createResp.Headers.Location.Should().NotBeNull();

        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = created.GetProperty("id").GetGuid();
        requestId.Should().NotBeEmpty();
        created.GetProperty("status").GetString().Should().Be(RequestStatusNames.Captured);
        created.GetProperty("customerName").GetString().Should().Be("RSUD Dr. Soetomo");
        created.GetProperty("productName").GetString().Should().Be("MyHospital Core");
        created.GetProperty("complexity").GetInt32().Should().Be(1);

        // 2. POST /api/v1/requests/{id}/assign -> 200 OK (ASSIGNED)
        var assignResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/assign", new
        {
            ownerPersonId = ctx.Programmer1Id,
            notes = "Assigned to bridging module specialist"
        });
        assignResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var assigned = await assignResp.Content.ReadFromJsonAsync<JsonElement>();
        assigned.GetProperty("status").GetString().Should().Be(RequestStatusNames.Assigned);
        assigned.GetProperty("ownerPersonId").GetGuid().Should().Be(ctx.Programmer1Id);
        assigned.GetProperty("ownerName").GetString().Should().Be("Budi Santoso");

        // 3. PATCH /api/v1/requests/{requestId}/complexity -> 200 OK
        var patchComplexityResp = await ctx.Client.PatchAsJsonAsync($"/api/v1/requests/{requestId}/complexity", new
        {
            complexity = 4,
            reason = "Architectural evaluation shows high integration complexity"
        });
        patchComplexityResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var patched = await patchComplexityResp.Content.ReadFromJsonAsync<JsonElement>();
        patched.GetProperty("complexity").GetInt32().Should().Be(4);

        // Invalid complexity -> 400 Bad Request
        var invalidPatchResp = await ctx.Client.PatchAsJsonAsync($"/api/v1/requests/{requestId}/complexity", new
        {
            complexity = 7
        });
        invalidPatchResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 4. POST /api/v1/requests/{id}/start -> 200 OK (IN_PROGRESS)
        var startResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/start", new
        {
            notes = "Refactoring to IHttpClientFactory with Polly retry policy"
        });
        startResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        started.GetProperty("status").GetString().Should().Be(RequestStatusNames.InProgress);

        // 4b. POST /api/v1/requests/{id}/pause -> 200 OK (PAUSED)
        var pauseResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/pause", new
        {
            note = "Waiting on test credentials"
        });
        pauseResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var paused = await pauseResp.Content.ReadFromJsonAsync<JsonElement>();
        paused.GetProperty("status").GetString().Should().Be(RequestStatusNames.Paused);

        // 4c. POST /api/v1/requests/{id}/start -> 200 OK (IN_PROGRESS, resumed)
        var resumeResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/start", new
        {
            notes = "Resumed work after credentials received"
        });
        resumeResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var resumed = await resumeResp.Content.ReadFromJsonAsync<JsonElement>();
        resumed.GetProperty("status").GetString().Should().Be(RequestStatusNames.InProgress);

        // 5. POST /api/v1/requests/{id}/complete -> 200 OK (COMPLETED)
        var completeResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/complete", new
        {
            resolutionDescription = "Replaced manual HttpClient with named IHttpClientFactory client and 10s timeout."
        });
        completeResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await completeResp.Content.ReadFromJsonAsync<JsonElement>();
        completed.GetProperty("status").GetString().Should().Be(RequestStatusNames.Completed);
        completed.GetProperty("resolution").GetProperty("outcome").GetString().Should().Be(ResolutionOutcomeNames.Completed);
        completed.GetProperty("resolution").GetProperty("description").GetString()
            .Should().Be("Replaced manual HttpClient with named IHttpClientFactory client and 10s timeout.");

        // 6. GET /api/v1/requests/{id} -> 200 OK
        var detailResp = await ctx.Client.GetAsync($"/api/v1/requests/{requestId}");
        detailResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailResp.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("id").GetGuid().Should().Be(requestId);
        detail.GetProperty("status").GetString().Should().Be(RequestStatusNames.Completed);
        detail.GetProperty("ownerName").GetString().Should().Be("Budi Santoso");
        detail.GetProperty("customerName").GetString().Should().Be("RSUD Dr. Soetomo");
        detail.GetProperty("productName").GetString().Should().Be("MyHospital Core");
        detail.GetProperty("complexity").GetInt32().Should().Be(4);

        // 7. GET /api/v1/requests/{id}/history -> 200 OK with ordered state transition audit trail
        var historyResp = await ctx.Client.GetAsync($"/api/v1/requests/{requestId}/history");
        historyResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await historyResp.Content.ReadFromJsonAsync<JsonElement>();
        history.GetArrayLength().Should().Be(6);

        var statuses = history.EnumerateArray()
            .Select(h => h.GetProperty("newStatus").GetString())
            .ToList();
        statuses.Should().ContainInOrder(
            RequestStatusNames.Captured,
            RequestStatusNames.Assigned,
            RequestStatusNames.InProgress,
            RequestStatusNames.Paused,
            RequestStatusNames.InProgress,
            RequestStatusNames.Completed);

        // Verify actor PersonId and ActorName recorded on audit entries (Architecture §18)
        foreach (var entry in history.EnumerateArray())
        {
            entry.GetProperty("actorPersonId").GetGuid().Should().Be(ctx.Programmer1Id);
            entry.GetProperty("actorName").GetString().Should().Be("Budi Santoso");
        }
    }

    [Fact]
    public async Task Cancel_reassign_and_obsolete_HTTP_endpoints_verification()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        var ctx = await SeedOperationalActorsAndMasterDataAsync();

        // 1. Create Request 1 and Cancel -> CANCELLED
        var create1Resp = await ctx.Client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Inpatient Pharmacy Narcotics Ledger Audit Lock",
            description = "Requires regulatory sign-off before unlocking closed monthly ledger.",
            customerId = ctx.Customer1Id,
            productId = ctx.Product1Id,
            requestType = "Support",
            priority = "HIGH"
        });
        create1Resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var req1Id = (await create1Resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req1Id}/assign", new
        {
            ownerPersonId = ctx.Programmer1Id
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        // Cancel Request -> transitions to CANCELLED
        var cancelResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req1Id}/cancel", new
        {
            reason = "Hospital withdrew request after internal audit reconciliation."
        });
        cancelResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = await cancelResp.Content.ReadFromJsonAsync<JsonElement>();
        cancelled.GetProperty("status").GetString().Should().Be(RequestStatusNames.Cancelled);
        cancelled.GetProperty("resolution").GetProperty("outcome").GetString().Should().Be(ResolutionOutcomeNames.Cancelled);

        // 2. Create Request 2, start work, then reassign to Programmer2 -> resets to ASSIGNED
        var create2Resp = await ctx.Client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Billing integration queue backlog",
            description = "Investigation required on queue backlog.",
            customerId = ctx.Customer1Id,
            productId = ctx.Product1Id,
            requestType = "Bug",
            priority = "HIGH"
        });
        var req2Id = (await create2Resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req2Id}/assign", new { ownerPersonId = ctx.Programmer1Id });
        await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req2Id}/start", new { notes = "Started" });

        var reassignResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req2Id}/reassign", new
        {
            newOwnerPersonId = ctx.Programmer2Id,
            notes = "Reassigned to Rina Wijaya"
        });
        reassignResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var reassigned = await reassignResp.Content.ReadFromJsonAsync<JsonElement>();
        reassigned.GetProperty("status").GetString().Should().Be(RequestStatusNames.Assigned);
        reassigned.GetProperty("ownerPersonId").GetGuid().Should().Be(ctx.Programmer2Id);

        // 3. Verify obsolete endpoints are not accessible (404 Not Found or 405 Method Not Allowed)
        (await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req2Id}/evaluate", new { evaluationNotes = "Notes" }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        (await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req2Id}/accept", new { notes = "Accept" }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        (await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req2Id}/reject", new { reason = "Reject" }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        (await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req2Id}/escalate", new { reason = "Escalate" }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        (await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req2Id}/management-decision", new { decisionDetails = "Decision" }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        (await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req2Id}/management-decision/apply", new { targetStatus = "IN_PROGRESS" }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Query_endpoints_my_requests_filtered_grid_and_problem_details_error_responses_succeed()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        var ctx = await SeedOperationalActorsAndMasterDataAsync();

        // Create 2 requests: one assigned to Programmer1 (authenticated user), one assigned to Programmer2
        var r1Resp = await ctx.Client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Lab LIS HL7 Order Message Parser Fix",
            description = "Order segments with repeating OBX fields fail parsing.",
            customerId = ctx.Customer1Id,
            productId = ctx.Product1Id,
            requestType = "Bug",
            priority = "HIGH"
        });
        var req1Id = (await r1Resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req1Id}/assign", new { ownerPersonId = ctx.Programmer1Id });

        var r2Resp = await ctx.Client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Radiology PACS DICOM Viewer Launch Link",
            description = "Add zero-footprint viewer URL button on radiology results screen.",
            customerId = ctx.Customer2Id,
            productId = ctx.Product2Id,
            requestType = "Feature",
            priority = "NORMAL"
        });
        var req2Id = (await r2Resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req2Id}/assign", new { ownerPersonId = ctx.Programmer2Id });

        // Start req1 by Programmer1 (owner) -> IN_PROGRESS
        await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req1Id}/start", new { notes = "Starting req1" });

        // 1. GET /api/v1/requests/my returns only requests assigned to authenticated Programmer1
        var myResp = await ctx.Client.GetAsync("/api/v1/requests/my");
        myResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var myRequests = await myResp.Content.ReadFromJsonAsync<JsonElement>();
        myRequests.GetArrayLength().Should().Be(1);
        myRequests[0].GetProperty("id").GetGuid().Should().Be(req1Id);

        // 2. GET /api/v1/requests with filters and pagination
        var allGridResp = await ctx.Client.GetAsync("/api/v1/requests?page=1&pageSize=10");
        allGridResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var allGrid = await allGridResp.Content.ReadFromJsonAsync<JsonElement>();
        allGrid.GetProperty("totalCount").GetInt32().Should().Be(2);
        allGrid.GetProperty("items").GetArrayLength().Should().Be(2);

        var statusFilterResp = await ctx.Client.GetAsync($"/api/v1/requests?status={RequestStatusNames.InProgress}");
        statusFilterResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var statusGrid = await statusFilterResp.Content.ReadFromJsonAsync<JsonElement>();
        statusGrid.GetProperty("totalCount").GetInt32().Should().Be(1);
        statusGrid.GetProperty("items")[0].GetProperty("id").GetGuid().Should().Be(req1Id);

        var assigneeFilterResp = await ctx.Client.GetAsync($"/api/v1/requests?assigneeId={ctx.Programmer1Id}&customerId={ctx.Customer1Id}&productId={ctx.Product1Id}");
        assigneeFilterResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var assigneeGrid = await assigneeFilterResp.Content.ReadFromJsonAsync<JsonElement>();
        assigneeGrid.GetProperty("totalCount").GetInt32().Should().Be(1);
        assigneeGrid.GetProperty("items")[0].GetProperty("id").GetGuid().Should().Be(req1Id);

        // 3. Validation failure -> 400 Bad Request ProblemDetails
        var invalidCreateResp = await ctx.Client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "",
            description = ""
        });
        invalidCreateResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        invalidCreateResp.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        // 4. Invalid state transition (completing an ASSIGNED request directly) -> 400 Bad Request ProblemDetails
        var invalidTransitionResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req2Id}/complete", new
        {
            resolutionDescription = "Premature completion"
        });
        invalidTransitionResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        invalidTransitionResp.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        // 5. Unknown request ID -> 404 Not Found ProblemDetails
        var notFoundResp = await ctx.Client.GetAsync($"/api/v1/requests/{Guid.NewGuid()}");
        notFoundResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        notFoundResp.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Request_complexity_endpoints_support_creation_patch_and_role_authorization()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        var ctx = await SeedOperationalActorsAndMasterDataAsync();

        Guid unauthorizedPersonId;
        using (var scope = _factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var unauthorizedPerson = await mediator.Send(new CreatePersonCommand("Viewer", "User", "viewer@cakra.id"));
            var viewerRole = await mediator.Send(new CreateRoleCommand("Viewer", "Read-only viewer"));
            await mediator.Send(new AssignRoleToPersonCommand(unauthorizedPerson.Id, viewerRole.Id));
            unauthorizedPersonId = unauthorizedPerson.Id;
        }

        // 1. Create with custom complexity (3) -> 201 Created
        var createResp = await ctx.Client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Request with initial complexity 3",
            description = "Initial complexity specified at capture time",
            customerId = ctx.Customer1Id,
            productId = ctx.Product1Id,
            requestType = "Feature",
            complexity = 3
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = created.GetProperty("id").GetGuid();
        created.GetProperty("complexity").GetInt32().Should().Be(3);

        // 2. Assign owner -> 200 OK
        await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/assign", new
        {
            ownerPersonId = ctx.Programmer1Id
        });

        // 3. Patch complexity with authorized actor (2) -> 200 OK
        var patchResp = await ctx.Client.PatchAsJsonAsync($"/api/v1/requests/{requestId}/complexity", new
        {
            complexity = 2,
            reason = "Scope reduced, complexity lowered."
        });
        patchResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var patched = await patchResp.Content.ReadFromJsonAsync<JsonElement>();
        patched.GetProperty("complexity").GetInt32().Should().Be(2);

        // 4. Patch complexity with unauthorized actor -> 403 Forbidden
        var forbiddenResp = await ctx.Client.PatchAsJsonAsync($"/api/v1/requests/{requestId}/complexity", new
        {
            complexity = 4,
            reason = "Unauthorized attempt",
            actorPersonId = unauthorizedPersonId
        });
        forbiddenResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 5. Patch complexity with out-of-range value (0 or 6) -> 400 Bad Request
        var badRangeResp1 = await ctx.Client.PatchAsJsonAsync($"/api/v1/requests/{requestId}/complexity", new
        {
            complexity = 0
        });
        badRangeResp1.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var badRangeResp2 = await ctx.Client.PatchAsJsonAsync($"/api/v1/requests/{requestId}/complexity", new
        {
            complexity = 6
        });
        badRangeResp2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Initial_subtasks_intake_via_POST_api_v1_requests_persists_subtasks_and_calculates_progress()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        var ctx = await SeedOperationalActorsAndMasterDataAsync();

        // 1. POST /api/v1/requests with initial subtasks -> 201 Created
        var createResp = await ctx.Client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Request with initial subtasks",
            description = "Intake includes 2 subtasks",
            customerId = ctx.Customer1Id,
            productId = ctx.Product1Id,
            requestType = "Bug",
            priority = "HIGH",
            initialSubTasks = new[]
            {
                new { title = "Sub-task 1: Root cause analysis", assigneePersonId = (Guid?)ctx.Programmer1Id },
                new { title = "Sub-task 2: Implement patch", assigneePersonId = (Guid?)ctx.Programmer2Id }
            }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = created.GetProperty("id").GetGuid();
        created.GetProperty("totalSubTasksCount").GetInt32().Should().Be(2);
        created.GetProperty("completedSubTasksCount").GetInt32().Should().Be(0);
        created.GetProperty("completionPercentage").GetInt32().Should().Be(0);

        var subtasks = created.GetProperty("subTasks");
        subtasks.GetArrayLength().Should().Be(2);
        subtasks[0].GetProperty("title").GetString().Should().Be("Sub-task 1: Root cause analysis");
        subtasks[0].GetProperty("isCompleted").GetBoolean().Should().BeFalse();
        subtasks[0].GetProperty("assigneePersonId").GetGuid().Should().Be(ctx.Programmer1Id);
        subtasks[1].GetProperty("title").GetString().Should().Be("Sub-task 2: Implement patch");

        // 2. GET /api/v1/requests/{id} -> 200 OK with subtasks
        var getResp = await ctx.Client.GetAsync($"/api/v1/requests/{requestId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("totalSubTasksCount").GetInt32().Should().Be(2);
        detail.GetProperty("subTasks").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Subtask_lifecycle_endpoints_add_complete_reopen_and_remove_update_progress_and_metrics()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        var ctx = await SeedOperationalActorsAndMasterDataAsync();

        // 1. Create request -> 201 Created
        var createResp = await ctx.Client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Subtask lifecycle tracking",
            description = "Track subtask additions, completion, and reopening",
            customerId = ctx.Customer1Id,
            productId = ctx.Product1Id
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var requestId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Assign to Programmer1 -> ASSIGNED
        (await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/assign", new
        {
            ownerPersonId = ctx.Programmer1Id
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        // Start -> IN_PROGRESS
        (await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/start", new
        {
            notes = "Started"
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. POST /api/v1/requests/{id}/subtasks -> 200 OK
        var addResp1 = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/subtasks", new
        {
            title = "Verify schema migrations",
            assigneePersonId = ctx.Programmer1Id
        });
        addResp1.StatusCode.Should().Be(HttpStatusCode.OK);
        var res1 = await addResp1.Content.ReadFromJsonAsync<JsonElement>();
        res1.GetProperty("totalSubTasksCount").GetInt32().Should().Be(1);
        res1.GetProperty("completedSubTasksCount").GetInt32().Should().Be(0);
        res1.GetProperty("completionPercentage").GetInt32().Should().Be(0);
        var subTask1Id = res1.GetProperty("subTasks")[0].GetProperty("id").GetGuid();

        // Add second subtask
        var addResp2 = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/subtasks", new
        {
            title = "Write integration tests",
            assigneePersonId = ctx.Programmer2Id
        });
        addResp2.StatusCode.Should().Be(HttpStatusCode.OK);
        var res2 = await addResp2.Content.ReadFromJsonAsync<JsonElement>();
        res2.GetProperty("totalSubTasksCount").GetInt32().Should().Be(2);
        res2.GetProperty("completedSubTasksCount").GetInt32().Should().Be(0);
        res2.GetProperty("completionPercentage").GetInt32().Should().Be(0);
        var subTask2Id = res2.GetProperty("subTasks")[1].GetProperty("id").GetGuid();

        // 3. Complete subtask 1 -> 200 OK (50% progress)
        var completeSubResp = await ctx.Client.PostAsJsonAsync(
            $"/api/v1/requests/{requestId}/subtasks/{subTask1Id}/complete",
            new { actorPersonId = ctx.Programmer1Id });
        completeSubResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var completedSub = await completeSubResp.Content.ReadFromJsonAsync<JsonElement>();
        completedSub.GetProperty("totalSubTasksCount").GetInt32().Should().Be(2);
        completedSub.GetProperty("completedSubTasksCount").GetInt32().Should().Be(1);
        completedSub.GetProperty("completionPercentage").GetInt32().Should().Be(50);

        // 4. Query GET /api/v1/requests/assigned-subtasks for Programmer2 -> should include requestId
        var assignedResp = await ctx.Client.GetAsync($"/api/v1/requests/assigned-subtasks?personId={ctx.Programmer2Id}");
        assignedResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var assignedList = await assignedResp.Content.ReadFromJsonAsync<JsonElement>();
        assignedList.EnumerateArray().Any(r => r.GetProperty("id").GetGuid() == requestId).Should().BeTrue();

        // 5. Reopen subtask 1 -> 200 OK (0% progress)
        var reopenResp = await ctx.Client.PostAsJsonAsync(
            $"/api/v1/requests/{requestId}/subtasks/{subTask1Id}/reopen",
            new { actorPersonId = ctx.Programmer1Id });
        reopenResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var reopened = await reopenResp.Content.ReadFromJsonAsync<JsonElement>();
        reopened.GetProperty("completedSubTasksCount").GetInt32().Should().Be(0);
        reopened.GetProperty("completionPercentage").GetInt32().Should().Be(0);

        // 6. Delete subtask 2 -> 200 OK (1 total subtask remaining)
        var deleteResp = await ctx.Client.DeleteAsync(
            $"/api/v1/requests/{requestId}/subtasks/{subTask2Id}?actorPersonId={ctx.Programmer1Id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var deletedResult = await deleteResp.Content.ReadFromJsonAsync<JsonElement>();
        deletedResult.GetProperty("totalSubTasksCount").GetInt32().Should().Be(1);
        deletedResult.GetProperty("subTasks").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Complete_request_fails_with_400_Bad_Request_when_unfinished_subtasks_exist_and_succeeds_once_all_completed()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        var ctx = await SeedOperationalActorsAndMasterDataAsync();

        // Create request
        var createResp = await ctx.Client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Completion guard test",
            description = "Verify RequestHasUnfinishedSubTasksException blocks completion",
            customerId = ctx.Customer1Id,
            productId = ctx.Product1Id
        });
        var requestId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Assign to Programmer1 -> ASSIGNED
        await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/assign", new { ownerPersonId = ctx.Programmer1Id });

        // Start -> IN_PROGRESS
        await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/start", new { notes = "Started" });

        // Add an unfinished sub-task
        var addResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/subtasks", new
        {
            title = "Mandatory checklist item",
            assigneePersonId = ctx.Programmer1Id
        });
        addResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var subTaskId = (await addResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("subTasks")[0].GetProperty("id").GetGuid();

        // 1. Attempt to complete request while subtask is unfinished -> 400 Bad Request
        var failCompleteResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/complete", new
        {
            resolutionDescription = "Attempting closure with unfinished tasks",
            actorPersonId = ctx.Programmer1Id
        });
        failCompleteResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 2. Complete the subtask -> 200 OK
        var compSubResp = await ctx.Client.PostAsJsonAsync(
            $"/api/v1/requests/{requestId}/subtasks/{subTaskId}/complete",
            new { actorPersonId = ctx.Programmer1Id });
        compSubResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Attempt to complete request now that all subtasks are finished -> 200 OK
        var successCompleteResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/complete", new
        {
            resolutionDescription = "All sub-tasks finished cleanly.",
            actorPersonId = ctx.Programmer1Id
        });
        successCompleteResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await successCompleteResp.Content.ReadFromJsonAsync<JsonElement>();
        completed.GetProperty("status").GetString().Should().Be(RequestStatusNames.Completed);
        completed.GetProperty("completionPercentage").GetInt32().Should().Be(100);

        // 4. Attempting sub-task mutation on completed request -> 400 Bad Request (closed state immutability)
        var mutateClosedResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/subtasks", new
        {
            title = "Illegal post-closure task",
            assigneePersonId = ctx.Programmer1Id
        });
        mutateClosedResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Subtask_authorization_guards_reject_unauthorized_actors_with_403_Forbidden()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        var ctx = await SeedOperationalActorsAndMasterDataAsync();

        // Create an unauthorized person (no roles, not request owner, not assignee)
        Guid unauthorizedPersonId;
        using (var scope = _factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var p = await mediator.Send(new CreatePersonCommand("Unauthorized", "User", "unauth.user@cakra.id"));
            unauthorizedPersonId = p.Id;
        }

        // Create a request owned by Programmer1
        var createResp = await ctx.Client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Subtask auth test",
            description = "Verify 403 Forbidden for unauthorized actors",
            customerId = ctx.Customer1Id,
            productId = ctx.Product1Id
        });
        var requestId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Assign to Programmer1 -> EVALUATING
        await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/assign", new { ownerPersonId = ctx.Programmer1Id });

        // 1. Unauthorized actor attempts to add sub-task -> 403 Forbidden
        var addForbiddenResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/subtasks", new
        {
            title = "Unauthorized subtask",
            actorPersonId = unauthorizedPersonId
        });
        addForbiddenResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Authorized add by owner
        var addResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/subtasks", new
        {
            title = "Owner subtask",
            assigneePersonId = ctx.Programmer1Id,
            actorPersonId = ctx.Programmer1Id
        });
        addResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var subTaskId = (await addResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("subTasks")[0].GetProperty("id").GetGuid();

        // 2. Unauthorized actor attempts to complete sub-task assigned to Programmer1 -> 403 Forbidden
        var completeForbiddenResp = await ctx.Client.PostAsJsonAsync(
            $"/api/v1/requests/{requestId}/subtasks/{subTaskId}/complete",
            new { actorPersonId = unauthorizedPersonId });
        completeForbiddenResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Unauthorized actor attempts to remove sub-task -> 403 Forbidden
        var removeForbiddenResp = await ctx.Client.DeleteAsync(
            $"/api/v1/requests/{requestId}/subtasks/{subTaskId}?actorPersonId={unauthorizedPersonId}");
        removeForbiddenResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record SeededContext(
        HttpClient Client,
        Guid Programmer1Id,
        Guid Programmer2Id,
        Guid Customer1Id,
        Guid Customer2Id,
        Guid InactiveCustomerId,
        Guid Product1Id,
        Guid Product2Id);

    private async Task<SeededContext> SeedOperationalActorsAndMasterDataAsync()
    {
        Guid prog1Id;
        Guid prog2Id;
        Guid cust1Id;
        Guid cust2Id;
        Guid inactiveCustId;
        Guid prod1Id;
        Guid prod2Id;
        string sessionToken;

        using (var scope = _factory!.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var userAccountRepo = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

            var prog1 = await mediator.Send(new CreatePersonCommand("Budi", "Santoso", "budi.santoso@cakra.id"));
            var prog2 = await mediator.Send(new CreatePersonCommand("Rina", "Wijaya", "rina.wijaya@cakra.id"));
            prog1Id = prog1.Id;
            prog2Id = prog2.Id;

            var progRole = await mediator.Send(new CreateRoleCommand("Programmer", "Programmer role"));
            await mediator.Send(new AssignRoleToPersonCommand(prog1.Id, progRole.Id));

            var cust1 = await mediator.Send(new CreateCustomerCommand("CUST-SOETOMO", "RSUD Dr. Soetomo", true));
            var cust2 = await mediator.Send(new CreateCustomerCommand("CUST-HARAPAN", "RS Harapan Kita", true));
            var inactiveCust = await mediator.Send(new CreateCustomerCommand("CUST-INACTIVE", "RS Nonaktif", false));
            await mediator.Send(new DeactivateCustomerCommand(inactiveCust.Id));
            cust1Id = cust1.Id;
            cust2Id = cust2.Id;
            inactiveCustId = inactiveCust.Id;

            var prod1 = await mediator.Send(new CreateProductCommand("MYHOSPITAL", "MyHospital Core", "HIS", prog1Id));
            var prod2 = await mediator.Send(new CreateProductCommand("PENAEL", "PenaEl EMR", "EMR", prog2Id));
            prod1Id = prod1.Id;
            prod2Id = prod2.Id;

            var user = new UserAccount
            {
                Id = Guid.NewGuid(),
                PersonId = prog1Id,
                Username = "budi.santoso",
                Email = "budi.santoso@cakra.id",
                Status = UserAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = authService.HashPassword(user, "Password123!");
            await userAccountRepo.AddAsync(user);

            var loginResult = await authService.LoginAsync("budi.santoso", "Password123!");
            loginResult.Succeeded.Should().BeTrue();
            sessionToken = loginResult.SessionToken!;
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}={sessionToken}");

        return new SeededContext(
            client,
            prog1Id,
            prog2Id,
            cust1Id,
            cust2Id,
            inactiveCustId,
            prod1Id,
            prod2Id);
    }
}
