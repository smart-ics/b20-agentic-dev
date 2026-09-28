namespace ICS.Tests.Integration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using ICS.Modules.Identity;
using ICS.Modules.Identity.Application;
using ICS.Modules.Organization.Application;
using ICS.Modules.Organization.Application.DTOs;
using ICS.Modules.Product.Application;
using ICS.Modules.Product.Application.DTOs;
using ICS.Web.Auth;
using ICS.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Integration tests verifying SCR-PRD-001 Product Catalog screen endpoints (/api/v1/products/*)
/// per Architecture §10, §16, §19.4, §19.5, §19.6, §19.8, and P3-S15 completion criteria:
/// 1. All endpoints protected by [Authorize] (returns 401 when unauthenticated).
/// 2. List all products (ListAllProducts) and active products (ListActiveProducts).
/// 3. Get product detail (GetProductById).
/// 4. Create product (CreateProduct).
/// 5. Update product attributes (UpdateProduct).
/// 6. Assign product owner (AssignProductOwner).
/// 7. Activate / Deactivate product status transitions.
/// 8. Product owner selector lists active persons (ListEligibleOwners).
/// </summary>
public class ProductScreenIntegrationTests : IntegrationTestBase
{
    private const string DefaultPassword = "Password123!";

    public ProductScreenIntegrationTests(IcsWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Endpoints_WithoutSessionCookie_ShouldReturn401Unauthorized()
    {
        // Act & Assert
        var listResp = await Client.GetAsync("/api/v1/products");
        listResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var activeResp = await Client.GetAsync("/api/v1/products/active");
        activeResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var ownersResp = await Client.GetAsync("/api/v1/products/owners");
        ownersResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var getResp = await Client.GetAsync($"/api/v1/products/{Guid.NewGuid()}");
        getResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var postResp = await Client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest("CODE", "Name", "Desc", Guid.NewGuid()));
        postResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var putResp = await Client.PutAsJsonAsync($"/api/v1/products/{Guid.NewGuid()}", new UpdateProductRequest("Name", "Desc"));
        putResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var ownerResp = await Client.PutAsJsonAsync($"/api/v1/products/{Guid.NewGuid()}/owner", new AssignProductOwnerRequest { OwnerPersonId = Guid.NewGuid() });
        ownerResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var actResp = await Client.PostAsync($"/api/v1/products/{Guid.NewGuid()}/activate", null);
        actResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var deactResp = await Client.PostAsync($"/api/v1/products/{Guid.NewGuid()}/deactivate", null);
        deactResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CatalogListing_WithAuthenticatedUser_ShouldReturnAllAndActiveProducts()
    {
        // Arrange
        var cookie = await CreateAuthenticatedSessionCookieAsync("catalog.reviewer");

        var (activeProduct, inactiveProduct) = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            var owner = await orgService.CreatePersonAsync("Alice Product Lead", $"alice.{Guid.NewGuid():N}@example.com");

            var prodService = sp.GetRequiredService<IProductService>();
            var active = await prodService.CreateProductAsync($"PRD-ACT-{Guid.NewGuid():N}"[..12], "Active Product", "Desc 1", owner.PersonId);
            var inactive = await prodService.CreateProductAsync($"PRD-INA-{Guid.NewGuid():N}"[..12], "Inactive Product", "Desc 2", owner.PersonId);
            await prodService.DeactivateProductAsync(inactive.ProductId);

            return (active, inactive);
        });

        // Act - List all products
        var allReq = CreateRequest(HttpMethod.Get, "/api/v1/products", cookie);
        var allResp = await Client.SendAsync(allReq);

        // Assert
        allResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var allProducts = await allResp.Content.ReadFromJsonAsync<List<ProductDto>>();
        allProducts.Should().NotBeNull();
        allProducts!.Should().Contain(p => p.ProductId == activeProduct.ProductId);
        allProducts.Should().Contain(p => p.ProductId == inactiveProduct.ProductId);

        // Act - List active products via query param
        var activeParamReq = CreateRequest(HttpMethod.Get, "/api/v1/products?activeOnly=true", cookie);
        var activeParamResp = await Client.SendAsync(activeParamReq);

        activeParamResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var activeProducts1 = await activeParamResp.Content.ReadFromJsonAsync<List<ProductDto>>();
        activeProducts1.Should().NotBeNull();
        activeProducts1!.Should().Contain(p => p.ProductId == activeProduct.ProductId);
        activeProducts1.Should().NotContain(p => p.ProductId == inactiveProduct.ProductId);

        // Act - List active products via dedicated endpoint
        var activeReq = CreateRequest(HttpMethod.Get, "/api/v1/products/active", cookie);
        var activeResp = await Client.SendAsync(activeReq);

        activeResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var activeProducts2 = await activeResp.Content.ReadFromJsonAsync<List<ProductDto>>();
        activeProducts2.Should().NotBeNull();
        activeProducts2!.Should().Contain(p => p.ProductId == activeProduct.ProductId);
        activeProducts2.Should().NotContain(p => p.ProductId == inactiveProduct.ProductId);
    }

    [Fact]
    public async Task CreateProduct_WithValidData_ShouldReturn201Created_AndBeRetrievable()
    {
        // Arrange
        var cookie = await CreateAuthenticatedSessionCookieAsync("product.creator");

        var owner = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.CreatePersonAsync("Bob Product Lead", $"bob.{Guid.NewGuid():N}@example.com");
        });

        var productCode = $"PRD-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var requestPayload = new CreateProductRequest(
            Code: productCode,
            Name: "Integrated Hospital Management",
            Description: "Authoritative health IT system",
            OwnerPersonId: owner.PersonId);

        // Act
        var createReq = CreateRequest(HttpMethod.Post, "/api/v1/products", cookie, requestPayload);
        var createResp = await Client.SendAsync(createReq);

        // Assert
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        createResp.Headers.Location.Should().NotBeNull();

        var created = await createResp.Content.ReadFromJsonAsync<ProductDto>();
        created.Should().NotBeNull();
        created!.ProductId.Should().NotBeEmpty();
        created.Code.Should().Be(productCode);
        created.Name.Should().Be("Integrated Hospital Management");
        created.Description.Should().Be("Authoritative health IT system");
        created.OwnerPersonId.Should().Be(owner.PersonId);
        created.OwnerName.Should().Be(owner.Name);
        created.Status.Should().Be("ACTIVE");

        // Verify retrieval via GET /api/v1/products/{id}
        var getReq = CreateRequest(HttpMethod.Get, $"/api/v1/products/{created.ProductId}", cookie);
        var getResp = await Client.SendAsync(getReq);

        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResp.Content.ReadFromJsonAsync<ProductDto>();
        fetched.Should().NotBeNull();
        fetched!.ProductId.Should().Be(created.ProductId);
        fetched.Code.Should().Be(productCode);
        fetched.OwnerName.Should().Be(owner.Name);
    }

    [Fact]
    public async Task UpdateProduct_WithValidData_ShouldReturn200OK_AndUpdatedDetails()
    {
        // Arrange
        var cookie = await CreateAuthenticatedSessionCookieAsync("product.updater");

        var product = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            var owner = await orgService.CreatePersonAsync("Charlie Lead", $"charlie.{Guid.NewGuid():N}@example.com");

            var prodService = sp.GetRequiredService<IProductService>();
            return await prodService.CreateProductAsync($"PRD-UPD-{Guid.NewGuid():N}"[..10], "Initial Name", "Initial Desc", owner.PersonId);
        });

        var updatePayload = new UpdateProductRequest(
            Name: "Updated Product Name",
            Description: "Updated product description text");

        // Act
        var updateReq = CreateRequest(HttpMethod.Put, $"/api/v1/products/{product.ProductId}", cookie, updatePayload);
        var updateResp = await Client.SendAsync(updateReq);

        // Assert
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResp.Content.ReadFromJsonAsync<ProductDto>();
        updated.Should().NotBeNull();
        updated!.ProductId.Should().Be(product.ProductId);
        updated.Name.Should().Be("Updated Product Name");
        updated.Description.Should().Be("Updated product description text");
    }

    [Fact]
    public async Task AssignProductOwner_WithValidOwner_ShouldUpdateOwnerSuccessfully()
    {
        // Arrange
        var cookie = await CreateAuthenticatedSessionCookieAsync("product.owner.reassigner");

        var (product, newOwner) = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            var initialOwner = await orgService.CreatePersonAsync("Owner One", $"owner1.{Guid.NewGuid():N}@example.com");
            var secondOwner = await orgService.CreatePersonAsync("Owner Two", $"owner2.{Guid.NewGuid():N}@example.com");

            var prodService = sp.GetRequiredService<IProductService>();
            var p = await prodService.CreateProductAsync($"PRD-OWN-{Guid.NewGuid():N}"[..10], "Owner Test Product", "Desc", initialOwner.PersonId);
            return (p, secondOwner);
        });

        var assignPayload = new AssignProductOwnerRequest
        {
            NewOwnerPersonId = newOwner.PersonId
        };

        // Act
        var assignReq = CreateRequest(HttpMethod.Put, $"/api/v1/products/{product.ProductId}/owner", cookie, assignPayload);
        var assignResp = await Client.SendAsync(assignReq);

        // Assert
        assignResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await assignResp.Content.ReadFromJsonAsync<ProductDto>();
        result.Should().NotBeNull();
        result!.ProductId.Should().Be(product.ProductId);
        result.OwnerPersonId.Should().Be(newOwner.PersonId);
        result.OwnerName.Should().Be(newOwner.Name);
    }

    [Fact]
    public async Task StatusTransitions_DeactivateAndActivate_ShouldTransitionCorrectly()
    {
        // Arrange
        var cookie = await CreateAuthenticatedSessionCookieAsync("product.lifecycle");

        var product = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            var owner = await orgService.CreatePersonAsync("Lifecycle Lead", $"lead.{Guid.NewGuid():N}@example.com");

            var prodService = sp.GetRequiredService<IProductService>();
            return await prodService.CreateProductAsync($"PRD-LIF-{Guid.NewGuid():N}"[..10], "Lifecycle Product", "Desc", owner.PersonId);
        });

        // Act 1 - Deactivate
        var deactReq = CreateRequest(HttpMethod.Post, $"/api/v1/products/{product.ProductId}/deactivate", cookie);
        var deactResp = await Client.SendAsync(deactReq);

        // Assert 1
        deactResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var deactResult = await deactResp.Content.ReadFromJsonAsync<ProductDto>();
        deactResult.Should().NotBeNull();
        deactResult!.Status.Should().Be("INACTIVE");

        // Act 2 - Activate
        var actReq = CreateRequest(HttpMethod.Post, $"/api/v1/products/{product.ProductId}/activate", cookie);
        var actResp = await Client.SendAsync(actReq);

        // Assert 2
        actResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var actResult = await actResp.Content.ReadFromJsonAsync<ProductDto>();
        actResult.Should().NotBeNull();
        actResult!.Status.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task ListEligibleOwners_ShouldReturnActivePersonsForDropdown()
    {
        // Arrange
        var cookie = await CreateAuthenticatedSessionCookieAsync("product.dropdown.tester");

        var seededPerson = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.CreatePersonAsync("Dr. Dropdown Owner", $"dropdown.{Guid.NewGuid():N}@example.com");
        });

        // Act
        var req = CreateRequest(HttpMethod.Get, "/api/v1/products/owners", cookie);
        var resp = await Client.SendAsync(req);

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var persons = await resp.Content.ReadFromJsonAsync<List<PersonDto>>();
        persons.Should().NotBeNull();
        persons!.Should().Contain(p => p.PersonId == seededPerson.PersonId && p.Name == seededPerson.Name);
    }

    [Fact]
    public async Task GetProductById_WhenNotFound_ShouldReturn404ProblemDetails()
    {
        // Arrange
        var cookie = await CreateAuthenticatedSessionCookieAsync("product.notfound.tester");
        var nonExistentId = Guid.NewGuid();

        // Act
        var req = CreateRequest(HttpMethod.Get, $"/api/v1/products/{nonExistentId}", cookie);
        var resp = await Client.SendAsync(req);

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await resp.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(404);
        problem.Extensions.Should().ContainKey("errorCode");
        problem.Extensions["errorCode"]?.ToString().Should().Be("PRODUCT_NOT_FOUND");
    }

    private async Task<string> CreateAuthenticatedSessionCookieAsync(string username)
    {
        await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            await seeder.CreateUserAsync(username, $"{username}@smart-ics.internal", DefaultPassword);
        });

        var loginResult = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.LoginAsync(username, DefaultPassword);
        });

        return $"{AuthenticationServiceExtensions.DefaultCookieName}={loginResult.SessionToken}";
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, string cookieHeader, object? payload = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("Cookie", cookieHeader);
        if (payload != null)
        {
            request.Content = JsonContent.Create(payload);
        }
        return request;
    }
}
