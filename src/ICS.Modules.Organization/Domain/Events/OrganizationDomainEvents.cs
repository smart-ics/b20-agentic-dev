namespace ICS.Modules.Organization.Domain.Events;

using ICS.Core.Domain;

/// <summary>
/// Domain event emitted when a new Person is created in the organization.
/// Architecture §9, §18.
/// </summary>
public sealed record PersonCreated(Guid PersonId, string Name, string Email) : DomainEvent;

/// <summary>
/// Domain event emitted when a Person is deactivated in the organization.
/// Architecture §9, §18.
/// </summary>
public sealed record PersonDeactivated(Guid PersonId, string Name) : DomainEvent;

/// <summary>
/// Domain event emitted when a Role is assigned to a Person.
/// Architecture §9, §18.
/// </summary>
public sealed record RoleAssigned(Guid AssignmentId, Guid PersonId, Guid RoleId, string RoleName) : DomainEvent;

/// <summary>
/// Domain event emitted when a Role assignment is revoked from a Person.
/// Architecture §9, §18.
/// </summary>
public sealed record RoleRevoked(Guid AssignmentId, Guid PersonId, Guid RoleId, string RoleName) : DomainEvent;

/// <summary>
/// Domain event emitted when a Person is assigned to a Team.
/// Architecture §9, §18.
/// </summary>
public sealed record TeamMemberAssigned(Guid MembershipId, Guid PersonId, Guid TeamId) : DomainEvent;

/// <summary>
/// Domain event emitted when a Responsibility is assigned to a Person.
/// Architecture §9, §18.
/// </summary>
public sealed record ResponsibilityAssigned(Guid AssignmentId, Guid PersonId, Guid ResponsibilityId, string ResponsibilityName) : DomainEvent;
