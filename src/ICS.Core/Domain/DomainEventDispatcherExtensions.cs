namespace ICS.Core.Domain;

/// <summary>
/// Extension methods for <see cref="IDomainEventDispatcher"/> supporting entity lifecycle event dispatching.
/// </summary>
public static class DomainEventDispatcherExtensions
{
    /// <summary>
    /// Dispatches all pending domain events from the entity and clears its pending event collection.
    /// Executes synchronously within the current transaction scope per Architecture §12 and §18.
    /// </summary>
    /// <typeparam name="TId">The type of the entity identifier.</typeparam>
    /// <param name="dispatcher">The domain event dispatcher instance.</param>
    /// <param name="entity">The entity whose events should be dispatched.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task DispatchAndClearEventsAsync<TId>(
        this IDomainEventDispatcher dispatcher,
        Entity<TId> entity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(entity);

        var events = entity.DomainEvents.ToList();
        if (events.Count == 0)
        {
            return;
        }

        entity.ClearDomainEvents();
        await dispatcher.PublishAsync(events, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Dispatches all pending domain events from a sequence of entities and clears their pending event collections.
    /// </summary>
    /// <typeparam name="TId">The type of the entity identifier.</typeparam>
    /// <param name="dispatcher">The domain event dispatcher instance.</param>
    /// <param name="entities">The sequence of entities whose events should be dispatched.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task DispatchAndClearEventsAsync<TId>(
        this IDomainEventDispatcher dispatcher,
        IEnumerable<Entity<TId>> entities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(entities);

        foreach (var entity in entities)
        {
            if (entity is not null)
            {
                await dispatcher.DispatchAndClearEventsAsync(entity, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
