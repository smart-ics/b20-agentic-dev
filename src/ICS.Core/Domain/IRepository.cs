namespace ICS.Core.Domain;

/// <summary>
/// Generic repository contract for managing persistence of entities with a strongly typed identifier.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TId">The entity identifier type.</typeparam>
public interface IRepository<T, in TId> where T : class
{
    /// <summary>
    /// Retrieves an entity by its identifier.
    /// </summary>
    Task<T?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new entity.
    /// </summary>
    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity.
    /// </summary>
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity by its identifier.
    /// </summary>
    Task DeleteAsync(TId id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Standard repository contract for entities using <see cref="Guid"/> identifiers.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IRepository<T> : IRepository<T, Guid> where T : class
{
}
