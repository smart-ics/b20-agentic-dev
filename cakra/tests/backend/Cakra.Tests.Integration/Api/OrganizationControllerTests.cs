using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cakra.Api.Controllers;
using Cakra.Api.Infrastructure.Authentication;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Identity.Services;
using Cakra.Modules.Organization;
using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using IIdentityAuthService = Cakra.Modules.Identity.Domain.IAuthenticationService;
using IIdentityAuthzService = Cakra.Modules.Identity.Services.IAuthorizationService;

namespace Cakra.Tests.Integration.Api;

/// <summary>
/// Integration tests verifying <see cref="OrganizationController"/> write endpoints and role-based authorization (P3-S07; CR-008; Architecture §7, §14):
/// - Role-based authorization: write endpoints restricted to Administrator or Admin roles.
/// - Read endpoint /all accessible to any authenticated user.
/// - HTTP 401 Unauthorized for unauthenticated calls.
/// - HTTP 403 Forbidden for authenticated users lacking Administrator or Admin roles.
/// - HTTP 200 OK / 201 Created for authenticated Administrator or Admin users.
/// - HTTP 404 Not Found for non-existent Person on update/activate/deactivate.
/// - HTTP 409 Conflict ProblemDetails on duplicate email address.
/// - HTTP 400 Bad Request ProblemDetails on invalid inputs.
/// </summary>
public class OrganizationControllerTests : IntegrationTestBase
{
    private readonly InMemoryOrganizationService _orgService = new();
    private readonly InMemoryOrganizationQueryService _orgQueryService;
    private readonly StubAuthService _authService = new();
    private readonly StubAuthzService _authzService = new();

    public OrganizationControllerTests()
    {
        _orgQueryService = new InMemoryOrganizationQueryService(_orgService);
    }

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
                services.AddScoped<IOrganizationService>(_ => _orgService);
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
                services.AddScoped<IOrganizationService>(_ => _orgService);
                services.AddScoped<IOrganizationQueryService>(_ => _orgQueryService);
            });
        }).CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
    }

    [Fact]
    public async Task CreatePerson_returns_201_for_Administrator_role()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var request = new CreatePersonRequest("Budi", "Santoso", "budi.santoso@cakra.id");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/organization/persons", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var created = await response.Content.ReadFromJsonAsync<Person>();
        created.Should().NotBeNull();
        created!.FirstName.Should().Be("Budi");
        created.LastName.Should().Be("Santoso");
        created.Email.Should().Be("budi.santoso@cakra.id");
        created.Status.Should().Be(Person.StatusActive);
        _orgService.Persons.Should().ContainKey(created.Id);
    }

    [Fact]
    public async Task CreatePerson_returns_201_for_Admin_role()
    {
        // Arrange
        var client = CreateClientWithRole("Admin");
        var request = new CreatePersonRequest("Siti", "Rahma", "siti.rahma@cakra.id");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/organization/persons", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var created = await response.Content.ReadFromJsonAsync<Person>();
        created.Should().NotBeNull();
        created!.FirstName.Should().Be("Siti");
        created.LastName.Should().Be("Rahma");
        created.Email.Should().Be("siti.rahma@cakra.id");
        created.Status.Should().Be(Person.StatusActive);
        _orgService.Persons.Should().ContainKey(created.Id);
    }

    [Theory]
    [InlineData("Programmer")]
    [InlineData("Customer")]
    [InlineData("Implementator")]
    public async Task CreatePerson_returns_403_for_non_admin_role(string role)
    {
        // Arrange
        var client = CreateClientWithRole(role);
        var request = new CreatePersonRequest("NonAdmin", "User", "nonadmin@cakra.id");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/organization/persons", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(403);
        problem.GetProperty("errorCode").GetString().Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task CreatePerson_returns_401_for_unauthenticated()
    {
        // Arrange
        var client = CreateUnauthenticatedClient();
        var request = new CreatePersonRequest("Anon", "User", "anon@cakra.id");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/organization/persons", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(401);
        problem.GetProperty("errorCode").GetString().Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task CreatePerson_returns_409_for_duplicate_email()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var existing = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = "Existing",
            LastName = "Person",
            Email = "duplicate@cakra.id",
            Status = Person.StatusActive,
            CreatedAt = DateTime.UtcNow
        };
        _orgService.Persons[existing.Id] = existing;

        var request = new CreatePersonRequest("Another", "Person", "DUPLICATE@CAKRA.ID");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/organization/persons", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(409);
        problem.GetProperty("title").GetString().Should().Be("Conflict");
        problem.GetProperty("errorCode").GetString().Should().Be("PERSON_CONFLICT");
        problem.GetProperty("detail").GetString().Should().Contain("already exists");
    }

    [Fact]
    public async Task CreatePerson_returns_400_for_invalid_input()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var invalidRequest = new CreatePersonRequest("", "", "not-an-email");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/organization/persons", invalidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(400);
        problem.GetProperty("errorCode").GetString().Should().Be("VALIDATION_FAILED");
        problem.GetProperty("errors").EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public async Task UpdatePerson_returns_200_for_Administrator_role()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var person = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = "Original",
            LastName = "Name",
            Email = "original@cakra.id",
            Status = Person.StatusActive,
            CreatedAt = DateTime.UtcNow
        };
        _orgService.Persons[person.Id] = person;

        var updateRequest = new UpdatePersonRequest("UpdatedFirst", "UpdatedLast", "updated@cakra.id");

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/organization/persons/{person.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<Person>();
        updated.Should().NotBeNull();
        updated!.FirstName.Should().Be("UpdatedFirst");
        updated.LastName.Should().Be("UpdatedLast");
        updated.Email.Should().Be("updated@cakra.id");

        _orgService.Persons[person.Id].FirstName.Should().Be("UpdatedFirst");
        _orgService.Persons[person.Id].LastName.Should().Be("UpdatedLast");
        _orgService.Persons[person.Id].Email.Should().Be("updated@cakra.id");
    }

    [Theory]
    [InlineData("Programmer")]
    [InlineData("Customer")]
    public async Task UpdatePerson_returns_403_for_non_admin_role(string role)
    {
        // Arrange
        var client = CreateClientWithRole(role);
        var personId = Guid.NewGuid();
        var updateRequest = new UpdatePersonRequest("Jane", "Doe", "jane@cakra.id");

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/organization/persons/{personId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(403);
        problem.GetProperty("errorCode").GetString().Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task UpdatePerson_returns_404_for_not_found()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var nonExistentId = Guid.NewGuid();
        var updateRequest = new UpdatePersonRequest("Jane", "Doe", "jane@cakra.id");

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/organization/persons/{nonExistentId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(404);
        problem.GetProperty("errorCode").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task UpdatePerson_returns_409_for_duplicate_email()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var person1 = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = "Person",
            LastName = "One",
            Email = "person1@cakra.id",
            Status = Person.StatusActive,
            CreatedAt = DateTime.UtcNow
        };
        var person2 = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = "Person",
            LastName = "Two",
            Email = "person2@cakra.id",
            Status = Person.StatusActive,
            CreatedAt = DateTime.UtcNow
        };
        _orgService.Persons[person1.Id] = person1;
        _orgService.Persons[person2.Id] = person2;

        var conflictRequest = new UpdatePersonRequest("Person", "TwoUpdated", "person1@cakra.id");

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/organization/persons/{person2.Id}", conflictRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(409);
        problem.GetProperty("title").GetString().Should().Be("Conflict");
        problem.GetProperty("errorCode").GetString().Should().Be("PERSON_CONFLICT");
        problem.GetProperty("detail").GetString().Should().Contain("already exists");
    }

    [Fact]
    public async Task ActivatePerson_returns_200_for_Administrator_role()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var person = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = "Inactive",
            LastName = "Person",
            Email = "inactive@cakra.id",
            Status = Person.StatusInactive,
            CreatedAt = DateTime.UtcNow
        };
        _orgService.Persons[person.Id] = person;

        // Act
        var response = await client.PutAsync($"/api/v1/organization/persons/{person.Id}/activate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var activated = await response.Content.ReadFromJsonAsync<Person>();
        activated.Should().NotBeNull();
        activated!.Status.Should().Be(Person.StatusActive);
        _orgService.Persons[person.Id].Status.Should().Be(Person.StatusActive);
    }

    [Theory]
    [InlineData("Programmer")]
    [InlineData("Customer")]
    public async Task ActivatePerson_returns_403_for_non_admin_role(string role)
    {
        // Arrange
        var client = CreateClientWithRole(role);
        var personId = Guid.NewGuid();

        // Act
        var response = await client.PutAsync($"/api/v1/organization/persons/{personId}/activate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(403);
        problem.GetProperty("errorCode").GetString().Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task ActivatePerson_returns_404_for_not_found()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.PutAsync($"/api/v1/organization/persons/{nonExistentId}/activate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(404);
        problem.GetProperty("errorCode").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task DeactivatePerson_returns_200_for_Administrator_role()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var person = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = "Active",
            LastName = "Person",
            Email = "active@cakra.id",
            Status = Person.StatusActive,
            CreatedAt = DateTime.UtcNow
        };
        _orgService.Persons[person.Id] = person;

        // Act
        var response = await client.PutAsync($"/api/v1/organization/persons/{person.Id}/deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var deactivated = await response.Content.ReadFromJsonAsync<Person>();
        deactivated.Should().NotBeNull();
        deactivated!.Status.Should().Be(Person.StatusInactive);
        _orgService.Persons[person.Id].Status.Should().Be(Person.StatusInactive);
    }

    [Theory]
    [InlineData("Programmer")]
    [InlineData("Customer")]
    public async Task DeactivatePerson_returns_403_for_non_admin_role(string role)
    {
        // Arrange
        var client = CreateClientWithRole(role);
        var personId = Guid.NewGuid();

        // Act
        var response = await client.PutAsync($"/api/v1/organization/persons/{personId}/deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(403);
        problem.GetProperty("errorCode").GetString().Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task DeactivatePerson_returns_404_for_not_found()
    {
        // Arrange
        var client = CreateClientWithRole("Administrator");
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.PutAsync($"/api/v1/organization/persons/{nonExistentId}/deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().Should().Be(404);
        problem.GetProperty("errorCode").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task ListAllPersons_returns_200_for_authenticated_user()
    {
        // Arrange - authenticated user with non-admin role (e.g. Programmer)
        var client = CreateClientWithRole("Programmer");
        var activePerson = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = "Active",
            LastName = "Member",
            Email = "member.active@cakra.id",
            Status = Person.StatusActive,
            CreatedAt = DateTime.UtcNow
        };
        var inactivePerson = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = "Inactive",
            LastName = "Member",
            Email = "member.inactive@cakra.id",
            Status = Person.StatusInactive,
            CreatedAt = DateTime.UtcNow
        };
        _orgService.Persons[activePerson.Id] = activePerson;
        _orgService.Persons[inactivePerson.Id] = inactivePerson;

        // Act
        var response = await client.GetAsync("/api/v1/organization/persons/all");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var persons = await response.Content.ReadFromJsonAsync<IReadOnlyList<PersonDto>>();
        persons.Should().NotBeNull();
        persons!.Should().HaveCount(2);
        persons.Should().Contain(p => p.Id == activePerson.Id && p.Status == "ACTIVE");
        persons.Should().Contain(p => p.Id == inactivePerson.Id && p.Status == "INACTIVE");
    }

    private sealed class InMemoryOrganizationService : IOrganizationService
    {
        public readonly Dictionary<Guid, Person> Persons = new();

        public Task<Person> CreatePersonAsync(
            string firstName,
            string lastName,
            string email,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
            ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
            ArgumentException.ThrowIfNullOrWhiteSpace(email);

            var normalizedEmail = email.Trim();
            if (Persons.Values.Any(p => string.Equals(p.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A person with email '{normalizedEmail}' already exists.");
            }

            var person = new Person
            {
                Id = Guid.NewGuid(),
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                Email = normalizedEmail,
                Status = Person.StatusActive,
                CreatedAt = DateTime.UtcNow
            };
            Persons[person.Id] = person;
            return Task.FromResult(person);
        }

        public Task<Person> UpdatePersonAsync(
            Guid personId,
            string firstName,
            string lastName,
            string email,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
            ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
            ArgumentException.ThrowIfNullOrWhiteSpace(email);

            if (!Persons.TryGetValue(personId, out var person))
            {
                throw new KeyNotFoundException($"Person '{personId}' was not found.");
            }

            var normalizedEmail = email.Trim();
            if (Persons.Values.Any(p => p.Id != personId && string.Equals(p.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A person with email '{normalizedEmail}' already exists.");
            }

            person.FirstName = firstName.Trim();
            person.LastName = lastName.Trim();
            person.Email = normalizedEmail;
            person.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(person);
        }

        public Task<Person> ActivatePersonAsync(
            Guid personId,
            CancellationToken cancellationToken = default)
        {
            if (!Persons.TryGetValue(personId, out var person))
            {
                throw new KeyNotFoundException($"Person '{personId}' was not found.");
            }

            person.Activate();
            return Task.FromResult(person);
        }

        public Task<Person> DeactivatePersonAsync(
            Guid personId,
            CancellationToken cancellationToken = default)
        {
            if (!Persons.TryGetValue(personId, out var person))
            {
                throw new KeyNotFoundException($"Person '{personId}' was not found.");
            }

            person.Deactivate();
            return Task.FromResult(person);
        }

        public Task<Team> CreateTeamAsync(string name, string? description = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<TeamMembership> AssignPersonToTeamAsync(Guid personId, Guid teamId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Role> CreateRoleAsync(string name, string? description = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<RoleAssignment> AssignRoleToPersonAsync(Guid personId, Guid roleId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task RevokeRoleFromPersonAsync(Guid personId, Guid roleId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Responsibility> CreateResponsibilityAsync(string name, string? description = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ResponsibilityAssignment> AssignResponsibilityToPersonAsync(Guid personId, Guid responsibilityId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class InMemoryOrganizationQueryService : IOrganizationQueryService
    {
        private readonly InMemoryOrganizationService _service;

        public InMemoryOrganizationQueryService(InMemoryOrganizationService service)
        {
            _service = service;
        }

        public Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_service.Persons.TryGetValue(personId, out var p) && p.IsActive);

        public Task<PersonDto?> GetPersonByIdAsync(Guid personId, CancellationToken cancellationToken = default)
        {
            if (_service.Persons.TryGetValue(personId, out var p))
            {
                return Task.FromResult<PersonDto?>(new PersonDto
                {
                    Id = p.Id,
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    Email = p.Email,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                });
            }
            return Task.FromResult<PersonDto?>(null);
        }

        public Task<IReadOnlyList<PersonDto>> ListActivePersonsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PersonDto>>(_service.Persons.Values
                .Where(p => p.IsActive)
                .Select(p => new PersonDto
                {
                    Id = p.Id,
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    Email = p.Email,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                }).ToList());

        public Task<IReadOnlyList<PersonDto>> ListAllPersonsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PersonDto>>(_service.Persons.Values
                .Select(p => new PersonDto
                {
                    Id = p.Id,
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    Email = p.Email,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                }).ToList());

        public Task<IReadOnlyList<string>> GetPersonRolesAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
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
}
