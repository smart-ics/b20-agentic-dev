using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cakra.Api.Infrastructure.Authentication;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Organization;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cakra.Tests.Integration.Identity;

/// <summary>
/// Integration tests verifying <see cref="Cakra.Api.Controllers.AuthController"/> (P2-S11, P3-S05 — SCR-AUTH-001, CR-009, FEAT-USR-002):
/// - POST /api/v1/auth/login success: sets secure, HttpOnly, SameSite=Strict session cookie and returns user profile
/// - POST /api/v1/auth/login failure (invalid credentials): returns 401 RFC 7807 ProblemDetails with INVALID_CREDENTIALS
/// - POST /api/v1/auth/login failure (account locked): returns 403 RFC 7807 ProblemDetails with ACCOUNT_LOCKED
/// - POST /api/v1/auth/login failure (account pending): returns 400 RFC 7807 ProblemDetails with ACCOUNT_PENDING_APPROVAL
/// - POST /api/v1/auth/login failure (repeated bad passwords lock account on 5th failure)
/// - POST /api/v1/auth/register success: returns 201 Created and creates account with Pending status and PersonId = Guid.Empty
/// - POST /api/v1/auth/register failure (duplicate username/email): returns 409 Conflict ProblemDetails
/// - POST /api/v1/auth/register failure (validation errors): returns 400 Bad Request ProblemDetails
/// - GET /api/v1/auth/me: returns 200 with UserId, PersonId, Roles when authenticated; 401 when unauthenticated
/// - POST /api/v1/auth/logout: revokes server-side session, clears session cookie, invalidates subsequent requests
/// </summary>
public class AuthControllerTests : IntegrationTestBase
{
    [Fact]
    public async Task Login_with_valid_credentials_returns_200_and_sets_secure_httpOnly_sameSite_strict_cookie()
    {
        var harness = CreateTestHarness();
        var user = harness.SeedUser("andi.wijaya", "andi@cakra.id", "CorrectHorseBatteryStaple!1", roles: ["Implementator", "Programmer"]);

        var response = await harness.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "andi.wijaya",
            password = "CorrectHorseBatteryStaple!1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify Set-Cookie header contains Cakra.Session with HttpOnly, SameSite=Strict, Secure
        response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders).Should().BeTrue();
        var sessionCookieHeader = setCookieHeaders!.FirstOrDefault(h => h.StartsWith($"{CakraAuthenticationDefaults.CookieName}=", StringComparison.Ordinal));
        sessionCookieHeader.Should().NotBeNull("login must issue the Cakra.Session cookie");
        sessionCookieHeader!.ToLowerInvariant().Should().Contain("httponly");
        sessionCookieHeader.ToLowerInvariant().Should().Contain("samesite=strict");
        sessionCookieHeader.ToLowerInvariant().Should().Contain("secure");

        // Verify JSON response body
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("userId").GetGuid().Should().Be(user.Id);
        body.GetProperty("personId").GetGuid().Should().Be(user.PersonId);
        var roles = body.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToList();
        roles.Should().BeEquivalentTo(["Implementator", "Programmer"]);
    }

    [Fact]
    public async Task Login_with_invalid_credentials_returns_401_problem_details_with_INVALID_CREDENTIALS()
    {
        var harness = CreateTestHarness();
        harness.SeedUser("budi.santoso", "budi@cakra.id", "ValidPass#2026");

        var response = await harness.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "budi.santoso",
            password = "WrongPassword!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        response.Headers.Contains("Set-Cookie").Should().BeFalse("failed login must not issue a session cookie");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(401);
        problem.GetProperty("title").GetString().Should().Be("Invalid Credentials");
        problem.GetProperty("errorCode").GetString().Should().Be("INVALID_CREDENTIALS");
        problem.GetProperty("detail").GetString().Should().Contain("Invalid username or password");
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_with_nonexistent_username_returns_401_problem_details_with_INVALID_CREDENTIALS()
    {
        var harness = CreateTestHarness();

        var response = await harness.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "unknown.user",
            password = "SomePassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(401);
        problem.GetProperty("title").GetString().Should().Be("Invalid Credentials");
        problem.GetProperty("errorCode").GetString().Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_with_locked_account_returns_distinct_403_problem_details_with_ACCOUNT_LOCKED()
    {
        var harness = CreateTestHarness();
        harness.SeedUser("locked.user", "locked@cakra.id", "ValidPass#2026", status: UserAccountStatus.Locked);

        var response = await harness.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "locked.user",
            password = "ValidPass#2026"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        response.Headers.Contains("Set-Cookie").Should().BeFalse("locked account must not issue a session cookie");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(403);
        problem.GetProperty("title").GetString().Should().Be("Account Locked");
        problem.GetProperty("errorCode").GetString().Should().Be("ACCOUNT_LOCKED");
        problem.GetProperty("detail").GetString().Should().ContainEquivalentOf("locked");
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_with_pending_account_returns_distinct_400_problem_details_with_ACCOUNT_PENDING_APPROVAL()
    {
        var harness = CreateTestHarness();
        harness.SeedUser("pending.user", "pending@cakra.id", "ValidPass#2026", status: UserAccountStatus.Pending);

        var response = await harness.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "pending.user",
            password = "ValidPass#2026"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        response.Headers.Contains("Set-Cookie").Should().BeFalse("pending account must not issue a session cookie");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(400);
        problem.GetProperty("title").GetString().Should().Be("Account Pending Approval");
        problem.GetProperty("errorCode").GetString().Should().Be("ACCOUNT_PENDING_APPROVAL");
        problem.GetProperty("code").GetString().Should().Be("ACCOUNT_PENDING_APPROVAL");
        problem.GetProperty("detail").GetString().Should().Contain("pending administrative approval");
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_with_valid_payload_returns_201_and_creates_pending_account()
    {
        var harness = CreateTestHarness();

        var response = await harness.Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            username = "johndoe",
            email = "john.doe@example.com",
            password = "SecurePassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Contains("Set-Cookie").Should().BeFalse("registration must not issue a session cookie");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("userId").GetGuid().Should().NotBeEmpty();
        body.GetProperty("personId").GetGuid().Should().Be(Guid.Empty);
        body.GetProperty("username").GetString().Should().Be("johndoe");
        body.GetProperty("email").GetString().Should().Be("john.doe@example.com");
        body.GetProperty("status").GetString().Should().Be(UserAccountStatus.Pending);

        // Verify account persisted in repository with Pending status
        var user = harness.AccountRepo.Items.Values.FirstOrDefault(u => u.Username == "johndoe");
        user.Should().NotBeNull();
        user!.Status.Should().Be(UserAccountStatus.Pending);
        user.PersonId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task Register_with_duplicate_username_returns_409_conflict_problem_details()
    {
        var harness = CreateTestHarness();
        harness.SeedUser("existinguser", "existing@example.com", "Password123!");

        var response = await harness.Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            username = "existinguser",
            email = "newemail@example.com",
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(409);
        problem.GetProperty("title").GetString().Should().Be("Conflict");
        problem.GetProperty("errorCode").GetString().Should().Be("CONFLICT");
        problem.GetProperty("detail").GetString().Should().Contain("Username is already taken.");
    }

    [Fact]
    public async Task Register_with_duplicate_email_returns_409_conflict_problem_details()
    {
        var harness = CreateTestHarness();
        harness.SeedUser("firstuser", "duplicate@example.com", "Password123!");

        var response = await harness.Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            username = "seconduser",
            email = "DUPLICATE@example.com",
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(409);
        problem.GetProperty("title").GetString().Should().Be("Conflict");
        problem.GetProperty("errorCode").GetString().Should().Be("CONFLICT");
        problem.GetProperty("detail").GetString().Should().Contain("Email is already registered.");
    }

    [Fact]
    public async Task Register_with_invalid_input_returns_400_bad_request_problem_details()
    {
        var harness = CreateTestHarness();

        var response = await harness.Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            username = "user",
            email = "invalid-email",
            password = "short"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(400);
        problem.GetProperty("errorCode").GetString().Should().Be("VALIDATION_FAILED");
        problem.GetProperty("errors").EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Login_locks_account_after_five_failed_attempts_and_returns_ACCOUNT_LOCKED_on_subsequent_attempt()
    {
        var harness = CreateTestHarness();
        harness.SeedUser("threshold.user", "threshold@cakra.id", "CorrectPassword#1");

        // 5 consecutive wrong password attempts
        for (var i = 0; i < 5; i++)
        {
            var failResponse = await harness.Client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                username = "threshold.user",
                password = "WrongPassword!"
            });
            failResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // 6th attempt (even with correct password) must fail with ACCOUNT_LOCKED
        var lockedResponse = await harness.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "threshold.user",
            password = "CorrectPassword#1"
        });

        lockedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await lockedResponse.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errorCode").GetString().Should().Be("ACCOUNT_LOCKED");
        problem.GetProperty("title").GetString().Should().Be("Account Locked");
    }

    [Fact]
    public async Task Full_session_lifecycle_login_me_logout_and_post_logout_rejection_succeeds()
    {
        var harness = CreateTestHarness();
        var user = harness.SeedUser("siti.rahma", "siti@cakra.id", "SitiSecret#99", roles: ["Management"]);

        // 1. Login
        var loginResponse = await harness.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "siti.rahma",
            password = "SitiSecret#99"
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var cookieValue = ExtractSessionTokenFromSetCookie(loginResponse);
        cookieValue.Should().NotBeNullOrWhiteSpace();

        // 2. Call GET /api/v1/auth/me with session cookie
        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meRequest.Headers.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}={cookieValue}");

        var meResponse = await harness.Client.SendAsync(meRequest);
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meBody = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
        meBody.GetProperty("userId").GetGuid().Should().Be(user.Id);
        meBody.GetProperty("personId").GetGuid().Should().Be(user.PersonId);
        meBody.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().ContainSingle().Which.Should().Be("Management");

        // 3. Call POST /api/v1/auth/logout with session cookie
        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutRequest.Headers.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}={cookieValue}");

        var logoutResponse = await harness.Client.SendAsync(logoutRequest);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify Set-Cookie header clears the Cakra.Session cookie
        logoutResponse.Headers.TryGetValues("Set-Cookie", out var logoutCookies).Should().BeTrue();
        logoutCookies!.Should().Contain(h => h.StartsWith($"{CakraAuthenticationDefaults.CookieName}=;", StringComparison.Ordinal));

        // 4. Subsequent GET /api/v1/auth/me with the revoked session cookie returns 401 Unauthorized
        var postLogoutMeRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        postLogoutMeRequest.Headers.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}={cookieValue}");

        var postLogoutMeResponse = await harness.Client.SendAsync(postLogoutMeRequest);
        postLogoutMeResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_without_cookie_returns_401_unauthorized_problem_details()
    {
        var harness = CreateTestHarness();

        var response = await harness.Client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(401);
        problem.GetProperty("errorCode").GetString().Should().Be("UNAUTHORIZED");
    }

    private static string ExtractSessionTokenFromSetCookie(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("Set-Cookie", out var values).Should().BeTrue();
        var prefix = $"{CakraAuthenticationDefaults.CookieName}=";
        var cookieHeader = values!.First(v => v.StartsWith(prefix, StringComparison.Ordinal));
        var semicolonIndex = cookieHeader.IndexOf(';');
        return semicolonIndex > prefix.Length
            ? cookieHeader.Substring(prefix.Length, semicolonIndex - prefix.Length)
            : cookieHeader.Substring(prefix.Length);
    }

    private AuthTestHarness CreateTestHarness()
    {
        var accountRepo = new InMemoryUserAccountRepository();
        var sessionRepo = new InMemoryUserSessionRepository();
        var orgQueryService = new InMemoryOrganizationQueryService();

        var client = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IUserAccountRepository>(_ => accountRepo);
                services.AddScoped<IUserSessionRepository>(_ => sessionRepo);
                services.AddScoped<IOrganizationQueryService>(_ => orgQueryService);
            });
        }).CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        return new AuthTestHarness(client, accountRepo, orgQueryService);
    }

    private sealed class AuthTestHarness
    {
        private readonly PasswordHasher<UserAccount> _hasher = new();

        public AuthTestHarness(
            HttpClient client,
            InMemoryUserAccountRepository accountRepo,
            InMemoryOrganizationQueryService orgQueryService)
        {
            Client = client;
            AccountRepo = accountRepo;
            OrgQueryService = orgQueryService;
        }

        public HttpClient Client { get; }
        public InMemoryUserAccountRepository AccountRepo { get; }
        public InMemoryOrganizationQueryService OrgQueryService { get; }

        public UserAccount SeedUser(
            string username,
            string email,
            string password,
            string status = UserAccountStatus.Active,
            bool isPersonActive = true,
            string[]? roles = null)
        {
            var user = new UserAccount
            {
                Id = Guid.NewGuid(),
                PersonId = Guid.NewGuid(),
                Username = username,
                Email = email,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = _hasher.HashPassword(user, password);
            AccountRepo.Items[user.Id] = user;

            if (isPersonActive)
            {
                OrgQueryService.ActivePersonIds.Add(user.PersonId);
            }

            if (roles is { Length: > 0 })
            {
                OrgQueryService.PersonRoles[user.PersonId] = roles.ToList();
            }

            return user;
        }
    }

    private sealed class InMemoryOrganizationQueryService : IOrganizationQueryService
    {
        public HashSet<Guid> ActivePersonIds { get; } = new();
        public Dictionary<Guid, List<string>> PersonRoles { get; } = new();

        public Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ActivePersonIds.Contains(personId));

        public Task<IReadOnlyList<string>> GetPersonRolesAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(
                PersonRoles.TryGetValue(personId, out var roles) ? roles : Array.Empty<string>());
    }

    private sealed class InMemoryUserAccountRepository : IUserAccountRepository
    {
        public Dictionary<Guid, UserAccount> Items { get; } = new();

        public Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.TryGetValue(id, out var user) ? user : null);

        public Task<UserAccount?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken = default)
        {
            var user = Items.Values.FirstOrDefault(u =>
                string.Equals(u.Username, usernameOrEmail, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.Email, usernameOrEmail, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(user);
        }

        public Task<UserAccount?> GetByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Values.FirstOrDefault(u => u.PersonId == personId));

        public Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserAccount>>(Items.Values.ToList());

        public Task AddAsync(UserAccount entity, CancellationToken cancellationToken = default)
        {
            if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
            Items[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(UserAccount entity, CancellationToken cancellationToken = default)
        {
            Items[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Items.Remove(id);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryUserSessionRepository : IUserSessionRepository
    {
        public Dictionary<Guid, UserSession> Items { get; } = new();

        public Task<UserSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.TryGetValue(id, out var session) ? session : null);

        public Task<UserSession?> GetByTokenAsync(string sessionToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Values.FirstOrDefault(s => s.SessionToken == sessionToken));

        public Task<IReadOnlyList<UserSession>> GetActiveSessionsByUserIdAsync(Guid userId, DateTime currentUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserSession>>(
                Items.Values.Where(s => s.UserId == userId && !s.IsRevoked && s.ExpiresAt > currentUtc).ToList());

        public Task<IReadOnlyList<UserSession>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserSession>>(Items.Values.ToList());

        public Task AddAsync(UserSession entity, CancellationToken cancellationToken = default)
        {
            if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
            Items[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(UserSession entity, CancellationToken cancellationToken = default)
        {
            Items[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task<bool> RevokeByTokenAsync(string sessionToken, DateTime? revokedAtUtc = null, CancellationToken cancellationToken = default)
        {
            var session = Items.Values.FirstOrDefault(s => s.SessionToken == sessionToken && !s.IsRevoked);
            if (session != null)
            {
                session.Revoke(revokedAtUtc);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Items.Remove(id);
            return Task.CompletedTask;
        }
    }
}
