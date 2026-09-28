namespace ICS.Core.Domain;

/// <summary>
/// Generic base entity type for domain entities with a strongly typed identifier.
/// </summary>
/// <typeparam name="TId">The type of the entity identifier.</typeparam>
public abstract class Entity<TId>
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// Authoritative identifier for the entity.
    /// </summary>
    public TId Id { get; protected set; } = default!;

    /// <summary>
    /// Timestamp when the entity was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the entity was last updated (UTC), or null if not modified since creation.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Read-only collection of domain events raised by this entity that have not yet been dispatched.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Adds a domain event to the entity's pending event list.
    /// </summary>
    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Removes a domain event from the entity's pending event list.
    /// </summary>
    public void RemoveDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Remove(domainEvent);
    }

    /// <summary>
    /// Clears all pending domain events.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        if (EqualityComparer<TId>.Default.Equals(Id, default!) || EqualityComparer<TId>.Default.Equals(other.Id, default!))
            return false;

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(GetType(), Id);
    }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        if (left is null && right is null)
            return true;
        if (left is null || right is null)
            return false;
        return left.Equals(right);
    }

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !(left == right);
    }
}

/// <summary>
/// Standard base entity type using <see cref="Guid"/> as the default identifier.
/// </summary>
public abstract class Entity : Entity<Guid>
{
    protected Entity()
    {
        Id = Guid.NewGuid();
    }

    protected Entity(Guid id)
    {
        Id = id;
    }
}
