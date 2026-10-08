using Cakra.Modules.Organization.Domain;

namespace Cakra.Modules.Organization.Models;

/// <summary>
/// Internal projection row for Dapper SQL queries joining Person, RoleAssignments, and Roles
/// using SQL Server STRING_AGG (Architecture §4 TD-003).
/// </summary>
internal sealed class PersonDtoRow
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Status { get; init; } = Person.StatusActive;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? RolesRaw { get; init; }

    public PersonDto ToDto() => new()
    {
        Id = Id,
        FirstName = FirstName,
        LastName = LastName,
        Email = Email,
        Status = Status,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        Roles = string.IsNullOrWhiteSpace(RolesRaw)
            ? Array.Empty<string>()
            : RolesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                .ToArray()
    };
}
