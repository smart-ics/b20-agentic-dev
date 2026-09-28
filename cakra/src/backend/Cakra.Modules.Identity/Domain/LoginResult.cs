namespace Cakra.Modules.Identity.Domain;

/// <summary>
/// Outcome of an authentication attempt (Architecture §14).
/// </summary>
public sealed record LoginResult
{
    /// <summary>
    /// Indicates whether authentication succeeded.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Cryptographic session token issued upon successful authentication.
    /// </summary>
    public string? SessionToken { get; init; }

    /// <summary>
    /// UTC expiration timestamp of the created session.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Identifier of the authenticated user.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Identifier of the linked organization person.
    /// </summary>
    public Guid? PersonId { get; init; }

    /// <summary>
    /// Error code if authentication failed (e.g. <c>INVALID_CREDENTIALS</c>, <c>ACCOUNT_LOCKED</c>, <c>PERSON_INACTIVE</c>).
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Human-readable explanation of why authentication failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful login result.
    /// </summary>
    public static LoginResult Success(string sessionToken, DateTime expiresAt, Guid userId, Guid personId) =>
        new()
        {
            Succeeded = true,
            SessionToken = sessionToken,
            ExpiresAt = expiresAt,
            UserId = userId,
            PersonId = personId
        };

    /// <summary>
    /// Creates a failed login result with the specified error code and message.
    /// </summary>
    public static LoginResult Failed(string errorCode, string errorMessage) =>
        new()
        {
            Succeeded = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
}
