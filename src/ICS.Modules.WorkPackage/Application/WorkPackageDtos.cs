namespace ICS.Modules.WorkPackage.Application;

/// <summary>
/// DTO carrying filter criteria for Work Package grid queries.
/// Architecture §11, §19.2.
/// </summary>
public sealed record WorkPackageGridFilterDto(
    string? SearchTerm = null,
    Guid? OwnerPersonId = null,
    Guid? CustomerId = null,
    Guid? ProductId = null,
    string? Statuses = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Skip = 0,
    int Take = 50);

/// <summary>
/// DTO representing a Work Package as returned by query services and command handlers.
/// Denormalized reference names are resolved from the owning modules' query services.
/// Architecture §11, §19.2.
/// </summary>
public sealed record WorkPackageDto(
    Guid Id,
    string Name,
    string Objective,
    string Status,
    bool IsDraft,
    bool IsActive,
    bool IsClosed,
    Guid OwnerPersonId,
    string? OwnerName,
    Guid? CustomerId,
    string? CustomerName,
    Guid? ProductId,
    string? ProductName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ClosedAt,
    string? CloseReason,
    int ActiveRequestCount,
    IReadOnlyList<WorkPackageRequestMembershipDto> Requests);

/// <summary>
/// DTO representing a single Work Package request membership row.
/// Architecture §11, §19.2.
/// </summary>
public sealed record WorkPackageRequestMembershipDto(
    Guid MembershipId,
    Guid WorkPackageId,
    Guid RequestId,
    DateTime AddedAt,
    DateTime? RemovedAt,
    bool IsActive);

/// <summary>
/// DTO representing the scope of a Work Package: the package plus its active request memberships.
/// Architecture §11, §19.2.
/// </summary>
public sealed record WorkPackageScopeDto(
    Guid Id,
    string Name,
    string Objective,
    string Status,
    Guid OwnerPersonId,
    string? OwnerName,
    Guid? CustomerId,
    string? CustomerName,
    Guid? ProductId,
    string? ProductName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ClosedAt,
    string? CloseReason,
    IReadOnlyList<WorkPackageRequestMembershipDto> ActiveRequests,
    IReadOnlyList<WorkPackageRequestMembershipDto> AllRequests);

/// <summary>
/// DTO representing a paged Work Package grid result.
/// Architecture §11, §19.2.
/// </summary>
public sealed record WorkPackageGridResultDto(
    int TotalCount,
    IReadOnlyList<WorkPackageDto> Items);

/// <summary>
/// DTO representing a single Work Package state transition audit record.
/// Architecture §18, §19.2.
/// </summary>
public sealed record WorkPackageStateHistoryDto(
    Guid StateHistoryId,
    Guid WorkPackageId,
    string? FromStatus,
    string ToStatus,
    Guid ActorPersonId,
    string? ActorName,
    string? Reason,
    DateTime ChangedAt);
