using Cakra.Core;

namespace Cakra.Modules.Organization.Domain.Events;

/// <summary>
/// Published when an organizational role is assigned to a person.
/// </summary>
public sealed record RoleAssigned(
    Guid PersonId,
    Guid RoleId,
    DateTime AssignedAt,
    Guid EventId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public RoleAssigned(Guid personId, Guid roleId, DateTime assignedAt)
        : this(personId, roleId, assignedAt, Guid.NewGuid(), DateTime.UtcNow)
    {
    }
}
