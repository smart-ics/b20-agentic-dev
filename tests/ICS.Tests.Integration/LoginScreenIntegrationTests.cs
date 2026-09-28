namespace ICS.Tests.Integration;

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using ICS.Modules.Identity;
using ICS.Modules.Identity.Domain;
using ICS.Modules.Identity.Persistence;
using ICS.Modules.Organization;
using ICS.Web.Auth;
using ICS.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Integration tests verifying SCR-AUTH-001 authentication endpoints (/api/v1/auth/login, /api/v1/auth/logout, /api/v1/auth/me)
/// per Architecture §14, §19.5, §19.6, and P2-S11 completion criteria:
/// 1. Login success sets secure HttpOnly SameSite=Strict session cookie.
/// 2. Subsequent requests with session cookie authenticate successfully.
/// 3. Login failure scenarios return distinct RFC 7807 ProblemDetails (invalid credentials -> 401, account locked -> 423, validation error -> 400).
/// 4. Logout invalidates session in database and clears the session cookie.
/// 5. /api/v1/auth/me returns authenticated and unauthenticated user context.
/// </summary>
public class LoginScreenIntegrationTests : IntegrationTestBase
{
    private const string DefaultPassword = "Password123!";

    public LoginScreenIntegrationTests(IcsWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturn200AndSetSessionCookie()
    {
        // Arrange
        var username = "engineer.login";
        var user = await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            var created = await seeder.CreateUserAsync(username, "engineer.login@smart-ics.internal", DefaultPassword);

            var orgQuery = sp.GetRequiredService<IOrganizationQueryService>();
            orgQuery.RegisterBootstrapRoles(created.PersonId, new[] { "Implementator" });

            return created;
        });

        var loginPayload = new LoginRequest
        {
            UsernameOrEmail = username,
            Password = DefaultPassword
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", loginPayload);

        // Assert - HTTP 200 OK & payload
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadFromJsonAsync<LoginSuccessResponse>();
        content.Should().NotBeNull();
        content!.UserId.Should().Be(user.UserId);
        content.PersonId.Should().Be(user.PersonId);
        content.Username.Should().Be(username);
        content.Roles.Should().Contain("Implementator");

        // Assert - Set-Cookie header contains secure HttpOnly SameSite=Strict cookie (Architecture §19.5)
        response.Headers.Should().ContainKey("Set-Cookie");
        var setCookieHeaders = response.Headers.GetValues("Set-Cookie").ToList();
        var sessionCookie = setCookieHeaders.FirstOrDefault(c => c.StartsWith($"{AuthenticationServiceExtensions.DefaultCookieName}="));

        sessionCookie.Should().NotBeNull();
        sessionCookie.Should().Contain("httponly", Exactly.Once());
        sessionCookie.Should().Contain("samesite=strict", Exactly.Once());
        sessionCookie.Should().Contain("path=/", Exactly.Once());

        // Extract raw cookie value to verify end-to-end authentication
        var rawCookieVal = sessionCookie!.Split(';')[0]; // e.g. "ICS_SESSION=<token>"

        var protectedReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/protected");
        protectedReq.Headers.Add("Cookie", rawCookieVal);
        var protectedResp = await Client.SendAsync(protectedReq);

        protectedResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var protectedBody = await protectedResp.Content.ReadFromJsonAsync<JsonElement>();
        protectedBody.GetProperty("userId").GetString().Should().Be(user.UserId.ToString());
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldReturn401ProblemDetails_InvalidCredentials()
    {
        // Arrange
        var username = "wrongpass.user";
        await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            return await seeder.CreateUserAsync(username, "wrongpass@smart-ics.internal", DefaultPassword);
        });

        var loginPayload = new LoginRequest
        {
            UsernameOrEmail = username,
            Password = "WrongPassword999!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", loginPayload);

        // Assert - HTTP 401 Unauthorized & RFC 7807 ProblemDetails
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(401);
        problem.Title.Should().Be("Invalid Credentials");
        problem.Detail.Should().Contain("Invalid username or password");
        problem.Instance.Should().Be("/api/v1/auth/login");
        problem.Extensions.Should().ContainKey("errorCode");
        problem.Extensions["errorCode"]?.ToString().Should().Be("INVALID_CREDENTIALS");
        problem.Extensions.Should().ContainKey("traceId");
        problem.Extensions.Should().ContainKey("timestamp");
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ShouldReturn401ProblemDetails_InvalidCredentials()
    {
        // Arrange
        var loginPayload = new LoginRequest
        {
            UsernameOrEmail = "does.not.exist@smart-ics.internal",
            Password = "AnyPassword123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", loginPayload);

        // Assert - HTTP 401 Unauthorized & RFC 7807 ProblemDetails
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(401);
        problem.Title.Should().Be("Invalid Credentials");
        problem.Extensions.Should().ContainKey("errorCode");
        problem.Extensions["errorCode"]?.ToString().Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_WithLockedAccount_ShouldReturn423ProblemDetails_AccountLocked()
    {
        // Arrange
        var username = "locked.user";
        await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            var user = await seeder.CreateUserAsync(username, "locked@smart-ics.internal", DefaultPassword);

            // Lock account explicitly
            user.Lock(DateTime.UtcNow);
            var accountRepo = sp.GetRequiredService<IUserAccountRepository>();
            await accountRepo.UpdateAsync(user);
        });

        var loginPayload = new LoginRequest
        {
            UsernameOrEmail = username,
            Password = DefaultPassword
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", loginPayload);

        // Assert - HTTP 423 Locked & distinct RFC 7807 ProblemDetails
        response.StatusCode.Should().Be(HttpStatusCode.Locked);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(423);
        problem.Title.Should().Be("Account Locked");
        problem.Detail.Should().Contain("locked");
        problem.Extensions.Should().ContainKey("errorCode");
        problem.Extensions["errorCode"]?.ToString().Should().Be("ACCOUNT_LOCKED");
        problem.Extensions.Should().ContainKey("traceId");
        problem.Extensions.Should().ContainKey("timestamp");
    }

    [Fact]
    public async Task Login_WithMissingCredentials_ShouldReturn400BadRequest_ValidationError()
    {
        // Arrange
        var loginPayload = new LoginRequest
        {
            UsernameOrEmail = "",
            Password = ""
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", loginPayload);

        // Assert - HTTP 400 Bad Request & RFC 7807 ProblemDetails
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
        problem.Title.Should().Be("Validation Error");
        problem.Extensions.Should().ContainKey("errorCode");
        problem.Extensions["errorCode"]?.ToString().Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Logout_WithActiveSession_ShouldInvalidateSessionAndClearCookie()
    {
        // Arrange
        var username = "logout.user";
        await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            return await seeder.CreateUserAsync(username, "logout@smart-ics.internal", DefaultPassword);
        });

        var loginResp = await Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            UsernameOrEmail = username,
            Password = DefaultPassword
        });
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var setCookieHeaders = loginResp.Headers.GetValues("Set-Cookie").ToList();
        var sessionCookie = setCookieHeaders.First(c => c.StartsWith($"{AuthenticationServiceExtensions.DefaultCookieName}="));
        var rawCookieVal = sessionCookie.Split(';')[0];
        var token = rawCookieVal.Substring($"{AuthenticationServiceExtensions.DefaultCookieName}=".Length);

        // Act - Call POST /api/v1/auth/logout with session cookie
        var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutReq.Headers.Add("Cookie", rawCookieVal);
        var logoutResp = await Client.SendAsync(logoutReq);

        // Assert - HTTP 200 OK
        logoutResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - Set-Cookie clears the cookie (contains expires in past or deleted cookie)
        logoutResp.Headers.Should().ContainKey("Set-Cookie");
        var clearCookieHeaders = logoutResp.Headers.GetValues("Set-Cookie").ToList();
        var clearCookie = clearCookieHeaders.FirstOrDefault(c => c.StartsWith($"{AuthenticationServiceExtensions.DefaultCookieName}="));
        clearCookie.Should().NotBeNull();

        // Assert - Session marked revoked in database
        await ExecuteInScopeAsync(async sp =>
        {
            var sessionRepo = sp.GetRequiredService<IUserSessionRepository>();
            var session = await sessionRepo.GetByTokenAsync(token);
            session.Should().NotBeNull();
            session!.IsRevoked.Should().BeTrue();
        });

        // Assert - Subsequent request with the same cookie returns 401 Unauthorized
        var protectedReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/protected");
        protectedReq.Headers.Add("Cookie", rawCookieVal);
        var protectedResp = await Client.SendAsync(protectedReq);

        protectedResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_WhenAuthenticated_ShouldReturnProfile()
    {
        // Arrange
        var username = "me.user";
        var user = await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            var created = await seeder.CreateUserAsync(username, "me@smart-ics.internal", DefaultPassword);

            var orgQuery = sp.GetRequiredService<IOrganizationQueryService>();
            orgQuery.RegisterBootstrapRoles(created.PersonId, new[] { "Management" });

            return created;
        });

        var loginResp = await Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            UsernameOrEmail = username,
            Password = DefaultPassword
        });
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessionCookie = loginResp.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith($"{AuthenticationServiceExtensions.DefaultCookieName}="));
        var rawCookieVal = sessionCookie.Split(';')[0];

        // Act
        var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meReq.Headers.Add("Cookie", rawCookieVal);
        var meResp = await Client.SendAsync(meReq);

        // Assert
        meResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await meResp.Content.ReadFromJsonAsync<JsonElement>();
        doc.GetProperty("isAuthenticated").GetBoolean().Should().BeTrue();
        doc.GetProperty("userId").GetString().Should().Be(user.UserId.ToString());
        doc.GetProperty("personId").GetString().Should().Be(user.PersonId.ToString());
        var roles = doc.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToList();
        roles.Should().Contain("Management");
    }

    [Fact]
    public async Task GetCurrentUser_WhenUnauthenticated_ShouldReturnUnauthenticated()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        doc.GetProperty("isAuthenticated").GetBoolean().Should().BeFalse();
    }
}
