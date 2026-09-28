using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Domain.Exceptions;
using MediatR;

namespace Cakra.Modules.Request.Services;

/// <summary>
/// Escalation, management decision, and ownership reassignment command handlers for
/// <see cref="RequestService"/> (Architecture §7, §8, §15, §18 — UC-REQ-006, UC-REQ-007, UC-MGT-001).
/// </summary>
public sealed partial class RequestService :
    IRequestHandler<EscalateRequestCommand, RequestDto>,
    IRequestHandler<RequestManagementDecisionCommand, RequestDto>,
    IRequestHandler<ReassignRequestOwnershipCommand, RequestDto>
{
    /// <inheritdoc />
    public async Task<RequestDto> EscalateRequestAsync(
        Guid requestId,
        string reason,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new RequestDomainValidationException("Escalation reason cannot be empty.", nameof(reason));
        }

        var request = await GetRequiredRequestAsync(requestId, cancellationToken);

        var resolvedActorId = ResolveActorPersonId(actorPersonId, fallbackPersonId: request.OwnerPersonId);
        var existingAssignmentIds = SnapshotAssignmentIds(request);
        var hadResolution = request.Resolution is not null;
        var now = GetNextMonotonicTimestamp(request);

        request.Escalate(reason, resolvedActorId, now);

        await PersistStateChangesAndDispatchAsync(
            request,
            existingAssignmentIds,
            hadResolution,
            cancellationToken);

        return RequestDto.FromDomain(request);
    }

    /// <inheritdoc />
    public Task<RequestDto> EscalateRequest(
        Guid requestId,
        string reason,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => EscalateRequestAsync(requestId, reason, actorPersonId, cancellationToken);

    /// <inheritdoc />
    public async Task<RequestDto> RequestManagementDecisionAsync(
        Guid requestId,
        string decisionDetails,
        Guid? actorPersonId = null,
        RequestStatus? targetStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(decisionDetails))
        {
            throw new RequestDomainValidationException("Decision details cannot be empty.", nameof(decisionDetails));
        }

        var request = await GetRequiredRequestAsync(requestId, cancellationToken);

        var resolvedActorId = ResolveActorPersonId(actorPersonId, fallbackPersonId: request.OwnerPersonId);
        var existingAssignmentIds = SnapshotAssignmentIds(request);
        var hadResolution = request.Resolution is not null;
        var previousStatus = request.Status;
        var now = GetNextMonotonicTimestamp(request);

        if (targetStatus.HasValue)
        {
            request.ApplyManagementDecision(targetStatus.Value, decisionDetails, resolvedActorId, now);
            request.RequestManagementDecision(decisionDetails, resolvedActorId, now);

            await PersistStateChangesAndDispatchAsync(
                request,
                existingAssignmentIds,
                hadResolution,
                cancellationToken);

            return RequestDto.FromDomain(request);
        }

        request.RequestManagementDecision(decisionDetails, resolvedActorId, now);

        var auditAssignment = RequestAssignment.Create(
            requestId: request.Id,
            previousOwnerPersonId: request.OwnerPersonId,
            assignedOwnerPersonId: request.OwnerPersonId,
            actorPersonId: resolvedActorId,
            previousStatus: previousStatus,
            newStatus: request.Status,
            assignedAtUtc: now,
            notes: $"Management decision requested: {decisionDetails.Trim()}");

        await PersistStateChangesAndDispatchAsync(
            request,
            existingAssignmentIds,
            hadResolution,
            cancellationToken);

        await RecordStateChangeAuditAsync(auditAssignment, cancellationToken);

        var dto = RequestDto.FromDomain(request);
        var updatedAssignments = dto.Assignments
            .Concat(new[] { RequestAssignmentDto.FromDomain(auditAssignment) })
            .ToList();

        return dto with { Assignments = updatedAssignments };
    }

    /// <inheritdoc />
    public Task<RequestDto> RequestManagementDecision(
        Guid requestId,
        string decisionDetails,
        Guid? actorPersonId = null,
        RequestStatus? targetStatus = null,
        CancellationToken cancellationToken = default)
        => RequestManagementDecisionAsync(requestId, decisionDetails, actorPersonId, targetStatus, cancellationToken);

    /// <inheritdoc />
    public async Task<RequestDto> ReassignRequestOwnershipAsync(
        Guid requestId,
        Guid newOwnerPersonId,
        string? notes = null,
        Guid? actorPersonId = null,
        RequestStatus? targetStatusForEscalated = null,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(requestId));
        }

        if (newOwnerPersonId == Guid.Empty)
        {
            throw new RequestDomainValidationException("NewOwnerPersonId cannot be empty.", nameof(newOwnerPersonId));
        }

        var request = await GetRequiredRequestAsync(requestId, cancellationToken);

        await ValidateAssigneeAsync(newOwnerPersonId, cancellationToken);

        var resolvedActorId = ResolveActorPersonId(actorPersonId, fallbackPersonId: newOwnerPersonId);
        var existingAssignmentIds = SnapshotAssignmentIds(request);
        var hadResolution = request.Resolution is not null;
        var now = GetNextMonotonicTimestamp(request);

        request.ReassignOwner(
            newOwnerPersonId: newOwnerPersonId,
            actorPersonId: resolvedActorId,
            targetStatusForEscalated: targetStatusForEscalated,
            notes: notes,
            utcNow: now);

        await PersistStateChangesAndDispatchAsync(
            request,
            existingAssignmentIds,
            hadResolution,
            cancellationToken);

        return RequestDto.FromDomain(request);
    }

    /// <inheritdoc />
    public Task<RequestDto> ReassignRequestOwnership(
        Guid requestId,
        Guid newOwnerPersonId,
        string? notes = null,
        Guid? actorPersonId = null,
        RequestStatus? targetStatusForEscalated = null,
        CancellationToken cancellationToken = default)
        => ReassignRequestOwnershipAsync(
            requestId,
            newOwnerPersonId,
            notes,
            actorPersonId,
            targetStatusForEscalated,
            cancellationToken);

    public Task<RequestDto> Handle(EscalateRequestCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return EscalateRequestAsync(
            request.RequestId,
            request.Reason,
            request.ActorPersonId,
            cancellationToken);
    }

    public Task<RequestDto> Handle(RequestManagementDecisionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RequestManagementDecisionAsync(
            request.RequestId,
            request.DecisionDetails,
            request.ActorPersonId,
            request.TargetStatus,
            cancellationToken);
    }

    public Task<RequestDto> Handle(ReassignRequestOwnershipCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ReassignRequestOwnershipAsync(
            request.RequestId,
            request.NewOwnerPersonId,
            request.Notes,
            request.ActorPersonId,
            request.TargetStatusForEscalated,
            cancellationToken);
    }
}
