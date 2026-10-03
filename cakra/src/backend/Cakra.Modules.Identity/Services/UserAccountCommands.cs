using Cakra.Modules.Identity.Domain;
using FluentValidation;
using MediatR;

namespace Cakra.Modules.Identity.Services;

/// <summary>
/// Command to create a new user account (Architecture §14; CR-007).
/// </summary>
public sealed record CreateUserAccountCommand(
    Guid PersonId,
    string Username,
    string Email,
    string Password,
    string Status = UserAccountStatus.Active) : IRequest<UserAccountDto>;

/// <summary>
/// FluentValidation validator for <see cref="CreateUserAccountCommand"/> (Architecture §19.2; CR-007).
/// </summary>
public sealed class CreateUserAccountCommandValidator : AbstractValidator<CreateUserAccountCommand>
{
    public CreateUserAccountCommandValidator()
    {
        RuleFor(x => x.PersonId)
            .NotEmpty().WithMessage("Person ID is required.");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .Length(3, 50).WithMessage("Username must be between 3 and 50 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email format is invalid.")
            .MaximumLength(150).WithMessage("Email must not exceed 150 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.");

        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s)
                || string.Equals(s.Trim(), UserAccountStatus.Active, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Trim(), UserAccountStatus.Locked, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Trim(), UserAccountStatus.Suspended, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Status must be ACTIVE, LOCKED, or SUSPENDED.");
    }
}

/// <summary>
/// Command to update an existing user account's attributes, status, and/or password (Architecture §14; CR-007).
/// </summary>
public sealed record UpdateUserAccountCommand(
    string? Email = null,
    string? Status = null,
    string? NewPassword = null,
    Guid? UserId = null) : IRequest<UserAccountDto>;

/// <summary>
/// FluentValidation validator for <see cref="UpdateUserAccountCommand"/> (Architecture §19.2; CR-007).
/// </summary>
public sealed class UpdateUserAccountCommandValidator : AbstractValidator<UpdateUserAccountCommand>
{
    public UpdateUserAccountCommandValidator()
    {
        RuleFor(x => x.Email)
            .Must(e => e == null || !string.IsNullOrWhiteSpace(e)).WithMessage("Email cannot be empty.")
            .EmailAddress().WithMessage("Email format is invalid.")
            .MaximumLength(150).WithMessage("Email must not exceed 150 characters.")
            .When(x => x.Email != null);

        RuleFor(x => x.Status)
            .Must(s => s == null || !string.IsNullOrWhiteSpace(s)).WithMessage("Status cannot be empty.")
            .Must(s => string.Equals(s!.Trim(), UserAccountStatus.Active, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s!.Trim(), UserAccountStatus.Locked, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s!.Trim(), UserAccountStatus.Suspended, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Status must be ACTIVE, LOCKED, or SUSPENDED.")
            .When(x => x.Status != null);

        RuleFor(x => x.NewPassword)
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .When(x => !string.IsNullOrEmpty(x.NewPassword));
    }
}
