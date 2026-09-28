namespace ICS.Modules.Product.Application;

using ICS.Modules.Product.Application.Commands;
using ICS.Modules.Product.Application.DTOs;
using MediatR;

/// <summary>
/// Concrete implementation of <see cref="IProductService"/> executing commands through MediatR.
/// Architecture §10, §16, §19.2, §20.
/// </summary>
public class ProductService : IProductService
{
    private readonly ISender _sender;

    public ProductService(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public Task<ProductDto> CreateProductAsync(
        string code,
        string name,
        string? description,
        Guid ownerPersonId,
        Guid? productId = null,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new CreateProductCommand(code, name, description, ownerPersonId, productId), cancellationToken);
    }

    public Task<ProductDto> UpdateProductAsync(
        Guid productId,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new UpdateProductCommand(productId, name, description), cancellationToken);
    }

    public Task<ProductDto> AssignProductOwnerAsync(
        Guid productId,
        Guid newOwnerPersonId,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new AssignProductOwnerCommand(productId, newOwnerPersonId), cancellationToken);
    }

    public Task<bool> ActivateProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new ActivateProductCommand(productId), cancellationToken);
    }

    public Task<bool> DeactivateProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new DeactivateProductCommand(productId), cancellationToken);
    }
}
