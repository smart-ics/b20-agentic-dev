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
        (await client.PostAsJsonAsync($"/api/v1/requests/{randomId}/evaluate", new { evaluationNotes = "Notes" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync($"/api/v1/requests/{randomId}/accept", new { notes = "Accept" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync($"/api/v1/requests/{randomId}/reject", new { reason = "Reject" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync($"/api/v1/requests/{randomId}/escalate", new { reason = "Escalate" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync($"/api/v1/requests/{randomId}/management-decision", new { decisionDetails = "Decision" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync($"/api/v1/requests/{randomId}/complete", new { resolutionDescription = "Done" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync($"/api/v1/requests/{randomId}/reassign", new { newOwnerPersonId = randomId }))
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

        // 2. POST /api/v1/requests/{id}/assign -> 200 OK (EVALUATING)
        var assignResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/assign", new
        {
            ownerPersonId = ctx.Programmer1Id,
            notes = "Assigned to bridging module specialist"
        });
        assignResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var assigned = await assignResp.Content.ReadFromJsonAsync<JsonElement>();
        assigned.GetProperty("status").GetString().Should().Be(RequestStatusNames.Evaluating);
        assigned.GetProperty("ownerPersonId").GetGuid().Should().Be(ctx.Programmer1Id);
        assigned.GetProperty("ownerName").GetString().Should().Be("Budi Santoso");

        // 3. POST /api/v1/requests/{id}/evaluate -> 200 OK (EVALUATING with evaluationNotes)
        var evaluateResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/evaluate", new
        {
            evaluationNotes = "HttpClient socket exhaustion due to per-call HttpClient instantiation."
        });
        evaluateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var evaluated = await evaluateResp.Content.ReadFromJsonAsync<JsonElement>();
        evaluated.GetProperty("status").GetString().Should().Be(RequestStatusNames.Evaluating);
        evaluated.GetProperty("evaluationNotes").GetString().Should().Be("HttpClient socket exhaustion due to per-call HttpClient instantiation.");

        // 4. POST /api/v1/requests/{id}/accept -> 200 OK (IN_PROGRESS)
        var acceptResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{requestId}/accept", new
        {
            notes = "Refactoring to IHttpClientFactory with Polly retry policy"
        });
        acceptResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var accepted = await acceptResp.Content.ReadFromJsonAsync<JsonElement>();
        accepted.GetProperty("status").GetString().Should().Be(RequestStatusNames.InProgress);

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

        // 7. GET /api/v1/requests/{id}/history -> 200 OK with ordered state transition audit trail
        var historyResp = await ctx.Client.GetAsync($"/api/v1/requests/{requestId}/history");
        historyResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await historyResp.Content.ReadFromJsonAsync<JsonElement>();
        history.GetArrayLength().Should().Be(5);

        var statuses = history.EnumerateArray()
            .Select(h => h.GetProperty("newStatus").GetString())
            .ToList();
        statuses.Should().ContainInOrder(
            RequestStatusNames.Captured,
            RequestStatusNames.Evaluating,
            RequestStatusNames.Accepted,
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
    public async Task Escalate_management_decision_reassign_and_reject_HTTP_endpoints_succeed_and_record_audit_entries()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        var ctx = await SeedOperationalActorsAndMasterDataAsync();

        // Create Request 1 for Escalate -> Management Decision -> Reassign flow
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

        // Assign to Programmer1 -> EVALUATING
        (await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req1Id}/assign", new
        {
            ownerPersonId = ctx.Programmer1Id
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        // Escalate -> ESCALATED
        var escalateResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req1Id}/escalate", new
        {
            reason = "Requires management approval and hospital compliance letter."
        });
        escalateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var escalated = await escalateResp.Content.ReadFromJsonAsync<JsonElement>();
        escalated.GetProperty("status").GetString().Should().Be(RequestStatusNames.Escalated);
        escalated.GetProperty("escalationReason").GetString().Should().Be("Requires management approval and hospital compliance letter.");

        // Management Decision -> records managementDecisionNotes
        var decisionResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req1Id}/management-decision", new
        {
            decisionDetails = "Compliance letter received; approved for senior architect execution."
        });
        decisionResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var decided = await decisionResp.Content.ReadFromJsonAsync<JsonElement>();
        decided.GetProperty("status").GetString().Should().Be(RequestStatusNames.Escalated);
        decided.GetProperty("managementDecisionNotes").GetString()
            .Should().Be("Compliance letter received; approved for senior architect execution.");

        // Reassign to Programmer2 -> transitions ESCALATED -> EVALUATING and updates OwnerPersonId
        var reassignResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req1Id}/reassign", new
        {
            newOwnerPersonId = ctx.Programmer2Id,
            notes = "Reassigned to Rina Wijaya for ledger adjustment"
        });
        reassignResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var reassigned = await reassignResp.Content.ReadFromJsonAsync<JsonElement>();
        reassigned.GetProperty("status").GetString().Should().Be(RequestStatusNames.Evaluating);
        reassigned.GetProperty("ownerPersonId").GetGuid().Should().Be(ctx.Programmer2Id);
        reassigned.GetProperty("ownerName").GetString().Should().Be("Rina Wijaya");

        // Reject Request -> transitions EVALUATING -> REJECTED
        var rejectResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req1Id}/reject", new
        {
            reason = "Hospital withdrew request after internal audit reconciliation."
        });
        rejectResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejected = await rejectResp.Content.ReadFromJsonAsync<JsonElement>();
        rejected.GetProperty("status").GetString().Should().Be(RequestStatusNames.Rejected);
        rejected.GetProperty("resolution").GetProperty("outcome").GetString().Should().Be(ResolutionOutcomeNames.Rejected);
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
        await ctx.Client.PostAsync($"/api/v1/requests/{req2Id}/accept", null);

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
        statusGrid.GetProperty("items")[0].GetProperty("id").GetGuid().Should().Be(req2Id);

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

        // 4. Invalid state transition (completing an EVALUATING request directly) -> 400 Bad Request ProblemDetails
        var invalidTransitionResp = await ctx.Client.PostAsJsonAsync($"/api/v1/requests/{req1Id}/complete", new
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
