namespace ICS.Modules.Request.Application.DTOs;

/// <summary>
/// Data transfer object representing a Request aggregate with all associated details.
/// Architecture §7, §8, §16, §17.
/// </summary>
public record RequestDto(
    Guid RequestId,
    string Title,
    string Description,
    string Type,
    string Priority,
    string Status,
    bool IsTerminal,
    bool IsActive,
    Guid? RequesterPersonId,
    Guid? RequesterContactId,
    string? RequesterName,
    Guid? OwnerPersonId,
    string? OwnerName,
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerCode,
    Guid? ProductId,
    string? ProductName,
    string? ProductCode,
    Guid? WorkPackageId,
    bool IsAwaitingManagementDecision,
    string? ManagementDecisionQuestion,
    string? ManagementDecisionOptions,
    string? ManagementDecisionImpact,
    DateTime? ManagementDecisionRequestedAt,
    string? EscalationReason,
    Guid? EscalatedByPersonId,
    string? EscalatedByName,
    DateTime? EscalatedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ClosedAt,
    RequestResolutionDto? Resolution = null,
    IReadOnlyList<RequestAssignmentDto>? Assignments = null,
    IReadOnlyList<RequestStateHistoryDto>? StateHistories = null);

/// <summary>
/// Data transfer object representing a recorded resolution outcome for a Request.
/// </summary>
public record RequestResolutionDto(
    Guid ResolutionId,
    Guid RequestId,
    string Outcome,
    string Summary,
    Guid ResolvedByPersonId,
    string? ResolvedByName,
    DateTime ResolvedAt);

/// <summary>
/// Data transfer object representing an ownership assignment history record.
/// </summary>
public record RequestAssignmentDto(
    Guid AssignmentId,
    Guid RequestId,
    Guid OwnerPersonId,
    string? OwnerName,
    Guid AssignedByPersonId,
    string? AssignedByName,
    DateTime AssignedAt,
    string? Note,
    bool IsActive);

/// <summary>
/// Data transfer object representing an audit log entry of a Request state transition.
/// </summary>
public record RequestStateHistoryDto(
    Guid StateHistoryId,
    Guid RequestId,
    string? FromStatus,
    string ToStatus,
    Guid ActorPersonId,
    string? ActorName,
    string? Reason,
    DateTime ChangedAt);

/// <summary>
/// Filter parameters for searching and paginating historical Request records (FEAT-COL-002).
/// </summary>
public record RequestGridFilterDto(
    string? SearchTerm = null,
    Guid? CustomerId = null,
    Guid? ProductId = null,
    Guid? OwnerPersonId = null,
    string? Statuses = null,
    string? Priority = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Skip = 0,
    int Take = 50);

/// <summary>
/// Lightweight projection record of a Request used in grids and summary lists.
/// </summary>
public record RequestSummaryDto(
    Guid RequestId,
    string Title,
    string Description,
    string Type,
    string Priority,
    string Status,
    Guid? RequesterPersonId,
    string? RequesterName,
    Guid? OwnerPersonId,
    string? OwnerName,
    Guid? CustomerId,
    string? CustomerName,
    Guid? ProductId,
    string? ProductName,
    Guid? WorkPackageId,
    bool IsAwaitingManagementDecision,
    DateTime? EscalatedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ClosedAt,
    string? ResolutionOutcome);

/// <summary>
/// Paginated result container for filtered request search queries.
/// </summary>
public record RequestGridResultDto(
    int TotalCount,
    IReadOnlyList<RequestSummaryDto> Items);

/// <summary>
/// Personalized response container for My Assigned Requests screen (SCR-REQ-005, FEAT-COL-004).
/// </summary>
public record MyAssignedRequestsResponseDto(
    int ActiveCount,
    int AwaitingDecisionCount,
    int EscalatedCount,
    IReadOnlyList<RequestSummaryDto> Items);
