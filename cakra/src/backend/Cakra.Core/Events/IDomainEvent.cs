using MediatR;

namespace Cakra.Core;

/// <summary>
/// Marker contract for domain events published on the in-process event bus.
/// Derives from MediatR <see cref="INotification"/> so handlers are plain
/// MediatR <c>INotificationHandler&lt;T&gt;</c> implementations.
/// </summary>
public interface IDomainEvent : INotification
{
    /// <summary>Unique identifier of this event occurrence.</summary>
    Guid EventId { get; }

    /// <summary>UTC timestamp when the event occurred.</summary>
    DateTime OccurredAtUtc { get; }
}
