namespace ICS.Core.Domain;

/// <summary>
/// Abstract base record for domain events within the ICS Modular Monolith.
/// Automatically initializes <see cref="EventId"/> to a new Guid and <see cref="OccurredAt"/> to current UTC time.
/// Implements <see cref="IDomainEvent"/> and MediatR <see cref="MediatR.INotification"/>.
/// Per Architecture §12, §18, and §19.2.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
