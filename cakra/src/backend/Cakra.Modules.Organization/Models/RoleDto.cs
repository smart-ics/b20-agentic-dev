namespace Cakra.Modules.Organization.Models;

/// <summary>
/// Read-only DTO representing an organizational role (Architecture CR-027 §4 TD-004, §5, §6).
/// </summary>
/// <param name="Id">The unique identifier of the role.</param>
/// <param name="Name">The role name (e.g. Administrator, Management, Operational User, Programmer, Implementator).</param>
/// <param name="Description">The optional description of the role.</param>
public sealed record RoleDto(Guid Id, string Name, string? Description);
