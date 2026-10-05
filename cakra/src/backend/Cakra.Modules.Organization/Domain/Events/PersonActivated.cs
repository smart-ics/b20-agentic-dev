using Cakra.Core;

namespace Cakra.Modules.Organization.Domain.Events;

/// <summary>
/// Published when an existing <see cref="Person"/> is activated in the organization.
/// </summary>
public sealed record PersonActivated(
    Guid PersonId,
    Guid EventId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public PersonActivated(Guid personId)
        : this(personId, Guid.NewGuid(), DateTime.UtcNow)
    {
    }
}
