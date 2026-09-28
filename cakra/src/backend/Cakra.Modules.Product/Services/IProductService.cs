namespace Cakra.Modules.Product.Services;

/// <summary>
/// Application command service contract for Product catalog master data, ownership, and lifecycle status
/// (Architecture §7, §10).
/// </summary>
public interface IProductService
{
    /// <summary>
    /// Creates a new active product record after validating unique code and owner existence in Organization (Architecture §10).
    /// </summary>
    Task<ProductDto> CreateProductAsync(
        string code,
        string name,
        string? description,
        Guid ownerPersonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new active product record after validating unique code and owner existence in Organization (Architecture §10).
    /// </summary>
    Task<ProductDto> CreateProduct(
        string code,
        string name,
        string? description,
        Guid ownerPersonId,
        CancellationToken cancellationToken = default)
        => CreateProductAsync(code, name, description, ownerPersonId, cancellationToken);

    /// <summary>
    /// Updates descriptive attributes of an existing product (Architecture §10).
    /// </summary>
    Task<ProductDto> UpdateProductAsync(
        Guid productId,
        string name,
        string? description,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates descriptive attributes of an existing product (Architecture §10).
    /// </summary>
    Task<ProductDto> UpdateProduct(
        Guid productId,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
        => UpdateProductAsync(productId, name, description, cancellationToken);

    /// <summary>
    /// Assigns a new product owner after validating the person in Organization and emits <c>ProductOwnerChanged</c> (Architecture §10).
    /// </summary>
    Task<ProductDto> AssignProductOwnerAsync(
        Guid productId,
        Guid newOwnerPersonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a new product owner after validating the person in Organization and emits <c>ProductOwnerChanged</c> (Architecture §10).
    /// </summary>
    Task<ProductDto> AssignProductOwner(
        Guid productId,
        Guid newOwnerPersonId,
        CancellationToken cancellationToken = default)
        => AssignProductOwnerAsync(productId, newOwnerPersonId, cancellationToken);

    /// <summary>
    /// Transitions the product lifecycle status to <c>ACTIVE</c> and emits <c>ProductActivated</c> (Architecture §10).
    /// </summary>
    Task<ProductDto> ActivateProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions the product lifecycle status to <c>ACTIVE</c> and emits <c>ProductActivated</c> (Architecture §10).
    /// </summary>
    Task<ProductDto> ActivateProduct(
        Guid productId,
        CancellationToken cancellationToken = default)
        => ActivateProductAsync(productId, cancellationToken);

    /// <summary>
    /// Transitions the product lifecycle status to <c>INACTIVE</c> and emits <c>ProductDeactivated</c> (Architecture §10).
    /// </summary>
    Task<ProductDto> DeactivateProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions the product lifecycle status to <c>INACTIVE</c> and emits <c>ProductDeactivated</c> (Architecture §10).
    /// </summary>
    Task<ProductDto> DeactivateProduct(
        Guid productId,
        CancellationToken cancellationToken = default)
        => DeactivateProductAsync(productId, cancellationToken);
}
