namespace ICS.Modules.Identity.Application;

/// <summary>
/// Result of an authentication attempt per Architecture §14.
/// </summary>
public record LoginResult
{
    /// <summary>
    /// Indicates whether authentication succeeded.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Human-readable error message in case of failure.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Cryptographically secure session token issued upon successful authentication.
    /// </summary>
    public string? SessionToken { get; init; }

    /// <summary>
    /// Authenticated UserAccount identifier.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Linked Organization Person identifier.
    /// </summary>
    public Guid? PersonId { get; init; }

    /// <summary>
    /// Timestamp when the issued session expires (UTC).
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Machine-readable error code in case of failure (e.g., 'INVALID_CREDENTIALS', 'ACCOUNT_LOCKED').
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Factory method for successful authentication outcome.
    /// </summary>
    public static LoginResult Success(string sessionToken, Guid userId, Guid personId, DateTime expiresAt) =>
        new()
        {
            Succeeded = true,
            SessionToken = sessionToken,
            UserId = userId,
            PersonId = personId,
            ExpiresAt = expiresAt
        };

    /// <summary>
    /// Factory method for failed authentication outcome.
    /// </summary>
    public static LoginResult Failure(string errorMessage, string? errorCode = null) =>
        new()
        {
            Succeeded = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode
        };
}
