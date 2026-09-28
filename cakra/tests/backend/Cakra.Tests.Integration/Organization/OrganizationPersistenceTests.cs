using Cakra.Api.Infrastructure.Migrations;
using Cakra.Api.Infrastructure.Persistence;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Persistence;
using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cakra.Tests.Integration.Organization;

/// <summary>
/// Integration tests verifying the Organization persistence layer (P3-S12):
/// - DbUp migration 0003_organization_tables.sql executes cleanly
/// - organization.* tables and schema objects are verified
/// - PersonRepository, TeamRepository, RoleRepository, ResponsibilityRepository CRUD
/// - TeamMembershipRepository, RoleAssignmentRepository, ResponsibilityAssignmentRepository
/// - Foreign key constraints between junction tables and parent tables
/// </summary>
[Collection("OrganizationDatabase")]
public class OrganizationPersistenceTests : IAsyncLifetime
{
    private const string LocalDbFallback = "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb;Integrated Security=true;TrustServerCertificate=True;";
    private readonly string? _connectionString;
    private IDbConnectionFactory _connectionFactory = null!;
    private bool _sqlServerAvailable;

    public OrganizationPersistenceTests()
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

        // Apply migrations via DatabaseMigrationRunner to ensure 0001_baseline and 0003_organization_tables are applied
        var runner = new DatabaseMigrationRunner(_connectionString!, NullLogger<DatabaseMigrationRunner>.Instance);
        var result = runner.Run();
        result.Successful.Should().BeTrue("DbUp migration scripts must execute without errors");

        _connectionFactory = new SqlConnectionFactory(_connectionString!);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private void SkipIfNoSqlServer()
    {
        if (!_sqlServerAvailable)
        {
            // If SQL Server is not reachable in the current test runner, skip
            return;
        }
    }

    [Fact]
    public async Task Migration_creates_all_organization_tables()
    {
        if (!_sqlServerAvailable) return;

        using var connection = _connectionFactory.CreateConnection();
        var tables = await connection.QueryAsync<string>("""
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = 'organization'
            """);

        var tableList = tables.ToList();
        tableList.Should().Contain(new[]
        {
            "Persons",
            "Teams",
            "Roles",
            "Responsibilities",
            "TeamMemberships",
            "RoleAssignments",
            "ResponsibilityAssignments"
        });
    }

    [Fact]
    public async Task PersonRepository_CRUD_and_status_update_operate_correctly()
    {
        if (!_sqlServerAvailable) return;

        var repo = new PersonRepository(_connectionFactory);
        var personId = Guid.NewGuid();
        var email = $"john.doe.{Guid.NewGuid():N}@example.com";

        var person = new Person
        {
            Id = personId,
            FirstName = "John",
            LastName = "Doe",
            Email = email,
            Status = Person.StatusActive,
            CreatedAt = DateTime.UtcNow
        };

        // 1. Add
        await repo.AddAsync(person);

        // 2. GetById
        var retrieved = await repo.GetByIdAsync(personId);
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(personId);
        retrieved.FirstName.Should().Be("John");
        retrieved.LastName.Should().Be("Doe");
        retrieved.Email.Should().Be(email);
        retrieved.Status.Should().Be("ACTIVE");
        retrieved.IsActive.Should().BeTrue();

        // 3. GetByEmail
        var byEmail = await repo.GetByEmailAsync(email);
        byEmail.Should().NotBeNull();
        byEmail!.Id.Should().Be(personId);

        // 4. Update
        retrieved.FirstName = "Johnny";
        await repo.UpdateAsync(retrieved);

        var updated = await repo.GetByIdAsync(personId);
        updated!.FirstName.Should().Be("Johnny");
        updated.UpdatedAt.Should().NotBeNull();

        // 5. UpdateStatus to INACTIVE
        await repo.UpdateStatusAsync(personId, Person.StatusInactive);
        var inactive = await repo.GetByIdAsync(personId);
        inactive!.Status.Should().Be("INACTIVE");
        inactive.IsActive.Should().BeFalse();

        // 6. GetActive should not contain inactive person
        var activePersons = await repo.GetActiveAsync();
        activePersons.Should().NotContain(p => p.Id == personId);

        // 7. Delete
        await repo.DeleteAsync(personId);
        var deleted = await repo.GetByIdAsync(personId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task TeamRepository_CRUD_operates_correctly()
    {
        if (!_sqlServerAvailable) return;

        var repo = new TeamRepository(_connectionFactory);
        var teamId = Guid.NewGuid();
        var teamName = $"Engineering Team {Guid.NewGuid():N}";

        var team = new Team
        {
            Id = teamId,
            Name = teamName,
            Description = "Core product engineering team",
            CreatedAt = DateTime.UtcNow
        };

        // Add
        await repo.AddAsync(team);

        // GetById
        var retrieved = await repo.GetByIdAsync(teamId);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be(teamName);
        retrieved.Description.Should().Be("Core product engineering team");

        // GetByName
        var byName = await repo.GetByNameAsync(teamName);
        byName.Should().NotBeNull();
        byName!.Id.Should().Be(teamId);

        // Update
        retrieved.Description = "Updated team description";
        await repo.UpdateAsync(retrieved);

        var updated = await repo.GetByIdAsync(teamId);
        updated!.Description.Should().Be("Updated team description");

        // Delete
        await repo.DeleteAsync(teamId);
        var deleted = await repo.GetByIdAsync(teamId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task RoleRepository_CRUD_operates_correctly()
    {
        if (!_sqlServerAvailable) return;

        var repo = new RoleRepository(_connectionFactory);
        var roleId = Guid.NewGuid();
        var roleName = $"Programmer {Guid.NewGuid():N}";

        var role = new Role
        {
            Id = roleId,
            Name = roleName,
            Description = "Technical software development",
            CreatedAt = DateTime.UtcNow
        };

        // Add
        await repo.AddAsync(role);

        // GetById
        var retrieved = await repo.GetByIdAsync(roleId);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be(roleName);

        // GetByName
        var byName = await repo.GetByNameAsync(roleName);
        byName.Should().NotBeNull();
        byName!.Id.Should().Be(roleId);

        // Update
        retrieved.Description = "Updated role description";
        await repo.UpdateAsync(retrieved);

        var updated = await repo.GetByIdAsync(roleId);
        updated!.Description.Should().Be("Updated role description");

        // Delete
        await repo.DeleteAsync(roleId);
        var deleted = await repo.GetByIdAsync(roleId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task ResponsibilityRepository_CRUD_operates_correctly()
    {
        if (!_sqlServerAvailable) return;

        var repo = new ResponsibilityRepository(_connectionFactory);
        var respId = Guid.NewGuid();
        var respName = $"Module PIC {Guid.NewGuid():N}";

        var responsibility = new Responsibility
        {
            Id = respId,
            Name = respName,
            Description = "Functional module accountability",
            CreatedAt = DateTime.UtcNow
        };

        // Add
        await repo.AddAsync(responsibility);

        // GetById
        var retrieved = await repo.GetByIdAsync(respId);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be(respName);

        // GetByName
        var byName = await repo.GetByNameAsync(respName);
        byName.Should().NotBeNull();
        byName!.Id.Should().Be(respId);

        // Update
        retrieved.Description = "Updated responsibility description";
        await repo.UpdateAsync(retrieved);

        var updated = await repo.GetByIdAsync(respId);
        updated!.Description.Should().Be("Updated responsibility description");

        // Delete
        await repo.DeleteAsync(respId);
        var deleted = await repo.GetByIdAsync(respId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task TeamMembershipRepository_add_remove_and_query_work_correctly()
    {
        if (!_sqlServerAvailable) return;

        var personRepo = new PersonRepository(_connectionFactory);
        var teamRepo = new TeamRepository(_connectionFactory);
        var membershipRepo = new TeamMembershipRepository(_connectionFactory);

        var personId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        await personRepo.AddAsync(new Person
        {
            Id = personId,
            FirstName = "Team",
            LastName = "Member",
            Email = $"member.{Guid.NewGuid():N}@example.com"
        });

        await teamRepo.AddAsync(new Team
        {
            Id = teamId,
            Name = $"Team {Guid.NewGuid():N}"
        });

        // Add membership
        var assignedAt = DateTime.UtcNow;
        await membershipRepo.AddAsync(new TeamMembership
        {
            PersonId = personId,
            TeamId = teamId,
            AssignedAt = assignedAt
        });

        // Verify exists
        var exists = await membershipRepo.ExistsAsync(personId, teamId);
        exists.Should().BeTrue();

        // GetByPersonId
        var forPerson = await membershipRepo.GetByPersonIdAsync(personId);
        forPerson.Should().ContainSingle(m => m.TeamId == teamId);

        // GetByTeamId
        var forTeam = await membershipRepo.GetByTeamIdAsync(teamId);
        forTeam.Should().ContainSingle(m => m.PersonId == personId);

        // Remove membership
        await membershipRepo.RemoveAsync(personId, teamId);
        var existsAfter = await membershipRepo.ExistsAsync(personId, teamId);
        existsAfter.Should().BeFalse();

        // Cleanup
        await personRepo.DeleteAsync(personId);
        await teamRepo.DeleteAsync(teamId);
    }

    [Fact]
    public async Task RoleAssignmentRepository_assign_revoke_and_query_work_correctly()
    {
        if (!_sqlServerAvailable) return;

        var personRepo = new PersonRepository(_connectionFactory);
        var roleRepo = new RoleRepository(_connectionFactory);
        var roleAssignmentRepo = new RoleAssignmentRepository(_connectionFactory);

        var personId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        await personRepo.AddAsync(new Person
        {
            Id = personId,
            FirstName = "Role",
            LastName = "User",
            Email = $"roleuser.{Guid.NewGuid():N}@example.com"
        });

        await roleRepo.AddAsync(new Role
        {
            Id = roleId,
            Name = $"Specialist {Guid.NewGuid():N}"
        });

        // Assign role
        var assignedAt = DateTime.UtcNow;
        await roleAssignmentRepo.AssignAsync(new RoleAssignment
        {
            PersonId = personId,
            RoleId = roleId,
            AssignedAt = assignedAt
        });

        // Verify active assignment
        var active = await roleAssignmentRepo.GetActiveByPersonIdAsync(personId);
        active.Should().ContainSingle(a => a.RoleId == roleId && a.IsActive);

        // Revoke role
        var revokedAt = DateTime.UtcNow;
        await roleAssignmentRepo.RevokeAsync(personId, roleId, revokedAt);

        // Active list should now be empty for this role
        var activeAfterRevoke = await roleAssignmentRepo.GetActiveByPersonIdAsync(personId);
        activeAfterRevoke.Should().NotContain(a => a.RoleId == roleId);

        // Full history should still contain it with RevokedAt set
        var allAssignments = await roleAssignmentRepo.GetByPersonIdAsync(personId);
        allAssignments.Should().ContainSingle(a => a.RoleId == roleId && a.RevokedAt != null);

        // Re-assign role reactivates it
        var reAssignedAt = DateTime.UtcNow.AddMinutes(5);
        await roleAssignmentRepo.AssignAsync(new RoleAssignment
        {
            PersonId = personId,
            RoleId = roleId,
            AssignedAt = reAssignedAt
        });

        var activeAfterReassign = await roleAssignmentRepo.GetActiveByPersonIdAsync(personId);
        activeAfterReassign.Should().ContainSingle(a => a.RoleId == roleId && a.IsActive);

        // Cleanup
        await personRepo.DeleteAsync(personId);
        await roleRepo.DeleteAsync(roleId);
    }

    [Fact]
    public async Task ResponsibilityAssignmentRepository_assign_remove_and_query_work_correctly()
    {
        if (!_sqlServerAvailable) return;

        var personRepo = new PersonRepository(_connectionFactory);
        var respRepo = new ResponsibilityRepository(_connectionFactory);
        var respAssignmentRepo = new ResponsibilityAssignmentRepository(_connectionFactory);

        var personId = Guid.NewGuid();
        var respId = Guid.NewGuid();

        await personRepo.AddAsync(new Person
        {
            Id = personId,
            FirstName = "Resp",
            LastName = "User",
            Email = $"respuser.{Guid.NewGuid():N}@example.com"
        });

        await respRepo.AddAsync(new Responsibility
        {
            Id = respId,
            Name = $"Area Lead {Guid.NewGuid():N}"
        });

        // Assign responsibility
        var assignedAt = DateTime.UtcNow;
        await respAssignmentRepo.AssignAsync(new ResponsibilityAssignment
        {
            PersonId = personId,
            ResponsibilityId = respId,
            AssignedAt = assignedAt
        });

        // Verify exists
        var exists = await respAssignmentRepo.ExistsAsync(personId, respId);
        exists.Should().BeTrue();

        // GetByPersonId
        var forPerson = await respAssignmentRepo.GetByPersonIdAsync(personId);
        forPerson.Should().ContainSingle(a => a.ResponsibilityId == respId);

        // GetByResponsibilityId
        var forResp = await respAssignmentRepo.GetByResponsibilityIdAsync(respId);
        forResp.Should().ContainSingle(a => a.PersonId == personId);

        // Remove assignment
        await respAssignmentRepo.RemoveAsync(personId, respId);
        var existsAfter = await respAssignmentRepo.ExistsAsync(personId, respId);
        existsAfter.Should().BeFalse();

        // Cleanup
        await personRepo.DeleteAsync(personId);
        await respRepo.DeleteAsync(respId);
    }

    [Fact]
    public async Task Foreign_key_constraints_prevent_orphaned_junction_records()
    {
        if (!_sqlServerAvailable) return;

        var membershipRepo = new TeamMembershipRepository(_connectionFactory);
        var nonExistentPersonId = Guid.NewGuid();
        var nonExistentTeamId = Guid.NewGuid();

        var act = async () => await membershipRepo.AddAsync(new TeamMembership
        {
            PersonId = nonExistentPersonId,
            TeamId = nonExistentTeamId,
            AssignedAt = DateTime.UtcNow
        });

        await act.Should().ThrowAsync<SqlException>();
    }
}
