namespace ICS.Modules.Identity.Domain;

/// <summary>
/// Authoritative status values for a UserAccount per Architecture §14.
/// </summary>
public static class UserAccountStatus
{
    /// <summary>
    /// Account is active and permitted to authenticate.
    /// </summary>
    public const string Active = "ACTIVE";

    /// <summary>
    /// Account is locked due to excessive failed login attempts or administrative action.
    /// </summary>
    public const string Locked = "LOCKED";

    /// <summary>
    /// Account is suspended administratively.
    /// </summary>
    public const string Suspended = "SUSPENDED";

    /// <summary>
    /// Returns true if the status represents an active user account.
    /// </summary>
    public static bool IsActive(string? status) =>
        string.Equals(status, Active, StringComparison.OrdinalIgnoreCase);
}
