using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cakra.Api.Infrastructure.Authentication;
using Cakra.Api.Infrastructure.Migrations;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Identity.Services;
using Cakra.Modules.Organization;
using Cakra.Modules.Organization.Commands;
using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Services;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Respawn;
using Respawn.Graph;
using Xunit;

namespace Cakra.Tests.Integration.Organization;

/// <summary>
/// Integration tests for Organization Module Application Services, Query Service, and Role Resolution (P3-S13)
/// using xUnit, <see cref="WebApplicationFactory{TEntryPoint}"/>, and <see cref="Respawner"/> (Architecture §7, §14, §19.8, §20):
/// - MediatR command handlers via <see cref="OrganizationService"/> (create Person, update Person,
///   create Team, assign Person to Team, create Role, assign Role to Person,
///   create Responsibility, assign Responsibility to Person, deactivate Person).
/// - <see cref="OrganizationQueryService"/> Dapper queries (<c>GetPersonById</c>, <c>ListActivePersons</c>,
///   <c>GetTeamRoster</c>, <c>GetPersonRoles</c>, <c>GetPersonResponsibilities</c>).
/// - Dynamic role resolution wired: <see cref="AuthorizationService.ResolveRoles"/> delegates to
///   <see cref="OrganizationQueryService.GetPersonRoles"/> and integrates end-to-end with the authentication pipeline.
/// </summary>
[Collection("OrganizationDatabase")]
public class OrganizationApplicationServiceIntegrationTests : IAsyncLifetime
{
    private const string LocalDbFallback =
        "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb;Integrated Security=true;TrustServerCertificate=True;";

    private readonly string _connectionString;
    private WebApplicationFactory<Program> _factory = null!;
    private Respawner? _respawner;
    private bool _sqlServerAvailable;

    public OrganizationApplicationServiceIntegrationTests()
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

        // Ensure database and migrations are applied
        var runner = new DatabaseMigrationRunner(_connectionString, NullLogger<DatabaseMigrationRunner>.Instance);
        var migrationResult = runner.Run();
        migrationResult.Successful.Should().BeTrue("DbUp migration scripts must execute cleanly before integration tests");

        // Reset database state with Respawn for clean test isolation
        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                SchemasToInclude = new[] { "organization", "identity" },
                TablesToIgnore = new Table[]
                {
                    new("dbo", "__SchemaVersions"),
                    new("dbo", "SchemaVersions")
                }
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
    public async Task CreatePerson_and_UpdatePerson_commands_persist_and_GetPersonById_returns_accurate_data()
    {
        if (!_sqlServerAvailable) return;

        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var queryService = scope.ServiceProvider.GetRequiredService<IOrganizationQueryService>();

        // 1. Create Person via MediatR command handler
        var created = await mediator.Send(new CreatePersonCommand(
            "Rina",
            "Kusuma",
            "rina.kusuma@cakra.id"));

        created.Id.Should().NotBeEmpty();
        created.Status.Should().Be(Person.StatusActive);

        // 2. Verify GetPersonById returns accurate data (async & sync)
        var byIdAsync = await queryService.GetPersonByIdAsync(created.Id);
        byIdAsync.Should().NotBeNull();
        byIdAsync!.Id.Should().Be(created.Id);
        byIdAsync.PersonId.Should().Be(created.Id);
        byIdAsync.FirstName.Should().Be("Rina");
        byIdAsync.LastName.Should().Be("Kusuma");
        byIdAsync.FullName.Should().Be("Rina Kusuma");
        byIdAsync.Email.Should().Be("rina.kusuma@cakra.id");
        byIdAsync.Status.Should().Be(Person.StatusActive);
        byIdAsync.IsActive.Should().BeTrue();

        var byIdSync = queryService.GetPersonById(created.Id);
        byIdSync.Should().NotBeNull();
        byIdSync!.Email.Should().Be("rina.kusuma@cakra.id");

        // 3. Update Person via MediatR command handler
        var updated = await mediator.Send(new UpdatePersonCommand(
            created.Id,
            "Rina",
            "Kusuma-Wijaya",
            "rina.wijaya@cakra.id"));

        updated.LastName.Should().Be("Kusuma-Wijaya");
        updated.Email.Should().Be("rina.wijaya@cakra.id");

        var afterUpdate = await queryService.GetPersonByIdAsync(created.Id);
        afterUpdate.Should().NotBeNull();
        afterUpdate!.LastName.Should().Be("Kusuma-Wijaya");
        afterUpdate.FullName.Should().Be("Rina Kusuma-Wijaya");
        afterUpdate.Email.Should().Be("rina.wijaya@cakra.id");
        afterUpdate.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeactivatePerson_command_excludes_deactivated_person_from_ListActivePersons()
    {
        if (!_sqlServerAvailable) return;

        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var queryService = scope.ServiceProvider.GetRequiredService<IOrganizationQueryService>();

        var activePerson = await mediator.Send(new CreatePersonCommand(
            "Andi",
            "Hakim",
            "andi.hakim@cakra.id"));

        var personToDeactivate = await mediator.Send(new CreatePersonCommand(
            "Sari",
            "Lestari",
            "sari.lestari@cakra.id"));

        // Initially both are in ListActivePersons
        var initialActive = await queryService.ListActivePersonsAsync();
        initialActive.Should().Contain(p => p.Id == activePerson.Id);
        initialActive.Should().Contain(p => p.Id == personToDeactivate.Id);

        // Deactivate second person via MediatR command
        var deactivated = await mediator.Send(new DeactivatePersonCommand(personToDeactivate.Id));
        deactivated.Status.Should().Be(Person.StatusInactive);
        deactivated.IsActive.Should().BeFalse();

        // ListActivePersons excludes deactivated person
        var activeAfter = await queryService.ListActivePersonsAsync();
        activeAfter.Should().Contain(p => p.Id == activePerson.Id);
        activeAfter.Should().NotContain(p => p.Id == personToDeactivate.Id);

        var activeAfterSync = queryService.ListActivePersons();
        activeAfterSync.Should().NotContain(p => p.Id == personToDeactivate.Id);

        // IsPersonActive returns false for deactivated person
        (await queryService.IsPersonActiveAsync(personToDeactivate.Id)).Should().BeFalse();
        (await queryService.IsPersonActiveAsync(activePerson.Id)).Should().BeTrue();

        // GetPersonById still returns historical record with INACTIVE status
        var deactivatedDto = await queryService.GetPersonByIdAsync(personToDeactivate.Id);
        deactivatedDto.Should().NotBeNull();
        deactivatedDto!.Status.Should().Be(Person.StatusInactive);
        deactivatedDto.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTeam_and_AssignPersonToTeam_commands_populate_GetTeamRoster()
    {
        if (!_sqlServerAvailable) return;

        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var queryService = scope.ServiceProvider.GetRequiredService<IOrganizationQueryService>();

        var person1 = await mediator.Send(new CreatePersonCommand("Dian", "Putra", "dian.putra@cakra.id"));
        var person2 = await mediator.Send(new CreatePersonCommand("Hendra", "Saputra", "hendra.saputra@cakra.id"));

        var team = await mediator.Send(new CreateTeamCommand("Hospital Implementation Team", "HIS rollouts"));

        await mediator.Send(new AssignPersonToTeamCommand(person1.Id, team.Id));
        await mediator.Send(new AssignPersonToTeamCommand(person2.Id, team.Id));

        var roster = await queryService.GetTeamRosterAsync(team.Id);
        roster.Should().HaveCount(2);
        roster.Should().Contain(m =>
            m.PersonId == person1.Id &&
            m.TeamId == team.Id &&
            m.TeamName == "Hospital Implementation Team" &&
            m.Email == "dian.putra@cakra.id" &&
            m.FullName == "Dian Putra");
        roster.Should().Contain(m =>
            m.PersonId == person2.Id &&
            m.TeamId == team.Id &&
            m.Email == "hendra.saputra@cakra.id");

        var rosterSync = queryService.GetTeamRoster(team.Id);
        rosterSync.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateRole_and_AssignRoleToPerson_commands_populate_GetPersonRoles_and_wire_AuthorizationService()
    {
        if (!_sqlServerAvailable) return;

        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var queryService = scope.ServiceProvider.GetRequiredService<IOrganizationQueryService>();
        var authorizationService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        var person = await mediator.Send(new CreatePersonCommand("Nadia", "Rahman", "nadia.rahman@cakra.id"));
        var programmerRole = await mediator.Send(new CreateRoleCommand("Programmer", "Software Engineer"));
        var managementRole = await mediator.Send(new CreateRoleCommand("Management", "Operations Management"));

        await mediator.Send(new AssignRoleToPersonCommand(person.Id, programmerRole.Id));
        await mediator.Send(new AssignRoleToPersonCommand(person.Id, managementRole.Id));

        // 1. OrganizationQueryService.GetPersonRoles includes assigned roles
        var rolesAsync = await queryService.GetPersonRolesAsync(person.Id);
        rolesAsync.Should().BeEquivalentTo(new[] { "Management", "Programmer" });

        var rolesSync = queryService.GetPersonRoles(person.Id);
        rolesSync.Should().BeEquivalentTo(new[] { "Management", "Programmer" });

        // 2. AuthorizationService.ResolveRoles delegates to OrganizationQueryService.GetPersonRoles
        var resolvedAsync = await authorizationService.ResolveRolesAsync(person.Id);
        resolvedAsync.Should().BeEquivalentTo(new[] { "Management", "Programmer" });

        var resolvedSync = authorizationService.ResolveRoles(person.Id);
        resolvedSync.Should().BeEquivalentTo(new[] { "Management", "Programmer" });
    }

    [Fact]
    public async Task CreateResponsibility_and_AssignResponsibilityToPerson_commands_populate_GetPersonResponsibilities()
    {
        if (!_sqlServerAvailable) return;

        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var queryService = scope.ServiceProvider.GetRequiredService<IOrganizationQueryService>();

        var person = await mediator.Send(new CreatePersonCommand("Reza", "Maulana", "reza.maulana@cakra.id"));
        var resp1 = await mediator.Send(new CreateResponsibilityCommand("Module PIC", "Lead for billing module"));
        var resp2 = await mediator.Send(new CreateResponsibilityCommand("Customer Pimpro", "Project lead for customer"));

        await mediator.Send(new AssignResponsibilityToPersonCommand(person.Id, resp1.Id));
        await mediator.Send(new AssignResponsibilityToPersonCommand(person.Id, resp2.Id));

        var responsibilities = await queryService.GetPersonResponsibilitiesAsync(person.Id);
        responsibilities.Should().HaveCount(2);
        responsibilities.Should().Contain(r =>
            r.ResponsibilityId == resp1.Id &&
            r.Name == "Module PIC" &&
            r.Description == "Lead for billing module");
        responsibilities.Should().Contain(r =>
            r.ResponsibilityId == resp2.Id &&
            r.Name == "Customer Pimpro");

        var responsibilitiesSync = queryService.GetPersonResponsibilities(person.Id);
        responsibilitiesSync.Select(r => r.Name).Should().BeEquivalentTo(new[] { "Customer Pimpro", "Module PIC" });
    }

    [Fact]
    public async Task EndToEnd_dynamic_role_resolution_authorizes_HTTP_endpoint_after_AssignRoleToPersonCommand()
    {
        if (!_sqlServerAvailable) return;

        Guid personId;
        string sessionToken;

        using (var scope = _factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var userAccountRepo = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

            // 1. Create Person and Role in Organization module
            var person = await mediator.Send(new CreatePersonCommand("Tono", "Sudirjo", "tono.sudirjo@cakra.id"));
            personId = person.Id;

            var managementRole = await mediator.Send(new CreateRoleCommand("Management", "Management Role"));
            await mediator.Send(new AssignRoleToPersonCommand(personId, managementRole.Id));

            // 2. Create linked UserAccount in Identity module and log in
            var user = new UserAccount
            {
                Id = Guid.NewGuid(),
                PersonId = personId,
                Username = "tono.sudirjo",
                Email = "tono.sudirjo@cakra.id",
                Status = UserAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = authService.HashPassword(user, "Password123!");
            await userAccountRepo.AddAsync(user);

            var loginResult = await authService.LoginAsync("tono.sudirjo", "Password123!");
            loginResult.Succeeded.Should().BeTrue();
            sessionToken = loginResult.SessionToken!;
        }

        // 3. Call [Authorize(Roles = "Management")] endpoint; roles are resolved dynamically via OrganizationQueryService
        using var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/probe/management");
        request.Headers.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}={sessionToken}");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("personId").GetGuid().Should().Be(personId);
        var roles = body.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToList();
        roles.Should().Contain("Management");
    }
}
