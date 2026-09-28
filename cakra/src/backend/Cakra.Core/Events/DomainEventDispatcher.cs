using MediatR;

namespace Cakra.Core;

/// <summary>
/// In-process domain event bus. Delegates publication to MediatR
/// <see cref="IPublisher"/>, which invokes every registered
/// <c>INotificationHandler&lt;T&gt;</c> for the event type.
/// </summary>
/// <remarks>
/// Architecture §18 (Domain Event Bus): dispatch is in-process and synchronous
/// with transactional consistency. Handlers run within the same database
/// transaction scope as the originating command because <see cref="IPublisher"/>
/// awaits each handler before returning; any exception raised by a handler
/// propagates to the caller and therefore rolls back the enclosing transaction.
/// Architecture §19.2: MediatR is the in-process mediator.
/// </remarks>
public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IPublisher _publisher;

    /// <summary>Creates a dispatcher over the supplied MediatR publisher.</summary>
    public DomainEventDispatcher(IPublisher publisher)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    /// <inheritdoc />
    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // IDomainEvent derives from MediatR.INotification, so Publish invokes all
        // INotificationHandler<IDomainEvent> implementations resolved from DI.
        return _publisher.Publish(domainEvent, cancellationToken);
    }
}
