namespace ICS.Modules.Identity.Application;

/// <summary>
/// Security context model returned upon validating an active session token per Architecture §14.
/// Contains verified identity identifiers and validity state.
/// </summary>
public record SecurityContext
{
    /// <summary>
    /// Indicates whether the session token is valid, active, and unexpired.
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// The authenticated UserAccount identifier.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// The linked Organization Person identifier.
    /// </summary>
    public Guid? PersonId { get; init; }

    /// <summary>
    /// The active session token validated.
    /// </summary>
    public string? SessionToken { get; init; }

    /// <summary>
    /// Timestamp when this validated session expires (UTC).
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Failure reason if session validation failed.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Factory method for a valid authenticated security context.
    /// </summary>
    public static SecurityContext Valid(Guid userId, Guid personId, string sessionToken, DateTime expiresAt) =>
        new()
        {
            IsAuthenticated = true,
            UserId = userId,
            PersonId = personId,
            SessionToken = sessionToken,
            ExpiresAt = expiresAt
        };

    /// <summary>
    /// Factory method for an invalid/unauthenticated session context.
    /// </summary>
    public static SecurityContext Invalid(string reason) =>
        new()
        {
            IsAuthenticated = false,
            FailureReason = reason
        };
}
