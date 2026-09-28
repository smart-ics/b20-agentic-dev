using Cakra.Modules.Request.Domain;

namespace Cakra.Modules.Request;

/// <summary>
/// Read-only data transfer object representing a <see cref="Domain.Request"/> aggregate root
/// (Architecture §7, §8, §15).
/// </summary>
public record RequestDto
{
    /// <summary>Unique identifier of the request.</summary>
    public Guid Id { get; init; }

    /// <summary>Unique identifier of the request (synonym for <see cref="Id"/>).</summary>
    public Guid RequestId
    {
        get => Id;
        init => Id = value;
    }

    /// <summary>Brief summary or subject of the operational demand.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Detailed description and operational context of the demand.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Classification of the demand (e.g. GENERAL, Bug, Feature, Support).</summary>
    public string RequestType { get; init; } = "GENERAL";

    /// <summary>Current authoritative lifecycle status string (CAPTURED, EVALUATING, ACCEPTED, REJECTED, IN_PROGRESS, ESCALATED, COMPLETED).</summary>
    public string Status { get; init; } = RequestStatusNames.Captured;

    /// <summary>Priority level (LOW, NORMAL, HIGH, URGENT).</summary>
    public string Priority { get; init; } = "NORMAL";

    /// <summary>PersonId of the assigned Request Owner in Organization domain, or <c>null</c> if unassigned.</summary>
    public Guid? OwnerPersonId { get; init; }

    /// <summary>Convenience alias for <see cref="OwnerPersonId"/>.</summary>
    public Guid? AssigneePersonId
    {
        get => OwnerPersonId;
        init => OwnerPersonId = value;
    }

    /// <summary>Resolved display name of the assigned Request Owner, when enriched via OrganizationQueryService.</summary>
    public string? OwnerName { get; init; }

    /// <summary>Convenience alias for <see cref="OwnerName"/>.</summary>
    public string? AssigneeName
    {
        get => OwnerName;
        init => OwnerName = value;
    }

    /// <summary>Optional CustomerId associated with this request.</summary>
    public Guid? CustomerId { get; init; }

    /// <summary>Resolved customer name, when enriched via CustomerQueryService.</summary>
    public string? CustomerName { get; init; }

    /// <summary>Resolved customer code, when enriched via CustomerQueryService.</summary>
    public string? CustomerCode { get; init; }

    /// <summary>Optional ProductId associated with this request.</summary>
    public Guid? ProductId { get; init; }

    /// <summary>Resolved product name, when enriched via ProductQueryService.</summary>
    public string? ProductName { get; init; }

    /// <summary>Resolved product code, when enriched via ProductQueryService.</summary>
    public string? ProductCode { get; init; }

    /// <summary>Optional WorkPackageId grouping this request.</summary>
    public Guid? WorkPackageId { get; init; }

    /// <summary>Evaluation notes recorded during triage assessment.</summary>
    public string? EvaluationNotes { get; init; }

    /// <summary>Escalation justification recorded when escalated.</summary>
    public string? EscalationReason { get; init; }

    /// <summary>Management decision notes recorded during management elevation.</summary>
    public string? ManagementDecisionNotes { get; init; }

    /// <summary>Recorded resolution outcome when the request is closed.</summary>
    public RequestResolutionDto? Resolution { get; init; }

    /// <summary>Audit trail of ownership and lifecycle state transitions.</summary>
    public IReadOnlyList<RequestAssignmentDto> Assignments { get; init; } = Array.Empty<RequestAssignmentDto>();

    /// <summary>UTC timestamp when the request was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>UTC timestamp when the request was last updated.</summary>
    public DateTime? UpdatedAt { get; init; }

    internal static RequestDto FromDomain(Domain.Request request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new RequestDto
        {
            Id = request.Id,
            Title = request.Title,
            Description = request.Description,
            RequestType = request.RequestType,
            Status = request.Status.ToName(),
            Priority = request.Priority,
            OwnerPersonId = request.OwnerPersonId,
            CustomerId = request.CustomerId,
            ProductId = request.ProductId,
            WorkPackageId = request.WorkPackageId,
            EvaluationNotes = request.EvaluationNotes,
            EscalationReason = request.EscalationReason,
            ManagementDecisionNotes = request.ManagementDecisionNotes,
            Resolution = request.Resolution is null ? null : RequestResolutionDto.FromDomain(request.Resolution),
            Assignments = request.Assignments.Select(RequestAssignmentDto.FromDomain).ToList(),
            CreatedAt = request.CreatedAt,
            UpdatedAt = request.UpdatedAt
        };
    }
}

/// <summary>
/// Detailed read model alias for <see cref="RequestDto"/> (Architecture §7, §8).
/// </summary>
public sealed record RequestDetailDto : RequestDto;

/// <summary>
/// Read-only data transfer object representing a <see cref="RequestResolution"/> record (Architecture §17).
/// </summary>
public record RequestResolutionDto
{
    public Guid Id { get; init; }
    public Guid RequestId { get; init; }
    public string Outcome { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Guid ResolvedBy { get; init; }
    public string? ResolvedByName { get; init; }
    public DateTime ResolvedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    internal static RequestResolutionDto FromDomain(RequestResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        return new RequestResolutionDto
        {
            Id = resolution.Id,
            RequestId = resolution.RequestId,
            Outcome = resolution.Outcome,
            Description = resolution.Description,
            ResolvedBy = resolution.ResolvedBy,
            ResolvedAt = resolution.ResolvedAt,
            CreatedAt = resolution.CreatedAt,
            UpdatedAt = resolution.UpdatedAt
        };
    }
}

/// <summary>
/// Read-only data transfer object representing a <see cref="RequestAssignment"/> audit record (Architecture §17, §18).
/// </summary>
public record RequestAssignmentDto
{
    public Guid Id { get; init; }
    public Guid RequestId { get; init; }
    public Guid? PreviousOwnerPersonId { get; init; }
    public string? PreviousOwnerName { get; init; }
    public Guid? AssignedOwnerPersonId { get; init; }
    public string? AssignedOwnerName { get; init; }
    public Guid ActorPersonId { get; init; }
    public string? ActorName { get; init; }
    public string? PreviousStatus { get; init; }
    public string NewStatus { get; init; } = RequestStatusNames.Captured;
    public DateTime AssignedAtUtc { get; init; }

    /// <summary>Convenience alias for <see cref="AssignedAtUtc"/>.</summary>
    public DateTime Timestamp => AssignedAtUtc;

    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    internal static RequestAssignmentDto FromDomain(RequestAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        return new RequestAssignmentDto
        {
            Id = assignment.Id,
            RequestId = assignment.RequestId,
            PreviousOwnerPersonId = assignment.PreviousOwnerPersonId,
            AssignedOwnerPersonId = assignment.AssignedOwnerPersonId,
            ActorPersonId = assignment.ActorPersonId,
            PreviousStatus = assignment.PreviousStatus?.ToName(),
            NewStatus = assignment.NewStatus.ToName(),
            AssignedAtUtc = assignment.AssignedAtUtc,
            Notes = assignment.Notes,
            CreatedAt = assignment.CreatedAt,
            UpdatedAt = assignment.UpdatedAt
        };
    }
}

/// <summary>
/// Read-only data transfer object alias for a state history timeline entry (Architecture §7, §8).
/// </summary>
public sealed record RequestStateHistoryItemDto : RequestAssignmentDto;
