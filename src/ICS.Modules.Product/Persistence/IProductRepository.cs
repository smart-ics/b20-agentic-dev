namespace ICS.Modules.Product.Persistence;

using ICS.Modules.Product.Domain;

/// <summary>
/// Module-internal repository interface for Product persistence.
/// Kept internal to maintain module boundaries per Architecture §10, §16, §20.
/// </summary>
internal interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<Product?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> ListAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Product product, CancellationToken cancellationToken = default);
    Task UpdateAsync(Product product, CancellationToken cancellationToken = default);
}
