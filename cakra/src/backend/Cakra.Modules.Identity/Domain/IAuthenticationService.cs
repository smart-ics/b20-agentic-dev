namespace Cakra.Modules.Identity.Domain;

/// <summary>
/// Core application service contract for Identity and Access Management (Architecture §14, §19.5).
/// Coordinates authentication, password verification, session issuance, and session validation.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates user credentials, verifies active status, creates an active session, and returns the session token.
    /// </summary>
    /// <param name="usernameOrEmail">User's username or email address.</param>
    /// <param name="password">Plain-text password to verify.</param>
    /// <param name="clientInfo">Optional client IP and user-agent metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="LoginResult"/> indicating success or failure details.</returns>
    Task<LoginResult> LoginAsync(string usernameOrEmail, string password, ClientInfo? clientInfo = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the specified session token, invalidating subsequent requests.
    /// </summary>
    /// <param name="sessionToken">The session token to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the session was found and revoked; otherwise <c>false</c>.</returns>
    Task<bool> LogoutAsync(string sessionToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the session token, verifying expiry and revocation status, and returns the authenticated security context.
    /// </summary>
    /// <param name="sessionToken">The session token to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="SecurityContext"/> containing authenticated <c>UserId</c> and <c>PersonId</c> if valid.</returns>
    Task<SecurityContext> ValidateSessionAsync(string sessionToken, CancellationToken cancellationToken = default);

    /// <summary>Synchronous convenience overload for <see cref="LoginAsync"/>.</summary>
    LoginResult Login(string usernameOrEmail, string password, ClientInfo? clientInfo = null);

    /// <summary>Synchronous convenience overload for <see cref="LogoutAsync"/>.</summary>
    bool Logout(string sessionToken);

    /// <summary>Synchronous convenience overload for <see cref="ValidateSessionAsync"/>.</summary>
    SecurityContext ValidateSession(string sessionToken);

    /// <summary>
    /// Generates a cryptographic password hash for the specified user and plain-text password using the configured hasher.
    /// </summary>
    string HashPassword(UserAccount user, string password);
}
