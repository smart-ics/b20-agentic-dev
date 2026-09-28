using Cakra.Core;

namespace Cakra.Modules.Customer.Domain.Events;

/// <summary>
/// Published when a <see cref="Customer"/> transitions to <c>ACTIVE</c> status (Domain §9).
/// </summary>
public sealed record CustomerActivated(
    Guid CustomerId,
    Guid EventId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public CustomerActivated(Guid customerId)
        : this(customerId, Guid.NewGuid(), DateTime.UtcNow)
    {
    }
}
