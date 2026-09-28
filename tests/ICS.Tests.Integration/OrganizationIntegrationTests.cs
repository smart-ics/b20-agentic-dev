namespace ICS.Tests.Integration;

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using ICS.Core.Domain.Verification;
using ICS.Modules.Identity.Application;
using ICS.Modules.Organization;
using ICS.Modules.Organization.Application;
using ICS.Modules.Organization.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Integration tests verifying the Organization module:
/// Domain entities, Dapper repositories, OrganizationService commands, OrganizationQueryService queries,
/// domain event emissions, and dynamic role resolution via AuthorizationService.
/// Architecture §6, §7, §14, §17, §19.3, §20.
/// </summary>
public class OrganizationIntegrationTests : IntegrationTestBase
{
    public OrganizationIntegrationTests(IcsWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CreatePerson_ShouldPersistInDatabase_EmitPersonCreatedEvent_AndBeQueryable()
    {
        // Arrange
        var personName = "Dr. Alice Morgan";
        var personEmail = $"alice.morgan.{Guid.NewGuid():N}@smart-ics.internal";

        // Act: Create person via OrganizationService command
        var createdPerson = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.CreatePersonAsync(personName, personEmail);
        });

        // Assert: Return value
        createdPerson.Should().NotBeNull();
        createdPerson.PersonId.Should().NotBeEmpty();
        createdPerson.Name.Should().Be(personName);
        createdPerson.Email.Should().Be(personEmail.ToLowerInvariant());
        createdPerson.Status.Should().Be("ACTIVE");

        // Assert: Query service returns accurate results from SQL Server
        await ExecuteInScopeAsync(async sp =>
        {
            var queryService = sp.GetRequiredService<IOrganizationQueryService>();
            var fetched = await queryService.GetPersonByIdAsync(createdPerson.PersonId);

            fetched.Should().NotBeNull();
            fetched!.PersonId.Should().Be(createdPerson.PersonId);
            fetched.Name.Should().Be(personName);
            fetched.Email.Should().Be(personEmail.ToLowerInvariant());
            fetched.Status.Should().Be("ACTIVE");

            var isActive = await queryService.IsPersonActiveAsync(createdPerson.PersonId);
            isActive.Should().BeTrue();

            var activeList = await queryService.ListActivePersonsAsync();
            activeList.Should().Contain(p => p.PersonId == createdPerson.PersonId);
        });

        // Assert: Domain event was dispatched
        TestDomainEventCollector.PublishedEvents
            .OfType<PersonCreated>()
            .Should().Contain(pc => pc.PersonId == createdPerson.PersonId && pc.Name == personName);
    }

    [Fact]
    public async Task UpdatePerson_ShouldPersistModifications_AndReflectInQueries()
    {
        // Arrange
        var initialEmail = $"dev.init.{Guid.NewGuid():N}@smart-ics.internal";
        var created = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.CreatePersonAsync("Dev Initial", initialEmail);
        });

        var updatedName = "Dev Updated Name";
        var updatedEmail = $"dev.updated.{Guid.NewGuid():N}@smart-ics.internal";

        // Act
        var updated = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.UpdatePersonAsync(created.PersonId, updatedName, updatedEmail);
        });

        // Assert
        updated.Name.Should().Be(updatedName);
        updated.Email.Should().Be(updatedEmail.ToLowerInvariant());

        await ExecuteInScopeAsync(async sp =>
        {
            var queryService = sp.GetRequiredService<IOrganizationQueryService>();
            var fetched = await queryService.GetPersonByIdAsync(created.PersonId);
            fetched.Should().NotBeNull();
            fetched!.Name.Should().Be(updatedName);
            fetched.Email.Should().Be(updatedEmail.ToLowerInvariant());
        });
    }

    [Fact]
    public async Task DeactivatePerson_ShouldSetStatusInactive_EmitEvent_AndExcludeFromActiveList()
    {
        // Arrange
        var email = $"to.deactivate.{Guid.NewGuid():N}@smart-ics.internal";
        var created = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.CreatePersonAsync("John ToDeactivate", email);
        });

        // Act
        var success = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.DeactivatePersonAsync(created.PersonId);
        });

        // Assert
        success.Should().BeTrue();

        await ExecuteInScopeAsync(async sp =>
        {
            var queryService = sp.GetRequiredService<IOrganizationQueryService>();
            var fetched = await queryService.GetPersonByIdAsync(created.PersonId);
            fetched.Should().NotBeNull();
            fetched!.Status.Should().Be("INACTIVE");

            var isActive = await queryService.IsPersonActiveAsync(created.PersonId);
            isActive.Should().BeFalse();

            var activeList = await queryService.ListActivePersonsAsync();
            activeList.Should().NotContain(p => p.PersonId == created.PersonId);
        });

        // Assert: Domain event PersonDeactivated
        TestDomainEventCollector.PublishedEvents
            .OfType<PersonDeactivated>()
            .Should().Contain(pd => pd.PersonId == created.PersonId);
    }

    [Fact]
    public async Task TeamAndMembership_CreateAndAssign_ShouldReturnTeamRoster()
    {
        // Arrange
        var person = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.CreatePersonAsync("Engineer Bob", $"bob.{Guid.NewGuid():N}@smart-ics.internal");
        });

        var teamName = $"Platform Squad {Guid.NewGuid():N}";
        var team = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.CreateTeamAsync(teamName, "Handles core platform infra");
        });

        // Act
        var membershipId = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.AssignPersonToTeamAsync(person.PersonId, team.TeamId);
        });

        // Assert
        membershipId.Should().NotBeEmpty();

        await ExecuteInScopeAsync(async sp =>
        {
            var queryService = sp.GetRequiredService<IOrganizationQueryService>();
            var roster = await queryService.GetTeamRosterAsync(team.TeamId);

            roster.Should().HaveCount(1);
            roster[0].PersonId.Should().Be(person.PersonId);
            roster[0].PersonName.Should().Be(person.Name);
            roster[0].PersonEmail.Should().Be(person.Email);
            roster[0].TeamId.Should().Be(team.TeamId);
            roster[0].IsActive.Should().BeTrue();
        });
    }

    [Fact]
    public async Task RoleAssignmentAndRevocation_ShouldEmitEvents_AndReflectInQueries()
    {
        // Arrange
        var person = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.CreatePersonAsync("Carol Dev", $"carol.{Guid.NewGuid():N}@smart-ics.internal");
        });

        var roleName = $"Specialist_{Guid.NewGuid():N}";
        var role = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.CreateRoleAsync(roleName, "Specialist Role");
        });

        // Act 1: Assign role
        var assignmentId = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.AssignRoleToPersonAsync(person.PersonId, role.RoleId);
        });

        assignmentId.Should().NotBeEmpty();

        // Assert 1: Query roles and event emission
        await ExecuteInScopeAsync(async sp =>
        {
            var queryService = sp.GetRequiredService<IOrganizationQueryService>();
            var roles = await queryService.GetPersonRolesAsync(person.PersonId);
            roles.Should().Contain(roleName);
        });

        TestDomainEventCollector.PublishedEvents
            .OfType<RoleAssigned>()
            .Should().Contain(ra => ra.PersonId == person.PersonId && ra.RoleName == roleName);

        // Act 2: Revoke role
        var revoked = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.RevokeRoleFromPersonAsync(person.PersonId, role.RoleId);
        });

        revoked.Should().BeTrue();

        // Assert 2: Role is no longer active and event emitted
        await ExecuteInScopeAsync(async sp =>
        {
            var queryService = sp.GetRequiredService<IOrganizationQueryService>();
            var roles = await queryService.GetPersonRolesAsync(person.PersonId);
            roles.Should().NotContain(roleName);
        });

        TestDomainEventCollector.PublishedEvents
            .OfType<RoleRevoked>()
            .Should().Contain(rr => rr.PersonId == person.PersonId && rr.RoleName == roleName);
    }

    [Fact]
    public async Task ResponsibilityAssignment_ShouldPersist_AndBeQueryable()
    {
        // Arrange
        var person = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.CreatePersonAsync("David Lead", $"david.{Guid.NewGuid():N}@smart-ics.internal");
        });

        var respName = $"Incident Command_{Guid.NewGuid():N}";
        var resp = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.CreateResponsibilityAsync(respName, "Operational Incident Lead");
        });

        // Act
        var assignmentId = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.AssignResponsibilityToPersonAsync(person.PersonId, resp.ResponsibilityId);
        });

        assignmentId.Should().NotBeEmpty();

        // Assert
        await ExecuteInScopeAsync(async sp =>
        {
            var queryService = sp.GetRequiredService<IOrganizationQueryService>();
            var responsibilities = await queryService.GetPersonResponsibilitiesAsync(person.PersonId);
            responsibilities.Should().Contain(respName);
        });
    }

    [Fact]
    public async Task DynamicRoleResolution_AuthorizationService_ShouldDelegateToOrganizationQueryService()
    {
        // Arrange: Create person, create roles, assign roles in database
        var person = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.CreatePersonAsync("Eve Manager", $"eve.{Guid.NewGuid():N}@smart-ics.internal");
        });

        var role1Name = $"RoleA_{Guid.NewGuid():N}";
        var role2Name = $"RoleB_{Guid.NewGuid():N}";

        var role1 = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.CreateRoleAsync(role1Name);
        });

        var role2 = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.CreateRoleAsync(role2Name);
        });

        await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            await orgService.AssignRoleToPersonAsync(person.PersonId, role1.RoleId);
            await orgService.AssignRoleToPersonAsync(person.PersonId, role2.RoleId);
        });

        // Act: Resolve roles using AuthorizationService in Identity module (delegates to OrganizationQueryService.GetPersonRoles)
        await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthorizationService>();
            var resolvedRoles = await authService.ResolveRolesAsync(person.PersonId);

            // Assert
            resolvedRoles.Should().Contain(role1Name);
            resolvedRoles.Should().Contain(role2Name);

            var hasRoleA = await authService.HasRoleAsync(person.PersonId, role1Name);
            hasRoleA.Should().BeTrue();

            var hasAnyRole = await authService.HasAnyRoleAsync(person.PersonId, new[] { "NonExistentRole", role2Name });
            hasAnyRole.Should().BeTrue();

            var syncResolved = authService.ResolveRoles(person.PersonId);
            syncResolved.Should().Contain(role1Name);
            syncResolved.Should().Contain(role2Name);
        });
    }

    [Fact]
    public async Task CreatePerson_WithInvalidInput_ShouldThrowValidationException()
    {
        // Act & Assert
        var act = async () =>
        {
            await ExecuteInScopeAsync(async sp =>
            {
                var orgService = sp.GetRequiredService<IOrganizationService>();
                await orgService.CreatePersonAsync("", "not-an-email");
            });
        };

        await act.Should().ThrowAsync<ValidationException>();
    }
}
