namespace ICS.Tests.Unit;

using System;
using System.Linq;
using FluentAssertions;
using ICS.Modules.Organization.Domain;
using ICS.Modules.Organization.Domain.Events;
using Xunit;

/// <summary>
/// Unit tests verifying domain model invariants and domain event emission in Organization module.
/// Architecture §6, §7, §9, §17.
/// </summary>
public class OrganizationDomainTests
{
    [Fact]
    public void Person_Create_ShouldInitializeCorrectly_AndEmitPersonCreatedEvent()
    {
        // Arrange
        var personId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var person = Person.Create(personId, "Alice Smith", "alice@smart-ics.internal", now);

        // Assert
        person.Id.Should().Be(personId);
        person.Name.Should().Be("Alice Smith");
        person.Email.Should().Be("alice@smart-ics.internal");
        person.Status.Should().Be(Person.StatusActive);
        person.IsActive.Should().BeTrue();
        person.CreatedAt.Should().Be(now);
        person.UpdatedAt.Should().BeNull();

        person.DomainEvents.Should().HaveCount(1);
        var createdEvent = person.DomainEvents.First() as PersonCreated;
        createdEvent.Should().NotBeNull();
        createdEvent!.PersonId.Should().Be(personId);
        createdEvent.Name.Should().Be("Alice Smith");
        createdEvent.Email.Should().Be("alice@smart-ics.internal");
    }

    [Theory]
    [InlineData("", "alice@example.com")]
    [InlineData("   ", "alice@example.com")]
    [InlineData("Alice", "")]
    [InlineData("Alice", "   ")]
    public void Person_Create_WithInvalidArguments_ShouldThrowArgumentException(string name, string email)
    {
        var act = () => Person.Create(Guid.NewGuid(), name, email, DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Person_Update_ShouldUpdateAttributes()
    {
        // Arrange
        var person = Person.Create(Guid.NewGuid(), "Alice Smith", "alice@smart-ics.internal", DateTime.UtcNow);
        var updateTime = DateTime.UtcNow.AddMinutes(5);

        // Act
        person.Update("Alice Johnson", "alice.j@smart-ics.internal", updateTime);

        // Assert
        person.Name.Should().Be("Alice Johnson");
        person.Email.Should().Be("alice.j@smart-ics.internal");
        person.UpdatedAt.Should().Be(updateTime);
    }

    [Fact]
    public void Person_Deactivate_ShouldTransitionStatus_AndEmitPersonDeactivatedEvent()
    {
        // Arrange
        var person = Person.Create(Guid.NewGuid(), "Alice Smith", "alice@smart-ics.internal", DateTime.UtcNow);
        person.ClearDomainEvents();
        var deactivateTime = DateTime.UtcNow.AddMinutes(10);

        // Act
        person.Deactivate(deactivateTime);

        // Assert
        person.Status.Should().Be(Person.StatusInactive);
        person.IsActive.Should().BeFalse();
        person.UpdatedAt.Should().Be(deactivateTime);

        person.DomainEvents.Should().HaveCount(1);
        var deactivatedEvent = person.DomainEvents.First() as PersonDeactivated;
        deactivatedEvent.Should().NotBeNull();
        deactivatedEvent!.PersonId.Should().Be(person.Id);
        deactivatedEvent.Name.Should().Be("Alice Smith");
    }

    [Fact]
    public void Team_Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var team = Team.Create(teamId, "Core Development", "Core platform engineering", now);

        // Assert
        team.Id.Should().Be(teamId);
        team.Name.Should().Be("Core Development");
        team.Description.Should().Be("Core platform engineering");
        team.Status.Should().Be(Team.StatusActive);
        team.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Role_Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var role = Role.Create(roleId, "Management", "Executive oversight", now);

        // Assert
        role.Id.Should().Be(roleId);
        role.Name.Should().Be("Management");
        role.Status.Should().Be(Role.StatusActive);
        role.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Responsibility_Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var respId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var resp = Responsibility.Create(respId, "Product Ownership", "Accountable for product definition", now);

        // Assert
        resp.Id.Should().Be(respId);
        resp.Name.Should().Be("Product Ownership");
        resp.Status.Should().Be(Responsibility.StatusActive);
        resp.IsActive.Should().BeTrue();
    }

    [Fact]
    public void TeamMembership_Create_And_EndMembership_ShouldManageLifecycle()
    {
        // Arrange
        var membershipId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var joinedAt = DateTime.UtcNow;

        // Act
        var membership = TeamMembership.Create(membershipId, personId, teamId, joinedAt);

        // Assert
        membership.Id.Should().Be(membershipId);
        membership.PersonId.Should().Be(personId);
        membership.TeamId.Should().Be(teamId);
        membership.JoinedAt.Should().Be(joinedAt);
        membership.IsActive.Should().BeTrue();
        membership.DomainEvents.Should().ContainSingle(e => e is TeamMemberAssigned);

        // Act 2: End membership
        var leftAt = joinedAt.AddDays(30);
        membership.EndMembership(leftAt);

        // Assert 2
        membership.IsActive.Should().BeFalse();
        membership.LeftAt.Should().Be(leftAt);
    }

    [Fact]
    public void RoleAssignment_Create_And_Revoke_ShouldEmitRoleAssigned_And_RoleRevokedEvents()
    {
        // Arrange
        var assignmentId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var assignedAt = DateTime.UtcNow;

        // Act
        var assignment = RoleAssignment.Create(assignmentId, personId, roleId, "Programmer", assignedAt);

        // Assert
        assignment.Id.Should().Be(assignmentId);
        assignment.PersonId.Should().Be(personId);
        assignment.RoleId.Should().Be(roleId);
        assignment.IsActive.Should().BeTrue();

        assignment.DomainEvents.Should().HaveCount(1);
        var assignedEvent = assignment.DomainEvents.First() as RoleAssigned;
        assignedEvent.Should().NotBeNull();
        assignedEvent!.RoleName.Should().Be("Programmer");

        // Act 2: Revoke
        assignment.ClearDomainEvents();
        var revokedAt = assignedAt.AddDays(7);
        assignment.Revoke("Programmer", revokedAt);

        // Assert 2
        assignment.IsActive.Should().BeFalse();
        assignment.RevokedAt.Should().Be(revokedAt);

        assignment.DomainEvents.Should().HaveCount(1);
        var revokedEvent = assignment.DomainEvents.First() as RoleRevoked;
        revokedEvent.Should().NotBeNull();
        revokedEvent!.RoleName.Should().Be("Programmer");
    }

    [Fact]
    public void ResponsibilityAssignment_Create_And_Revoke_ShouldManageLifecycle()
    {
        // Arrange
        var assignmentId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var respId = Guid.NewGuid();
        var assignedAt = DateTime.UtcNow;

        // Act
        var assignment = ResponsibilityAssignment.Create(assignmentId, personId, respId, "Module PIC", assignedAt);

        // Assert
        assignment.IsActive.Should().BeTrue();
        assignment.DomainEvents.Should().ContainSingle(e => e is ResponsibilityAssigned);

        // Act 2: Revoke
        var revokedAt = assignedAt.AddDays(14);
        assignment.Revoke(revokedAt);

        // Assert 2
        assignment.IsActive.Should().BeFalse();
        assignment.RevokedAt.Should().Be(revokedAt);
    }
}
