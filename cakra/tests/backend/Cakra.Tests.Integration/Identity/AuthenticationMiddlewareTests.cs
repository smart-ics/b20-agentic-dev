using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cakra.Api.Infrastructure.Authentication;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Identity.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Cakra.Tests.Integration.Identity;

/// <summary>
/// Integration tests verifying the Authentication Middleware &amp; Security Context Pipeline (P2-S10):
/// - Cookie authentication configuration (HttpOnly, SameSite=Strict, Secure=true) - Architecture §19.5
/// - Rejection of unauthenticated requests with HTTP 401 ProblemDetails on protected endpoints
/// - Rejection of invalid/expired/revoked session cookies with HTTP 401 ProblemDetails
/// - Session validation calling AuthenticationService.ValidateSession and populating CurrentContextProvider
/// - RBAC enforcement rejecting non-Management users with HTTP 403 Forbidden
/// - RBAC enforcement allowing Management users with HTTP 200 OK
/// </summary>
public class AuthenticationMiddlewareTests : IntegrationTestBase
{
    [Fact]
    public void Cookie_authentication_is_registered_with_secure_httpOnly_sameSite_strict()
    {
        using var scope = Factory.Services.CreateScope();
        var optionsMonitor = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        var options = optionsMonitor.Get(CookieAuthenticationDefaults.AuthenticationScheme);

        options.Cookie.Name.Should().Be("Cakra.Session");
        options.Cookie.HttpOnly.Should().BeTrue("Session cookies must be HttpOnly per Architecture §19.5");
        options.Cookie.SameSite.Should().Be(SameSiteMode.Strict, "Session cookies must be SameSite=Strict per Architecture §19.5");
        options.Cookie.SecurePolicy.Should().Be(CookieSecurePolicy.Always, "Session cookies must require Secure transmission per Architecture §19.5");
    }

    [Fact]
    public async Task Unauthenticated_request_to_protected_endpoint_returns_401_unauthorized_problem_details()
    {
        var response = await Client.GetAsync("/api/v1/system/probe/secure");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(401);
        problem.GetProperty("title").GetString().Should().Be("Unauthorized");
        problem.GetProperty("errorCode").GetString().Should().Be("UNAUTHORIZED");
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Request_with_invalid_cookie_returns_401_unauthorized_problem_details()
    {
        var mockAuthService = new StubAuthService();
        var mockAuthzService = new StubAuthzService();

        var testClient = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IAuthenticationService>(_ => mockAuthService);
                services.AddScoped<IAuthorizationService>(_ => mockAuthzService);
            });
        }).CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/probe/secure");
        request.Headers.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}=invalid-or-revoked-token");

        var response = await testClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(401);
        problem.GetProperty("errorCode").GetString().Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Request_with_valid_cookie_calls_ValidateSession_and_populates_CurrentContextProvider()
    {
        var userId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        const string validToken = "valid-session-token-abc-123";

        var mockAuthService = new StubAuthService();
        mockAuthService.RegisterValidSession(validToken, userId, personId);

        var mockAuthzService = new StubAuthzService();
        mockAuthzService.RegisterRoles(personId, "Programmer");

        var testClient = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IAuthenticationService>(_ => mockAuthService);
                services.AddScoped<IAuthorizationService>(_ => mockAuthzService);
            });
        }).CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/probe/secure");
        request.Headers.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}={validToken}");

        var response = await testClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("userId").GetGuid().Should().Be(userId);
        body.GetProperty("personId").GetGuid().Should().Be(personId);

        var roles = body.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToList();
        roles.Should().ContainSingle().Which.Should().Be("Programmer");
    }

    [Fact]
    public async Task Request_with_insufficient_role_is_rejected_with_403_forbidden_problem_details()
    {
        var userId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        const string programmerToken = "programmer-session-token-xyz-789";

        var mockAuthService = new StubAuthService();
        mockAuthService.RegisterValidSession(programmerToken, userId, personId);

        var mockAuthzService = new StubAuthzService();
        mockAuthzService.RegisterRoles(personId, "Programmer"); // Not Management

        var testClient = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IAuthenticationService>(_ => mockAuthService);
                services.AddScoped<IAuthorizationService>(_ => mockAuthzService);
            });
        }).CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/probe/management");
        request.Headers.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}={programmerToken}");

        var response = await testClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(403);
        problem.GetProperty("title").GetString().Should().Be("Forbidden");
        problem.GetProperty("errorCode").GetString().Should().Be("FORBIDDEN");
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Request_with_Management_role_succeeds_with_200_ok()
    {
        var userId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        const string managementToken = "management-session-token-mgt-555";

        var mockAuthService = new StubAuthService();
        mockAuthService.RegisterValidSession(managementToken, userId, personId);

        var mockAuthzService = new StubAuthzService();
        mockAuthzService.RegisterRoles(personId, "Management", "Programmer");

        var testClient = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IAuthenticationService>(_ => mockAuthService);
                services.AddScoped<IAuthorizationService>(_ => mockAuthzService);
            });
        }).CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/probe/management");
        request.Headers.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}={managementToken}");

        var response = await testClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("management-authorized");
        body.GetProperty("userId").GetGuid().Should().Be(userId);
        body.GetProperty("personId").GetGuid().Should().Be(personId);

        var roles = body.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToList();
        roles.Should().Contain("Management");
    }

    private sealed class StubAuthService : IAuthenticationService
    {
        private readonly Dictionary<string, (Guid UserId, Guid PersonId)> _validSessions = new();

        public void RegisterValidSession(string token, Guid userId, Guid personId)
        {
            _validSessions[token] = (userId, personId);
        }

        public Task<SecurityContext> ValidateSessionAsync(string sessionToken, CancellationToken cancellationToken = default)
        {
            if (_validSessions.TryGetValue(sessionToken, out var session))
            {
                return Task.FromResult(SecurityContext.Valid(session.UserId, session.PersonId));
            }

            return Task.FromResult(SecurityContext.Invalid("Invalid session token."));
        }

        public SecurityContext ValidateSession(string sessionToken) =>
            ValidateSessionAsync(sessionToken).GetAwaiter().GetResult();

        public Task<LoginResult> LoginAsync(string usernameOrEmail, string password, ClientInfo? clientInfo = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public LoginResult Login(string usernameOrEmail, string password, ClientInfo? clientInfo = null) =>
            throw new NotImplementedException();

        public Task<bool> LogoutAsync(string sessionToken, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public bool Logout(string sessionToken) =>
            throw new NotImplementedException();

        public string HashPassword(UserAccount user, string password) =>
            throw new NotImplementedException();
    }

    private sealed class StubAuthzService : IAuthorizationService
    {
        private readonly Dictionary<Guid, List<string>> _personRoles = new();

        public void RegisterRoles(Guid personId, params string[] roles)
        {
            if (!_personRoles.TryGetValue(personId, out var list))
            {
                list = new List<string>();
                _personRoles[personId] = list;
            }

            list.AddRange(roles);
        }

        public Task<IReadOnlyList<string>> ResolveRolesAsync(Guid personId, CancellationToken cancellationToken = default)
        {
            if (_personRoles.TryGetValue(personId, out var roles))
            {
                return Task.FromResult<IReadOnlyList<string>>(roles);
            }

            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }
    }
}
