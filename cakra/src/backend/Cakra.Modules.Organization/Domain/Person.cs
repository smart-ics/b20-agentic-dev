using Cakra.Core;

namespace Cakra.Modules.Organization.Domain;

/// <summary>
/// Authoritative organizational individual participating in operations (Architecture §6, §16).
/// Owns personal identity, email, and lifecycle status.
/// </summary>
public sealed class Person : EntityBase
{
    public const string StatusActive = "ACTIVE";
    public const string StatusInactive = "INACTIVE";

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = StatusActive;

    public string FullName => string.IsNullOrWhiteSpace(LastName)
        ? FirstName
        : $"{FirstName} {LastName}".Trim();

    public bool IsActive => string.Equals(Status, StatusActive, StringComparison.OrdinalIgnoreCase);

    public void Deactivate(DateTime? updatedAt = null)
    {
        Status = StatusInactive;
        UpdatedAt = updatedAt ?? DateTime.UtcNow;
    }

    public void Activate(DateTime? updatedAt = null)
    {
        Status = StatusActive;
        UpdatedAt = updatedAt ?? DateTime.UtcNow;
    }
}
