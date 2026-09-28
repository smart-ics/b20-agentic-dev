namespace ICS.Modules.Product.Application.Commands;

using FluentValidation;
using ICS.Core.Domain;
using ICS.Core.Time;
using ICS.Modules.Organization;
using ICS.Modules.Product.Application.DTOs;
using ICS.Modules.Product.Domain;
using ICS.Modules.Product.Persistence;
using MediatR;

#region CreateProduct

public sealed record CreateProductCommand(
    string Code,
    string Name,
    string? Description,
    Guid OwnerPersonId,
    Guid? ProductId = null) : IRequest<ProductDto>;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Product code is required.")
            .MaximumLength(50).WithMessage("Product code must not exceed 50 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(150).WithMessage("Product name must not exceed 150 characters.");

        RuleFor(x => x.OwnerPersonId)
            .NotEmpty().WithMessage("Product owner is required.");
    }
}

internal sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, ProductDto>
{
    private readonly IProductRepository _productRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public CreateProductCommandHandler(
        IProductRepository productRepository,
        IOrganizationQueryService organizationQueryService,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var owner = await _organizationQueryService.GetPersonByIdAsync(request.OwnerPersonId, cancellationToken);
        if (owner == null)
        {
            throw new InvalidOperationException($"Product owner with PersonId '{request.OwnerPersonId}' does not exist in Organization.");
        }

        if (!string.Equals(owner.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Product owner with PersonId '{request.OwnerPersonId}' is not active.");
        }

        var existing = await _productRepository.GetByCodeAsync(request.Code, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"A product with code '{request.Code}' already exists.");
        }

        var productId = request.ProductId ?? Guid.NewGuid();
        var product = Product.Create(
            productId,
            request.Code,
            request.Name,
            request.Description,
            request.OwnerPersonId,
            _clock.UtcNow);

        await _productRepository.AddAsync(product, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(product, cancellationToken);

        return new ProductDto(
            product.Id,
            product.Code,
            product.Name,
            product.Description,
            product.OwnerPersonId,
            product.Status,
            product.CreatedAt,
            product.UpdatedAt)
        {
            OwnerName = owner.Name
        };
    }
}

#endregion

#region UpdateProduct

public sealed record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string? Description) : IRequest<ProductDto>;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("ProductId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(150).WithMessage("Product name must not exceed 150 characters.");
    }
}

internal sealed class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, ProductDto>
{
    private readonly IProductRepository _productRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public UpdateProductCommandHandler(
        IProductRepository productRepository,
        IOrganizationQueryService organizationQueryService,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<ProductDto> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            throw new InvalidOperationException($"Product with ID '{request.ProductId}' was not found.");
        }

        product.Update(request.Name, request.Description, _clock.UtcNow);

        await _productRepository.UpdateAsync(product, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(product, cancellationToken);

        var owner = await _organizationQueryService.GetPersonByIdAsync(product.OwnerPersonId, cancellationToken);

        return new ProductDto(
            product.Id,
            product.Code,
            product.Name,
            product.Description,
            product.OwnerPersonId,
            product.Status,
            product.CreatedAt,
            product.UpdatedAt)
        {
            OwnerName = owner?.Name
        };
    }
}

#endregion

#region AssignProductOwner

public sealed record AssignProductOwnerCommand(
    Guid ProductId,
    Guid NewOwnerPersonId) : IRequest<ProductDto>;

public sealed class AssignProductOwnerCommandValidator : AbstractValidator<AssignProductOwnerCommand>
{
    public AssignProductOwnerCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("ProductId is required.");

        RuleFor(x => x.NewOwnerPersonId)
            .NotEmpty().WithMessage("NewOwnerPersonId is required.");
    }
}

internal sealed class AssignProductOwnerCommandHandler : IRequestHandler<AssignProductOwnerCommand, ProductDto>
{
    private readonly IProductRepository _productRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public AssignProductOwnerCommandHandler(
        IProductRepository productRepository,
        IOrganizationQueryService organizationQueryService,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<ProductDto> Handle(AssignProductOwnerCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            throw new InvalidOperationException($"Product with ID '{request.ProductId}' was not found.");
        }

        var newOwner = await _organizationQueryService.GetPersonByIdAsync(request.NewOwnerPersonId, cancellationToken);
        if (newOwner == null)
        {
            throw new InvalidOperationException($"Product owner with PersonId '{request.NewOwnerPersonId}' does not exist in Organization.");
        }

        if (!string.Equals(newOwner.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Product owner with PersonId '{request.NewOwnerPersonId}' is not active.");
        }

        product.AssignOwner(request.NewOwnerPersonId, _clock.UtcNow);

        await _productRepository.UpdateAsync(product, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(product, cancellationToken);

        return new ProductDto(
            product.Id,
            product.Code,
            product.Name,
            product.Description,
            product.OwnerPersonId,
            product.Status,
            product.CreatedAt,
            product.UpdatedAt)
        {
            OwnerName = newOwner.Name
        };
    }
}

#endregion

#region ActivateProduct

public sealed record ActivateProductCommand(Guid ProductId) : IRequest<bool>;

public sealed class ActivateProductCommandValidator : AbstractValidator<ActivateProductCommand>
{
    public ActivateProductCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("ProductId is required.");
    }
}

internal sealed class ActivateProductCommandHandler : IRequestHandler<ActivateProductCommand, bool>
{
    private readonly IProductRepository _productRepository;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public ActivateProductCommandHandler(
        IProductRepository productRepository,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<bool> Handle(ActivateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            throw new InvalidOperationException($"Product with ID '{request.ProductId}' was not found.");
        }

        product.Activate(_clock.UtcNow);

        await _productRepository.UpdateAsync(product, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(product, cancellationToken);

        return true;
    }
}

#endregion

#region DeactivateProduct

public sealed record DeactivateProductCommand(Guid ProductId) : IRequest<bool>;

public sealed class DeactivateProductCommandValidator : AbstractValidator<DeactivateProductCommand>
{
    public DeactivateProductCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("ProductId is required.");
    }
}

internal sealed class DeactivateProductCommandHandler : IRequestHandler<DeactivateProductCommand, bool>
{
    private readonly IProductRepository _productRepository;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public DeactivateProductCommandHandler(
        IProductRepository productRepository,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<bool> Handle(DeactivateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            throw new InvalidOperationException($"Product with ID '{request.ProductId}' was not found.");
        }

        product.Deactivate(_clock.UtcNow);

        await _productRepository.UpdateAsync(product, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(product, cancellationToken);

        return true;
    }
}

#endregion
