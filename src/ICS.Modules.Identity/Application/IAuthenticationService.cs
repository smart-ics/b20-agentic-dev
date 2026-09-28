namespace ICS.Modules.Identity.Application;

/// <summary>
/// Authoritative application service contract for Identity & Access authentication operations.
/// Architecture §14 (IAM Components) and §19.5.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates user credentials, verifies active status of both UserAccount and linked Person,
    /// creates a server-side session, and returns a session token.
    /// </summary>
    Task<LoginResult> LoginAsync(string usernameOrEmail, string password, ClientInfo? clientInfo = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates an active session in identity.UserSessions.
    /// </summary>
    Task<bool> LogoutAsync(string sessionToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an active session token, checks revocation and expiration, and returns authenticated identity context.
    /// </summary>
    Task<SecurityContext> ValidateSessionAsync(string sessionToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous convenience overload for LoginAsync.
    /// </summary>
    LoginResult Login(string usernameOrEmail, string password, ClientInfo? clientInfo = null);

    /// <summary>
    /// Synchronous convenience overload for LogoutAsync.
    /// </summary>
    bool Logout(string sessionToken);

    /// <summary>
    /// Synchronous convenience overload for ValidateSessionAsync.
    /// </summary>
    SecurityContext ValidateSession(string sessionToken);
}
