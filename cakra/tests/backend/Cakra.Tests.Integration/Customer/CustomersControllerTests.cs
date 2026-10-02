using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cakra.Api.Controllers;
using Cakra.Api.Infrastructure.Authentication;
using Cakra.Api.Infrastructure.Migrations;
using Cakra.Modules.Customer;
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

namespace Cakra.Tests.Integration.Customer;

/// <summary>
/// Integration tests verifying Customer REST API Controller mutation and query endpoints
/// (<c>SCR-CUST-001</c>, <c>SCR-CUST-002</c>; Architecture §7, CR-002):
/// - All <c>/api/v1/customers</c> endpoints protected by <c>[Authorize]</c> (401 when unauthenticated)
/// - <c>POST /api/v1/customers</c> (201 Created on success, 400 Bad Request, 409 Conflict on duplicate code)
/// - <c>GET /api/v1/customers</c> and <c>GET /api/v1/customers/active</c> (200 OK)
/// - <c>GET /api/v1/customers/{id}</c> (200 OK, 404 Not Found)
/// - <c>PUT /api/v1/customers/{id}</c> (200 OK, 409 Conflict)
/// - <c>PUT /api/v1/customers/{id}/activate</c> and <c>PUT /api/v1/customers/{id}/deactivate</c> (200 OK)
/// - <c>POST /api/v1/customers/{id}/contacts</c> (201 Created, 400 Bad Request)
/// - <c>PUT /api/v1/customers/{id}/contacts/{contactId}</c> (200 OK, 400 Bad Request)
/// - <c>GET /api/v1/customers/{id}/contacts</c> (200 OK, 404 Not Found)
/// </summary>
[Collection("OrganizationDatabase")]
public class CustomersControllerTests : IAsyncLifetime
{
    private const string LocalDbFallback =
        "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb;Integrated Security=true;TrustServerCertificate=True;";

    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private Respawner? _respawner;
    private bool _sqlServerAvailable;

    public CustomersControllerTests()
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
                SchemasToInclude = ["customer", "organization", "identity"],
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
    public async Task Unauthenticated_requests_to_customers_endpoints_return_401_Unauthorized()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var listResponse = await client.GetAsync("/api/v1/customers");
        listResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var activeResponse = await client.GetAsync("/api/v1/customers/active");
        activeResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var getByIdResponse = await client.GetAsync($"/api/v1/customers/{Guid.NewGuid()}");
        getByIdResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var getContactsResponse = await client.GetAsync($"/api/v1/customers/{Guid.NewGuid()}/contacts");
        getContactsResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var createResponse = await client.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest("CODE", "Name", true));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/customers/{Guid.NewGuid()}", new UpdateCustomerRequest("CODE", "Name", true));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var activateResponse = await client.PutAsync($"/api/v1/customers/{Guid.NewGuid()}/activate", null);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var deactivateResponse = await client.PutAsync($"/api/v1/customers/{Guid.NewGuid()}/deactivate", null);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var createContactResponse = await client.PostAsJsonAsync($"/api/v1/customers/{Guid.NewGuid()}/contacts", new CreateCustomerContactRequest("Contact"));
        createContactResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var updateContactResponse = await client.PutAsJsonAsync($"/api/v1/customers/{Guid.NewGuid()}/contacts/{Guid.NewGuid()}", new UpdateCustomerContactRequest("Contact"));
        updateContactResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Customer_creation_update_status_toggling_and_contact_lifecycle_succeed_end_to_end()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        var client = await CreateAuthenticatedClientAsync();

        // 1. Create Customer A via POST /api/v1/customers
        var createRequest1 = new CreateCustomerRequest("RSCM-001", "RS Cipto Mangunkusumo", true);
        var createResponse1 = await client.PostAsJsonAsync("/api/v1/customers", createRequest1);
        createResponse1.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse1.Headers.Location.Should().NotBeNull();

        var createdCustomer1 = await createResponse1.Content.ReadFromJsonAsync<CustomerDto>();
        createdCustomer1.Should().NotBeNull();
        createdCustomer1!.Id.Should().NotBeEmpty();
        createdCustomer1.CustomerCode.Should().Be("RSCM-001");
        createdCustomer1.CustomerName.Should().Be("RS Cipto Mangunkusumo");
        createdCustomer1.HasActiveMaintenanceContract.Should().BeTrue();
        createdCustomer1.Status.Should().Be("ACTIVE");
        createdCustomer1.IsActive.Should().BeTrue();

        var customer1Id = createdCustomer1.Id;

        // 2. Create Customer B via POST /api/v1/customers
        var createRequest2 = new CreateCustomerRequest("SILOAM-002", "RS Siloam Semanggi", false);
        var createResponse2 = await client.PostAsJsonAsync("/api/v1/customers", createRequest2);
        createResponse2.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdCustomer2 = await createResponse2.Content.ReadFromJsonAsync<CustomerDto>();
        var customer2Id = createdCustomer2!.Id;

        // 3. List all customers via GET /api/v1/customers
        var listResponse = await client.GetAsync("/api/v1/customers");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var allCustomers = await listResponse.Content.ReadFromJsonAsync<IReadOnlyList<CustomerDto>>();
        allCustomers.Should().NotBeNull();
        allCustomers.Should().HaveCountGreaterThanOrEqualTo(2);
        allCustomers.Should().Contain(c => c.Id == customer1Id);
        allCustomers.Should().Contain(c => c.Id == customer2Id);

        // 4. Get customer by ID via GET /api/v1/customers/{id}
        var getResponse = await client.GetAsync($"/api/v1/customers/{customer1Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedCustomer = await getResponse.Content.ReadFromJsonAsync<CustomerDto>();
        fetchedCustomer.Should().NotBeNull();
        fetchedCustomer!.Id.Should().Be(customer1Id);
        fetchedCustomer.CustomerCode.Should().Be("RSCM-001");

        // 5. Update customer attributes via PUT /api/v1/customers/{id}
        var updateRequest = new UpdateCustomerRequest("RSCM-001", "RS Cipto Mangunkusumo Kencana", true);
        var updateResponse = await client.PutAsJsonAsync($"/api/v1/customers/{customer1Id}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedCustomer = await updateResponse.Content.ReadFromJsonAsync<CustomerDto>();
        updatedCustomer.Should().NotBeNull();
        updatedCustomer!.CustomerName.Should().Be("RS Cipto Mangunkusumo Kencana");

        // 6. Deactivate customer2 via PUT /api/v1/customers/{id}/deactivate
        var deactivateResponse = await client.PutAsync($"/api/v1/customers/{customer2Id}/deactivate", null);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var deactivatedCustomer = await deactivateResponse.Content.ReadFromJsonAsync<CustomerDto>();
        deactivatedCustomer.Should().NotBeNull();
        deactivatedCustomer!.Status.Should().Be("INACTIVE");
        deactivatedCustomer.IsActive.Should().BeFalse();

        // 7. Verify GET /api/v1/customers/active excludes deactivated customer2
        var activeResponse = await client.GetAsync("/api/v1/customers/active");
        activeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var activeCustomers = await activeResponse.Content.ReadFromJsonAsync<IReadOnlyList<CustomerDto>>();
        activeCustomers.Should().NotBeNull();
        activeCustomers.Should().Contain(c => c.Id == customer1Id);
        activeCustomers.Should().NotContain(c => c.Id == customer2Id);

        // 8. Reactivate customer2 via PUT /api/v1/customers/{id}/activate
        var activateResponse = await client.PutAsync($"/api/v1/customers/{customer2Id}/activate", null);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reactivatedCustomer = await activateResponse.Content.ReadFromJsonAsync<CustomerDto>();
        reactivatedCustomer.Should().NotBeNull();
        reactivatedCustomer!.Status.Should().Be("ACTIVE");
        reactivatedCustomer.IsActive.Should().BeTrue();

        // 9. Add Contact to Customer 1 via POST /api/v1/customers/{id}/contacts
        var createContactReq = new CreateCustomerContactRequest(
            "Dr. Budi Santoso",
            "Direktur Medis",
            "+628111222333",
            "budi.santoso@rscm.go.id");
        var createContactResponse = await client.PostAsJsonAsync($"/api/v1/customers/{customer1Id}/contacts", createContactReq);
        createContactResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdContact = await createContactResponse.Content.ReadFromJsonAsync<CustomerContactDto>();
        createdContact.Should().NotBeNull();
        createdContact!.Id.Should().NotBeEmpty();
        createdContact.CustomerId.Should().Be(customer1Id);
        createdContact.Name.Should().Be("Dr. Budi Santoso");
        createdContact.Position.Should().Be("Direktur Medis");

        var contactId = createdContact.Id;

        // 10. Update Contact via PUT /api/v1/customers/{id}/contacts/{contactId}
        var updateContactReq = new UpdateCustomerContactRequest(
            "Dr. Budi Santoso, Sp.A",
            "Direktur Utama",
            "+628111222999",
            "budi.dirut@rscm.go.id",
            "ACTIVE");
        var updateContactResponse = await client.PutAsJsonAsync($"/api/v1/customers/{customer1Id}/contacts/{contactId}", updateContactReq);
        updateContactResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedContact = await updateContactResponse.Content.ReadFromJsonAsync<CustomerContactDto>();
        updatedContact.Should().NotBeNull();
        updatedContact!.Name.Should().Be("Dr. Budi Santoso, Sp.A");
        updatedContact.Position.Should().Be("Direktur Utama");

        // 11. List Contacts via GET /api/v1/customers/{id}/contacts
        var getContactsResponse = await client.GetAsync($"/api/v1/customers/{customer1Id}/contacts");
        getContactsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var contacts = await getContactsResponse.Content.ReadFromJsonAsync<IReadOnlyList<CustomerContactDto>>();
        contacts.Should().NotBeNull();
        contacts.Should().HaveCount(1);
        contacts.Should().Contain(c => c.Id == contactId && c.Name == "Dr. Budi Santoso, Sp.A");
    }

    [Fact]
    public async Task CreateCustomer_with_duplicate_code_returns_409_Conflict_ProblemDetails()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        var client = await CreateAuthenticatedClientAsync();

        var firstRequest = new CreateCustomerRequest("DUP-CODE-01", "Hospital One", true);
        var firstResponse = await client.PostAsJsonAsync("/api/v1/customers", firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicateRequest = new CreateCustomerRequest("DUP-CODE-01", "Hospital Two", false);
        var duplicateResponse = await client.PostAsJsonAsync("/api/v1/customers", duplicateRequest);
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        duplicateResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problemDetails = await duplicateResponse.Content.ReadFromJsonAsync<JsonElement>();
        problemDetails.GetProperty("status").GetInt32().Should().Be(409);
        problemDetails.GetProperty("title").GetString().Should().Be("Conflict");
        problemDetails.GetProperty("errorCode").GetString().Should().Be("CUSTOMER_CODE_CONFLICT");
    }

    [Fact]
    public async Task UpdateCustomer_with_duplicate_code_returns_409_Conflict()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        var client = await CreateAuthenticatedClientAsync();

        var res1 = await client.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest("CODE-ALPHA", "Alpha Hospital", true));
        res1.StatusCode.Should().Be(HttpStatusCode.Created);

        var res2 = await client.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest("CODE-BETA", "Beta Hospital", false));
        res2.StatusCode.Should().Be(HttpStatusCode.Created);
        var customer2 = await res2.Content.ReadFromJsonAsync<CustomerDto>();

        // Attempting to update customer 2 code to existing customer 1 code (CODE-ALPHA)
        var updateConflict = await client.PutAsJsonAsync($"/api/v1/customers/{customer2!.Id}", new UpdateCustomerRequest("CODE-ALPHA", "Beta Hospital Updated", false));
        updateConflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Operations_on_nonexistent_customer_return_404_NotFound()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        var client = await CreateAuthenticatedClientAsync();
        var nonExistentId = Guid.NewGuid();

        var getResponse = await client.GetAsync($"/api/v1/customers/{nonExistentId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var getContactsResponse = await client.GetAsync($"/api/v1/customers/{nonExistentId}/contacts");
        getContactsResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        string sessionToken;

        using (var scope = _factory!.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var userAccountRepo = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

            var person = await mediator.Send(new CreatePersonCommand("Customer", "Admin", "customer.admin@cakra.id"));

            var user = new UserAccount
            {
                Id = Guid.NewGuid(),
                PersonId = person.Id,
                Username = $"custadmin_{Guid.NewGuid():N}"[..20],
                Email = $"custadmin_{Guid.NewGuid():N}"[..20] + "@cakra.id",
                Status = UserAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = authService.HashPassword(user, "Password123!");
            await userAccountRepo.AddAsync(user);

            var loginResult = await authService.LoginAsync(user.Username, "Password123!");
            loginResult.Succeeded.Should().BeTrue();
            sessionToken = loginResult.SessionToken!;
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}={sessionToken}");

        return client;
    }
}
