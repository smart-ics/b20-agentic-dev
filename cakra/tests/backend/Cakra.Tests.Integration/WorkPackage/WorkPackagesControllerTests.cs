using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cakra.Api.Infrastructure.Authentication;
using Cakra.Api.Infrastructure.Context;
using Cakra.Api.Infrastructure.Migrations;
using Cakra.Core;
using Cakra.Modules.Customer.Services;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Organization.Commands;
using Cakra.Modules.Product.Services;
using Cakra.Modules.Request.Services;
using Cakra.Modules.WorkPackage.Domain;
using Cakra.Modules.WorkPackage.Models;
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
/// Integration tests verifying P5-S26 (<c>SCR-WP-001</c> Work Package API Controller):
/// - All <c>/api/v1/work-packages</c> endpoints protected by <c>[Authorize]</c> (401 when unauthenticated)
/// - <c>POST /api/v1/work-packages</c> (<c>WorkPackageService.CreateWorkPackage</c> -&gt; 201 Created)
/// - <c>PUT /api/v1/work-packages/{id}/objective</c> (<c>WorkPackageService.UpdateObjective</c> -&gt; 200 OK)
/// - <c>POST /api/v1/work-packages/{id}/assign-owner</c> (<c>WorkPackageService.AssignOwner</c> -&gt; 200 OK)
/// - <c>POST /api/v1/work-packages/{id}/requests</c> (<c>WorkPackageService.AddRequestToWorkPackage</c> -&gt; 200 OK)
/// - <c>DELETE /api/v1/work-packages/{id}/requests/{requestId}</c> (<c>WorkPackageService.RemoveRequestFromWorkPackage</c> -&gt; 200 OK)
/// - <c>POST /api/v1/work-packages/{id}/activate</c> (<c>WorkPackageService.ActivateWorkPackage</c> -&gt; 200 OK)
/// - <c>POST /api/v1/work-packages/{id}/close</c> (<c>WorkPackageService.CloseWorkPackage</c> -&gt; 200 OK)
/// - <c>GET /api/v1/work-packages</c> (<c>WorkPackageQueryService.ListWorkPackages</c> with status/owner/customer/product filters -&gt; 200 OK)
/// - <c>GET /api/v1/work-packages/{id}</c> (<c>WorkPackageQueryService.GetWorkPackageById</c> -&gt; 200 OK / 404 NotFound)
/// - <c>GET /api/v1/work-packages/{id}/scope</c> (<c>WorkPackageQueryService.GetWorkPackageScope</c> -&gt; 200 OK / 404 NotFound)
/// - RFC 7807 <c>ProblemDetails</c> responses for 400 validation/domain errors (including Business Rule 9) and 404 not found.
/// </summary>
public sealed class WorkPackagesControllerTests : IAsyncLifetime
{
    private const string LocalDbFallback =
        "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb_WorkPackagesController;Integrated Security=true;TrustServerCertificate=True;";

    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private Respawner? _respawner;
    private bool _sqlServerAvailable;

    public WorkPackagesControllerTests()
    {
        var baseConnectionString = DatabaseResetHelper.ResolveConnectionString() ?? LocalDbFallback;
        _connectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "CakraTestDb_WorkPackagesController"
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

        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                SchemasToInclude = ["workpackage", "request", "product", "customer", "organization", "identity"],
                TablesToIgnore =
                [
                    new Table("dbo", "__SchemaVersions"),
                    new Table("dbo", "SchemaVersions")
                ]
            });
            await _respawner.ResetAsync(connection);
        }

        _factory = new CakraWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
            builder.UseSetting("ConnectionStrings:TestConnection", _connectionString);
        });
    }

    public async Task DisposeAsync()
    {
        if (_sqlServerAvailable && _respawner is not null)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await _respawner.ResetAsync(connection);
        }

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Unauthenticated_requests_to_work_packages_endpoints_return_401_Unauthorized()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var wpId = Guid.NewGuid();
        var reqId = Guid.NewGuid();

        (await client.GetAsync("/api/v1/work-packages"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.GetAsync("/api/v1/work-packages/operations-cockpit"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.GetAsync($"/api/v1/work-packages/{wpId}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.GetAsync($"/api/v1/work-packages/{wpId}/scope"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PostAsJsonAsync("/api/v1/work-packages", new
        {
            name = "Package",
            objective = "Objective",
            ownerPersonId = Guid.NewGuid()
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/objective", new
        {
            name = "Updated",
            objective = "Updated Objective"
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/deadline", new
        {
            deadline = "2026-11-01"
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/context", new
        {
            customerId = Guid.NewGuid(),
            productId = Guid.NewGuid()
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PostAsJsonAsync($"/api/v1/work-packages/{wpId}/assign-owner", new
        {
            newOwnerPersonId = Guid.NewGuid()
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PostAsync($"/api/v1/work-packages/{wpId}/activate", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PostAsJsonAsync($"/api/v1/work-packages/{wpId}/close", new
        {
            reason = "Done"
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PostAsJsonAsync($"/api/v1/work-packages/{wpId}/requests", new
        {
            requestId = reqId
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/requests/reorder", new
        {
            orderedRequestIds = new[] { reqId }
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.DeleteAsync($"/api/v1/work-packages/{wpId}/requests/{reqId}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WorkPackage_endpoints_support_full_lifecycle_scope_management_and_filtered_queries()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        var seeded = await CreateAuthenticatedClientAndSeedContextAsync();
        using var client = seeded.Client;

        // 1. POST /api/v1/work-packages -> 201 Created in DRAFT state
        var create1Response = await client.PostAsJsonAsync("/api/v1/work-packages", new
        {
            name = "RSUP Sardjito Q4 Billing Stabilization",
            objective = "Deliver claim and billing stabilization items prior to monthly closing.",
            ownerPersonId = seeded.Owner1Id,
            customerId = seeded.Customer1Id,
            productId = seeded.Product1Id
        });
        create1Response.StatusCode.Should().Be(HttpStatusCode.Created);
        create1Response.Headers.Location.Should().NotBeNull();

        var created1 = await create1Response.Content.ReadFromJsonAsync<JsonElement>();
        var wp1Id = created1.GetProperty("id").GetGuid();
        wp1Id.Should().NotBeEmpty();
        created1.GetProperty("name").GetString().Should().Be("RSUP Sardjito Q4 Billing Stabilization");
        created1.GetProperty("objective").GetString().Should().Be("Deliver claim and billing stabilization items prior to monthly closing.");
        created1.GetProperty("status").GetString().Should().Be(WorkPackageStatusNames.Draft);
        created1.GetProperty("ownerPersonId").GetGuid().Should().Be(seeded.Owner1Id);
        created1.GetProperty("ownerName").GetString().Should().Be("Dewi Lestari");
        created1.GetProperty("customerId").GetGuid().Should().Be(seeded.Customer1Id);
        created1.GetProperty("customerName").GetString().Should().Be("RSUP Dr. Sardjito");
        created1.GetProperty("productId").GetGuid().Should().Be(seeded.Product1Id);
        created1.GetProperty("productName").GetString().Should().Be("MyHospital Billing");

        // Create a second work package for list filter testing
        var create2Response = await client.PostAsJsonAsync("/api/v1/work-packages", new
        {
            name = "RSUD Jogja Pharmacy Rollout",
            objective = "Coordinate pharmacy module rollout tasks.",
            ownerPersonId = seeded.Owner1Id,
            customerId = seeded.Customer2Id,
            productId = seeded.Product2Id
        });
        create2Response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created2 = await create2Response.Content.ReadFromJsonAsync<JsonElement>();
        var wp2Id = created2.GetProperty("id").GetGuid();

        // 2. PUT /api/v1/work-packages/{id}/objective -> 200 OK
        var updateObjResponse = await client.PutAsJsonAsync($"/api/v1/work-packages/{wp1Id}/objective", new
        {
            name = "RSUP Sardjito Billing & Bridging Stabilization",
            objective = "Deliver INA-CBGs and BPJS SEP bridging stabilization prior to monthly closing."
        });
        updateObjResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedObj = await updateObjResponse.Content.ReadFromJsonAsync<JsonElement>();
        updatedObj.GetProperty("name").GetString().Should().Be("RSUP Sardjito Billing & Bridging Stabilization");
        updatedObj.GetProperty("objective").GetString().Should().Be("Deliver INA-CBGs and BPJS SEP bridging stabilization prior to monthly closing.");

        // 3. POST /api/v1/work-packages/{id}/assign-owner -> 200 OK
        var assignOwnerResponse = await client.PostAsJsonAsync($"/api/v1/work-packages/{wp1Id}/assign-owner", new
        {
            newOwnerPersonId = seeded.Owner2Id
        });
        assignOwnerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reassigned = await assignOwnerResponse.Content.ReadFromJsonAsync<JsonElement>();
        reassigned.GetProperty("ownerPersonId").GetGuid().Should().Be(seeded.Owner2Id);
        reassigned.GetProperty("ownerName").GetString().Should().Be("Fajar Pratama");

        // 4. POST /api/v1/work-packages/{id}/requests -> 200 OK (add Request1 and Request2)
        var addReq1Response = await client.PostAsJsonAsync($"/api/v1/work-packages/{wp1Id}/requests", new
        {
            requestId = seeded.Request1Id
        });
        addReq1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterAdd1 = await addReq1Response.Content.ReadFromJsonAsync<JsonElement>();
        afterAdd1.GetProperty("activeRequestCount").GetInt32().Should().Be(1);

        var addReq2Response = await client.PostAsJsonAsync($"/api/v1/work-packages/{wp1Id}/requests", new
        {
            requestId = seeded.Request2Id
        });
        addReq2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterAdd2 = await addReq2Response.Content.ReadFromJsonAsync<JsonElement>();
        afterAdd2.GetProperty("activeRequestCount").GetInt32().Should().Be(2);

        // 5. POST /api/v1/work-packages/{id}/activate -> 200 OK (DRAFT -> ACTIVE)
        var activateResponse = await client.PostAsync($"/api/v1/work-packages/{wp1Id}/activate", null);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var activated = await activateResponse.Content.ReadFromJsonAsync<JsonElement>();
        activated.GetProperty("status").GetString().Should().Be(WorkPackageStatusNames.Active);

        // 6. DELETE /api/v1/work-packages/{id}/requests/{requestId} -> 200 OK (remove Request2)
        var removeReq2Response = await client.DeleteAsync($"/api/v1/work-packages/{wp1Id}/requests/{seeded.Request2Id}");
        removeReq2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterRemove2 = await removeReq2Response.Content.ReadFromJsonAsync<JsonElement>();
        afterRemove2.GetProperty("activeRequestCount").GetInt32().Should().Be(1);
        afterRemove2.GetProperty("totalRequestCount").GetInt32().Should().Be(2);

        // 7. GET /api/v1/work-packages/{id} -> 200 OK
        var detailResponse = await client.GetAsync($"/api/v1/work-packages/{wp1Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("id").GetGuid().Should().Be(wp1Id);
        detail.GetProperty("status").GetString().Should().Be(WorkPackageStatusNames.Active);
        detail.GetProperty("ownerPersonId").GetGuid().Should().Be(seeded.Owner2Id);
        detail.GetProperty("ownerName").GetString().Should().Be("Fajar Pratama");
        detail.GetProperty("activeRequestCount").GetInt32().Should().Be(1);
        detail.GetProperty("totalRequestCount").GetInt32().Should().Be(2);

        // 8. GET /api/v1/work-packages/{id}/scope -> 200 OK (returns active + historical requests enriched)
        var scopeResponse = await client.GetAsync($"/api/v1/work-packages/{wp1Id}/scope");
        scopeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var scopeArray = await scopeResponse.Content.ReadFromJsonAsync<JsonElement>();
        scopeArray.GetArrayLength().Should().Be(2);

        var scopeItems = scopeArray.EnumerateArray().ToList();
        var activeItem = scopeItems.Single(x => x.GetProperty("requestId").GetGuid() == seeded.Request1Id);
        activeItem.GetProperty("isActive").GetBoolean().Should().BeTrue();
        activeItem.GetProperty("title").GetString().Should().Be("INA-CBGs tariff grouping validation fix");
        activeItem.GetProperty("priority").GetString().Should().Be("HIGH");

        var removedItem = scopeItems.Single(x => x.GetProperty("requestId").GetGuid() == seeded.Request2Id);
        removedItem.GetProperty("isActive").GetBoolean().Should().BeFalse();
        removedItem.GetProperty("removedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
        removedItem.GetProperty("title").GetString().Should().Be("Discharge summary claim export PDF header");

        // 9. GET /api/v1/work-packages with filters (status, ownerPersonId, customerId, productId)
        var listAllResponse = await client.GetAsync("/api/v1/work-packages");
        listAllResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var allItems = await listAllResponse.Content.ReadFromJsonAsync<JsonElement>();
        allItems.GetArrayLength().Should().Be(2);

        var activeListResponse = await client.GetAsync("/api/v1/work-packages?status=ACTIVE");
        activeListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var activeItems = await activeListResponse.Content.ReadFromJsonAsync<JsonElement>();
        activeItems.EnumerateArray().Select(x => x.GetProperty("id").GetGuid())
            .Should().ContainSingle().Which.Should().Be(wp1Id);

        var draftListResponse = await client.GetAsync("/api/v1/work-packages?status=DRAFT");
        draftListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var draftItems = await draftListResponse.Content.ReadFromJsonAsync<JsonElement>();
        draftItems.EnumerateArray().Select(x => x.GetProperty("id").GetGuid())
            .Should().ContainSingle().Which.Should().Be(wp2Id);

        var ownerListResponse = await client.GetAsync($"/api/v1/work-packages?ownerPersonId={seeded.Owner2Id}");
        ownerListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownerItems = await ownerListResponse.Content.ReadFromJsonAsync<JsonElement>();
        ownerItems.EnumerateArray().Select(x => x.GetProperty("id").GetGuid())
            .Should().ContainSingle().Which.Should().Be(wp1Id);

        var customerListResponse = await client.GetAsync($"/api/v1/work-packages?customerId={seeded.Customer2Id}");
        customerListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var customerItems = await customerListResponse.Content.ReadFromJsonAsync<JsonElement>();
        customerItems.EnumerateArray().Select(x => x.GetProperty("id").GetGuid())
            .Should().ContainSingle().Which.Should().Be(wp2Id);

        var productListResponse = await client.GetAsync($"/api/v1/work-packages?productId={seeded.Product1Id}");
        productListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var productItems = await productListResponse.Content.ReadFromJsonAsync<JsonElement>();
        productItems.EnumerateArray().Select(x => x.GetProperty("id").GetGuid())
            .Should().ContainSingle().Which.Should().Be(wp1Id);

        // 10. POST /api/v1/work-packages/{id}/close -> 200 OK (ACTIVE -> CLOSED)
        var closeResponse = await client.PostAsJsonAsync($"/api/v1/work-packages/{wp1Id}/close", new
        {
            reason = "Monthly billing stabilization completed and signed off."
        });
        closeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var closed = await closeResponse.Content.ReadFromJsonAsync<JsonElement>();
        closed.GetProperty("status").GetString().Should().Be(WorkPackageStatusNames.Closed);
        closed.GetProperty("closedReason").GetString().Should().Be("Monthly billing stabilization completed and signed off.");
        closed.GetProperty("closedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task WorkPackage_endpoints_return_ProblemDetails_on_validation_not_found_and_Business_Rule_9_violations()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        var seeded = await CreateAuthenticatedClientAndSeedContextAsync();
        using var client = seeded.Client;

        // 1. GET unknown work package -> 404 NotFound ProblemDetails
        var unknownId = Guid.NewGuid();
        var notFoundResponse = await client.GetAsync($"/api/v1/work-packages/{unknownId}");
        notFoundResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        notFoundResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var notFoundScopeResponse = await client.GetAsync($"/api/v1/work-packages/{unknownId}/scope");
        notFoundScopeResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        notFoundScopeResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        // 2. POST with missing required fields -> 400 BadRequest ProblemDetails
        var invalidCreateResponse = await client.PostAsJsonAsync("/api/v1/work-packages", new
        {
            name = "",
            objective = "",
            ownerPersonId = Guid.Empty
        });
        invalidCreateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        invalidCreateResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        // 3. POST with nonexistent owner -> 404 NotFound ProblemDetails
        var unknownOwnerResponse = await client.PostAsJsonAsync("/api/v1/work-packages", new
        {
            name = "Unknown Owner Package",
            objective = "Objective",
            ownerPersonId = Guid.NewGuid()
        });
        unknownOwnerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknownOwnerResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        // 4. Business Rule 9 violation across active work packages -> 400 BadRequest ProblemDetails
        var wp1Create = await client.PostAsJsonAsync("/api/v1/work-packages", new
        {
            name = "Active Package A",
            objective = "First active package",
            ownerPersonId = seeded.Owner1Id
        });
        var wp1Id = (await wp1Create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var wp2Create = await client.PostAsJsonAsync("/api/v1/work-packages", new
        {
            name = "Active Package B",
            objective = "Second active package",
            ownerPersonId = seeded.Owner1Id
        });
        var wp2Id = (await wp2Create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await client.PostAsync($"/api/v1/work-packages/{wp1Id}/activate", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/api/v1/work-packages/{wp2Id}/activate", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Add Request1 to wp1 -> 200 OK
        (await client.PostAsJsonAsync($"/api/v1/work-packages/{wp1Id}/requests", new
        {
            requestId = seeded.Request1Id
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        // Adding Request1 to wp2 while active in wp1 violates Business Rule 9 -> 400 BadRequest ProblemDetails
        var rule9ViolationResponse = await client.PostAsJsonAsync($"/api/v1/work-packages/{wp2Id}/requests", new
        {
            requestId = seeded.Request1Id
        });
        rule9ViolationResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        rule9ViolationResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problem = await rule9ViolationResponse.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("detail").GetString().Should().Contain("Business Rule 9");

        // Activating an already active work package -> 400 BadRequest ProblemDetails
        var duplicateActivateResponse = await client.PostAsync($"/api/v1/work-packages/{wp1Id}/activate", null);
        duplicateActivateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        duplicateActivateResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Reorder_requests_endpoint_updates_sort_orders_in_database_and_returns_sorted_scope()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        var seeded = await CreateAuthenticatedClientAndSeedContextAsync();
        using var client = seeded.Client;

        // 1. Create a 3rd request for clear 3-item reordering
        Guid request3Id;
        using (var scope = _factory!.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var req3 = await mediator.Send(new RecordRequestCommand(
                Title: "BPJS SEP bridging retry timeout adjustment",
                Description: "Increase timeout threshold.",
                CustomerId: seeded.Customer1Id,
                ProductId: seeded.Product1Id,
                RequestType: "Support",
                Priority: "URGENT"));
            request3Id = req3.Id;
        }

        // 2. Create a Work Package
        var createResponse = await client.PostAsJsonAsync("/api/v1/work-packages", new
        {
            name = "Reorder Test Package",
            objective = "Test drag and drop reordering",
            ownerPersonId = seeded.Owner1Id,
            customerId = seeded.Customer1Id,
            productId = seeded.Product1Id
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var wpId = created.GetProperty("id").GetGuid();

        // 3. Add 3 requests (Request1, Request2, Request3)
        (await client.PostAsJsonAsync($"/api/v1/work-packages/{wpId}/requests", new { requestId = seeded.Request1Id }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"/api/v1/work-packages/{wpId}/requests", new { requestId = seeded.Request2Id }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"/api/v1/work-packages/{wpId}/requests", new { requestId = request3Id }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify initial scope order: [Request1 (sortOrder 0), Request2 (sortOrder 1), Request3 (sortOrder 2)]
        var initialScopeResponse = await client.GetAsync($"/api/v1/work-packages/{wpId}/scope");
        initialScopeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialScope = await initialScopeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var initialItems = initialScope.EnumerateArray().ToList();
        initialItems.Should().HaveCount(3);
        initialItems[0].GetProperty("requestId").GetGuid().Should().Be(seeded.Request1Id);
        initialItems[0].GetProperty("sortOrder").GetInt32().Should().Be(0);
        initialItems[1].GetProperty("requestId").GetGuid().Should().Be(seeded.Request2Id);
        initialItems[1].GetProperty("sortOrder").GetInt32().Should().Be(1);
        initialItems[2].GetProperty("requestId").GetGuid().Should().Be(request3Id);
        initialItems[2].GetProperty("sortOrder").GetInt32().Should().Be(2);

        // 4. Reorder requests to: [Request3, Request1, Request2]
        var reorderPayload = new
        {
            orderedRequestIds = new[] { request3Id, seeded.Request1Id, seeded.Request2Id }
        };
        var reorderResponse = await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/requests/reorder", reorderPayload);
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Query GET /api/v1/work-packages/{id}/scope and verify updated sort order
        var reorderedScopeResponse = await client.GetAsync($"/api/v1/work-packages/{wpId}/scope");
        reorderedScopeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reorderedScope = await reorderedScopeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var reorderedItems = reorderedScope.EnumerateArray().ToList();
        reorderedItems.Should().HaveCount(3);

        reorderedItems[0].GetProperty("requestId").GetGuid().Should().Be(request3Id);
        reorderedItems[0].GetProperty("sortOrder").GetInt32().Should().Be(0);

        reorderedItems[1].GetProperty("requestId").GetGuid().Should().Be(seeded.Request1Id);
        reorderedItems[1].GetProperty("sortOrder").GetInt32().Should().Be(1);

        reorderedItems[2].GetProperty("requestId").GetGuid().Should().Be(seeded.Request2Id);
        reorderedItems[2].GetProperty("sortOrder").GetInt32().Should().Be(2);

        // 6. Direct SQL query verification on [workpackage].[WorkPackageRequests] database table
        await using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var dbRows = (await conn.QueryAsync<(Guid RequestId, int SortOrder)>(
                "SELECT RequestId, SortOrder FROM [workpackage].[WorkPackageRequests] WHERE WorkPackageId = @wpId AND RemovedAt IS NULL ORDER BY SortOrder ASC",
                new { wpId })).ToList();

            dbRows.Should().HaveCount(3);
            dbRows[0].RequestId.Should().Be(request3Id);
            dbRows[0].SortOrder.Should().Be(0);
            dbRows[1].RequestId.Should().Be(seeded.Request1Id);
            dbRows[1].SortOrder.Should().Be(1);
            dbRows[2].RequestId.Should().Be(seeded.Request2Id);
            dbRows[2].SortOrder.Should().Be(2);
        }

        // 7. Test error cases on reorder endpoint:
        // a. Mismatched IDs (missing an item) -> 400 BadRequest ProblemDetails
        var invalidCountResponse = await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/requests/reorder", new
        {
            orderedRequestIds = new[] { request3Id, seeded.Request1Id }
        });
        invalidCountResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // b. Foreign / unknown Request ID -> 400 BadRequest ProblemDetails
        var foreignIdResponse = await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/requests/reorder", new
        {
            orderedRequestIds = new[] { request3Id, seeded.Request1Id, Guid.NewGuid() }
        });
        foreignIdResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // c. Duplicate IDs -> 400 BadRequest ProblemDetails
        var duplicateIdResponse = await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/requests/reorder", new
        {
            orderedRequestIds = new[] { request3Id, request3Id, seeded.Request2Id }
        });
        duplicateIdResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // d. Non-existent WorkPackage -> 404 NotFound ProblemDetails
        var notFoundResponse = await client.PutAsJsonAsync($"/api/v1/work-packages/{Guid.NewGuid()}/requests/reorder", reorderPayload);
        notFoundResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // e. Closed WorkPackage -> 409 Conflict ProblemDetails
        await client.PostAsJsonAsync($"/api/v1/work-packages/{wpId}/close", new { reason = "Finished" });
        var closedReorderResponse = await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/requests/reorder", reorderPayload);
        closedReorderResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task WorkPackage_deadline_endpoint_supports_creation_updating_clearing_and_rejects_closed()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        var seeded = await CreateAuthenticatedClientAndSeedContextAsync();
        using var client = seeded.Client;

        // 1. Create with deadline via POST /api/v1/work-packages
        var targetDeadline = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
        var createResponse = await client.PostAsJsonAsync("/api/v1/work-packages", new
        {
            name = "Work Package With Initial Deadline",
            objective = "Validate deadline creation and updates via REST API.",
            ownerPersonId = seeded.Owner1Id,
            customerId = seeded.Customer1Id,
            productId = seeded.Product1Id,
            deadline = targetDeadline
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var wpId = created.GetProperty("id").GetGuid();
        created.GetProperty("deadline").GetDateTime().Should().Be(targetDeadline);

        // 2. Verify GET /api/v1/work-packages/{id} preserves deadline
        var getResponse = await client.GetAsync($"/api/v1/work-packages/{wpId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrieved = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        retrieved.GetProperty("deadline").GetDateTime().Should().Be(targetDeadline);

        // 3. Update deadline via PUT /api/v1/work-packages/{id}/deadline
        var updatedDeadline = new DateTime(2026, 12, 15, 0, 0, 0, DateTimeKind.Utc);
        var updateResponse = await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/deadline", new
        {
            deadline = updatedDeadline
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("deadline").GetDateTime().Should().Be(updatedDeadline);

        // 4. Clear deadline via PUT /api/v1/work-packages/{id}/deadline with null
        var clearResponse = await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/deadline", new
        {
            deadline = (DateTime?)null
        });
        clearResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cleared = await clearResponse.Content.ReadFromJsonAsync<JsonElement>();
        cleared.GetProperty("deadline").ValueKind.Should().Be(JsonValueKind.Null);

        // 5. Non-existent work package -> 404 NotFound
        var notFoundResponse = await client.PutAsJsonAsync($"/api/v1/work-packages/{Guid.NewGuid()}/deadline", new
        {
            deadline = updatedDeadline
        });
        notFoundResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 6. Close work package
        var closeResponse = await client.PostAsJsonAsync($"/api/v1/work-packages/{wpId}/close", new
        {
            reason = "Completed all scope"
        });
        closeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 7. Update deadline on closed work package -> 400 BadRequest
        var closedUpdateResponse = await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/deadline", new
        {
            deadline = updatedDeadline
        });
        closedUpdateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WorkPackage_context_endpoint_supports_updating_clearing_and_rejects_closed()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        var seeded = await CreateAuthenticatedClientAndSeedContextAsync();
        using var client = seeded.Client;

        // 1. Create a work package initially without customer/product
        var createResponse = await client.PostAsJsonAsync("/api/v1/work-packages", new
        {
            name = "Context Test Work Package",
            objective = "Validate customer and product context update endpoint.",
            ownerPersonId = seeded.Owner1Id
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var wpId = created.GetProperty("id").GetGuid();
        created.GetProperty("customerId").ValueKind.Should().Be(JsonValueKind.Null);
        created.GetProperty("productId").ValueKind.Should().Be(JsonValueKind.Null);

        // 2. Update context via PUT /api/v1/work-packages/{id}/context (Customer1 & Product1)
        var updateResponse = await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/context", new
        {
            customerId = seeded.Customer1Id,
            productId = seeded.Product1Id
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("customerId").GetGuid().Should().Be(seeded.Customer1Id);
        updated.GetProperty("customerName").GetString().Should().Be("RSUP Dr. Sardjito");
        updated.GetProperty("productId").GetGuid().Should().Be(seeded.Product1Id);
        updated.GetProperty("productName").GetString().Should().Be("MyHospital Billing");

        // Verify persistence via GET /api/v1/work-packages/{id}
        var getResponse = await client.GetAsync($"/api/v1/work-packages/{wpId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrieved = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        retrieved.GetProperty("customerId").GetGuid().Should().Be(seeded.Customer1Id);
        retrieved.GetProperty("productId").GetGuid().Should().Be(seeded.Product1Id);

        // 3. Update to Customer2 and Product2
        var update2Response = await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/context", new
        {
            customerId = seeded.Customer2Id,
            productId = seeded.Product2Id
        });
        update2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated2 = await update2Response.Content.ReadFromJsonAsync<JsonElement>();
        updated2.GetProperty("customerId").GetGuid().Should().Be(seeded.Customer2Id);
        updated2.GetProperty("customerName").GetString().Should().Be("RSUD Kota Yogyakarta");
        updated2.GetProperty("productId").GetGuid().Should().Be(seeded.Product2Id);
        updated2.GetProperty("productName").GetString().Should().Be("PenaEl Pharmacy");

        // 4. Clear Customer and Product by setting them to null
        var clearResponse = await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/context", new
        {
            customerId = (Guid?)null,
            productId = (Guid?)null
        });
        clearResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cleared = await clearResponse.Content.ReadFromJsonAsync<JsonElement>();
        cleared.GetProperty("customerId").ValueKind.Should().Be(JsonValueKind.Null);
        cleared.GetProperty("productId").ValueKind.Should().Be(JsonValueKind.Null);

        // Verify cleared in database via GET
        var getClearedResponse = await client.GetAsync($"/api/v1/work-packages/{wpId}");
        getClearedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrievedCleared = await getClearedResponse.Content.ReadFromJsonAsync<JsonElement>();
        retrievedCleared.GetProperty("customerId").ValueKind.Should().Be(JsonValueKind.Null);
        retrievedCleared.GetProperty("productId").ValueKind.Should().Be(JsonValueKind.Null);

        // 5. Non-existent work package -> 404 NotFound ProblemDetails
        var notFoundResponse = await client.PutAsJsonAsync($"/api/v1/work-packages/{Guid.NewGuid()}/context", new
        {
            customerId = seeded.Customer1Id
        });
        notFoundResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 6. Non-existent or invalid Customer/Product -> 400 BadRequest or 404 NotFound ProblemDetails
        var invalidCustomerResponse = await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/context", new
        {
            customerId = Guid.NewGuid()
        });
        invalidCustomerResponse.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);

        var invalidProductResponse = await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/context", new
        {
            productId = Guid.NewGuid()
        });
        invalidProductResponse.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);

        // 7. Close work package and verify updating context is rejected with 400 BadRequest
        var closeResponse = await client.PostAsJsonAsync($"/api/v1/work-packages/{wpId}/close", new
        {
            reason = "Completed"
        });
        closeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Updating context on closed package -> 400 BadRequest
        var closedUpdateResponse = await client.PutAsJsonAsync($"/api/v1/work-packages/{wpId}/context", new
        {
            customerId = seeded.Customer1Id,
            productId = seeded.Product1Id
        });
        closedUpdateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var closedProblem = await closedUpdateResponse.Content.ReadFromJsonAsync<JsonElement>();
        closedProblem.GetProperty("detail").GetString().Should().Contain("closed");
    }

    [Fact]
    public async Task GetOperationsCockpit_returns_200_OK_with_valid_telemetry_matrix_and_metrics()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        var seeded = await CreateAuthenticatedClientAndSeedContextAsync();
        using var client = seeded.Client;

        // 1. Initial call when no active work packages exist yet
        var initialResponse = await client.GetAsync("/api/v1/work-packages/operations-cockpit");
        initialResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var initialCockpit = await initialResponse.Content.ReadFromJsonAsync<OperationsCockpitDto>(
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        initialCockpit.Should().NotBeNull();
        initialCockpit!.PortfolioMetrics.Should().NotBeNull();
        initialCockpit.PortfolioMetrics.TotalActiveWorkPackages.Should().Be(0);
        initialCockpit.Matrix.Should().NotBeNull();
        initialCockpit.Matrix.Cells.Should().ContainKey(WorkPackageHealthStates.Flowing);
        initialCockpit.Packages.Should().BeEmpty();

        // 2. Create a work package, associate requests, and activate it
        var createResponse = await client.PostAsJsonAsync("/api/v1/work-packages", new
        {
            name = "Radiology PACS Migration",
            objective = "Migrate DICOM viewer storage to cloud cluster.",
            ownerPersonId = seeded.Owner1Id,
            customerId = seeded.Customer1Id,
            productId = seeded.Product1Id,
            deadline = DateTime.UtcNow.AddDays(14)
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdWp = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var wpId = createdWp.GetProperty("id").GetGuid();

        // Add request to work package
        var addReqResponse = await client.PostAsJsonAsync($"/api/v1/work-packages/{wpId}/requests", new
        {
            requestId = seeded.Request1Id
        });
        addReqResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Activate work package
        var activateResponse = await client.PostAsync($"/api/v1/work-packages/{wpId}/activate", null);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Query operations-cockpit without query params
        var response = await client.GetAsync("/api/v1/work-packages/operations-cockpit");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var cockpit = await response.Content.ReadFromJsonAsync<OperationsCockpitDto>(
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        cockpit.Should().NotBeNull();

        // Portfolio metrics assertions
        cockpit!.PortfolioMetrics.TotalActiveWorkPackages.Should().Be(1);
        cockpit.PortfolioMetrics.WithDeadlineCount.Should().Be(1);
        cockpit.PortfolioMetrics.WithoutDeadlineCount.Should().Be(0);
        cockpit.PortfolioMetrics.OrgDailyThroughput.Should().BeGreaterThanOrEqualTo(1.0);

        // Matrix assertions
        cockpit.Matrix.Cells.Should().ContainKeys(
            WorkPackageHealthStates.DeadlineBreached,
            WorkPackageHealthStates.ActiveBlockers,
            WorkPackageHealthStates.Dormant,
            WorkPackageHealthStates.WipStagnant,
            WorkPackageHealthStates.Flowing);
        cockpit.Matrix.RowTotals.Values.Sum().Should().Be(1);
        cockpit.Matrix.ColumnTotals.Values.Sum().Should().Be(1);

        // Packages assertions
        cockpit.Packages.Should().HaveCount(1);
        var pkg = cockpit.Packages.Single();
        pkg.Id.Should().Be(wpId);
        pkg.Name.Should().Be("Radiology PACS Migration");
        pkg.Status.Should().Be(WorkPackageStatusNames.Active);
        pkg.Deadline.Should().NotBeNull();
        pkg.TotalRequestsCount.Should().Be(1);
        pkg.PressureTier.Should().BeOneOf(
            WorkPackagePressureTiers.Unplanned,
            WorkPackagePressureTiers.Nominal,
            WorkPackagePressureTiers.Elevated,
            WorkPackagePressureTiers.Critical,
            WorkPackagePressureTiers.Impossible);
        pkg.HealthState.Should().BeOneOf(
            WorkPackageHealthStates.DeadlineBreached,
            WorkPackageHealthStates.ActiveBlockers,
            WorkPackageHealthStates.Dormant,
            WorkPackageHealthStates.WipStagnant,
            WorkPackageHealthStates.Flowing);
        pkg.FlowBarcode.Should().HaveCount(14);

        // 4. Query with asOfDate query parameter
        var asOf = DateTime.UtcNow.ToString("O");
        var filteredResponse = await client.GetAsync($"/api/v1/work-packages/operations-cockpit?asOfDate={asOf}");
        filteredResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var asOfUtcResponse = await client.GetAsync($"/api/v1/work-packages/operations-cockpit?asOfDateUtc={asOf}");
        asOfUtcResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<SeededWorkPackageTestContext> CreateAuthenticatedClientAndSeedContextAsync()
    {
        Guid owner1Id;
        Guid owner2Id;
        Guid customer1Id;
        Guid customer2Id;
        Guid product1Id;
        Guid product2Id;
        Guid request1Id;
        Guid request2Id;
        string sessionToken;

        using (var scope = _factory!.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var userAccountRepo = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
            var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

            var owner1 = await mediator.Send(new CreatePersonCommand(
                "Dewi",
                "Lestari",
                $"dewi.{Guid.NewGuid():N}@cakra.id"));
            var owner2 = await mediator.Send(new CreatePersonCommand(
                "Fajar",
                "Pratama",
                $"fajar.{Guid.NewGuid():N}@cakra.id"));
            owner1Id = owner1.Id;
            owner2Id = owner2.Id;

            var customer1 = await mediator.Send(new CreateCustomerCommand(
                $"CUST1-{Guid.NewGuid():N}"[..15],
                "RSUP Dr. Sardjito",
                HasActiveMaintenanceContract: true));
            var customer2 = await mediator.Send(new CreateCustomerCommand(
                $"CUST2-{Guid.NewGuid():N}"[..15],
                "RSUD Kota Yogyakarta",
                HasActiveMaintenanceContract: true));
            customer1Id = customer1.Id;
            customer2Id = customer2.Id;

            var product1 = await mediator.Send(new CreateProductCommand(
                $"PRD1-{Guid.NewGuid():N}"[..14],
                "MyHospital Billing",
                "Hospital Billing & INA-CBGs Integration",
                owner1Id));
            var product2 = await mediator.Send(new CreateProductCommand(
                $"PRD2-{Guid.NewGuid():N}"[..14],
                "PenaEl Pharmacy",
                "Pharmacy Inventory & e-Prescription",
                owner1Id));
            product1Id = product1.Id;
            product2Id = product2.Id;

            currentContext?.Initialize(Guid.NewGuid(), owner1Id, new[] { "Management", "Implementator" });

            var req1 = await mediator.Send(new RecordRequestCommand(
                Title: "INA-CBGs tariff grouping validation fix",
                Description: "Ensure inpatient procedure codes map to updated tariff table.",
                CustomerId: customer1Id,
                ProductId: product1Id,
                RequestType: "Bug",
                Priority: "HIGH"));
            var req2 = await mediator.Send(new RecordRequestCommand(
                Title: "Discharge summary claim export PDF header",
                Description: "Include hospital accreditation badge on claim summary export.",
                CustomerId: customer1Id,
                ProductId: product1Id,
                RequestType: "Feature",
                Priority: "NORMAL"));
            request1Id = req1.Id;
            request2Id = req2.Id;

            var username = $"dewi.{Guid.NewGuid():N}"[..20];
            var user = new UserAccount
            {
                Id = Guid.NewGuid(),
                PersonId = owner1Id,
                Username = username,
                Email = owner1.Email,
                Status = UserAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = authService.HashPassword(user, "Password123!");
            await userAccountRepo.AddAsync(user);

            var loginResult = await authService.LoginAsync(username, "Password123!");
            loginResult.Succeeded.Should().BeTrue();
            sessionToken = loginResult.SessionToken!;
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}={sessionToken}");

        return new SeededWorkPackageTestContext(
            client,
            owner1Id,
            owner2Id,
            customer1Id,
            customer2Id,
            product1Id,
            product2Id,
            request1Id,
            request2Id);
    }

    private sealed record SeededWorkPackageTestContext(
        HttpClient Client,
        Guid Owner1Id,
        Guid Owner2Id,
        Guid Customer1Id,
        Guid Customer2Id,
        Guid Product1Id,
        Guid Product2Id,
        Guid Request1Id,
        Guid Request2Id);
}
