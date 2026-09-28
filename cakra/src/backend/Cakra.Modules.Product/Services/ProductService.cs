using Cakra.Core;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Organization;
using Cakra.Modules.Product.Domain;
using Cakra.Modules.Product.Domain.Events;
using Cakra.Modules.Product.Persistence;
using MediatR;

namespace Cakra.Modules.Product.Services;

/// <summary>
/// Application command service and MediatR command handler for Product catalog master data, ownership,
/// and lifecycle management (Architecture §6, §7, §10, §15, §19.2).
/// Validates product owners via <see cref="IOrganizationQueryService"/> without direct cross-schema writes.
/// </summary>
public sealed class ProductService :
    IProductService,
    IRequestHandler<CreateProductCommand, ProductDto>,
    IRequestHandler<UpdateProductCommand, ProductDto>,
    IRequestHandler<AssignProductOwnerCommand, ProductDto>,
    IRequestHandler<ActivateProductCommand, ProductDto>,
    IRequestHandler<DeactivateProductCommand, ProductDto>
{
    private readonly IProductRepository _productRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly IDomainEventDispatcher? _eventDispatcher;
    private readonly ISystemClock? _clock;

    public ProductService(
        IDbConnectionFactory connectionFactory,
        IOrganizationQueryService organizationQueryService,
        IDomainEventDispatcher? eventDispatcher = null,
        ISystemClock? clock = null)
        : this(
            new ProductRepository(connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory))),
            organizationQueryService,
            eventDispatcher,
            clock)
    {
    }

    internal ProductService(
        IProductRepository productRepository,
        IOrganizationQueryService organizationQueryService,
        IDomainEventDispatcher? eventDispatcher = null,
        ISystemClock? clock = null)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _eventDispatcher = eventDispatcher;
        _clock = clock;
    }

    private DateTime UtcNow => _clock?.UtcNow ?? DateTime.UtcNow;

    /// <inheritdoc />
    public async Task<ProductDto> CreateProductAsync(
        string code,
        string name,
        string? description,
        Guid ownerPersonId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Product code cannot be null or whitespace.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name cannot be null or whitespace.", nameof(name));
        }

        if (ownerPersonId == Guid.Empty)
        {
            throw new ArgumentException("OwnerPersonId cannot be empty.", nameof(ownerPersonId));
        }

        var normalizedCode = code.Trim();
        var existing = await _productRepository.GetByCodeAsync(normalizedCode, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"A product with code '{normalizedCode}' already exists.");
        }

        await ValidateOwnerAsync(ownerPersonId, cancellationToken);

        var now = UtcNow;
        var product = Domain.Product.Create(
            normalizedCode,
            name.Trim(),
            description,
            ownerPersonId,
            createdAtUtc: now);

        await _productRepository.AddAsync(product, cancellationToken);

        if (_eventDispatcher is not null)
        {
            await _eventDispatcher.DispatchAsync(
                new ProductCreated(
                    product.Id,
                    product.Code,
                    product.Name,
                    product.Description,
                    product.OwnerPersonId,
                    Guid.NewGuid(),
                    now),
                cancellationToken);
        }

        return ProductDto.FromDomain(product);
    }

    /// <inheritdoc />
    public async Task<ProductDto> UpdateProductAsync(
        Guid productId,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId cannot be empty.", nameof(productId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name cannot be null or whitespace.", nameof(name));
        }

        var product = await _productRepository.GetByIdAsync(productId, cancellationToken)
            ?? throw new KeyNotFoundException($"Product '{productId}' was not found.");

        product.Update(name, description, UtcNow);
        await _productRepository.UpdateAsync(product, cancellationToken);

        return ProductDto.FromDomain(product);
    }

    /// <inheritdoc />
    public async Task<ProductDto> AssignProductOwnerAsync(
        Guid productId,
        Guid newOwnerPersonId,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId cannot be empty.", nameof(productId));
        }

        if (newOwnerPersonId == Guid.Empty)
        {
            throw new ArgumentException("NewOwnerPersonId cannot be empty.", nameof(newOwnerPersonId));
        }

        var product = await _productRepository.GetByIdAsync(productId, cancellationToken)
            ?? throw new KeyNotFoundException($"Product '{productId}' was not found.");

        await ValidateOwnerAsync(newOwnerPersonId, cancellationToken);

        var previousOwnerPersonId = product.OwnerPersonId;
        var now = UtcNow;

        product.AssignOwner(newOwnerPersonId, now);
        await _productRepository.UpdateAsync(product, cancellationToken);

        if (_eventDispatcher is not null)
        {
            await _eventDispatcher.DispatchAsync(
                new ProductOwnerChanged(
                    product.Id,
                    previousOwnerPersonId,
                    newOwnerPersonId,
                    Guid.NewGuid(),
                    now),
                cancellationToken);
        }

        return ProductDto.FromDomain(product);
    }

    /// <inheritdoc />
    public async Task<ProductDto> ActivateProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId cannot be empty.", nameof(productId));
        }

        var product = await _productRepository.GetByIdAsync(productId, cancellationToken)
            ?? throw new KeyNotFoundException($"Product '{productId}' was not found.");

        var now = UtcNow;
        product.Activate(now);
        await _productRepository.UpdateAsync(product, cancellationToken);

        if (_eventDispatcher is not null)
        {
            await _eventDispatcher.DispatchAsync(
                new ProductActivated(product.Id, Guid.NewGuid(), now),
                cancellationToken);
        }

        return ProductDto.FromDomain(product);
    }

    /// <inheritdoc />
    public async Task<ProductDto> DeactivateProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId cannot be empty.", nameof(productId));
        }

        var product = await _productRepository.GetByIdAsync(productId, cancellationToken)
            ?? throw new KeyNotFoundException($"Product '{productId}' was not found.");

        var now = UtcNow;
        product.Deactivate(now);
        await _productRepository.UpdateAsync(product, cancellationToken);

        if (_eventDispatcher is not null)
        {
            await _eventDispatcher.DispatchAsync(
                new ProductDeactivated(product.Id, Guid.NewGuid(), now),
                cancellationToken);
        }

        return ProductDto.FromDomain(product);
    }

    private async Task ValidateOwnerAsync(Guid ownerPersonId, CancellationToken cancellationToken)
    {
        var isActive = await _organizationQueryService.IsPersonActiveAsync(ownerPersonId, cancellationToken);
        if (isActive)
        {
            return;
        }

        var person = await _organizationQueryService.GetPersonByIdAsync(ownerPersonId, cancellationToken);
        if (person is not null)
        {
            if (person.IsActive)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Person '{ownerPersonId}' is not active in Organization and cannot be assigned as product owner.");
        }

        throw new KeyNotFoundException(
            $"Owner person '{ownerPersonId}' was not found or is not active in Organization.");
    }

    // MediatR command handler entry points
    public Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CreateProductAsync(
            request.Code,
            request.Name,
            request.Description,
            request.OwnerPersonId,
            cancellationToken);
    }

    public Task<ProductDto> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return UpdateProductAsync(
            request.ProductId,
            request.Name,
            request.Description,
            cancellationToken);
    }

    public Task<ProductDto> Handle(AssignProductOwnerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return AssignProductOwnerAsync(
            request.ProductId,
            request.NewOwnerPersonId,
            cancellationToken);
    }

    public Task<ProductDto> Handle(ActivateProductCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ActivateProductAsync(request.ProductId, cancellationToken);
    }

    public Task<ProductDto> Handle(DeactivateProductCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DeactivateProductAsync(request.ProductId, cancellationToken);
    }
}
