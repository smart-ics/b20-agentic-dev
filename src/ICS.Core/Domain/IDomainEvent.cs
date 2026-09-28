using MediatR;

namespace ICS.Core.Domain;

/// <summary>
/// Marker interface for domain events within the modular monolith.
/// Implements MediatR <see cref="INotification"/> to enable synchronous in-process event dispatching.
/// </summary>
public interface IDomainEvent : INotification
{
    /// <summary>
    /// Unique identifier for the domain event occurrence.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// UTC timestamp when the domain event occurred.
    /// </summary>
    DateTime OccurredAt { get; }
}
