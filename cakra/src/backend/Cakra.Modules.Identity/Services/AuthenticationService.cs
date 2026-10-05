using System.Security.Cryptography;
using Cakra.Core;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Organization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Cakra.Modules.Identity.Services;

/// <summary>
/// Core implementation of <see cref="IAuthenticationService"/> (Architecture §14, §19.5).
/// Coordinates credential verification via PBKDF2/HMAC-SHA512 (<see cref="IPasswordHasher{TUser}"/>),
/// status validation, server-side session persistence in <c>identity.UserSessions</c>, and token issuance.
/// </summary>
public sealed class AuthenticationService : IAuthenticationService
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
        ILogger<AuthenticationService>? logger = null,
        TimeSpan? sessionLifetime = null)
    {
        _userAccountRepository = userAccountRepository ?? throw new ArgumentNullException(nameof(userAccountRepository));
        _userSessionRepository = userSessionRepository ?? throw new ArgumentNullException(nameof(userSessionRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _organizationQueryService = organizationQueryService;
        _logger = logger;
        _sessionLifetime = sessionLifetime ?? TimeSpan.FromHours(8);
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
            return LoginResult.Failed("INVALID_CREDENTIALS", "Username or email and password are required.");
        }

        var normalizedInput = usernameOrEmail.Trim();
        var user = await _userAccountRepository.GetByUsernameOrEmailAsync(normalizedInput, cancellationToken);
        if (user == null)
        {
            _logger?.LogWarning("Authentication failed: user '{UsernameOrEmail}' not found.", normalizedInput);
            return LoginResult.Failed("INVALID_CREDENTIALS", "Invalid username or password.");
        }

        if (string.Equals(user.Status, UserAccountStatus.Pending, StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogWarning("Authentication failed: user '{UserId}' is pending approval.", user.Id);
            return LoginResult.Failed("ACCOUNT_PENDING_APPROVAL", "Your account is pending administrative approval.");
        }

        if (!user.IsActive)
        {
            _logger?.LogWarning("Authentication failed: user '{UserId}' is not active (Status: {Status}).", user.Id, user.Status);
            return LoginResult.Failed("ACCOUNT_NOT_ACTIVE", $"Account is {user.Status.ToLowerInvariant()}.");
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            _logger?.LogWarning("Authentication failed: invalid password for user '{UserId}'.", user.Id);
            user.RecordLoginFailure(maxAttemptsBeforeLock: 5, timestampUtc: _clock.UtcNow);
            await _userAccountRepository.UpdateAsync(user, cancellationToken);
            return LoginResult.Failed("INVALID_CREDENTIALS", "Invalid username or password.");
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
        }

        // Verify Person active status if Organization service is registered (Architecture §14, §15)
        if (_organizationQueryService != null)
        {
            var isPersonActive = await _organizationQueryService.IsPersonActiveAsync(user.PersonId, cancellationToken);
            if (!isPersonActive)
            {
                _logger?.LogWarning("Authentication failed: Person '{PersonId}' linked to user '{UserId}' is inactive.", user.PersonId, user.Id);
                return LoginResult.Failed("PERSON_INACTIVE", "The linked person record is inactive.");
            }
        }

        // Record successful login
        user.RecordLoginSuccess(_clock.UtcNow);
        await _userAccountRepository.UpdateAsync(user, cancellationToken);

        // Issue session
        var sessionToken = GenerateSecureToken();
        var expiresAt = _clock.UtcNow.Add(_sessionLifetime);

        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PersonId = user.PersonId,
            SessionToken = sessionToken,
            ExpiresAt = expiresAt,
            CreatedAt = _clock.UtcNow,
            ClientIp = clientInfo?.ClientIp,
            UserAgent = clientInfo?.UserAgent,
            IsRevoked = false
        };

        await _userSessionRepository.AddAsync(session, cancellationToken);
        _logger?.LogInformation("User '{UserId}' successfully authenticated. Session '{SessionId}' created.", user.Id, session.Id);

        return LoginResult.Success(sessionToken, expiresAt, user.Id, user.PersonId);
    }

    /// <inheritdoc />
    public async Task<bool> LogoutAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return false;
        }

        var revoked = await _userSessionRepository.RevokeByTokenAsync(sessionToken, _clock.UtcNow, cancellationToken);
        if (revoked)
        {
            _logger?.LogInformation("Session token '{MaskedToken}' was revoked.", MaskToken(sessionToken));
        }
        return revoked;
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

        if (session.IsExpired(_clock.UtcNow))
        {
            return SecurityContext.Invalid("Session has expired.");
        }

        return SecurityContext.Valid(session.UserId, session.PersonId);
    }

    /// <inheritdoc />
    public LoginResult Login(string usernameOrEmail, string password, ClientInfo? clientInfo = null) =>
        LoginAsync(usernameOrEmail, password, clientInfo, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public bool Logout(string sessionToken) =>
        LogoutAsync(sessionToken, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public SecurityContext ValidateSession(string sessionToken) =>
        ValidateSessionAsync(sessionToken, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public string HashPassword(UserAccount user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return _passwordHasher.HashPassword(user, password);
    }

    private static string GenerateSecureToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    }

    private static string MaskToken(string token)
    {
        if (token.Length <= 8)
        {
            return "***";
        }
        return string.Concat(token.AsSpan(0, 4), "...", token.AsSpan(token.Length - 4));
    }
}
