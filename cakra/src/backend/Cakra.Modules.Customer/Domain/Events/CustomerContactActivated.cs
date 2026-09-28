using Cakra.Core;

namespace Cakra.Modules.Customer.Domain.Events;

/// <summary>
/// Published when a <see cref="CustomerContact"/> transitions to <c>ACTIVE</c> status (Domain §9).
/// </summary>
public sealed record CustomerContactActivated(
    Guid ContactId,
    Guid CustomerId,
    Guid EventId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public CustomerContactActivated(Guid contactId, Guid customerId)
        : this(contactId, customerId, Guid.NewGuid(), DateTime.UtcNow)
    {
    }
}
