using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cakra.Api.Controllers;
using Cakra.Api.Infrastructure.Authentication;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Identity.Services;
using Cakra.Modules.Organization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using IIdentityAuthService = Cakra.Modules.Identity.Domain.IAuthenticationService;
using IIdentityAuthzService = Cakra.Modules.Identity.Services.IAuthorizationService;

namespace Cakra.Tests.Integration.Identity;

/// <summary>
/// Integration tests verifying <see cref="UsersController"/> (P3-S05; CR-007; Architecture §14):
/// - Strict role-based authorization: restricted to Administrator or Admin roles.
/// - HTTP 401 Unauthorized for unauthenticated calls across all endpoints.
/// - HTTP 403 Forbidden for authenticated users lacking Administrator or Admin roles.
/// - HTTP 200 OK / 201 Created for authenticated Administrator or Admin users.
/// - Conflict handling (HTTP 409 Conflict ProblemDetails) on duplicate username, email, or person association.
/// - Validation error handling (HTTP 400 Bad Request ProblemDetails) on invalid command inputs.
/// </summary>
public class UsersControllerTests : IntegrationTestBase
{
    private readonly InMemoryUserAccountRepository _accountRepo = new();
    private readonly InMemoryOrganizationQueryService _orgQueryService = new();
    private readonly StubAuthService _authService = new();
    private readonly StubAuthzService _authzService = new();

    private HttpClient CreateClientWithRole(params string[] roles)
    {
        var userId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var token = $"session-token-{Guid.NewGuid():N}";

        _authService.RegisterValidSession(token, userId, personId);
        _authzService.RegisterRoles(personId, roles);

        var client = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IIdentityAuthService>(_ => _authService);
                services.AddScoped<IIdentityAuthzService>(_ => _authzService);
                services.AddScoped<IUserAccountRepository>(_ => _accountRepo);
                services.AddScoped<IOrganizationQueryService>(_ => _orgQueryService);
            });
        }).CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        client.DefaultRequestHeaders.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}={token}");
        return client;
    }

    private HttpClient CreateUnauthenticatedClient()
    {
        return Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IIdentityAuthService>(_ => _authService);
                services.AddScoped<IIdentityAuthzService>(_ => _authzService);
                services.AddScoped<IUserAccountRepository>(_ => _accountRepo);
                services.AddScoped<IOrganizationQueryService>(_ => _orgQueryService);
            });
        }).CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Admin")]
    public async Task GetAllUsers_returns_200_ok_for_authorized_roles(string role)
    {
        // Arrange
        var client = CreateClientWithRole(role);
        var personId = Guid.NewGuid();
        _orgQueryService.AddPerson(personId, "Bambang", "Pamungkas");

        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            Username = $"user_{role.ToLowerInvariant()}",
            Email = $"user_{role.ToLowerInvariant()}@cakra.id",
            Status = UserAccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        _accountRepo.Items[user.Id] = user;

        // Act
        var response = await client.GetAsync("/api/v1/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<IReadOnlyList<UserAccountSummaryDto>>();
        users.Should().NotBeNull();
        users.Should().Contain(u => u.UserId == user.Id && u.PersonName == "Bambang Pamungkas");
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Admin")]
    public async Task CreateUser_and_GetUserById_and_UpdateUser_succeed_for_authorized_roles(string role)
    {
        // Arrange
        var client = CreateClientWithRole(role);
        var personId = Guid.NewGuid();
        _orgQueryService.AddPerson(personId, "Ahmad", "Dahlan");

        var createPayload = new CreateUserAccountRequest(
            PersonId: personId,
            Username: $"ahmad_{role.ToLowerInvariant()}",
            Email: $"ahmad_{role.ToLowerInvariant()}@cakra.id",
            Password: "Password123#Secure",
            Status: UserAccountStatus.Active);

        // 1. POST /api/v1/users
        var createResponse = await client.PostAsJsonAsync("/api/v1/users", createPayload);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();

        var createdDto = await createResponse.Content.ReadFromJsonAsync<UserAccountDto>();
        createdDto.Should().NotBeNull();
        createdDto!.PersonId.Should().Be(personId);
        createdDto.PersonName.Should().Be("Ahmad Dahlan");
        createdDto.Username.Should().Be(createPayload.Username);
        createdDto.Email.Should().Be(createPayload.Email);
        createdDto.Status.Should().Be(UserAccountStatus.Active);

        var userId = createdDto.UserId;

        // 2. GET /api/v1/users/{id}
        var getResponse = await client.GetAsync($"/api/v1/users/{userId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedDto = await getResponse.Content.ReadFromJsonAsync<UserAccountDto>();
        fetchedDto.Should().NotBeNull();
        fetchedDto!.UserId.Should().Be(userId);
        fetchedDto.PersonName.Should().Be("Ahmad Dahlan");

        // 3. PUT /api/v1/users/{id}
        var updatePayload = new UpdateUserAccountRequest(
            Email: $"ahmad_updated_{role.ToLowerInvariant()}@cakra.id",
            Status: UserAccountStatus.Suspended,
            NewPassword: "BrandNewPassword2026!");

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/users/{userId}", updatePayload);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedDto = await updateResponse.Content.ReadFromJsonAsync<UserAccountDto>();
        updatedDto.Should().NotBeNull();
        updatedDto!.Email.Should().Be(updatePayload.Email);
        updatedDto.Status.Should().Be(UserAccountStatus.Suspended);
    }

    [Fact]
    public async Task Unauthenticated_requests_return_401_Unauthorized_problem_details()
    {
        // Arrange
        var client = CreateUnauthenticatedClient();
        var anyId = Guid.NewGuid();

        // Act & Assert GET /api/v1/users
        var getListResponse = await client.GetAsync("/api/v1/users");
        getListResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        getListResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var getListProblem = await getListResponse.Content.ReadFromJsonAsync<JsonElement>();
        getListProblem.GetProperty("status").GetInt32().Should().Be(401);
        getListProblem.GetProperty("errorCode").GetString().Should().Be("UNAUTHORIZED");

        // Act & Assert GET /api/v1/users/{id}
        var getByIdResponse = await client.GetAsync($"/api/v1/users/{anyId}");
        getByIdResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Act & Assert POST /api/v1/users
        var postResponse = await client.PostAsJsonAsync("/api/v1/users", new CreateUserAccountRequest(
            PersonId: Guid.NewGuid(),
            Username: "unauth.user",
            Email: "unauth@cakra.id",
            Password: "Password123!"));
        postResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Act & Assert PUT /api/v1/users/{id}
        var putResponse = await client.PutAsJsonAsync($"/api/v1/users/{anyId}", new UpdateUserAccountRequest(
            Status: UserAccountStatus.Active));
        putResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Programmer")]
    [InlineData("Implementator")]
    [InlineData("Management")]
    [InlineData("Customer")]
    public async Task Requests_with_non_admin_roles_return_403_Forbidden_problem_details(string role)
    {
        // Arrange
        var client = CreateClientWithRole(role);
        var anyId = Guid.NewGuid();

        // Act & Assert GET /api/v1/users
        var getListResponse = await client.GetAsync("/api/v1/users");
        getListResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        getListResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await getListResponse.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(403);
        problem.GetProperty("errorCode").GetString().Should().Be("FORBIDDEN");

        // Act & Assert GET /api/v1/users/{id}
        var getByIdResponse = await client.GetAsync($"/api/v1/users/{anyId}");
        getByIdResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Act & Assert POST /api/v1/users
        var postResponse = await client.PostAsJsonAsync("/api/v1/users", new CreateUserAccountRequest(
            PersonId: Guid.NewGuid(),
            Username: "forbidden.user",
            Email: "forbidden@cakra.id",
            Password: "Password123!"));
        postResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Act & Assert PUT /api/v1/users/{id}
        var putResponse = await client.PutAsJsonAsync($"/api/v1/users/{anyId}", new UpdateUserAccountRequest(
            Status: UserAccountStatus.Active));
        putResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_with_duplicate_username_returns_409_Conflict_problem_details()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var person1 = Guid.NewGuid();
        var person2 = Guid.NewGuid();
        _orgQueryService.AddPerson(person1, "Person", "One");
        _orgQueryService.AddPerson(person2, "Person", "Two");

        var firstRequest = new CreateUserAccountRequest(
            PersonId: person1,
            Username: "duplicate.user",
            Email: "unique1@cakra.id",
            Password: "Password123!");
        var firstResponse = await client.PostAsJsonAsync("/api/v1/users", firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicateRequest = new CreateUserAccountRequest(
            PersonId: person2,
            Username: "DUPLICATE.USER", // Same username, different casing
            Email: "unique2@cakra.id",
            Password: "Password123!");

        // Act
        var conflictResponse = await client.PostAsJsonAsync("/api/v1/users", duplicateRequest);

        // Assert
        conflictResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        conflictResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await conflictResponse.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(409);
        problem.GetProperty("title").GetString().Should().Be("Conflict");
        problem.GetProperty("errorCode").GetString().Should().Be("CONFLICT");
        problem.GetProperty("detail").GetString().Should().Contain("already in use");
    }

    [Fact]
    public async Task Post_with_duplicate_email_returns_409_Conflict_problem_details()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var person1 = Guid.NewGuid();
        var person2 = Guid.NewGuid();
        _orgQueryService.AddPerson(person1, "Person", "One");
        _orgQueryService.AddPerson(person2, "Person", "Two");

        var firstRequest = new CreateUserAccountRequest(
            PersonId: person1,
            Username: "user.one",
            Email: "shared@cakra.id",
            Password: "Password123!");
        var firstResponse = await client.PostAsJsonAsync("/api/v1/users", firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicateEmailRequest = new CreateUserAccountRequest(
            PersonId: person2,
            Username: "user.two",
            Email: "SHARED@CAKRA.ID",
            Password: "Password123!");

        // Act
        var conflictResponse = await client.PostAsJsonAsync("/api/v1/users", duplicateEmailRequest);

        // Assert
        conflictResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await conflictResponse.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(409);
        problem.GetProperty("errorCode").GetString().Should().Be("CONFLICT");
        problem.GetProperty("detail").GetString().Should().Contain("already in use");
    }

    [Fact]
    public async Task Post_with_already_associated_person_returns_409_Conflict_problem_details()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var personId = Guid.NewGuid();
        _orgQueryService.AddPerson(personId, "Single", "Person");

        var firstRequest = new CreateUserAccountRequest(
            PersonId: personId,
            Username: "user.first",
            Email: "first@cakra.id",
            Password: "Password123!");
        var firstResponse = await client.PostAsJsonAsync("/api/v1/users", firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var secondRequest = new CreateUserAccountRequest(
            PersonId: personId, // Same PersonId
            Username: "user.second",
            Email: "second@cakra.id",
            Password: "Password123!");

        // Act
        var conflictResponse = await client.PostAsJsonAsync("/api/v1/users", secondRequest);

        // Assert
        conflictResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await conflictResponse.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(409);
        problem.GetProperty("errorCode").GetString().Should().Be("CONFLICT");
        problem.GetProperty("detail").GetString().Should().Contain("already associated");
    }

    [Fact]
    public async Task Post_with_invalid_command_returns_400_Bad_Request_Validation_problem_details()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var invalidPayload = new CreateUserAccountRequest(
            PersonId: Guid.NewGuid(),
            Username: "ab", // Too short (min 3)
            Email: "not-an-email",
            Password: "short"); // Too short (min 8)

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/users", invalidPayload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(400);
        problem.GetProperty("errorCode").GetString().Should().Be("VALIDATION_FAILED");
        problem.GetProperty("errors").EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetUserById_for_nonexistent_user_returns_404_NotFound_problem_details()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/api/v1/users/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(404);
        problem.GetProperty("errorCode").GetString().Should().Be("RESOURCE_NOT_FOUND");
    }

    [Fact]
    public async Task PutUser_with_PersonId_maps_person_and_activates_unmapped_account_returning_200_OK()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var personId = Guid.NewGuid();
        _orgQueryService.AddPerson(personId, "Haryono", "Sudirman");

        var unmappedUser = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.Empty,
            Username = "pending.approval",
            Email = "pending.approval@cakra.id",
            Status = UserAccountStatus.Pending,
            FailedLoginAttempts = 4,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2)
        };
        _accountRepo.Items[unmappedUser.Id] = unmappedUser;

        var updatePayload = new UpdateUserAccountRequest(
            PersonId: personId,
            Status: UserAccountStatus.Active);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/users/{unmappedUser.Id}", updatePayload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UserAccountDto>();
        result.Should().NotBeNull();
        result!.UserId.Should().Be(unmappedUser.Id);
        result.PersonId.Should().Be(personId);
        result.PersonName.Should().Be("Haryono Sudirman");
        result.Status.Should().Be(UserAccountStatus.Active);
        result.FailedLoginAttempts.Should().Be(0);

        var persisted = _accountRepo.Items[unmappedUser.Id];
        persisted.PersonId.Should().Be(personId);
        persisted.Status.Should().Be(UserAccountStatus.Active);
        persisted.FailedLoginAttempts.Should().Be(0);
    }

    [Fact]
    public async Task PutUser_with_PersonId_returns_400_BadRequest_when_person_is_inactive_or_nonexistent()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var unmappedUser = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.Empty,
            Username = "inactive.target",
            Email = "inactive.target@cakra.id",
            Status = UserAccountStatus.Pending
        };
        _accountRepo.Items[unmappedUser.Id] = unmappedUser;

        var inactivePersonId = Guid.NewGuid();
        _orgQueryService.AddPerson(inactivePersonId, "Inactive", "Person", status: "INACTIVE");

        var payload = new UpdateUserAccountRequest(PersonId: inactivePersonId);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/users/{unmappedUser.Id}", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(400);
        problem.GetProperty("title").GetString().Should().Be("Bad Request");
        problem.GetProperty("errorCode").GetString().Should().Be("BAD_REQUEST");
        problem.GetProperty("detail").GetString().Should().Contain("The specified person is not active or does not exist.");
    }

    [Fact]
    public async Task PutUser_returns_400_BadRequest_when_activating_without_person_mapping()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var unmappedUser = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.Empty,
            Username = "unmapped.pending",
            Email = "unmapped.pending@cakra.id",
            Status = UserAccountStatus.Pending
        };
        _accountRepo.Items[unmappedUser.Id] = unmappedUser;

        var payload = new UpdateUserAccountRequest(Status: UserAccountStatus.Active);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/users/{unmappedUser.Id}", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(400);
        problem.GetProperty("title").GetString().Should().Be("Bad Request");
        problem.GetProperty("errorCode").GetString().Should().Be("BAD_REQUEST");
        problem.GetProperty("detail").GetString().Should().Contain("An active organizational person must be linked before activating a user account.");
    }

    [Fact]
    public async Task PutUser_with_empty_guid_PersonId_returns_400_BadRequest_validation_error()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.Empty,
            Username = "empty.guid.target",
            Email = "empty.guid@cakra.id",
            Status = UserAccountStatus.Pending
        };
        _accountRepo.Items[user.Id] = user;

        var payload = new UpdateUserAccountRequest(PersonId: Guid.Empty);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/users/{user.Id}", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(400);
        problem.GetProperty("errorCode").GetString().Should().Be("VALIDATION_FAILED");
        problem.GetProperty("errors").EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public async Task PutUser_with_PersonId_returns_409_Conflict_when_changing_person_on_already_mapped_account()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var initialPersonId = Guid.NewGuid();
        var newPersonId = Guid.NewGuid();
        _orgQueryService.AddPerson(initialPersonId, "Initial", "Person");
        _orgQueryService.AddPerson(newPersonId, "New", "Person");

        var mappedUser = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = initialPersonId,
            Username = "mapped.user",
            Email = "mapped@cakra.id",
            Status = UserAccountStatus.Active
        };
        _accountRepo.Items[mappedUser.Id] = mappedUser;

        var updatePayload = new UpdateUserAccountRequest(PersonId: newPersonId);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/users/{mappedUser.Id}", updatePayload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(409);
        problem.GetProperty("title").GetString().Should().Be("Conflict");
        problem.GetProperty("errorCode").GetString().Should().Be("CONFLICT");
        problem.GetProperty("detail").GetString().Should().Contain("Changing the organizational person of an already mapped user account is not permitted.");
    }

    [Fact]
    public async Task PutUser_with_PersonId_returns_409_Conflict_when_person_is_already_linked_to_another_account()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var linkedPersonId = Guid.NewGuid();
        _orgQueryService.AddPerson(linkedPersonId, "Existing", "Linked");

        var existingUser = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = linkedPersonId,
            Username = "existing.user",
            Email = "existing@cakra.id",
            Status = UserAccountStatus.Active
        };
        var targetUser = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.Empty,
            Username = "target.user",
            Email = "target@cakra.id",
            Status = UserAccountStatus.Pending
        };
        _accountRepo.Items[existingUser.Id] = existingUser;
        _accountRepo.Items[targetUser.Id] = targetUser;

        var updatePayload = new UpdateUserAccountRequest(PersonId: linkedPersonId);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/users/{targetUser.Id}", updatePayload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(409);
        problem.GetProperty("title").GetString().Should().Be("Conflict");
        problem.GetProperty("errorCode").GetString().Should().Be("CONFLICT");
        problem.GetProperty("detail").GetString().Should().Contain("The specified person is already linked to another user account.");
    }

    private sealed class StubAuthService : IIdentityAuthService
    {
        private readonly Dictionary<string, (Guid UserId, Guid PersonId)> _sessions = new();

        public void RegisterValidSession(string token, Guid userId, Guid personId)
        {
            _sessions[token] = (userId, personId);
        }

        public Task<SecurityContext> ValidateSessionAsync(string sessionToken, CancellationToken cancellationToken = default)
        {
            if (_sessions.TryGetValue(sessionToken, out var session))
            {
                return Task.FromResult(SecurityContext.Valid(session.UserId, session.PersonId));
            }
            return Task.FromResult(SecurityContext.Invalid("Invalid session token"));
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

    private sealed class StubAuthzService : IIdentityAuthzService
    {
        private readonly Dictionary<Guid, List<string>> _roles = new();

        public void RegisterRoles(Guid personId, params string[] roles)
        {
            if (!_roles.TryGetValue(personId, out var list))
            {
                list = new List<string>();
                _roles[personId] = list;
            }
            list.AddRange(roles);
        }

        public Task<IReadOnlyList<string>> ResolveRolesAsync(Guid personId, CancellationToken cancellationToken = default)
        {
            if (_roles.TryGetValue(personId, out var list))
            {
                return Task.FromResult<IReadOnlyList<string>>(list);
            }
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }
    }

    private sealed class InMemoryOrganizationQueryService : IOrganizationQueryService
    {
        private readonly List<PersonDto> _persons = new();

        public void AddPerson(Guid id, string firstName, string lastName, string status = "ACTIVE")
        {
            _persons.Add(new PersonDto
            {
                Id = id,
                FirstName = firstName,
                LastName = lastName,
                Email = $"{firstName.ToLowerInvariant()}@cakra.id",
                Status = status
            });
        }

        public Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_persons.Any(p => p.Id == personId && p.IsActive));

        public Task<PersonDto?> GetPersonByIdAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_persons.FirstOrDefault(p => p.Id == personId));

        public Task<IReadOnlyList<PersonDto>> ListActivePersonsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PersonDto>>(_persons.Where(p => p.IsActive).ToList());

        public Task<IReadOnlyList<PersonDto>> ListAllPersonsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PersonDto>>(_persons.ToList());

        public Task<IReadOnlyList<string>> GetPersonRolesAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class InMemoryUserAccountRepository : IUserAccountRepository
    {
        public readonly Dictionary<Guid, UserAccount> Items = new();

        public Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Items.TryGetValue(id, out var user);
            return Task.FromResult(user);
        }

        public Task<UserAccount?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken = default)
        {
            var user = Items.Values.FirstOrDefault(u =>
                string.Equals(u.Username, usernameOrEmail, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.Email, usernameOrEmail, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(user);
        }

        public Task<UserAccount?> GetByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default)
        {
            if (personId == Guid.Empty) return Task.FromResult<UserAccount?>(null);
            var user = Items.Values.FirstOrDefault(u => u.PersonId == personId);
            return Task.FromResult(user);
        }

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
}
