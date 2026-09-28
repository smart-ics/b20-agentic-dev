using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cakra.Api.Infrastructure.Authentication;
using Cakra.Api.Infrastructure.Migrations;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Organization.Commands;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Respawn;
using Respawn.Graph;
using Xunit;

namespace Cakra.Tests.Integration.Product;

/// <summary>
/// Integration tests verifying P3-S16 (<c>SCR-PRD-001</c> Product Catalog API Controller):
/// - All <c>/api/v1/products</c> endpoints protected by <c>[Authorize]</c> (401 when unauthenticated)
/// - <c>GET /api/v1/organization/persons/active</c> and <c>GET /api/v1/products/owners</c> return active persons via <c>OrganizationQueryService.ListActivePersons</c>
/// - <c>POST /api/v1/products</c> (<c>CreateProduct</c>)
/// - <c>GET /api/v1/products</c> (<c>ListAllProducts</c>) and <c>GET /api/v1/products/active</c> (<c>ListActiveProducts</c>)
/// - <c>GET /api/v1/products/{id}</c> (<c>GetProductById</c>)
/// - <c>PUT /api/v1/products/{id}</c> (<c>UpdateProduct</c>)
/// - <c>PUT /api/v1/products/{id}/owner</c> (<c>AssignProductOwner</c>)
/// - <c>POST /api/v1/products/{id}/deactivate</c> and <c>POST /api/v1/products/{id}/activate</c>
/// </summary>
[Collection("OrganizationDatabase")]
public class ProductsControllerTests : IAsyncLifetime
{
    private const string LocalDbFallback =
        "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb;Integrated Security=true;TrustServerCertificate=True;";

    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private Respawner? _respawner;
    private bool _sqlServerAvailable;

    public ProductsControllerTests()
    {
        _connectionString = DatabaseResetHelper.ResolveConnectionString() ?? LocalDbFallback;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
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
                SchemasToInclude = ["product", "organization", "identity"],
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
    public async Task Unauthenticated_requests_to_products_endpoints_return_401_Unauthorized()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var listResponse = await client.GetAsync("/api/v1/products");
        listResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var activeResponse = await client.GetAsync("/api/v1/products/active");
        activeResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var getByIdResponse = await client.GetAsync($"/api/v1/products/{Guid.NewGuid()}");
        getByIdResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var createResponse = await client.PostAsJsonAsync("/api/v1/products", new
        {
            code = "MYHOSPITAL",
            name = "MyHospital",
            description = "HIS",
            ownerPersonId = Guid.NewGuid()
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/products/{Guid.NewGuid()}", new
        {
            name = "Updated",
            description = "Updated"
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var assignOwnerResponse = await client.PutAsJsonAsync($"/api/v1/products/{Guid.NewGuid()}/owner", new
        {
            newOwnerPersonId = Guid.NewGuid()
        });
        assignOwnerResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var deactivateResponse = await client.PostAsync($"/api/v1/products/{Guid.NewGuid()}/deactivate", null);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Product_catalog_listing_creation_update_owner_assignment_and_status_toggle_succeed_end_to_end()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        var (client, owner1Id, owner2Id) = await CreateAuthenticatedClientAndSeedPersonsAsync();

        // 1. Verify active persons selector endpoint (OrganizationQueryService.ListActivePersons)
        var personsResponse = await client.GetAsync("/api/v1/organization/persons/active");
        personsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var activePersons = await personsResponse.Content.ReadFromJsonAsync<JsonElement>();
        activePersons.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
        var personIds = activePersons.EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToList();
        personIds.Should().Contain(owner1Id);
        personIds.Should().Contain(owner2Id);

        // 2. Create two products via POST /api/v1/products
        var create1Response = await client.PostAsJsonAsync("/api/v1/products", new
        {
            code = "MYHOSPITAL",
            name = "MyHospital",
            description = "Hospital Information System",
            ownerPersonId = owner1Id
        });
        create1Response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created1 = await create1Response.Content.ReadFromJsonAsync<JsonElement>();
        var product1Id = created1.GetProperty("id").GetGuid();
        product1Id.Should().NotBeEmpty();
        created1.GetProperty("code").GetString().Should().Be("MYHOSPITAL");
        created1.GetProperty("name").GetString().Should().Be("MyHospital");
        created1.GetProperty("ownerPersonId").GetGuid().Should().Be(owner1Id);
        created1.GetProperty("ownerName").GetString().Should().Be("Budi Santoso");
        created1.GetProperty("status").GetString().Should().Be("ACTIVE");

        var create2Response = await client.PostAsJsonAsync("/api/v1/products", new
        {
            code = "PENAEL",
            name = "PenaEl",
            description = "Electronic Medical Record",
            ownerPersonId = owner1Id
        });
        create2Response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created2 = await create2Response.Content.ReadFromJsonAsync<JsonElement>();
        var product2Id = created2.GetProperty("id").GetGuid();

        // 3. List all products via GET /api/v1/products
        var listAllResponse = await client.GetAsync("/api/v1/products");
        listAllResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var allProducts = await listAllResponse.Content.ReadFromJsonAsync<JsonElement>();
        allProducts.GetArrayLength().Should().Be(2);

        // 4. Get product detail via GET /api/v1/products/{id}
        var detailResponse = await client.GetAsync($"/api/v1/products/{product1Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("id").GetGuid().Should().Be(product1Id);
        detail.GetProperty("code").GetString().Should().Be("MYHOSPITAL");
        detail.GetProperty("name").GetString().Should().Be("MyHospital");

        // 5. Update product attributes via PUT /api/v1/products/{id}
        var updateResponse = await client.PutAsJsonAsync($"/api/v1/products/{product1Id}", new
        {
            name = "MyHospital Enterprise",
            description = "Integrated Hospital Information System v2"
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("name").GetString().Should().Be("MyHospital Enterprise");
        updated.GetProperty("description").GetString().Should().Be("Integrated Hospital Information System v2");

        // 6. Assign new product owner via PUT /api/v1/products/{id}/owner
        var assignOwnerResponse = await client.PutAsJsonAsync($"/api/v1/products/{product1Id}/owner", new
        {
            newOwnerPersonId = owner2Id
        });
        assignOwnerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reassigned = await assignOwnerResponse.Content.ReadFromJsonAsync<JsonElement>();
        reassigned.GetProperty("ownerPersonId").GetGuid().Should().Be(owner2Id);
        reassigned.GetProperty("ownerName").GetString().Should().Be("Rina Wijaya");

        // 7. Deactivate product2 via POST /api/v1/products/{id}/deactivate
        var deactivateResponse = await client.PostAsync($"/api/v1/products/{product2Id}/deactivate", null);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var deactivated = await deactivateResponse.Content.ReadFromJsonAsync<JsonElement>();
        deactivated.GetProperty("status").GetString().Should().Be("INACTIVE");

        // 8. List active products via GET /api/v1/products/active excludes deactivated product2
        var activeOnlyResponse = await client.GetAsync("/api/v1/products/active");
        activeOnlyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var activeProducts = await activeOnlyResponse.Content.ReadFromJsonAsync<JsonElement>();
        var activeProductIds = activeProducts.EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToList();
        activeProductIds.Should().Contain(product1Id);
        activeProductIds.Should().NotContain(product2Id);

        // 9. Reactivate product2 via POST /api/v1/products/{id}/activate
        var activateResponse = await client.PostAsync($"/api/v1/products/{product2Id}/activate", null);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reactivated = await activateResponse.Content.ReadFromJsonAsync<JsonElement>();
        reactivated.GetProperty("status").GetString().Should().Be("ACTIVE");
    }

    [Fact]
    public async Task CreateProduct_with_duplicate_code_or_invalid_owner_returns_problem_details()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        var (client, owner1Id, _) = await CreateAuthenticatedClientAndSeedPersonsAsync();

        var firstResponse = await client.PostAsJsonAsync("/api/v1/products", new
        {
            code = "BTRADE3",
            name = "BTrade3",
            description = "Trading System",
            ownerPersonId = owner1Id
        });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Duplicate code returns 400 Bad Request ProblemDetails
        var duplicateResponse = await client.PostAsJsonAsync("/api/v1/products", new
        {
            code = "BTRADE3",
            name = "BTrade3 Duplicate",
            description = "Duplicate",
            ownerPersonId = owner1Id
        });
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        duplicateResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        // Nonexistent owner returns 404 Not Found ProblemDetails
        var unknownOwnerResponse = await client.PostAsJsonAsync("/api/v1/products", new
        {
            code = "JETSET",
            name = "Jetset",
            description = "Aviation Suite",
            ownerPersonId = Guid.NewGuid()
        });
        unknownOwnerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknownOwnerResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    private async Task<(HttpClient Client, Guid Owner1Id, Guid Owner2Id)> CreateAuthenticatedClientAndSeedPersonsAsync()
    {
        Guid owner1Id;
        Guid owner2Id;
        string sessionToken;

        using (var scope = _factory!.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var userAccountRepo = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

            var owner1 = await mediator.Send(new CreatePersonCommand("Budi", "Santoso", "budi.santoso@cakra.id"));
            var owner2 = await mediator.Send(new CreatePersonCommand("Rina", "Wijaya", "rina.wijaya@cakra.id"));
            owner1Id = owner1.Id;
            owner2Id = owner2.Id;

            var user = new UserAccount
            {
                Id = Guid.NewGuid(),
                PersonId = owner1Id,
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

        return (client, owner1Id, owner2Id);
    }
}
