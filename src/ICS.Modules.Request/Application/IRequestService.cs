namespace ICS.Modules.Request.Application;

using ICS.Modules.Request.Application.DTOs;

/// <summary>
/// Application service contract for executing Request lifecycle commands.
/// Architecture §7, §8, §16, §19.2.
/// </summary>
public interface IRequestService
{
    Task<RequestDto> RecordRequestAsync(
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
        CancellationToken cancellationToken = default);

    Task<RequestDto> AssignRequestOwnerAsync(
        Guid requestId,
        Guid newOwnerPersonId,
        Guid assignedByPersonId,
        string? note = null,
        CancellationToken cancellationToken = default);

    Task<RequestDto> EvaluateRequestAsync(
        Guid requestId,
        Guid evaluatedByPersonId,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task<RequestDto> AcceptRequestResponsibilityAsync(
        Guid requestId,
        Guid acceptedByPersonId,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task<RequestDto> RejectRequestAsync(
        Guid requestId,
        Guid rejectedByPersonId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<RequestDto> StartRequestProgressAsync(
        Guid requestId,
        Guid actorPersonId,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task<RequestDto> EscalateRequestAsync(
        Guid requestId,
        Guid escalatedByPersonId,
        string reason,
        string? requiredAssistance = null,
        CancellationToken cancellationToken = default);

    Task<RequestDto> RequestManagementDecisionAsync(
        Guid requestId,
        Guid requestedByPersonId,
        string question,
        string? options = null,
        string? impact = null,
        CancellationToken cancellationToken = default);

    Task<RequestDto> ReviewRequestCompletionAsync(
        Guid requestId,
        Guid reviewerPersonId,
        bool acceptResolution,
        string summaryOrFeedback,
        string? outcome = null,
        CancellationToken cancellationToken = default);

    Task<RequestDto> ReassignRequestOwnershipAsync(
        Guid requestId,
        Guid newOwnerPersonId,
        Guid reassignedByPersonId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<RequestDto> ResolveEscalationAsync(
        Guid requestId,
        Guid actorPersonId,
        string? notes = null,
        CancellationToken cancellationToken = default);
}
