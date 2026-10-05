using Cakra.Core;
using Cakra.Modules.Organization.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Organization;

public class OrganizationDomainEventTests
{
    [Fact]
    public void PersonCreated_populates_event_properties_and_implements_IDomainEvent()
    {
        var personId = Guid.NewGuid();
        var evt = new PersonCreated(personId, "Alice", "Smith", "alice@example.com");

        evt.Should().BeAssignableTo<IDomainEvent>();
        evt.PersonId.Should().Be(personId);
        evt.FirstName.Should().Be("Alice");
        evt.LastName.Should().Be("Smith");
        evt.Email.Should().Be("alice@example.com");
        evt.EventId.Should().NotBeEmpty();
        evt.OccurredAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PersonDeactivated_populates_event_properties()
    {
        var personId = Guid.NewGuid();
        var evt = new PersonDeactivated(personId);

        evt.Should().BeAssignableTo<IDomainEvent>();
        evt.PersonId.Should().Be(personId);
        evt.EventId.Should().NotBeEmpty();
        evt.OccurredAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PersonActivated_populates_event_properties()
    {
        var personId = Guid.NewGuid();
        var evt = new PersonActivated(personId);

        evt.Should().BeAssignableTo<IDomainEvent>();
        evt.PersonId.Should().Be(personId);
        evt.EventId.Should().NotBeEmpty();
        evt.OccurredAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RoleAssigned_populates_event_properties()
    {
        var personId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var assignedAt = DateTime.UtcNow;
        var evt = new RoleAssigned(personId, roleId, assignedAt);

        evt.Should().BeAssignableTo<IDomainEvent>();
        evt.PersonId.Should().Be(personId);
        evt.RoleId.Should().Be(roleId);
        evt.AssignedAt.Should().Be(assignedAt);
        evt.EventId.Should().NotBeEmpty();
        evt.OccurredAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RoleRevoked_populates_event_properties()
    {
        var personId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var revokedAt = DateTime.UtcNow;
        var evt = new RoleRevoked(personId, roleId, revokedAt);

        evt.Should().BeAssignableTo<IDomainEvent>();
        evt.PersonId.Should().Be(personId);
        evt.RoleId.Should().Be(roleId);
        evt.RevokedAt.Should().Be(revokedAt);
        evt.EventId.Should().NotBeEmpty();
        evt.OccurredAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
