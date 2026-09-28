using Cakra.Core;

namespace Cakra.Modules.Organization.Domain.Events;

/// <summary>
/// Published when an organizational role assignment is revoked from a person.
/// </summary>
public sealed record RoleRevoked(
    Guid PersonId,
    Guid RoleId,
    DateTime RevokedAt,
    Guid EventId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public RoleRevoked(Guid personId, Guid roleId, DateTime revokedAt)
        : this(personId, roleId, revokedAt, Guid.NewGuid(), DateTime.UtcNow)
    {
    }
}
