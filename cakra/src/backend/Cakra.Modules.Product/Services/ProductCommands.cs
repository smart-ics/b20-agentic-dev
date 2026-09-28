using FluentValidation;
using MediatR;

namespace Cakra.Modules.Product.Services;

/// <summary>
/// Command to create a new <see cref="Domain.Product"/> in the catalog (Architecture §10).
/// </summary>
public sealed record CreateProductCommand(
    string Code,
    string Name,
    string? Description,
    Guid OwnerPersonId) : IRequest<ProductDto>;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Product code is required.")
            .MaximumLength(50).WithMessage("Product code must not exceed 50 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200).WithMessage("Product name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Product description must not exceed 1000 characters.")
            .When(x => x.Description is not null);

        RuleFor(x => x.OwnerPersonId)
            .NotEmpty().WithMessage("Product owner person ID is required.");
    }
}

/// <summary>
/// Command to update descriptive attributes of an existing <see cref="Domain.Product"/> (Architecture §10).
/// </summary>
public sealed record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string? Description) : IRequest<ProductDto>;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200).WithMessage("Product name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Product description must not exceed 1000 characters.")
            .When(x => x.Description is not null);
    }
}

/// <summary>
/// Command to assign a new product owner to a <see cref="Domain.Product"/> (Architecture §10).
/// </summary>
public sealed record AssignProductOwnerCommand(
    Guid ProductId,
    Guid NewOwnerPersonId) : IRequest<ProductDto>;

public sealed class AssignProductOwnerCommandValidator : AbstractValidator<AssignProductOwnerCommand>
{
    public AssignProductOwnerCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID is required.");

        RuleFor(x => x.NewOwnerPersonId)
            .NotEmpty().WithMessage("New owner person ID is required.");
    }
}

/// <summary>
/// Command to transition a <see cref="Domain.Product"/> to <c>ACTIVE</c> status (Architecture §10).
/// </summary>
public sealed record ActivateProductCommand(Guid ProductId) : IRequest<ProductDto>;

public sealed class ActivateProductCommandValidator : AbstractValidator<ActivateProductCommand>
{
    public ActivateProductCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID is required.");
    }
}

/// <summary>
/// Command to transition a <see cref="Domain.Product"/> to <c>INACTIVE</c> status (Architecture §10).
/// </summary>
public sealed record DeactivateProductCommand(Guid ProductId) : IRequest<ProductDto>;

public sealed class DeactivateProductCommandValidator : AbstractValidator<DeactivateProductCommand>
{
    public DeactivateProductCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID is required.");
    }
}
