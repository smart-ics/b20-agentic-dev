namespace Cakra.Core;

/// <summary>
/// Publishes domain events to the in-process event bus. The concrete
/// implementation is supplied by P1-S05.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Publishes a domain event to all registered handlers within the current
    /// transactional scope.
    /// </summary>
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
