namespace Cakra.Core;

/// <summary>
/// Generic persistence contract for an aggregate root. Concrete implementations
/// execute explicit parameterized SQL via Dapper (Architecture 19.3).
/// </summary>
/// <typeparam name="TEntity">Aggregate root type.</typeparam>
/// <typeparam name="TId">Identifier type.</typeparam>
public interface IRepository<TEntity, in TId>
    where TEntity : EntityBase
{
    /// <summary>Retrieves an entity by its identifier, or <c>null</c> when not found.</summary>
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>Retrieves all entities.</summary>
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists a new entity.</summary>
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing entity.</summary>
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Removes an entity by its identifier.</summary>
    Task DeleteAsync(TId id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Convenience repository contract for entities identified by <see cref="Guid"/>.
/// </summary>
/// <typeparam name="TEntity">Aggregate root type.</typeparam>
public interface IRepository<TEntity> : IRepository<TEntity, Guid>
    where TEntity : EntityBase
{
}
