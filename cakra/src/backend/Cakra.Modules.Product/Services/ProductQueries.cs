using MediatR;

namespace Cakra.Modules.Product.Services;

/// <summary>
/// MediatR query to retrieve a product by its unique identifier (Architecture §10).
/// </summary>
public sealed record GetProductByIdQuery(Guid ProductId) : IRequest<ProductDto?>;

/// <summary>
/// MediatR query to retrieve a product by its unique business code (Architecture §10).
/// </summary>
public sealed record GetProductByCodeQuery(string Code) : IRequest<ProductDto?>;

/// <summary>
/// MediatR query to retrieve all active products (Architecture §10).
/// </summary>
public sealed record ListActiveProductsQuery : IRequest<IReadOnlyList<ProductDto>>;

/// <summary>
/// MediatR query to retrieve all products including inactive products (Architecture §10).
/// </summary>
public sealed record ListAllProductsQuery : IRequest<IReadOnlyList<ProductDto>>;
