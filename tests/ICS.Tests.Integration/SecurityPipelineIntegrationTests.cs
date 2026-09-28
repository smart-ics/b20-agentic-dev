namespace ICS.Tests.Integration;

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using ICS.Core.Auth;
using ICS.Modules.Identity;
using ICS.Modules.Identity.Application;
using ICS.Modules.Identity.Persistence;
using ICS.Modules.Organization;
using ICS.Web.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Integration tests verifying the Authentication Middleware & Security Context Pipeline
/// per Architecture §14, §18, §19.5, and P2-S10 completion criteria:
/// 1. Cookie Authentication configuration (HttpOnly, SameSite=Strict, SecurePolicy, CookieName).
/// 2. Unauthenticated requests to protected endpoints return HTTP 401.
/// 3. Authenticated requests with valid session cookie return HTTP 200 and populate CurrentContextProvider.
/// 4. Requests with invalid or revoked session cookie return HTTP 401.
/// 5. RBAC enforcement rejects non-Management users with HTTP 403.
/// 6. RBAC enforcement grants access to users holding the 'Management' role with HTTP 200.
/// </summary>
public class SecurityPipelineIntegrationTests : IntegrationTestBase
{
    private const string DefaultPassword = "SecurePassword123!";

    public SecurityPipelineIntegrationTests(IcsWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public void CookieAuthentication_ShouldBeConfiguredWithSecureDefaults()
    {
        // Arrange & Act
        using var scope = CreateScope();
        var cookieOptions = scope.ServiceProvider
            .GetRequiredService<IOptionsSnapshot<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        // Assert
        cookieOptions.Should().NotBeNull();
        cookieOptions.Cookie.Name.Should().Be(AuthenticationServiceExtensions.DefaultCookieName);
        cookieOptions.Cookie.HttpOnly.Should().BeTrue();
        cookieOptions.Cookie.SameSite.Should().Be(Microsoft.AspNetCore.Http.SameSiteMode.Strict);
        cookieOptions.Cookie.SecurePolicy.Should().Be(Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest);
    }

    [Fact]
    public async Task UnauthenticatedRequest_ToProtectedMinimalApi_ShouldReturn401Unauthorized()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/system/protected");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(401);
        problem.Extensions.Should().ContainKey("errorCode");
        problem.Extensions["errorCode"]?.ToString().Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task UnauthenticatedRequest_ToProtectedController_ShouldReturn401Unauthorized()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/test/security/protected");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(401);
    }

    [Fact]
    public async Task AuthenticatedRequest_WithValidSessionCookie_ShouldReturn200AndPopulateContext()
    {
        // Arrange
        var username = "engineer.auth";
        var user = await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            return await seeder.CreateUserAsync(username, "engineer.auth@smart-ics.internal", DefaultPassword);
        });

        var loginResult = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.LoginAsync(username, DefaultPassword);
        });

        loginResult.Succeeded.Should().BeTrue();
        loginResult.SessionToken.Should().NotBeNullOrWhiteSpace();

        // Create request with session cookie
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/protected");
        request.Headers.Add("Cookie", $"{AuthenticationServiceExtensions.DefaultCookieName}={loginResult.SessionToken}");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("userId").GetString().Should().Be(user.UserId.ToString());
        content.GetProperty("personId").GetString().Should().Be(user.PersonId.ToString());
        content.GetProperty("isAuthenticated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticatedRequest_ToController_WithValidSessionCookie_ShouldReturn200()
    {
        // Arrange
        var username = "controller.user";
        var user = await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            return await seeder.CreateUserAsync(username, "controller.user@smart-ics.internal", DefaultPassword);
        });

        var loginResult = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.LoginAsync(username, DefaultPassword);
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test/security/protected");
        request.Headers.Add("Cookie", $"{AuthenticationServiceExtensions.DefaultCookieName}={loginResult.SessionToken}");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("userId").GetString().Should().Be(user.UserId.ToString());
        content.GetProperty("personId").GetString().Should().Be(user.PersonId.ToString());
        content.GetProperty("isAuthenticated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Request_WithRevokedSessionCookie_ShouldReturn401Unauthorized()
    {
        // Arrange
        var username = "revoked.user";
        await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            return await seeder.CreateUserAsync(username, "revoked.user@smart-ics.internal", DefaultPassword);
        });

        var loginResult = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.LoginAsync(username, DefaultPassword);
        });

        // Explicitly revoke session
        await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            await authService.LogoutAsync(loginResult.SessionToken!);
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/protected");
        request.Headers.Add("Cookie", $"{AuthenticationServiceExtensions.DefaultCookieName}={loginResult.SessionToken}");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_WithInvalidNonExistentSessionCookie_ShouldReturn401Unauthorized()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/protected");
        request.Headers.Add("Cookie", $"{AuthenticationServiceExtensions.DefaultCookieName}=non-existent-random-token-12345");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rbac_UserWithoutManagementRole_ShouldReturn403Forbidden()
    {
        // Arrange: User with 'Programmer' role attempting to access Management endpoint
        var username = "dev.programmer";
        var user = await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            return await seeder.CreateUserAsync(username, "programmer@smart-ics.internal", DefaultPassword);
        });

        await ExecuteInScopeAsync(async sp =>
        {
            var orgQuery = sp.GetRequiredService<IOrganizationQueryService>();
            orgQuery.RegisterBootstrapRoles(user.PersonId, new[] { "Programmer" });
            await Task.CompletedTask;
        });

        var loginResult = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.LoginAsync(username, DefaultPassword);
        });

        // Act 1: Minimal API [Authorize(Roles = "Management")]
        var reqMinimal = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/management");
        reqMinimal.Headers.Add("Cookie", $"{AuthenticationServiceExtensions.DefaultCookieName}={loginResult.SessionToken}");
        var respMinimal = await Client.SendAsync(reqMinimal);

        // Assert 1
        respMinimal.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        respMinimal.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await respMinimal.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(403);
        problem.Extensions.Should().ContainKey("errorCode");
        problem.Extensions["errorCode"]?.ToString().Should().Be("FORBIDDEN");

        // Act 2: Controller [Authorize(Roles = "Management")]
        var reqCtrl = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test/security/management");
        reqCtrl.Headers.Add("Cookie", $"{AuthenticationServiceExtensions.DefaultCookieName}={loginResult.SessionToken}");
        var respCtrl = await Client.SendAsync(reqCtrl);

        // Assert 2
        respCtrl.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rbac_UserWithManagementRole_ShouldReturn200Ok()
    {
        // Arrange: User with 'Management' role accessing Management endpoint
        var username = "head.management";
        var user = await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            return await seeder.CreateUserAsync(username, "management@smart-ics.internal", DefaultPassword);
        });

        await ExecuteInScopeAsync(async sp =>
        {
            var orgQuery = sp.GetRequiredService<IOrganizationQueryService>();
            orgQuery.RegisterBootstrapRoles(user.PersonId, new[] { "Management", "TeamLead" });
            await Task.CompletedTask;
        });

        var loginResult = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.LoginAsync(username, DefaultPassword);
        });

        // Act 1: Minimal API [Authorize(Roles = "Management")]
        var reqMinimal = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/management");
        reqMinimal.Headers.Add("Cookie", $"{AuthenticationServiceExtensions.DefaultCookieName}={loginResult.SessionToken}");
        var respMinimal = await Client.SendAsync(reqMinimal);

        // Assert 1
        respMinimal.StatusCode.Should().Be(HttpStatusCode.OK);
        var contentMinimal = await respMinimal.Content.ReadFromJsonAsync<JsonElement>();
        contentMinimal.GetProperty("userId").GetString().Should().Be(user.UserId.ToString());
        contentMinimal.GetProperty("personId").GetString().Should().Be(user.PersonId.ToString());
        var rolesMinimal = contentMinimal.GetProperty("roles");
        rolesMinimal.EnumerateArray().Should().Contain(e => e.GetString() == "Management");

        // Act 2: Controller [Authorize(Roles = "Management")]
        var reqCtrl = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test/security/management");
        reqCtrl.Headers.Add("Cookie", $"{AuthenticationServiceExtensions.DefaultCookieName}={loginResult.SessionToken}");
        var respCtrl = await Client.SendAsync(reqCtrl);

        // Assert 2
        respCtrl.StatusCode.Should().Be(HttpStatusCode.OK);
        var contentCtrl = await respCtrl.Content.ReadFromJsonAsync<JsonElement>();
        contentCtrl.GetProperty("userId").GetString().Should().Be(user.UserId.ToString());
    }

    [Fact]
    public async Task AmbientContextEndpoint_ShouldReflectAuthenticatedUserAndRoles()
    {
        // Arrange
        var username = "context.auditor";
        var user = await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            return await seeder.CreateUserAsync(username, "auditor@smart-ics.internal", DefaultPassword);
        });

        await ExecuteInScopeAsync(async sp =>
        {
            var orgQuery = sp.GetRequiredService<IOrganizationQueryService>();
            orgQuery.RegisterBootstrapRoles(user.PersonId, new[] { "Auditor", "Viewer" });
            await Task.CompletedTask;
        });

        var loginResult = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.LoginAsync(username, DefaultPassword);
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/context");
        request.Headers.Add("Cookie", $"{AuthenticationServiceExtensions.DefaultCookieName}={loginResult.SessionToken}");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        var security = content.GetProperty("security");
        security.GetProperty("userId").GetString().Should().Be(user.UserId.ToString());
        security.GetProperty("personId").GetString().Should().Be(user.PersonId.ToString());
        security.GetProperty("isAuthenticated").GetBoolean().Should().BeTrue();

        var roles = security.GetProperty("roles");
        roles.EnumerateArray().Should().Contain(e => e.GetString() == "Auditor");
        roles.EnumerateArray().Should().Contain(e => e.GetString() == "Viewer");
    }

    [Fact]
    public async Task AuthorizationService_DirectEvaluation_ShouldVerifyRolesAccurately()
    {
        // Arrange
        var testPersonId = Guid.NewGuid();
        await ExecuteInScopeAsync(async sp =>
        {
            var orgQuery = sp.GetRequiredService<IOrganizationQueryService>();
            orgQuery.RegisterBootstrapRoles(testPersonId, new[] { "Management", "Module PIC" });
            await Task.CompletedTask;
        });

        // Act & Assert
        await ExecuteInScopeAsync(async sp =>
        {
            var authzService = sp.GetRequiredService<IAuthorizationService>();

            var roles = await authzService.GetRolesForPersonAsync(testPersonId);
            roles.Should().Contain(new[] { "Management", "Module PIC" });

            var hasManagement = await authzService.HasRoleAsync(testPersonId, "Management");
            hasManagement.Should().BeTrue();

            var hasProgrammer = await authzService.HasRoleAsync(testPersonId, "Programmer");
            hasProgrammer.Should().BeFalse();

            var hasAny = await authzService.HasAnyRoleAsync(testPersonId, new[] { "Programmer", "Management" });
            hasAny.Should().BeTrue();

            var hasNone = await authzService.HasAnyRoleAsync(testPersonId, new[] { "Programmer", "Tester" });
            hasNone.Should().BeFalse();
        });
    }
}
