namespace Cakra.Modules.Identity.Domain;

/// <summary>
/// Authenticated security context returned by session validation (Architecture §14, §18).
/// Exposes <see cref="UserId"/> and <see cref="PersonId"/> for ambient request context population.
/// </summary>
public sealed record SecurityContext
{
    /// <summary>
    /// Indicates whether the validated session is valid (active, not revoked, and not expired).
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Identifier of the authenticated user.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Authoritative organization person identifier linked to the user account.
    /// </summary>
    public Guid PersonId { get; init; }

    /// <summary>
    /// Explanation of why the session is invalid, or <c>null</c> if valid.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Creates a valid security context with the authenticated identifiers.
    /// </summary>
    public static SecurityContext Valid(Guid userId, Guid personId) =>
        new()
        {
            IsValid = true,
            UserId = userId,
            PersonId = personId
        };

    /// <summary>
    /// Creates an invalid security context with the specified reason.
    /// </summary>
    public static SecurityContext Invalid(string reason = "Invalid, expired, or revoked session.") =>
        new()
        {
            IsValid = false,
            Reason = reason
        };
}
