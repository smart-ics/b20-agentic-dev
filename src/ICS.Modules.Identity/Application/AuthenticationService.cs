namespace ICS.Modules.Identity.Application;

using System.Security.Cryptography;
using ICS.Core.Time;
using ICS.Modules.Identity.Domain;
using ICS.Modules.Identity.Persistence;
using ICS.Modules.Organization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Concrete application service implementing Identity & Access authentication operations.
/// Architecture §14 (IAM Components) and §19.5 (Cookie Auth, server-side session, password hashing).
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserSessionRepository _userSessionRepository;
    private readonly IPasswordHasher<UserAccount> _passwordHasher;
    private readonly ISystemClock _clock;
    private readonly IOrganizationQueryService? _organizationQueryService;
    private readonly ILogger<AuthenticationService>? _logger;
    private readonly TimeSpan _sessionLifetime;

    public AuthenticationService(
        IUserAccountRepository userAccountRepository,
        IUserSessionRepository userSessionRepository,
        IPasswordHasher<UserAccount> passwordHasher,
        ISystemClock clock,
        IOrganizationQueryService? organizationQueryService = null,
        IConfiguration? configuration = null,
        ILogger<AuthenticationService>? logger = null)
    {
        _userAccountRepository = userAccountRepository ?? throw new ArgumentNullException(nameof(userAccountRepository));
        _userSessionRepository = userSessionRepository ?? throw new ArgumentNullException(nameof(userSessionRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _organizationQueryService = organizationQueryService;
        _logger = logger;

        // Default session duration is 8 hours unless configured
        var configuredHours = configuration?["Identity:SessionLifetimeHours"];
        if (double.TryParse(configuredHours, out var hours) && hours > 0)
        {
            _sessionLifetime = TimeSpan.FromHours(hours);
        }
        else
        {
            _sessionLifetime = TimeSpan.FromHours(8);
        }
    }

    /// <inheritdoc />
    public async Task<LoginResult> LoginAsync(
        string usernameOrEmail,
        string password,
        ClientInfo? clientInfo = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(password))
        {
            return LoginResult.Failure("Invalid username or password.", "INVALID_CREDENTIALS");
        }

        var account = await _userAccountRepository.GetByUsernameOrEmailAsync(usernameOrEmail, cancellationToken);
        if (account == null)
        {
            _logger?.LogWarning("Authentication failed: User '{Identifier}' not found.", usernameOrEmail);
            return LoginResult.Failure("Invalid username or password.", "INVALID_CREDENTIALS");
        }

        if (account.IsLocked)
        {
            _logger?.LogWarning("Authentication rejected: Account for user '{Identifier}' is locked.", usernameOrEmail);
            return LoginResult.Failure("Account is locked. Please contact your administrator.", "ACCOUNT_LOCKED");
        }

        if (account.IsSuspended)
        {
            _logger?.LogWarning("Authentication rejected: Account for user '{Identifier}' is suspended.", usernameOrEmail);
            return LoginResult.Failure("Account is suspended.", "ACCOUNT_SUSPENDED");
        }

        if (!account.IsActive)
        {
            _logger?.LogWarning("Authentication rejected: Account for user '{Identifier}' is not active.", usernameOrEmail);
            return LoginResult.Failure("Account is not active.", "ACCOUNT_INACTIVE");
        }

        // Verify cryptographic password hash using PBKDF2/HMAC-SHA512 via IPasswordHasher<UserAccount> (Architecture §19.5)
        var verificationResult = _passwordHasher.VerifyHashedPassword(account, account.PasswordHash, password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            account.RecordLoginFailure(_clock.UtcNow);
            await _userAccountRepository.UpdateAsync(account, cancellationToken);

            if (account.IsLocked)
            {
                _logger?.LogWarning("Account for user '{Identifier}' was locked due to excessive failed attempts.", usernameOrEmail);
                return LoginResult.Failure("Account has been locked due to too many failed login attempts.", "ACCOUNT_LOCKED");
            }

            _logger?.LogWarning("Authentication failed: Invalid credentials for user '{Identifier}'.", usernameOrEmail);
            return LoginResult.Failure("Invalid username or password.", "INVALID_CREDENTIALS");
        }

        // Verify linked Person active status via OrganizationQueryService (Architecture §14, §15)
        if (_organizationQueryService != null)
        {
            var isPersonActive = await _organizationQueryService.IsPersonActiveAsync(account.PersonId, cancellationToken);
            if (!isPersonActive)
            {
                _logger?.LogWarning("Authentication rejected: Linked person for user '{Identifier}' is not active.", usernameOrEmail);
                return LoginResult.Failure("Linked organizational person profile is not active.", "PERSON_INACTIVE");
            }
        }

        // Record successful login
        account.RecordLoginSuccess(_clock.UtcNow);
        await _userAccountRepository.UpdateAsync(account, cancellationToken);

        // Create server-side session in identity.UserSessions (Architecture §14, §19.5)
        var sessionToken = GenerateSecureToken();
        var expiresAt = _clock.UtcNow.Add(_sessionLifetime);
        var session = UserSession.Create(
            sessionId: Guid.NewGuid(),
            userId: account.UserId,
            personId: account.PersonId,
            sessionToken: sessionToken,
            expiresAt: expiresAt,
            createdAt: _clock.UtcNow,
            clientIp: clientInfo?.IpAddress,
            userAgent: clientInfo?.UserAgent);

        await _userSessionRepository.AddAsync(session, cancellationToken);

        _logger?.LogInformation("User '{Username}' authenticated successfully. Session issued.", account.Username);
        return LoginResult.Success(sessionToken, account.UserId, account.PersonId, expiresAt);
    }

    /// <inheritdoc />
    public async Task<bool> LogoutAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return false;
        }

        await _userSessionRepository.RevokeByTokenAsync(sessionToken, cancellationToken);
        _logger?.LogInformation("Session revoked successfully.");
        return true;
    }

    /// <inheritdoc />
    public async Task<SecurityContext> ValidateSessionAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return SecurityContext.Invalid("Missing session token.");
        }

        var session = await _userSessionRepository.GetByTokenAsync(sessionToken, cancellationToken);
        if (session == null)
        {
            return SecurityContext.Invalid("Session not found.");
        }

        if (session.IsRevoked)
        {
            return SecurityContext.Invalid("Session has been revoked.");
        }

        if (!session.IsValid(_clock.UtcNow))
        {
            return SecurityContext.Invalid("Session has expired.");
        }

        return SecurityContext.Valid(session.UserId, session.PersonId, session.SessionToken, session.ExpiresAt);
    }

    /// <inheritdoc />
    public LoginResult Login(string usernameOrEmail, string password, ClientInfo? clientInfo = null) =>
        LoginAsync(usernameOrEmail, password, clientInfo).GetAwaiter().GetResult();

    /// <inheritdoc />
    public bool Logout(string sessionToken) =>
        LogoutAsync(sessionToken).GetAwaiter().GetResult();

    /// <inheritdoc />
    public SecurityContext ValidateSession(string sessionToken) =>
        ValidateSessionAsync(sessionToken).GetAwaiter().GetResult();

    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
