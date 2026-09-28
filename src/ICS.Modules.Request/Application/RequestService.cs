namespace ICS.Modules.Request.Application;

using ICS.Modules.Request.Application.Commands;
using ICS.Modules.Request.Application.DTOs;
using MediatR;

/// <summary>
/// Concrete implementation of <see cref="IRequestService"/> executing commands through MediatR pipeline.
/// Architecture §7, §8, §16, §19.2, §20.
/// </summary>
public class RequestService : IRequestService
{
    private readonly ISender _sender;

    public RequestService(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public Task<RequestDto> RecordRequestAsync(
        string title,
        string description,
        string type,
        string? priority = null,
        Guid? requesterPersonId = null,
        Guid? requesterContactId = null,
        string? requesterName = null,
        Guid? customerId = null,
        Guid? productId = null,
        Guid? workPackageId = null,
        Guid? initialOwnerPersonId = null,
        Guid? assignedByPersonId = null,
        Guid? requestId = null,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new RecordRequestCommand(
            title,
            description,
            type,
            priority,
            requesterPersonId,
            requesterContactId,
            requesterName,
            customerId,
            productId,
            workPackageId,
            initialOwnerPersonId,
            assignedByPersonId,
            requestId), cancellationToken);
    }

    public Task<RequestDto> AssignRequestOwnerAsync(
        Guid requestId,
        Guid newOwnerPersonId,
        Guid assignedByPersonId,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new AssignRequestOwnerCommand(
            requestId,
            newOwnerPersonId,
            assignedByPersonId,
            note), cancellationToken);
    }

    public Task<RequestDto> EvaluateRequestAsync(
        Guid requestId,
        Guid evaluatedByPersonId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new EvaluateRequestCommand(
            requestId,
            evaluatedByPersonId,
            notes), cancellationToken);
    }

    public Task<RequestDto> AcceptRequestResponsibilityAsync(
        Guid requestId,
        Guid acceptedByPersonId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new AcceptRequestResponsibilityCommand(
            requestId,
            acceptedByPersonId,
            notes), cancellationToken);
    }

    public Task<RequestDto> RejectRequestAsync(
        Guid requestId,
        Guid rejectedByPersonId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new RejectRequestCommand(
            requestId,
            rejectedByPersonId,
            reason), cancellationToken);
    }

    public Task<RequestDto> StartRequestProgressAsync(
        Guid requestId,
        Guid actorPersonId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new StartRequestProgressCommand(
            requestId,
            actorPersonId,
            notes), cancellationToken);
    }

    public Task<RequestDto> EscalateRequestAsync(
        Guid requestId,
        Guid escalatedByPersonId,
        string reason,
        string? requiredAssistance = null,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new EscalateRequestCommand(
            requestId,
            escalatedByPersonId,
            reason,
            requiredAssistance), cancellationToken);
    }

    public Task<RequestDto> RequestManagementDecisionAsync(
        Guid requestId,
        Guid requestedByPersonId,
        string question,
        string? options = null,
        string? impact = null,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new RequestManagementDecisionCommand(
            requestId,
            requestedByPersonId,
            question,
            options,
            impact), cancellationToken);
    }

    public Task<RequestDto> ReviewRequestCompletionAsync(
        Guid requestId,
        Guid reviewerPersonId,
        bool acceptResolution,
        string summaryOrFeedback,
        string? outcome = null,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new ReviewRequestCompletionCommand(
            requestId,
            reviewerPersonId,
            acceptResolution,
            summaryOrFeedback,
            outcome), cancellationToken);
    }

    public Task<RequestDto> ReassignRequestOwnershipAsync(
        Guid requestId,
        Guid newOwnerPersonId,
        Guid reassignedByPersonId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new ReassignRequestOwnershipCommand(
            requestId,
            newOwnerPersonId,
            reassignedByPersonId,
            reason), cancellationToken);
    }

    public Task<RequestDto> ResolveEscalationAsync(
        Guid requestId,
        Guid actorPersonId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new ResolveEscalationCommand(
            requestId,
            actorPersonId,
            notes), cancellationToken);
    }
}
