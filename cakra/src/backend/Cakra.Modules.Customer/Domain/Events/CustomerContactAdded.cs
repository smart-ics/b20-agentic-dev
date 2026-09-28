using Cakra.Core;

namespace Cakra.Modules.Customer.Domain.Events;

/// <summary>
/// Published when a new <see cref="CustomerContact"/> is added to a <see cref="Customer"/> (Domain §9).
/// </summary>
public sealed record CustomerContactAdded(
    Guid ContactId,
    Guid CustomerId,
    string Name,
    Guid EventId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public CustomerContactAdded(Guid contactId, Guid customerId, string name)
        : this(contactId, customerId, name, Guid.NewGuid(), DateTime.UtcNow)
    {
    }
}
