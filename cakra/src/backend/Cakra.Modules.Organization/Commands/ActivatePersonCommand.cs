using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Services;
using FluentValidation;
using MediatR;

namespace Cakra.Modules.Organization.Commands;

/// <summary>
/// Command to activate an organizational <see cref="Person"/> (Architecture §7, §14, §19.2).
/// </summary>
public sealed record ActivatePersonCommand(
    Guid PersonId) : IRequest<Person>;

/// <summary>
/// FluentValidation validator for <see cref="ActivatePersonCommand"/>.
/// </summary>
public sealed class ActivatePersonCommandValidator : AbstractValidator<ActivatePersonCommand>
{
    public ActivatePersonCommandValidator()
    {
        RuleFor(x => x.PersonId)
            .NotEmpty().WithMessage("PersonId is required.");
    }
}

/// <summary>
/// MediatR handler for <see cref="ActivatePersonCommand"/> delegating to <see cref="IOrganizationService"/>.
/// </summary>
public sealed class ActivatePersonCommandHandler : IRequestHandler<ActivatePersonCommand, Person>
{
    private readonly IOrganizationService _organizationService;

    public ActivatePersonCommandHandler(IOrganizationService organizationService)
    {
        _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
    }

    public Task<Person> Handle(ActivatePersonCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _organizationService.ActivatePersonAsync(
            request.PersonId,
            cancellationToken);
    }
}