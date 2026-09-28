using FluentValidation;
using MediatR;

namespace Cakra.Modules.Customer.Services;

/// <summary>
/// Command to create a new <see cref="Domain.Customer"/> master record (Architecture §7).
/// </summary>
public sealed record CreateCustomerCommand(
    string CustomerCode,
    string CustomerName,
    bool HasActiveMaintenanceContract = false) : IRequest<CustomerDto>;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerCode)
            .NotEmpty().WithMessage("Customer code is required.")
            .MaximumLength(50).WithMessage("Customer code must not exceed 50 characters.");

        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Customer name is required.")
            .MaximumLength(200).WithMessage("Customer name must not exceed 200 characters.");
    }
}

/// <summary>
/// Command to update <see cref="Domain.Customer"/> master data (Architecture §7).
/// </summary>
public sealed record UpdateCustomerCommand(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    bool HasActiveMaintenanceContract) : IRequest<CustomerDto>;

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer ID is required.");

        RuleFor(x => x.CustomerCode)
            .NotEmpty().WithMessage("Customer code is required.")
            .MaximumLength(50).WithMessage("Customer code must not exceed 50 characters.");

        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Customer name is required.")
            .MaximumLength(200).WithMessage("Customer name must not exceed 200 characters.");
    }
}

/// <summary>
/// Command to create a new <see cref="Domain.CustomerContact"/> associated with a customer (Architecture §7).
/// </summary>
public sealed record CreateCustomerContactCommand(
    Guid CustomerId,
    string Name,
    string? Position = null,
    string? PhoneNumber = null,
    string? Email = null) : IRequest<CustomerContactDto>;

public sealed class CreateCustomerContactCommandValidator : AbstractValidator<CreateCustomerContactCommand>
{
    public CreateCustomerContactCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Contact name is required.")
            .MaximumLength(150).WithMessage("Contact name must not exceed 150 characters.");

        RuleFor(x => x.Position)
            .MaximumLength(100)
            .When(x => x.Position is not null);

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(50)
            .When(x => x.PhoneNumber is not null);

        RuleFor(x => x.Email)
            .MaximumLength(255)
            .When(x => x.Email is not null);
    }
}

/// <summary>
/// Command to update an existing <see cref="Domain.CustomerContact"/> (Architecture §7).
/// </summary>
public sealed record UpdateCustomerContactCommand(
    Guid ContactId,
    string Name,
    string? Position = null,
    string? PhoneNumber = null,
    string? Email = null,
    string? Status = null) : IRequest<CustomerContactDto>;

public sealed class UpdateCustomerContactCommandValidator : AbstractValidator<UpdateCustomerContactCommand>
{
    public UpdateCustomerContactCommandValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("Contact ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Contact name is required.")
            .MaximumLength(150).WithMessage("Contact name must not exceed 150 characters.");

        RuleFor(x => x.Position)
            .MaximumLength(100)
            .When(x => x.Position is not null);

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(50)
            .When(x => x.PhoneNumber is not null);

        RuleFor(x => x.Email)
            .MaximumLength(255)
            .When(x => x.Email is not null);

        RuleFor(x => x.Status)
            .Must(s => string.Equals(s?.Trim(), Domain.CustomerContact.StatusActive, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s?.Trim(), Domain.CustomerContact.StatusInactive, StringComparison.OrdinalIgnoreCase))
            .When(x => x.Status is not null)
            .WithMessage("Contact status must be 'ACTIVE' or 'INACTIVE'.");
    }
}

/// <summary>
/// Command to deactivate a <see cref="Domain.Customer"/> while preserving historical references (Architecture §7; Domain Rule 5, 6).
/// </summary>
public sealed record DeactivateCustomerCommand(Guid CustomerId) : IRequest<CustomerDto>;

public sealed class DeactivateCustomerCommandValidator : AbstractValidator<DeactivateCustomerCommand>
{
    public DeactivateCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer ID is required.");
    }
}

/// <summary>
/// Command to activate a <see cref="Domain.Customer"/> (Domain §9).
/// </summary>
public sealed record ActivateCustomerCommand(Guid CustomerId) : IRequest<CustomerDto>;

public sealed class ActivateCustomerCommandValidator : AbstractValidator<ActivateCustomerCommand>
{
    public ActivateCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer ID is required.");
    }
}
