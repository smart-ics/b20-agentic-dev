using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Services;
using FluentValidation;
using MediatR;

namespace Cakra.Modules.Organization.Commands;

/// <summary>
/// Command to assign an organizational <see cref="Responsibility"/> to a <see cref="Person"/> (Architecture §7, §16, §19.2).
/// </summary>
public sealed record AssignResponsibilityToPersonCommand(
    Guid PersonId,
    Guid ResponsibilityId) : IRequest<ResponsibilityAssignment>;

/// <summary>
/// FluentValidation validator for <see cref="AssignResponsibilityToPersonCommand"/>.
/// </summary>
public sealed class AssignResponsibilityToPersonCommandValidator : AbstractValidator<AssignResponsibilityToPersonCommand>
{
    public AssignResponsibilityToPersonCommandValidator()
    {
        RuleFor(x => x.PersonId)
            .NotEmpty().WithMessage("PersonId is required.");

        RuleFor(x => x.ResponsibilityId)
            .NotEmpty().WithMessage("ResponsibilityId is required.");
    }
}

/// <summary>
/// MediatR handler for <see cref="AssignResponsibilityToPersonCommand"/> delegating to <see cref="IOrganizationService"/>.
/// </summary>
public sealed class AssignResponsibilityToPersonCommandHandler : IRequestHandler<AssignResponsibilityToPersonCommand, ResponsibilityAssignment>
{
    private readonly IOrganizationService _organizationService;

    public AssignResponsibilityToPersonCommandHandler(IOrganizationService organizationService)
    {
        _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
    }

    public Task<ResponsibilityAssignment> Handle(AssignResponsibilityToPersonCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _organizationService.AssignResponsibilityToPersonAsync(
            request.PersonId,
            request.ResponsibilityId,
            cancellationToken);
    }
}
