namespace ICS.Modules.Identity.Domain;

using ICS.Core.Domain;

/// <summary>
/// UserAccount domain aggregate representing an authenticated user identity and credential record.
/// Architecture §14 (IAM Boundary and IAM Persistence Tables).
/// Has a 1-to-1 association with an Organization Person.
/// </summary>
public class UserAccount : Entity<Guid>
{
    /// <summary>
    /// Gets the unique user identifier (equivalent to <see cref="Entity{TId}.Id"/>).
    /// </summary>
    public Guid UserId => Id;

    /// <summary>
    /// Foreign key identifier referencing an authoritative Person in the Organization bounded context.
    /// </summary>
    public Guid PersonId { get; private set; }

    /// <summary>
    /// Unique login username.
    /// </summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>
    /// Unique user email address.
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// Cryptographic password hash (PBKDF2/HMAC-SHA512 or Argon2id per Architecture §19.5).
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>
    /// Account status: 'ACTIVE', 'LOCKED', or 'SUSPENDED'.
    /// </summary>
    public string Status { get; private set; } = UserAccountStatus.Active;

    /// <summary>
    /// Current count of consecutive failed login attempts.
    /// </summary>
    public int FailedLoginAttempts { get; private set; }

    /// <summary>
    /// Timestamp of the last successful login (UTC), or null if the user has never logged in.
    /// </summary>
    public DateTime? LastLoginAt { get; private set; }

    /// <summary>
    /// Parameterless constructor required for serialization and ORM/Dapper mapping.
    /// </summary>
    protected UserAccount()
    {
    }

    /// <summary>
    /// Constructor matching database schema columns for Dapper instantiation.
    /// </summary>
    public UserAccount(
        Guid userId,
        Guid personId,
        string username,
        string email,
        string passwordHash,
        string status,
        int failedLoginAttempts,
        DateTime? lastLoginAt,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        Id = userId;
        PersonId = personId;
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        Status = status;
        FailedLoginAttempts = failedLoginAttempts;
        LastLoginAt = lastLoginAt;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Factory method for creating a new active UserAccount.
    /// </summary>
    public static UserAccount Create(
        Guid userId,
        Guid personId,
        string username,
        string email,
        string passwordHash,
        DateTime createdAt)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        if (personId == Guid.Empty)
            throw new ArgumentException("PersonId cannot be empty.", nameof(personId));
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty.", nameof(username));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("PasswordHash cannot be empty.", nameof(passwordHash));

        return new UserAccount(
            userId: userId,
            personId: personId,
            username: username.Trim(),
            email: email.Trim().ToLowerInvariant(),
            passwordHash: passwordHash,
            status: UserAccountStatus.Active,
            failedLoginAttempts: 0,
            lastLoginAt: null,
            createdAt: createdAt,
            updatedAt: null);
    }

    /// <summary>
    /// Indicates whether the account is currently active.
    /// </summary>
    public bool IsActive => UserAccountStatus.IsActive(Status);

    /// <summary>
    /// Indicates whether the account is currently locked.
    /// </summary>
    public bool IsLocked => string.Equals(Status, UserAccountStatus.Locked, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Indicates whether the account is currently suspended.
    /// </summary>
    public bool IsSuspended => string.Equals(Status, UserAccountStatus.Suspended, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Records a successful authentication, resetting failed attempts and updating login timestamp.
    /// </summary>
    /// <param name="loginTime">Timestamp of successful authentication (UTC).</param>
    public void RecordLoginSuccess(DateTime loginTime)
    {
        FailedLoginAttempts = 0;
        LastLoginAt = loginTime;
        UpdatedAt = loginTime;
    }

    /// <summary>
    /// Records a failed authentication attempt, incrementing the failed counter and locking if limit reached.
    /// </summary>
    /// <param name="attemptTime">Timestamp of failed authentication (UTC).</param>
    /// <param name="maxFailedAttempts">Maximum attempts before locking account (default: 5).</param>
    public void RecordLoginFailure(DateTime attemptTime, int maxFailedAttempts = 5)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxFailedAttempts)
        {
            Status = UserAccountStatus.Locked;
        }

        UpdatedAt = attemptTime;
    }

    /// <summary>
    /// Locks the account administratively.
    /// </summary>
    public void Lock(DateTime now)
    {
        Status = UserAccountStatus.Locked;
        UpdatedAt = now;
    }

    /// <summary>
    /// Unlocks the account and resets failed login attempts.
    /// </summary>
    public void Unlock(DateTime now)
    {
        Status = UserAccountStatus.Active;
        FailedLoginAttempts = 0;
        UpdatedAt = now;
    }

    /// <summary>
    /// Updates the cryptographic password hash.
    /// </summary>
    public void ChangePassword(string newPasswordHash, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("New password hash cannot be empty.", nameof(newPasswordHash));

        PasswordHash = newPasswordHash;
        UpdatedAt = now;
    }
}
