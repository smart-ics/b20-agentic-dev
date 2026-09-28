using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Services;
using FluentValidation;
using MediatR;

namespace Cakra.Modules.Organization.Commands;

/// <summary>
/// Command to create a new organizational <see cref="Role"/> (Architecture §7, §14, §19.2).
/// </summary>
public sealed record CreateRoleCommand(
    string Name,
    string? Description = null) : IRequest<Role>;

/// <summary>
/// FluentValidation validator for <see cref="CreateRoleCommand"/>.
/// </summary>
public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }
}

/// <summary>
/// MediatR handler for <see cref="CreateRoleCommand"/> delegating to <see cref="IOrganizationService"/>.
/// </summary>
public sealed class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, Role>
{
    private readonly IOrganizationService _organizationService;

    public CreateRoleCommandHandler(IOrganizationService organizationService)
    {
        _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
    }

    public Task<Role> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _organizationService.CreateRoleAsync(
            request.Name,
            request.Description,
            cancellationToken);
    }
}
