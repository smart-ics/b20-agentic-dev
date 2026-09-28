using Cakra.Core;

namespace Cakra.Modules.Organization.Domain.Events;

/// <summary>
/// Published when a new <see cref="Person"/> is registered in the organization.
/// </summary>
public sealed record PersonCreated(
    Guid PersonId,
    string FirstName,
    string LastName,
    string Email,
    Guid EventId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public PersonCreated(Guid personId, string firstName, string lastName, string email)
        : this(personId, firstName, lastName, email, Guid.NewGuid(), DateTime.UtcNow)
    {
    }
}
