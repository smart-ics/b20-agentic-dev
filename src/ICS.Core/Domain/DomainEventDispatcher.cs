using MediatR;
using Microsoft.Extensions.Logging;

namespace ICS.Core.Domain;

/// <summary>
/// In-process synchronous domain event dispatcher providing transactional consistency.
/// Implements <see cref="IDomainEventDispatcher"/> using MediatR's <see cref="IPublisher"/>.
/// Per Architecture §12 (Synchronization Guarantee), §18 (Domain Event Bus), and §19.2 (MediatR).
/// </summary>
public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IPublisher _publisher;
    private readonly ILogger<DomainEventDispatcher>? _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="DomainEventDispatcher"/>.
    /// </summary>
    /// <param name="publisher">The MediatR publisher used for synchronous in-process event notification.</param>
    /// <param name="logger">Optional logger for operational diagnostics.</param>
    public DomainEventDispatcher(IPublisher publisher, ILogger<DomainEventDispatcher>? logger = null)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        _logger?.LogDebug(
            "Publishing in-process domain event {EventType} (EventId: {EventId}, OccurredAt: {OccurredAt:O})",
            domainEvent.GetType().Name,
            domainEvent.EventId,
            domainEvent.OccurredAt);

        // Publish using the runtime object notification to ensure MediatR resolves
        // handlers registered for the concrete event type
        await _publisher.Publish((object)domainEvent, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PublishAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            if (domainEvent is not null)
            {
                await PublishAsync(domainEvent, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        => PublishAsync(domainEvent, cancellationToken);

    /// <inheritdoc />
    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        => PublishAsync(domainEvents, cancellationToken);
}
