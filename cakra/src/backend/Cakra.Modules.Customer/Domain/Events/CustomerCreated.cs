using Cakra.Core;

namespace Cakra.Modules.Customer.Domain.Events;

/// <summary>
/// Published when a new <see cref="Customer"/> is registered (Domain §9).
/// </summary>
public sealed record CustomerCreated(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    bool HasActiveMaintenanceContract,
    Guid EventId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public CustomerCreated(
        Guid customerId,
        string customerCode,
        string customerName,
        bool hasActiveMaintenanceContract = false)
        : this(customerId, customerCode, customerName, hasActiveMaintenanceContract, Guid.NewGuid(), DateTime.UtcNow)
    {
    }
}
