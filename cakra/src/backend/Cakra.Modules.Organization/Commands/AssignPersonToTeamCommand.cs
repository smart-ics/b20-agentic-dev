using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Services;
using FluentValidation;
using MediatR;

namespace Cakra.Modules.Organization.Commands;

/// <summary>
/// Command to assign a <see cref="Person"/> to an organizational <see cref="Team"/> (Architecture §7, §19.2).
/// </summary>
public sealed record AssignPersonToTeamCommand(
    Guid PersonId,
    Guid TeamId) : IRequest<TeamMembership>;

/// <summary>
/// FluentValidation validator for <see cref="AssignPersonToTeamCommand"/>.
/// </summary>
public sealed class AssignPersonToTeamCommandValidator : AbstractValidator<AssignPersonToTeamCommand>
{
    public AssignPersonToTeamCommandValidator()
    {
        RuleFor(x => x.PersonId)
            .NotEmpty().WithMessage("PersonId is required.");

        RuleFor(x => x.TeamId)
            .NotEmpty().WithMessage("TeamId is required.");
    }
}

/// <summary>
/// MediatR handler for <see cref="AssignPersonToTeamCommand"/> delegating to <see cref="IOrganizationService"/>.
/// </summary>
public sealed class AssignPersonToTeamCommandHandler : IRequestHandler<AssignPersonToTeamCommand, TeamMembership>
{
    private readonly IOrganizationService _organizationService;

    public AssignPersonToTeamCommandHandler(IOrganizationService organizationService)
    {
        _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
    }

    public Task<TeamMembership> Handle(AssignPersonToTeamCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _organizationService.AssignPersonToTeamAsync(
            request.PersonId,
            request.TeamId,
            cancellationToken);
    }
}
