namespace Cakra.Modules.Product;

/// <summary>
/// Read-only data transfer object representing an authoritative <see cref="Domain.Product"/> record
/// (Architecture §7, §10, §15).
/// </summary>
public sealed record ProductDto
{
    /// <summary>Unique identifier of the product.</summary>
    public Guid Id { get; init; }

    /// <summary>Unique identifier of the product (synonym for <see cref="Id"/>).</summary>
    public Guid ProductId
    {
        get => Id;
        init => Id = value;
    }

    /// <summary>Unique business code of the product.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Convenience alias for <see cref="Code"/>.</summary>
    public string ProductCode
    {
        get => Code;
        init => Code = value;
    }

    /// <summary>Official name of the product.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Convenience alias for <see cref="Name"/>.</summary>
    public string ProductName
    {
        get => Name;
        init => Name = value;
    }

    /// <summary>Optional description of the product.</summary>
    public string? Description { get; init; }

    /// <summary>Identifier of the organizational <c>Person</c> who owns the product.</summary>
    public Guid OwnerPersonId { get; init; }

    /// <summary>Authoritative lifecycle status (<c>ACTIVE</c> or <c>INACTIVE</c>).</summary>
    public string Status { get; init; } = Domain.Product.StatusActive;

    /// <summary>Returns <c>true</c> when <see cref="Status"/> is <c>ACTIVE</c>.</summary>
    public bool IsActive => string.Equals(Status, Domain.Product.StatusActive, StringComparison.OrdinalIgnoreCase);

    /// <summary>UTC timestamp when the product record was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>UTC timestamp when the product record was last updated.</summary>
    public DateTime? UpdatedAt { get; init; }

    internal static ProductDto FromDomain(Domain.Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        return new ProductDto
        {
            Id = product.Id,
            Code = product.Code,
            Name = product.Name,
            Description = product.Description,
            OwnerPersonId = product.OwnerPersonId,
            Status = product.Status,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }
}

/// <summary>
/// Published cross-module query contract for Product catalog master data and product owner associations
/// (Architecture §7, §10, §15, §21). Internal repositories are not exposed outside the Product module boundary.
/// </summary>
public interface IProductQueryService
{
    /// <summary>
    /// Retrieves an authoritative product record by its unique identifier, or <c>null</c> if not found (Architecture §10).
    /// </summary>
    Task<ProductDto?> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an authoritative product record by its unique identifier, or <c>null</c> if not found (Architecture §10).
    /// </summary>
    Task<ProductDto?> GetProductById(Guid productId, CancellationToken cancellationToken = default)
        => GetProductByIdAsync(productId, cancellationToken);

    /// <summary>
    /// Retrieves an authoritative product record by its unique business code, or <c>null</c> if not found (Architecture §10).
    /// </summary>
    Task<ProductDto?> GetProductByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an authoritative product record by its unique business code, or <c>null</c> if not found (Architecture §10).
    /// </summary>
    Task<ProductDto?> GetProductByCode(string code, CancellationToken cancellationToken = default)
        => GetProductByCodeAsync(code, cancellationToken);

    /// <summary>
    /// Retrieves all active products ordered by name and code (Architecture §10).
    /// </summary>
    Task<IReadOnlyList<ProductDto>> ListActiveProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active products ordered by name and code (Architecture §10).
    /// </summary>
    Task<IReadOnlyList<ProductDto>> ListActiveProducts(CancellationToken cancellationToken = default)
        => ListActiveProductsAsync(cancellationToken);

    /// <summary>
    /// Retrieves the complete product catalog including inactive products for historical audit views (Architecture §10).
    /// </summary>
    Task<IReadOnlyList<ProductDto>> ListAllProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the complete product catalog including inactive products for historical audit views (Architecture §10).
    /// </summary>
    Task<IReadOnlyList<ProductDto>> ListAllProducts(CancellationToken cancellationToken = default)
        => ListAllProductsAsync(cancellationToken);

    /// <summary>
    /// Checks whether the specified product exists and is currently in <c>ACTIVE</c> status.
    /// </summary>
    Task<bool> IsProductActiveAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous convenience overload for <see cref="IsProductActiveAsync"/>.
    /// </summary>
    bool IsProductActive(Guid productId)
        => IsProductActiveAsync(productId, CancellationToken.None).GetAwaiter().GetResult();
}
