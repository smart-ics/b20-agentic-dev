namespace ICS.Modules.Organization.Application.DTOs;

/// <summary>
/// Authoritative DTO representing a Person in the Organization module.
/// </summary>
public sealed record PersonDto(
    Guid PersonId,
    string Name,
    string Email,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Authoritative DTO representing a Team in the Organization module.
/// </summary>
public sealed record TeamDto(
    Guid TeamId,
    string Name,
    string? Description,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Authoritative DTO representing an organizational Role.
/// </summary>
public sealed record RoleDto(
    Guid RoleId,
    string Name,
    string? Description,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Authoritative DTO representing an area of Responsibility.
/// </summary>
public sealed record ResponsibilityDto(
    Guid ResponsibilityId,
    string Name,
    string? Description,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Authoritative DTO representing a member of a team roster.
/// </summary>
public sealed record TeamMemberDto(
    Guid MembershipId,
    Guid PersonId,
    string PersonName,
    string PersonEmail,
    Guid TeamId,
    DateTime JoinedAt,
    DateTime? LeftAt,
    bool IsActive);

/// <summary>
/// DTO representing an active or historical role assignment.
/// </summary>
public sealed record RoleAssignmentDto(
    Guid AssignmentId,
    Guid PersonId,
    Guid RoleId,
    string RoleName,
    DateTime AssignedAt,
    DateTime? RevokedAt,
    bool IsActive);

/// <summary>
/// DTO representing an active or historical responsibility assignment.
/// </summary>
public sealed record ResponsibilityAssignmentDto(
    Guid AssignmentId,
    Guid PersonId,
    Guid ResponsibilityId,
    string ResponsibilityName,
    DateTime AssignedAt,
    DateTime? RevokedAt,
    bool IsActive);
