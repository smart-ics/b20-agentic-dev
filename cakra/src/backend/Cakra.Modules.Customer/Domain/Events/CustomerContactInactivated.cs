using Cakra.Core;

namespace Cakra.Modules.Customer.Domain.Events;

/// <summary>
/// Published when a <see cref="CustomerContact"/> transitions to <c>INACTIVE</c> status (Domain §9).
/// </summary>
public sealed record CustomerContactInactivated(
    Guid ContactId,
    Guid CustomerId,
    Guid EventId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public CustomerContactInactivated(Guid contactId, Guid customerId)
        : this(contactId, customerId, Guid.NewGuid(), DateTime.UtcNow)
    {
    }
}
