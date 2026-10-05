using FluentValidation;

namespace Cakra.Modules.Identity.Commands;

/// <summary>
/// FluentValidation validator for <see cref="RegisterUserAccountCommand"/> (CR-009; Architecture §4 TD-001).
/// Enforces required fields, email format, and password minimum length >= 8.
/// </summary>
public sealed class RegisterUserAccountCommandValidator : AbstractValidator<RegisterUserAccountCommand>
{
    public RegisterUserAccountCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MaximumLength(50).WithMessage("Username must not exceed 50 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email format is invalid.")
            .MaximumLength(150).WithMessage("Email must not exceed 150 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.");
    }
}
