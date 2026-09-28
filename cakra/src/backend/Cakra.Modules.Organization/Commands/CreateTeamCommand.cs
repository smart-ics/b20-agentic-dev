using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Services;
using FluentValidation;
using MediatR;

namespace Cakra.Modules.Organization.Commands;

/// <summary>
/// Command to create a new organizational <see cref="Team"/> (Architecture §7, §19.2).
/// </summary>
public sealed record CreateTeamCommand(
    string Name,
    string? Description = null) : IRequest<Team>;

/// <summary>
/// FluentValidation validator for <see cref="CreateTeamCommand"/>.
/// </summary>
public sealed class CreateTeamCommandValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }
}

/// <summary>
/// MediatR handler for <see cref="CreateTeamCommand"/> delegating to <see cref="IOrganizationService"/>.
/// </summary>
public sealed class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, Team>
{
    private readonly IOrganizationService _organizationService;

    public CreateTeamCommandHandler(IOrganizationService organizationService)
    {
        _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
    }

    public Task<Team> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _organizationService.CreateTeamAsync(
            request.Name,
            request.Description,
            cancellationToken);
    }
}
