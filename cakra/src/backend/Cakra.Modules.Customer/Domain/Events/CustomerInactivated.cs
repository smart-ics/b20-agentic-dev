using Cakra.Core;

namespace Cakra.Modules.Customer.Domain.Events;

/// <summary>
/// Published when a <see cref="Customer"/> transitions to <c>INACTIVE</c> status (Domain §9).
/// </summary>
public sealed record CustomerInactivated(
    Guid CustomerId,
    Guid EventId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public CustomerInactivated(Guid customerId)
        : this(customerId, Guid.NewGuid(), DateTime.UtcNow)
    {
    }
}

/// <summary>
/// Published when a <see cref="Customer"/> is deactivated.
/// </summary>
public sealed record CustomerDeactivated(
    Guid CustomerId,
    Guid EventId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public CustomerDeactivated(Guid customerId)
        : this(customerId, Guid.NewGuid(), DateTime.UtcNow)
    {
    }
}
