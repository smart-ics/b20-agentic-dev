using Cakra.Core;

namespace Cakra.Modules.Product.Persistence;

/// <summary>
/// Internal persistence contract for <see cref="Domain.Product"/> aggregate roots (Architecture §10, §19.3, §21).
/// Not exposed outside the Product module boundary.
/// </summary>
internal interface IProductRepository : IRepository<Domain.Product>
{
    /// <summary>
    /// Retrieves a product by its unique business code, or <c>null</c> when not found.
    /// </summary>
    Task<Domain.Product?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active products ordered by name and code.
    /// </summary>
    Task<IReadOnlyList<Domain.Product>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all products owned by the specified organizational person.
    /// </summary>
    Task<IReadOnlyList<Domain.Product>> GetByOwnerPersonIdAsync(Guid ownerPersonId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the lifecycle status of a product.
    /// </summary>
    Task UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);
}
