namespace ICS.Core.Domain;

/// <summary>
/// Dispatcher contract for publishing domain events synchronously in-process.
/// Concrete implementation is provided in P1-S05.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Publishes a single domain event to all registered notification handlers.
    /// </summary>
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a collection of domain events in sequential order.
    /// </summary>
    Task PublishAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a domain event (alias for PublishAsync).
    /// </summary>
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        => PublishAsync(domainEvent, cancellationToken);

    /// <summary>
    /// Dispatches a collection of domain events (alias for PublishAsync).
    /// </summary>
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        => PublishAsync(domainEvents, cancellationToken);
}
