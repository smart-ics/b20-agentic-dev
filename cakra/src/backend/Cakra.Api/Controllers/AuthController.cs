using System.Diagnostics;
using System.Security.Claims;
using Cakra.Api.Infrastructure.Authentication;
using Cakra.Core;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Identity.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using IIdentityAuthenticationService = Cakra.Modules.Identity.Domain.IAuthenticationService;
using IIdentityAuthorizationService = Cakra.Modules.Identity.Services.IAuthorizationService;

namespace Cakra.Api.Controllers;

/// <summary>
/// REST API controller for authentication and session management (<c>SCR-AUTH-001</c>, <c>UC-AUTH-001</c>, <c>FEAT-AUTH-001</c>).
/// Exposes <c>/api/v1/auth/login</c>, <c>/api/v1/auth/logout</c>, and <c>/api/v1/auth/me</c> (Architecture §14, §19.5, §19.6).
/// </summary>
[Route("api/v1/auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IIdentityAuthenticationService _authenticationService;
    private readonly IIdentityAuthorizationService _authorizationService;
    private readonly ICurrentContextProvider _currentContextProvider;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IIdentityAuthenticationService authenticationService,
        IIdentityAuthorizationService authorizationService,
        ICurrentContextProvider currentContextProvider,
        ILogger<AuthController> logger)
    {
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _currentContextProvider = currentContextProvider ?? throw new ArgumentNullException(nameof(currentContextProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Authenticates user credentials and establishes a server-side session with a secure
    /// <c>HttpOnly</c>, <c>SameSite=Strict</c> session cookie (Architecture §14, §19.5).
    /// Returns distinct RFC 7807 <see cref="ProblemDetails"/> responses on failure (Architecture §19.6).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] LoginRequest? request, CancellationToken cancellationToken = default)
    {
        var usernameOrEmail = request?.ResolvedUsername ?? string.Empty;
        var password = request?.Password ?? string.Empty;

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var clientInfo = new ClientInfo(clientIp, string.IsNullOrWhiteSpace(userAgent) ? null : userAgent);

        var result = await _authenticationService.LoginAsync(usernameOrEmail, password, clientInfo, cancellationToken);
        if (!result.Succeeded)
        {
            return CreateLoginFailureProblem(result);
        }

        var userId = result.UserId!.Value;
        var personId = result.PersonId!.Value;
        var sessionToken = result.SessionToken!;

        var roles = await _authorizationService.ResolveRolesAsync(personId, cancellationToken);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("userId", userId.ToString()),
            new("personId", personId.ToString()),
            new("session_token", sessionToken)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, CakraAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = result.ExpiresAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(result.ExpiresAt.Value, DateTimeKind.Utc))
                : null
        };
        authProperties.SetString("SessionToken", sessionToken);

        await HttpContext.SignInAsync(
            CakraAuthenticationDefaults.AuthenticationScheme,
            principal,
            authProperties);

        _logger.LogInformation("User '{UserId}' (Person '{PersonId}') logged in via /api/v1/auth/login.", userId, personId);

        return Ok(new LoginResponse
        {
            UserId = userId,
            PersonId = personId,
            Roles = roles,
            ExpiresAt = result.ExpiresAt
        });
    }

    /// <summary>
    /// Invalidates the current server-side session and clears the session authentication cookie (Architecture §14, §19.5).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
    {
        var sessionToken = User.FindFirst("session_token")?.Value
            ?? Request.Cookies[CakraAuthenticationDefaults.CookieName];

        if (!string.IsNullOrWhiteSpace(sessionToken))
        {
            await _authenticationService.LogoutAsync(sessionToken, cancellationToken);
        }

        await HttpContext.SignOutAsync(CakraAuthenticationDefaults.AuthenticationScheme);

        return Ok(new { message = "Logged out successfully." });
    }

    /// <summary>
    /// Returns the currently authenticated user's security profile (<c>UserId</c>, <c>PersonId</c>, <c>Roles</c>).
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        return Ok(new CurrentUserResponse
        {
            UserId = _currentContextProvider.CurrentUserId ?? Guid.Empty,
            PersonId = _currentContextProvider.CurrentPersonId ?? Guid.Empty,
            Roles = _currentContextProvider.CurrentRoles.ToArray()
        });
    }

    private ObjectResult CreateLoginFailureProblem(LoginResult result)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        var isAccountLocked =
            string.Equals(result.ErrorCode, "ACCOUNT_LOCKED", StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(result.ErrorCode, "ACCOUNT_NOT_ACTIVE", StringComparison.OrdinalIgnoreCase) &&
             result.ErrorMessage != null &&
             result.ErrorMessage.Contains("locked", StringComparison.OrdinalIgnoreCase));

        int statusCode;
        string title;
        string detail;
        string errorCode;
        string typeUrl;

        if (isAccountLocked)
        {
            statusCode = StatusCodes.Status403Forbidden;
            title = "Account Locked";
            detail = !string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? result.ErrorMessage
                : "Your account has been locked due to multiple failed login attempts.";
            errorCode = "ACCOUNT_LOCKED";
            typeUrl = "https://tools.ietf.org/html/rfc7231#section-6.5.3";
        }
        else if (string.Equals(result.ErrorCode, "ACCOUNT_NOT_ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            statusCode = StatusCodes.Status403Forbidden;
            title = "Account Inactive";
            detail = !string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? result.ErrorMessage
                : "Your account is not active.";
            errorCode = "ACCOUNT_NOT_ACTIVE";
            typeUrl = "https://tools.ietf.org/html/rfc7231#section-6.5.3";
        }
        else if (string.Equals(result.ErrorCode, "PERSON_INACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            statusCode = StatusCodes.Status403Forbidden;
            title = "Person Inactive";
            detail = !string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? result.ErrorMessage
                : "The linked person record is inactive.";
            errorCode = "PERSON_INACTIVE";
            typeUrl = "https://tools.ietf.org/html/rfc7231#section-6.5.3";
        }
        else
        {
            statusCode = StatusCodes.Status401Unauthorized;
            title = "Invalid Credentials";
            detail = !string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? result.ErrorMessage
                : "Invalid username or password.";
            errorCode = "INVALID_CREDENTIALS";
            typeUrl = "https://tools.ietf.org/html/rfc7235#section-3.1";
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Type = typeUrl
        };
        problem.Extensions["errorCode"] = errorCode;
        problem.Extensions["traceId"] = traceId;

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/auth/login</c>.
    /// Accepts either <c>username</c> or <c>usernameOrEmail</c>.
    /// </summary>
    public sealed class LoginRequest
    {
        public string? Username { get; set; }
        public string? UsernameOrEmail { get; set; }
        public string? Password { get; set; }

        public string ResolvedUsername =>
            !string.IsNullOrWhiteSpace(Username) ? Username : (UsernameOrEmail ?? string.Empty);
    }

    /// <summary>
    /// Response payload for a successful <c>POST /api/v1/auth/login</c>.
    /// </summary>
    public sealed class LoginResponse
    {
        public Guid UserId { get; init; }
        public Guid PersonId { get; init; }
        public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
        public DateTime? ExpiresAt { get; init; }
    }

    /// <summary>
    /// Response payload for <c>GET /api/v1/auth/me</c>.
    /// </summary>
    public sealed class CurrentUserResponse
    {
        public Guid UserId { get; init; }
        public Guid PersonId { get; init; }
        public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    }
}
