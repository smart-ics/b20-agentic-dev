using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Services;
using FluentValidation;
using MediatR;

namespace Cakra.Modules.Organization.Commands;

/// <summary>
/// Command to assign an organizational <see cref="Role"/> to a <see cref="Person"/> (Architecture §7, §14, §19.2).
/// </summary>
public sealed record AssignRoleToPersonCommand(
    Guid PersonId,
    Guid RoleId) : IRequest<RoleAssignment>;

/// <summary>
/// FluentValidation validator for <see cref="AssignRoleToPersonCommand"/>.
/// </summary>
public sealed class AssignRoleToPersonCommandValidator : AbstractValidator<AssignRoleToPersonCommand>
{
    public AssignRoleToPersonCommandValidator()
    {
        RuleFor(x => x.PersonId)
            .NotEmpty().WithMessage("PersonId is required.");

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("RoleId is required.");
    }
}

/// <summary>
/// MediatR handler for <see cref="AssignRoleToPersonCommand"/> delegating to <see cref="IOrganizationService"/>.
/// </summary>
public sealed class AssignRoleToPersonCommandHandler : IRequestHandler<AssignRoleToPersonCommand, RoleAssignment>
{
    private readonly IOrganizationService _organizationService;

    public AssignRoleToPersonCommandHandler(IOrganizationService organizationService)
    {
        _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
    }

    public Task<RoleAssignment> Handle(AssignRoleToPersonCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _organizationService.AssignRoleToPersonAsync(
            request.PersonId,
            request.RoleId,
            cancellationToken);
    }
}
