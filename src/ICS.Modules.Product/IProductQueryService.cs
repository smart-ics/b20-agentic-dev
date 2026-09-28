namespace ICS.Modules.Product;

using ICS.Modules.Product.Application.DTOs;

/// <summary>
/// In-process query contract exposed by the Product module for cross-module integration and UI queries.
/// Architecture §10, §15, §16, §20.
/// </summary>
public interface IProductQueryService
{
    /// <summary>
    /// Gets a Product by identifier.
    /// </summary>
    Task<ProductDto?> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a Product by its unique business code.
    /// </summary>
    Task<ProductDto?> GetProductByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all active Products.
    /// </summary>
    Task<IReadOnlyList<ProductDto>> ListActiveProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all Products, including inactive ones for historical audit views.
    /// </summary>
    Task<IReadOnlyList<ProductDto>> ListAllProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronously gets a Product by identifier.
    /// </summary>
    ProductDto? GetProductById(Guid productId);

    /// <summary>
    /// Synchronously gets a Product by its unique business code.
    /// </summary>
    ProductDto? GetProductByCode(string code);

    /// <summary>
    /// Synchronously lists all active Products.
    /// </summary>
    IReadOnlyList<ProductDto> ListActiveProducts();

    /// <summary>
    /// Synchronously lists all Products.
    /// </summary>
    IReadOnlyList<ProductDto> ListAllProducts();
}
