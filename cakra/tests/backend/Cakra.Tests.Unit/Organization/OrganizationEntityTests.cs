using Cakra.Modules.Organization.Domain;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Organization;

public class OrganizationEntityTests
{
    [Fact]
    public void Person_initializes_with_default_active_status_and_correct_properties()
    {
        var id = Guid.NewGuid();
        var person = new Person
        {
            Id = id,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com"
        };

        person.Id.Should().Be(id);
        person.FirstName.Should().Be("John");
        person.LastName.Should().Be("Doe");
        person.Email.Should().Be("john.doe@example.com");
        person.Status.Should().Be("ACTIVE");
        person.IsActive.Should().BeTrue();
        person.FullName.Should().Be("John Doe");
        person.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Person_FullName_handles_empty_or_whitespace_last_name()
    {
        var person = new Person
        {
            FirstName = "Alice",
            LastName = ""
        };

        person.FullName.Should().Be("Alice");
    }

    [Fact]
    public void Person_Deactivate_and_Activate_transitions_status()
    {
        var person = new Person
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com"
        };

        person.IsActive.Should().BeTrue();

        var deactivatedAt = DateTime.UtcNow;
        person.Deactivate(deactivatedAt);

        person.Status.Should().Be(Person.StatusInactive);
        person.IsActive.Should().BeFalse();
        person.UpdatedAt.Should().Be(deactivatedAt);

        var activatedAt = DateTime.UtcNow.AddMinutes(5);
        person.Activate(activatedAt);

        person.Status.Should().Be(Person.StatusActive);
        person.IsActive.Should().BeTrue();
        person.UpdatedAt.Should().Be(activatedAt);
    }

    [Fact]
    public void Team_initializes_with_correct_properties()
    {
        var id = Guid.NewGuid();
        var team = new Team
        {
            Id = id,
            Name = "Core Engineering",
            Description = "Backend and platform infrastructure team"
        };

        team.Id.Should().Be(id);
        team.Name.Should().Be("Core Engineering");
        team.Description.Should().Be("Backend and platform infrastructure team");
    }

    [Fact]
    public void Role_initializes_with_correct_properties()
    {
        var id = Guid.NewGuid();
        var role = new Role
        {
            Id = id,
            Name = "Programmer",
            Description = "Software development and operational support"
        };

        role.Id.Should().Be(id);
        role.Name.Should().Be("Programmer");
        role.Description.Should().Be("Software development and operational support");
    }

    [Fact]
    public void Responsibility_initializes_with_correct_properties()
    {
        var id = Guid.NewGuid();
        var responsibility = new Responsibility
        {
            Id = id,
            Name = "Module PIC",
            Description = "Primary contact and lead for a functional module"
        };

        responsibility.Id.Should().Be(id);
        responsibility.Name.Should().Be("Module PIC");
        responsibility.Description.Should().Be("Primary contact and lead for a functional module");
    }

    [Fact]
    public void TeamMembership_initializes_with_correct_properties()
    {
        var personId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var assignedAt = DateTime.UtcNow;

        var membership = new TeamMembership
        {
            PersonId = personId,
            TeamId = teamId,
            AssignedAt = assignedAt
        };

        membership.PersonId.Should().Be(personId);
        membership.TeamId.Should().Be(teamId);
        membership.AssignedAt.Should().Be(assignedAt);
    }

    [Fact]
    public void RoleAssignment_initializes_and_tracks_active_status_and_revocation()
    {
        var personId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var assignedAt = DateTime.UtcNow;

        var assignment = new RoleAssignment
        {
            PersonId = personId,
            RoleId = roleId,
            AssignedAt = assignedAt
        };

        assignment.PersonId.Should().Be(personId);
        assignment.RoleId.Should().Be(roleId);
        assignment.AssignedAt.Should().Be(assignedAt);
        assignment.RevokedAt.Should().BeNull();
        assignment.IsActive.Should().BeTrue();

        var revokedAt = DateTime.UtcNow.AddDays(30);
        assignment.Revoke(revokedAt);

        assignment.RevokedAt.Should().Be(revokedAt);
        assignment.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ResponsibilityAssignment_initializes_with_correct_properties()
    {
        var personId = Guid.NewGuid();
        var respId = Guid.NewGuid();
        var assignedAt = DateTime.UtcNow;

        var assignment = new ResponsibilityAssignment
        {
            PersonId = personId,
            ResponsibilityId = respId,
            AssignedAt = assignedAt
        };

        assignment.PersonId.Should().Be(personId);
        assignment.ResponsibilityId.Should().Be(respId);
        assignment.AssignedAt.Should().Be(assignedAt);
    }
}
