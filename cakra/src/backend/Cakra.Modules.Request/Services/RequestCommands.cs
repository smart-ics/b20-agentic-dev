using FluentValidation;
using MediatR;

namespace Cakra.Modules.Request.Services;

/// <summary>
/// Lightweight data transfer record for defining an initial sub-task during request recording.
/// </summary>
public sealed record InitialSubTaskDto(string Title, Guid? AssigneePersonId = null);

/// <summary>
/// Command to record a new operational Request in <c>CAPTURED</c> state (Architecture §7, §8 — UC-REQ-001).
/// </summary>
public sealed record RecordRequestCommand(
    string Title,
    string Description,
    Guid? CustomerId = null,
    Guid? ProductId = null,
    string RequestType = "GENERAL",
    string Priority = "NORMAL",
    Guid? ActorPersonId = null,
    Guid? WorkPackageId = null,
    int? Complexity = null,
    IReadOnlyList<InitialSubTaskDto>? InitialSubTasks = null) : IRequest<RequestDto>;

public sealed class RecordRequestCommandValidator : AbstractValidator<RecordRequestCommand>
{
    public RecordRequestCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Request title is required.")
            .MaximumLength(255).WithMessage("Request title must not exceed 255 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Request description is required.");

        RuleFor(x => x.RequestType)
            .NotEmpty().WithMessage("Request type is required.")
            .MaximumLength(50).WithMessage("Request type must not exceed 50 characters.");

        RuleFor(x => x.Priority)
            .NotEmpty().WithMessage("Priority is required.")
            .MaximumLength(20).WithMessage("Priority must not exceed 20 characters.");

        RuleFor(x => x.Complexity)
            .InclusiveBetween(1, 5)
            .When(x => x.Complexity.HasValue)
            .WithMessage("Complexity must be an integer between 1 and 5.");

        RuleFor(x => x.CustomerId)
            .Must(id => id != Guid.Empty)
            .When(x => x.CustomerId.HasValue)
            .WithMessage("Customer ID cannot be empty.");

        RuleFor(x => x.ProductId)
            .Must(id => id != Guid.Empty)
            .When(x => x.ProductId.HasValue)
            .WithMessage("Product ID cannot be empty.");

        RuleFor(x => x.WorkPackageId)
            .Must(id => id != Guid.Empty)
            .When(x => x.WorkPackageId.HasValue)
            .WithMessage("Work package ID cannot be empty.");

        RuleFor(x => x.ActorPersonId)
            .Must(id => id != Guid.Empty)
            .When(x => x.ActorPersonId.HasValue)
            .WithMessage("Actor person ID cannot be empty.");

        RuleForEach(x => x.InitialSubTasks)
            .ChildRules(subTask =>
            {
                subTask.RuleFor(s => s.Title)
                    .NotEmpty().WithMessage("Sub-task title is required.")
                    .MaximumLength(255).WithMessage("Sub-task title must not exceed 255 characters.");

                subTask.RuleFor(s => s.AssigneePersonId)
                    .Must(id => id != Guid.Empty)
                    .When(s => s.AssigneePersonId.HasValue)
                    .WithMessage("Assignee person ID cannot be empty.");
            })
            .When(x => x.InitialSubTasks != null);
    }
}

/// <summary>
/// Command to assign an organizational owner to a Request, transitioning <c>CAPTURED -&gt; EVALUATING</c>
/// (Architecture §7, §8 — UC-REQ-002).
/// </summary>
public sealed record AssignRequestOwnerCommand(
    Guid RequestId,
    Guid OwnerPersonId,
    string? Notes = null,
    Guid? ActorPersonId = null) : IRequest<RequestDto>;

public sealed class AssignRequestOwnerCommandValidator : AbstractValidator<AssignRequestOwnerCommand>
{
    public AssignRequestOwnerCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");

        RuleFor(x => x.OwnerPersonId)
            .NotEmpty().WithMessage("Owner person ID is required.");

        RuleFor(x => x.ActorPersonId)
            .Must(id => id != Guid.Empty)
            .When(x => x.ActorPersonId.HasValue)
            .WithMessage("Actor person ID cannot be empty.");
    }
}

/// <summary>
/// Command to record triage evaluation notes on a Request in <c>EVALUATING</c> state
/// (Architecture §7, §8 — UC-REQ-003).
/// </summary>
public sealed record EvaluateRequestCommand(
    Guid RequestId,
    string EvaluationNotes,
    Guid? ActorPersonId = null,
    int? Complexity = null) : IRequest<RequestDto>;

public sealed class EvaluateRequestCommandValidator : AbstractValidator<EvaluateRequestCommand>
{
    public EvaluateRequestCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");

        RuleFor(x => x.EvaluationNotes)
            .NotEmpty().WithMessage("Evaluation notes are required.");

        RuleFor(x => x.Complexity)
            .InclusiveBetween(1, 5)
            .When(x => x.Complexity.HasValue)
            .WithMessage("Complexity must be an integer between 1 and 5.");

        RuleFor(x => x.ActorPersonId)
            .Must(id => id != Guid.Empty)
            .When(x => x.ActorPersonId.HasValue)
            .WithMessage("Actor person ID cannot be empty.");
    }
}

/// <summary>
/// Command to update the complexity of an operational Request (Architecture §4 TD-004, TD-005).
/// </summary>
public sealed record UpdateRequestComplexityCommand(
    Guid RequestId,
    int Complexity,
    string? Reason = null,
    Guid? ActorPersonId = null) : IRequest<RequestDto>;

public sealed class UpdateRequestComplexityCommandValidator : AbstractValidator<UpdateRequestComplexityCommand>
{
    public UpdateRequestComplexityCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");

        RuleFor(x => x.Complexity)
            .InclusiveBetween(1, 5).WithMessage("Complexity must be an integer between 1 and 5.");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");

        RuleFor(x => x.ActorPersonId)
            .Must(id => id != Guid.Empty)
            .When(x => x.ActorPersonId.HasValue)
            .WithMessage("Actor person ID cannot be empty.");
    }
}

/// <summary>
/// Command to accept operational responsibility for a Request, transitioning it to <c>IN_PROGRESS</c>
/// and emitting <c>RequestAccepted</c> (Architecture §7, §8 — UC-REQ-004).
/// </summary>
public sealed record AcceptRequestResponsibilityCommand(
    Guid RequestId,
    string? Notes = null,
    Guid? ActorPersonId = null) : IRequest<RequestDto>;

public sealed class AcceptRequestResponsibilityCommandValidator : AbstractValidator<AcceptRequestResponsibilityCommand>
{
    public AcceptRequestResponsibilityCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");

        RuleFor(x => x.ActorPersonId)
            .Must(id => id != Guid.Empty)
            .When(x => x.ActorPersonId.HasValue)
            .WithMessage("Actor person ID cannot be empty.");
    }
}

/// <summary>
/// Command to reject a Request during evaluation, transitioning <c>EVALUATING -&gt; REJECTED</c>,
/// recording the rejection reason, and emitting <c>RequestRejected</c> (Architecture §7, §8 — UC-REQ-005).
/// </summary>
public sealed record RejectRequestCommand(
    Guid RequestId,
    string Reason,
    Guid? ActorPersonId = null) : IRequest<RequestDto>
{
    /// <summary>Convenience alias for <see cref="Reason"/>.</summary>
    public string RejectionReason => Reason;
}

public sealed class RejectRequestCommandValidator : AbstractValidator<RejectRequestCommand>
{
    public RejectRequestCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Rejection reason is required.");

        RuleFor(x => x.ActorPersonId)
            .Must(id => id != Guid.Empty)
            .When(x => x.ActorPersonId.HasValue)
            .WithMessage("Actor person ID cannot be empty.");
    }
}

/// <summary>
/// Command to review and complete work on a Request, transitioning <c>IN_PROGRESS -&gt; COMPLETED</c>,
/// recording completion details, and emitting <c>RequestCompleted</c> (Architecture §7, §8 — UC-REQ-008).
/// </summary>
public sealed record ReviewRequestCompletionCommand(
    Guid RequestId,
    string ResolutionDescription,
    Guid? ActorPersonId = null) : IRequest<RequestDto>
{
    /// <summary>Convenience alias for <see cref="ResolutionDescription"/>.</summary>
    public string CompletionDetails => ResolutionDescription;

    /// <summary>Convenience alias for <see cref="ResolutionDescription"/>.</summary>
    public string Summary => ResolutionDescription;
}

public sealed class ReviewRequestCompletionCommandValidator : AbstractValidator<ReviewRequestCompletionCommand>
{
    public ReviewRequestCompletionCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");

        RuleFor(x => x.ResolutionDescription)
            .NotEmpty().WithMessage("Resolution description is required.");

        RuleFor(x => x.ActorPersonId)
            .Must(id => id != Guid.Empty)
            .When(x => x.ActorPersonId.HasValue)
            .WithMessage("Actor person ID cannot be empty.");
    }
}

/// <summary>
/// Convenience alias command for <see cref="ReviewRequestCompletionCommand"/> (UC-REQ-008).
/// </summary>
public sealed record CompleteRequestCommand(
    Guid RequestId,
    string ResolutionDescription,
    Guid? ActorPersonId = null) : IRequest<RequestDto>;

public sealed class CompleteRequestCommandValidator : AbstractValidator<CompleteRequestCommand>
{
    public CompleteRequestCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");

        RuleFor(x => x.ResolutionDescription)
            .NotEmpty().WithMessage("Resolution description is required.");

        RuleFor(x => x.ActorPersonId)
            .Must(id => id != Guid.Empty)
            .When(x => x.ActorPersonId.HasValue)
            .WithMessage("Actor person ID cannot be empty.");
    }
}

/// <summary>
/// Command to add an operational sub-task checklist item to a Request (Architecture CR-006 §4 TD-001, TD-006).
/// </summary>
public sealed record AddRequestSubTaskCommand(
    Guid RequestId,
    string Title,
    Guid? AssigneePersonId = null,
    Guid? ActorPersonId = null) : IRequest<RequestDto>;

public sealed class AddRequestSubTaskCommandValidator : AbstractValidator<AddRequestSubTaskCommand>
{
    public AddRequestSubTaskCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Sub-task title is required.")
            .MaximumLength(255).WithMessage("Sub-task title must not exceed 255 characters.");

        RuleFor(x => x.AssigneePersonId)
            .Must(id => id != Guid.Empty)
            .When(x => x.AssigneePersonId.HasValue)
            .WithMessage("Assignee person ID cannot be empty.");

        RuleFor(x => x.ActorPersonId)
            .Must(id => id != Guid.Empty)
            .When(x => x.ActorPersonId.HasValue)
            .WithMessage("Actor person ID cannot be empty.");
    }
}

/// <summary>
/// Command to mark a sub-task checklist item completed on a Request (Architecture CR-006 §4 TD-001, TD-006).
/// </summary>
public sealed record CompleteRequestSubTaskCommand(
    Guid RequestId,
    Guid SubTaskId,
    Guid? ActorPersonId = null) : IRequest<RequestDto>;

public sealed class CompleteRequestSubTaskCommandValidator : AbstractValidator<CompleteRequestSubTaskCommand>
{
    public CompleteRequestSubTaskCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");

        RuleFor(x => x.SubTaskId)
            .NotEmpty().WithMessage("Sub-task ID is required.");

        RuleFor(x => x.ActorPersonId)
            .Must(id => id != Guid.Empty)
            .When(x => x.ActorPersonId.HasValue)
            .WithMessage("Actor person ID cannot be empty.");
    }
}

/// <summary>
/// Command to reopen a completed sub-task checklist item back to pending on a Request (Architecture CR-006 §4 TD-001, TD-003, TD-006).
/// </summary>
public sealed record ReopenRequestSubTaskCommand(
    Guid RequestId,
    Guid SubTaskId,
    Guid? ActorPersonId = null) : IRequest<RequestDto>;

public sealed class ReopenRequestSubTaskCommandValidator : AbstractValidator<ReopenRequestSubTaskCommand>
{
    public ReopenRequestSubTaskCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");

        RuleFor(x => x.SubTaskId)
            .NotEmpty().WithMessage("Sub-task ID is required.");

        RuleFor(x => x.ActorPersonId)
            .Must(id => id != Guid.Empty)
            .When(x => x.ActorPersonId.HasValue)
            .WithMessage("Actor person ID cannot be empty.");
    }
}

/// <summary>
/// Command to remove a sub-task checklist item from a Request (Architecture CR-006 §4 TD-001, TD-006).
/// </summary>
public sealed record RemoveRequestSubTaskCommand(
    Guid RequestId,
    Guid SubTaskId,
    Guid? ActorPersonId = null) : IRequest<RequestDto>;

public sealed class RemoveRequestSubTaskCommandValidator : AbstractValidator<RemoveRequestSubTaskCommand>
{
    public RemoveRequestSubTaskCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");

        RuleFor(x => x.SubTaskId)
            .NotEmpty().WithMessage("Sub-task ID is required.");

        RuleFor(x => x.ActorPersonId)
            .Must(id => id != Guid.Empty)
            .When(x => x.ActorPersonId.HasValue)
            .WithMessage("Actor person ID cannot be empty.");
    }
}
