using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Services;
using FluentValidation;
using MediatR;

namespace Cakra.Modules.Organization.Commands;

/// <summary>
/// Command to create a new organizational <see cref="Person"/> with assigned roles (Architecture §7, §19.2; CR-027).
/// </summary>
public sealed record CreatePersonCommand(
    string FirstName,
    string LastName,
    string Email,
    IReadOnlyList<Guid>? RoleIds) : IRequest<Person>
{
    public bool IsLegacy { get; init; }

    public CreatePersonCommand(string firstName, string lastName, string email)
        : this(firstName, lastName, email, Array.Empty<Guid>())
    {
        IsLegacy = true;
    }
}

/// <summary>
/// FluentValidation validator for <see cref="CreatePersonCommand"/>.
/// </summary>
public sealed class CreatePersonCommandValidator : AbstractValidator<CreatePersonCommand>
{
    public CreatePersonCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("FirstName is required.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("LastName is required.")
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(255);

        RuleFor(x => x.RoleIds)
            .NotEmpty().WithMessage("At least one role must be assigned.")
            .When(x => !x.IsLegacy);
    }
}

/// <summary>
/// MediatR handler for <see cref="CreatePersonCommand"/> delegating to <see cref="IOrganizationService"/>.
/// </summary>
public sealed class CreatePersonCommandHandler : IRequestHandler<CreatePersonCommand, Person>
{
    private readonly IOrganizationService _organizationService;

    public CreatePersonCommandHandler(IOrganizationService organizationService)
    {
        _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
    }

    public Task<Person> Handle(CreatePersonCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _organizationService.CreatePersonAsync(
            request.FirstName,
            request.LastName,
            request.Email,
            request.RoleIds ?? Array.Empty<Guid>(),
            cancellationToken);
    }
}
