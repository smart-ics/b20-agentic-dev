using Cakra.Core;

namespace Cakra.Modules.Organization.Domain.Events;

/// <summary>
/// Published when an existing <see cref="Person"/> is deactivated in the organization.
/// </summary>
public sealed record PersonDeactivated(
    Guid PersonId,
    Guid EventId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public PersonDeactivated(Guid personId)
        : this(personId, Guid.NewGuid(), DateTime.UtcNow)
    {
    }
}
