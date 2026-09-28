using Cakra.Core;

namespace Cakra.Modules.Identity.Domain;

/// <summary>
/// Domain entity representing a user credential and identity record (Architecture §14).
/// Possesses a 1-to-1 association to an authoritative Organization <c>Person</c>.
/// </summary>
public class UserAccount : EntityBase
{
    /// <summary>
    /// Unique identifier of the user account. Synonymous with <see cref="EntityBase.Id"/>.
    /// </summary>
    public Guid UserId
    {
        get => Id;
        set => Id = value;
    }

    /// <summary>
    /// Authoritative organization person identifier referenced by this account (1-to-1).
    /// </summary>
    public Guid PersonId { get; set; }

    /// <summary>
    /// Unique login username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Unique contact and login email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Cryptographic password hash (PBKDF2/HMAC-SHA512 or Argon2id per Architecture §19.5).
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Account status (<see cref="UserAccountStatus.Active"/>, <see cref="UserAccountStatus.Locked"/>,
    /// or <see cref="UserAccountStatus.Suspended"/>).
    /// </summary>
    public string Status { get; set; } = UserAccountStatus.Active;

    /// <summary>
    /// Running counter of consecutive failed login attempts.
    /// </summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>
    /// UTC timestamp of the most recent successful login, or <c>null</c> if never logged in.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Indicates whether the account is currently active.
    /// </summary>
    public bool IsActive => string.Equals(Status, UserAccountStatus.Active, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Records a successful authentication, resetting failed attempts and updating timestamps.
    /// </summary>
    public void RecordLoginSuccess(DateTime timestampUtc)
    {
        FailedLoginAttempts = 0;
        LastLoginAt = timestampUtc;
        UpdatedAt = timestampUtc;
    }

    /// <summary>
    /// Records an unsuccessful authentication attempt, incrementing failed attempts and locking
    /// the account if the threshold is reached.
    /// </summary>
    public void RecordLoginFailure(int maxAttemptsBeforeLock = 5, DateTime? timestampUtc = null)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxAttemptsBeforeLock)
        {
            Status = UserAccountStatus.Locked;
        }
        UpdatedAt = timestampUtc;
    }

    /// <summary>
    /// Unlocks the account and resets failed attempts.
    /// </summary>
    public void Unlock(DateTime? timestampUtc = null)
    {
        Status = UserAccountStatus.Active;
        FailedLoginAttempts = 0;
        UpdatedAt = timestampUtc;
    }
}
