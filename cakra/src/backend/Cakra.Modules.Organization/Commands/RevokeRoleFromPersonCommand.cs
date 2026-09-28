using Cakra.Modules.Organization.Services;
using FluentValidation;
using MediatR;

namespace Cakra.Modules.Organization.Commands;

/// <summary>
/// Command to revoke an organizational role assignment from a person (Architecture §7, §14, §19.2).
/// </summary>
public sealed record RevokeRoleFromPersonCommand(
    Guid PersonId,
    Guid RoleId) : IRequest;

/// <summary>
/// FluentValidation validator for <see cref="RevokeRoleFromPersonCommand"/>.
/// </summary>
public sealed class RevokeRoleFromPersonCommandValidator : AbstractValidator<RevokeRoleFromPersonCommand>
{
    public RevokeRoleFromPersonCommandValidator()
    {
        RuleFor(x => x.PersonId)
            .NotEmpty().WithMessage("PersonId is required.");

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("RoleId is required.");
    }
}

/// <summary>
/// MediatR handler for <see cref="RevokeRoleFromPersonCommand"/> delegating to <see cref="IOrganizationService"/>.
/// </summary>
public sealed class RevokeRoleFromPersonCommandHandler : IRequestHandler<RevokeRoleFromPersonCommand>
{
    private readonly IOrganizationService _organizationService;

    public RevokeRoleFromPersonCommandHandler(IOrganizationService organizationService)
    {
        _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
    }

    public Task Handle(RevokeRoleFromPersonCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _organizationService.RevokeRoleFromPersonAsync(
            request.PersonId,
            request.RoleId,
            cancellationToken);
    }
}
