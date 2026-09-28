using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Services;
using FluentValidation;
using MediatR;

namespace Cakra.Modules.Organization.Commands;

/// <summary>
/// Command to deactivate an organizational <see cref="Person"/> (Architecture §7, §14, §19.2).
/// </summary>
public sealed record DeactivatePersonCommand(
    Guid PersonId) : IRequest<Person>;

/// <summary>
/// FluentValidation validator for <see cref="DeactivatePersonCommand"/>.
/// </summary>
public sealed class DeactivatePersonCommandValidator : AbstractValidator<DeactivatePersonCommand>
{
    public DeactivatePersonCommandValidator()
    {
        RuleFor(x => x.PersonId)
            .NotEmpty().WithMessage("PersonId is required.");
    }
}

/// <summary>
/// MediatR handler for <see cref="DeactivatePersonCommand"/> delegating to <see cref="IOrganizationService"/>.
/// </summary>
public sealed class DeactivatePersonCommandHandler : IRequestHandler<DeactivatePersonCommand, Person>
{
    private readonly IOrganizationService _organizationService;

    public DeactivatePersonCommandHandler(IOrganizationService organizationService)
    {
        _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
    }

    public Task<Person> Handle(DeactivatePersonCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _organizationService.DeactivatePersonAsync(
            request.PersonId,
            cancellationToken);
    }
}
