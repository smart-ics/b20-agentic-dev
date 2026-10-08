using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Persistence;
using Cakra.Modules.Organization.Services;
using FluentValidation;
using MediatR;

namespace Cakra.Modules.Organization.Commands;

/// <summary>
/// Command to update an existing organizational <see cref="Person"/> with assigned roles and self-demotion verification (Architecture §7, §19.2; CR-027).
/// </summary>
public sealed record UpdatePersonCommand(
    Guid PersonId,
    string FirstName,
    string LastName,
    string Email,
    IReadOnlyList<Guid>? RoleIds,
    Guid? ActorPersonId = null) : IRequest<Person>
{
    public bool IsLegacy { get; init; }
    public Guid Id => PersonId;

    public UpdatePersonCommand(Guid personId, string firstName, string lastName, string email)
        : this(personId, firstName, lastName, email, null, null)
    {
        IsLegacy = true;
    }
}

/// <summary>
/// FluentValidation validator for <see cref="UpdatePersonCommand"/>.
/// Validates person attributes and prevents administrator self-demotion (Architecture §7, §19.2; CR-027).
/// Note: When <see cref="IRoleAssignmentRepository"/> is provided, verifies if the target person currently holds the Administrator role;
/// otherwise assumes caller is currently an Administrator editing their own record per FEAT-ORG-001 administrative preconditions.
/// </summary>
public sealed class UpdatePersonCommandValidator : AbstractValidator<UpdatePersonCommand>
{
    public UpdatePersonCommandValidator() : this((IRoleRepository?)null, (IRoleAssignmentRepository?)null, null)
    {
    }

    public UpdatePersonCommandValidator(Guid administratorRoleId) : this(null, null, administratorRoleId)
    {
    }

    internal UpdatePersonCommandValidator(IRoleRepository? roleRepository) : this(roleRepository, null, null)
    {
    }

    internal UpdatePersonCommandValidator(IRoleRepository? roleRepository, IRoleAssignmentRepository? roleAssignmentRepository) : this(roleRepository, roleAssignmentRepository, null)
    {
    }

    internal UpdatePersonCommandValidator(IRoleRepository? roleRepository, IRoleAssignmentRepository? roleAssignmentRepository, Guid? administratorRoleId)
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

        RuleFor(x => x.RoleIds)
            .NotEmpty().WithMessage("At least one role must be assigned.")
            .When(x => !x.IsLegacy);

        if (administratorRoleId.HasValue)
        {
            RuleFor(x => x)
                .MustAsync(async (cmd, cancellationToken) =>
                {
                    if (cmd.ActorPersonId.HasValue && cmd.ActorPersonId.Value == cmd.PersonId)
                    {
                        if (roleAssignmentRepository != null)
                        {
                            var activeAssignments = await roleAssignmentRepository.GetActiveByPersonIdAsync(cmd.PersonId, cancellationToken);
                            var isCurrentlyAdmin = activeAssignments.Any(a => a.RoleId == administratorRoleId.Value);
                            if (!isCurrentlyAdmin)
                            {
                                return true;
                            }
                        }
                        return cmd.RoleIds != null && cmd.RoleIds.Contains(administratorRoleId.Value);
                    }
                    return true;
                })
                .WithMessage("Cannot remove the Administrator role from your own person record.")
                .When(x => !x.IsLegacy);
        }
        else if (roleRepository != null)
        {
            RuleFor(x => x)
                .MustAsync(async (cmd, cancellationToken) =>
                {
                    if (cmd.ActorPersonId.HasValue && cmd.ActorPersonId.Value == cmd.PersonId)
                    {
                        var adminRole = await roleRepository.GetByNameAsync("Administrator", cancellationToken);
                        if (adminRole != null)
                        {
                            if (roleAssignmentRepository != null)
                            {
                                var activeAssignments = await roleAssignmentRepository.GetActiveByPersonIdAsync(cmd.PersonId, cancellationToken);
                                var isCurrentlyAdmin = activeAssignments.Any(a => a.RoleId == adminRole.Id);
                                if (!isCurrentlyAdmin)
                                {
                                    return true;
                                }
                            }
                            return cmd.RoleIds != null && cmd.RoleIds.Contains(adminRole.Id);
                        }
                    }
                    return true;
                })
                .WithMessage("Cannot remove the Administrator role from your own person record.")
                .When(x => !x.IsLegacy);
        }
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
        if (request.IsLegacy || request.RoleIds is null)
        {
            return _organizationService.UpdatePersonAsync(
                request.PersonId,
                request.FirstName,
                request.LastName,
                request.Email,
                cancellationToken);
        }

        return _organizationService.UpdatePersonAsync(
            request.PersonId,
            request.FirstName,
            request.LastName,
            request.Email,
            request.RoleIds,
            request.ActorPersonId,
            cancellationToken);
    }
}
