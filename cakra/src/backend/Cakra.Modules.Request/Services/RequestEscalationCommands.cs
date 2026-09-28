using Cakra.Modules.Request.Domain;
using FluentValidation;
using MediatR;

namespace Cakra.Modules.Request.Services;

/// <summary>
/// Command to escalate a Request in <c>EVALUATING</c> or <c>IN_PROGRESS</c> state to <c>ESCALATED</c>,
/// recording the escalation reason and actor and emitting <c>RequestEscalated</c>
/// (Architecture §7, §8 — UC-REQ-006).
/// </summary>
public sealed record EscalateRequestCommand(
    Guid RequestId,
    string Reason,
    Guid? ActorPersonId = null) : IRequest<RequestDto>
{
    /// <summary>Convenience alias for <see cref="Reason"/>.</summary>
    public string EscalationReason => Reason;
}

public sealed class EscalateRequestCommandValidator : AbstractValidator<EscalateRequestCommand>
{
    public EscalateRequestCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Escalation reason is required.");

        RuleFor(x => x.ActorPersonId)
            .Must(id => id != Guid.Empty)
            .When(x => x.ActorPersonId.HasValue)
            .WithMessage("Actor person ID cannot be empty.");
    }
}

/// <summary>
/// Command to record a management decision request or determination on an active or escalated Request,
/// emitting <c>ManagementDecisionRequested</c> (Architecture §7, §8 — UC-REQ-007).
/// </summary>
public sealed record RequestManagementDecisionCommand(
    Guid RequestId,
    string DecisionDetails,
    Guid? ActorPersonId = null,
    RequestStatus? TargetStatus = null) : IRequest<RequestDto>
{
    /// <summary>Convenience alias for <see cref="DecisionDetails"/>.</summary>
    public string DecisionNotes => DecisionDetails;

    /// <summary>Convenience alias for <see cref="DecisionDetails"/>.</summary>
    public string Reason => DecisionDetails;
}

public sealed class RequestManagementDecisionCommandValidator : AbstractValidator<RequestManagementDecisionCommand>
{
    public RequestManagementDecisionCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");

        RuleFor(x => x.DecisionDetails)
            .NotEmpty().WithMessage("Management decision details are required.");

        RuleFor(x => x.ActorPersonId)
            .Must(id => id != Guid.Empty)
            .When(x => x.ActorPersonId.HasValue)
            .WithMessage("Actor person ID cannot be empty.");

        RuleFor(x => x.TargetStatus)
            .Must(status => status == RequestStatus.Evaluating || status == RequestStatus.InProgress)
            .When(x => x.TargetStatus.HasValue)
            .WithMessage("Target status for management decision must be EVALUATING or IN_PROGRESS.");
    }
}

/// <summary>
/// Command to reassign Request ownership to a new active Person in Organization, validating the
/// new assignee via <c>IOrganizationQueryService</c> and emitting <c>RequestAssigned</c>
/// (Architecture §7, §8 — UC-MGT-001).
/// </summary>
public sealed record ReassignRequestOwnershipCommand(
    Guid RequestId,
    Guid NewOwnerPersonId,
    string? Notes = null,
    Guid? ActorPersonId = null,
    RequestStatus? TargetStatusForEscalated = null) : IRequest<RequestDto>
{
    /// <summary>Convenience alias for <see cref="NewOwnerPersonId"/>.</summary>
    public Guid OwnerPersonId => NewOwnerPersonId;
}

public sealed class ReassignRequestOwnershipCommandValidator : AbstractValidator<ReassignRequestOwnershipCommand>
{
    public ReassignRequestOwnershipCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");

        RuleFor(x => x.NewOwnerPersonId)
            .NotEmpty().WithMessage("New owner person ID is required.");

        RuleFor(x => x.ActorPersonId)
            .Must(id => id != Guid.Empty)
            .When(x => x.ActorPersonId.HasValue)
            .WithMessage("Actor person ID cannot be empty.");

        RuleFor(x => x.TargetStatusForEscalated)
            .Must(status => status == RequestStatus.Evaluating || status == RequestStatus.InProgress)
            .When(x => x.TargetStatusForEscalated.HasValue)
            .WithMessage("Target status when reassigning an escalated request must be EVALUATING or IN_PROGRESS.");
    }
}
