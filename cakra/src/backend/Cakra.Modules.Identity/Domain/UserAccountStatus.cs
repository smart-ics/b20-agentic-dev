namespace Cakra.Modules.Identity.Domain;

/// <summary>
/// Authoritative status values for <see cref="UserAccount"/> per Architecture §14.
/// </summary>
public static class UserAccountStatus
{
    /// <summary>Account is active and permitted to authenticate.</summary>
    public const string Active = "ACTIVE";

    /// <summary>Account is locked due to repeated failed login attempts.</summary>
    public const string Locked = "LOCKED";

    /// <summary>Account is suspended administratively.</summary>
    public const string Suspended = "SUSPENDED";

    /// <summary>Account registration is pending administrator approval (CR-009).</summary>
    public const string Pending = "Pending";
}
