using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Services;
using FluentValidation;
using MediatR;

namespace Cakra.Modules.Organization.Commands;

/// <summary>
/// Command to create a new organizational <see cref="Responsibility"/> (Architecture §7, §16, §19.2).
/// </summary>
public sealed record CreateResponsibilityCommand(
    string Name,
    string? Description = null) : IRequest<Responsibility>;

/// <summary>
/// FluentValidation validator for <see cref="CreateResponsibilityCommand"/>.
/// </summary>
public sealed class CreateResponsibilityCommandValidator : AbstractValidator<CreateResponsibilityCommand>
{
    public CreateResponsibilityCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }
}

/// <summary>
/// MediatR handler for <see cref="CreateResponsibilityCommand"/> delegating to <see cref="IOrganizationService"/>.
/// </summary>
public sealed class CreateResponsibilityCommandHandler : IRequestHandler<CreateResponsibilityCommand, Responsibility>
{
    private readonly IOrganizationService _organizationService;

    public CreateResponsibilityCommandHandler(IOrganizationService organizationService)
    {
        _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
    }

    public Task<Responsibility> Handle(CreateResponsibilityCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _organizationService.CreateResponsibilityAsync(
            request.Name,
            request.Description,
            cancellationToken);
    }
}
