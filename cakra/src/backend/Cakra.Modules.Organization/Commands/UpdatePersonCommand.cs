using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Services;
using FluentValidation;
using MediatR;

namespace Cakra.Modules.Organization.Commands;

/// <summary>
/// Command to update an existing organizational <see cref="Person"/> (Architecture §7, §19.2).
/// </summary>
public sealed record UpdatePersonCommand(
    Guid PersonId,
    string FirstName,
    string LastName,
    string Email) : IRequest<Person>;

/// <summary>
/// FluentValidation validator for <see cref="UpdatePersonCommand"/>.
/// </summary>
public sealed class UpdatePersonCommandValidator : AbstractValidator<UpdatePersonCommand>
{
    public UpdatePersonCommandValidator()
    {
        RuleFor(x => x.PersonId)
            .NotEmpty().WithMessage("PersonId is required.");

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
    }
}

/// <summary>
/// MediatR handler for <see cref="UpdatePersonCommand"/> delegating to <see cref="IOrganizationService"/>.
/// </summary>
public sealed class UpdatePersonCommandHandler : IRequestHandler<UpdatePersonCommand, Person>
{
    private readonly IOrganizationService _organizationService;

    public UpdatePersonCommandHandler(IOrganizationService organizationService)
    {
        _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
    }

    public Task<Person> Handle(UpdatePersonCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _organizationService.UpdatePersonAsync(
            request.PersonId,
            request.FirstName,
            request.LastName,
            request.Email,
            cancellationToken);
    }
}
