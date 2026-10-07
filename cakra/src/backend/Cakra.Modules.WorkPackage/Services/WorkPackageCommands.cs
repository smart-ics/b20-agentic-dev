using FluentValidation;
using MediatR;

namespace Cakra.Modules.WorkPackage.Services;

/// <summary>
/// Command to create a new <see cref="Domain.WorkPackage"/> in <c>DRAFT</c> state (Architecture §11 — UC-WP-001).
/// </summary>
public sealed record CreateWorkPackageCommand(
    string Name,
    string Objective,
    Guid OwnerPersonId,
    Guid? CustomerId = null,
    Guid? ProductId = null,
    DateTime? Deadline = null) : IRequest<WorkPackageDto>;

public sealed class CreateWorkPackageCommandValidator : AbstractValidator<CreateWorkPackageCommand>
{
    public CreateWorkPackageCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Work package name is required.")
            .MaximumLength(255).WithMessage("Work package name must not exceed 255 characters.");

        RuleFor(x => x.Objective)
            .NotEmpty().WithMessage("Work package objective is required.");

        RuleFor(x => x.OwnerPersonId)
            .NotEmpty().WithMessage("Work package owner person ID is required.");

        RuleFor(x => x.CustomerId)
            .Must(id => id != Guid.Empty)
            .When(x => x.CustomerId.HasValue)
            .WithMessage("Customer ID cannot be empty.");

        RuleFor(x => x.ProductId)
            .Must(id => id != Guid.Empty)
            .When(x => x.ProductId.HasValue)
            .WithMessage("Product ID cannot be empty.");
    }
}

/// <summary>
/// Command to update the title and objective of an existing <see cref="Domain.WorkPackage"/> (Architecture §11 — UC-WP-001).
/// </summary>
public sealed record UpdateObjectiveCommand(
    Guid WorkPackageId,
    string Name,
    string Objective) : IRequest<WorkPackageDto>;

public sealed class UpdateObjectiveCommandValidator : AbstractValidator<UpdateObjectiveCommand>
{
    public UpdateObjectiveCommandValidator()
    {
        RuleFor(x => x.WorkPackageId)
            .NotEmpty().WithMessage("Work package ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Work package name is required.")
            .MaximumLength(255).WithMessage("Work package name must not exceed 255 characters.");

        RuleFor(x => x.Objective)
            .NotEmpty().WithMessage("Work package objective is required.");
    }
}

/// <summary>
/// Command to reassign ownership of a <see cref="Domain.WorkPackage"/> to a new Person (Architecture §11 — UC-WP-001).
/// </summary>
public sealed record AssignOwnerCommand(
    Guid WorkPackageId,
    Guid NewOwnerPersonId) : IRequest<WorkPackageDto>;

public sealed class AssignOwnerCommandValidator : AbstractValidator<AssignOwnerCommand>
{
    public AssignOwnerCommandValidator()
    {
        RuleFor(x => x.WorkPackageId)
            .NotEmpty().WithMessage("Work package ID is required.");

        RuleFor(x => x.NewOwnerPersonId)
            .NotEmpty().WithMessage("New owner person ID is required.");
    }
}

/// <summary>
/// Convenience alias command for <see cref="AssignOwnerCommand"/> (Architecture §11 — UC-WP-001).
/// </summary>
public sealed record AssignWorkPackageOwnerCommand(
    Guid WorkPackageId,
    Guid NewOwnerPersonId) : IRequest<WorkPackageDto>;

public sealed class AssignWorkPackageOwnerCommandValidator : AbstractValidator<AssignWorkPackageOwnerCommand>
{
    public AssignWorkPackageOwnerCommandValidator()
    {
        RuleFor(x => x.WorkPackageId)
            .NotEmpty().WithMessage("Work package ID is required.");

        RuleFor(x => x.NewOwnerPersonId)
            .NotEmpty().WithMessage("New owner person ID is required.");
    }
}

/// <summary>
/// Command to associate a Request with a <see cref="Domain.WorkPackage"/>, enforcing Business Rule 9
/// (Architecture §11 — UC-WP-002).
/// </summary>
public sealed record AddRequestToWorkPackageCommand(
    Guid WorkPackageId,
    Guid RequestId) : IRequest<WorkPackageDto>;

public sealed class AddRequestToWorkPackageCommandValidator : AbstractValidator<AddRequestToWorkPackageCommand>
{
    public AddRequestToWorkPackageCommandValidator()
    {
        RuleFor(x => x.WorkPackageId)
            .NotEmpty().WithMessage("Work package ID is required.");

        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");
    }
}

/// <summary>
/// Command to remove a Request from a <see cref="Domain.WorkPackage"/> by deactivating its membership link
/// (Architecture §11 — UC-WP-002).
/// </summary>
public sealed record RemoveRequestFromWorkPackageCommand(
    Guid WorkPackageId,
    Guid RequestId) : IRequest<WorkPackageDto>;

public sealed class RemoveRequestFromWorkPackageCommandValidator : AbstractValidator<RemoveRequestFromWorkPackageCommand>
{
    public RemoveRequestFromWorkPackageCommandValidator()
    {
        RuleFor(x => x.WorkPackageId)
            .NotEmpty().WithMessage("Work package ID is required.");

        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");
    }
}

/// <summary>
/// Command to transition a <see cref="Domain.WorkPackage"/> from <c>DRAFT</c> to <c>ACTIVE</c> state
/// (Architecture §11 — UC-WP-001).
/// </summary>
public sealed record ActivateWorkPackageCommand(
    Guid WorkPackageId) : IRequest<WorkPackageDto>;

public sealed class ActivateWorkPackageCommandValidator : AbstractValidator<ActivateWorkPackageCommand>
{
    public ActivateWorkPackageCommandValidator()
    {
        RuleFor(x => x.WorkPackageId)
            .NotEmpty().WithMessage("Work package ID is required.");
    }
}

/// <summary>
/// Command to transition a <see cref="Domain.WorkPackage"/> from <c>ACTIVE</c> or <c>DRAFT</c> to <c>CLOSED</c> state
/// (Architecture §11 — UC-WP-001).
/// </summary>
public sealed record CloseWorkPackageCommand(
    Guid WorkPackageId,
    string Reason) : IRequest<WorkPackageDto>;

public sealed class CloseWorkPackageCommandValidator : AbstractValidator<CloseWorkPackageCommand>
{
    public CloseWorkPackageCommandValidator()
    {
        RuleFor(x => x.WorkPackageId)
            .NotEmpty().WithMessage("Work package ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Close reason is required.");
    }
}

/// <summary>
/// Command to reorder the active constituent requests within a <see cref="Domain.WorkPackage"/>
/// (CR-015; Architecture §4 TD-002, TD-003).
/// </summary>
public sealed record ReorderWorkPackageRequestsCommand(
    Guid WorkPackageId,
    IReadOnlyList<Guid> OrderedRequestIds) : IRequest<WorkPackageDto>;

public sealed class ReorderWorkPackageRequestsCommandValidator : AbstractValidator<ReorderWorkPackageRequestsCommand>
{
    public ReorderWorkPackageRequestsCommandValidator()
    {
        RuleFor(x => x.WorkPackageId)
            .NotEmpty().WithMessage("Work package ID is required.");

        RuleFor(x => x.OrderedRequestIds)
            .NotNull().WithMessage("Ordered request IDs list is required.")
            .NotEmpty().WithMessage("Ordered request IDs list cannot be empty.");
    }
}

/// <summary>
/// Command to update or clear the target deadline date of a <see cref="Domain.WorkPackage"/>
/// (CR-023; Architecture §4 TD-004).
/// </summary>
public sealed record UpdateWorkPackageDeadlineCommand(
    Guid WorkPackageId,
    DateTime? Deadline) : IRequest<WorkPackageDto>;

public sealed class UpdateWorkPackageDeadlineCommandValidator : AbstractValidator<UpdateWorkPackageDeadlineCommand>
{
    public UpdateWorkPackageDeadlineCommandValidator()
    {
        RuleFor(x => x.WorkPackageId)
            .NotEmpty().WithMessage("Work package ID is required.");
    }
}

/// <summary>
/// Command to update or clear the Customer and Product context associations of a <see cref="Domain.WorkPackage"/>
/// (CR-024; Architecture §4 TD-004).
/// </summary>
public sealed record UpdateWorkPackageContextCommand(
    Guid WorkPackageId,
    Guid? CustomerId,
    Guid? ProductId) : IRequest<WorkPackageDto>;

public sealed class UpdateWorkPackageContextCommandValidator : AbstractValidator<UpdateWorkPackageContextCommand>
{
    public UpdateWorkPackageContextCommandValidator()
    {
        RuleFor(x => x.WorkPackageId)
            .NotEmpty().WithMessage("Work package ID is required.");
    }
}

