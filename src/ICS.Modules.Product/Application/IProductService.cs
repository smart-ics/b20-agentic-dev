namespace ICS.Modules.Product.Application;

using ICS.Modules.Product.Application.DTOs;

/// <summary>
/// Application service contract for executing Product commands.
/// Architecture §10, §16, §19.2.
/// </summary>
public interface IProductService
{
    Task<ProductDto> CreateProductAsync(
        string code,
        string name,
        string? description,
        Guid ownerPersonId,
        Guid? productId = null,
        CancellationToken cancellationToken = default);

    Task<ProductDto> UpdateProductAsync(
        Guid productId,
        string name,
        string? description,
        CancellationToken cancellationToken = default);

    Task<ProductDto> AssignProductOwnerAsync(
        Guid productId,
        Guid newOwnerPersonId,
        CancellationToken cancellationToken = default);

    Task<bool> ActivateProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<bool> DeactivateProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default);
}
