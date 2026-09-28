namespace ICS.Web.Controllers;

using System.Diagnostics;
using System.Text.Json.Serialization;
using ICS.Core.Auth;
using ICS.Modules.Identity.Application;
using ICS.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IIcsAuthorizationService = ICS.Modules.Identity.Application.IAuthorizationService;

/// <summary>
/// REST API Controller managing authentication and session lifecycle per Architecture §14, §19.5, §19.6,
/// and SCR-AUTH-001 (Slice P2-S11).
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IIcsAuthorizationService? _authorizationService;
    private readonly ICurrentContextProvider _currentContext;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AuthController> _logger;
    private readonly string _cookieName;

    public AuthController(
        IAuthenticationService authenticationService,
        ICurrentContextProvider currentContext,
        IHostEnvironment environment,
        ILogger<AuthController> logger,
        IIcsAuthorizationService? authorizationService = null,
        IConfiguration? configuration = null)
    {
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _currentContext = currentContext ?? throw new ArgumentNullException(nameof(currentContext));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _authorizationService = authorizationService;
        _cookieName = configuration?["Authentication:CookieName"] ?? AuthenticationServiceExtensions.DefaultCookieName;
    }

    /// <summary>
    /// Authenticates user credentials, sets secure HttpOnly SameSite=Strict session cookie,
    /// and returns user profile upon success.
    /// Returns distinct RFC 7807 ProblemDetails on invalid credentials (401) or account locked (423).
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(LoginSuccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;

        var identifier = request?.ResolvedIdentifier;
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(request?.Password))
        {
            var validationProblem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Validation Error",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Username/email and password are required.",
                Instance = HttpContext.Request.Path
            };
            validationProblem.Extensions["errorCode"] = "VALIDATION_ERROR";
            validationProblem.Extensions["traceId"] = traceId;
            validationProblem.Extensions["timestamp"] = DateTime.UtcNow;

            return ProblemResult(StatusCodes.Status400BadRequest, validationProblem);
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var userAgent = Request.Headers.UserAgent.ToString();
        var clientInfo = new ClientInfo(clientIp, userAgent);

        var result = await _authenticationService.LoginAsync(identifier, request.Password, clientInfo, cancellationToken);

        if (!result.Succeeded)
        {
            var isLocked = string.Equals(result.ErrorCode, "ACCOUNT_LOCKED", StringComparison.OrdinalIgnoreCase) ||
                           (result.ErrorMessage?.Contains("lock", StringComparison.OrdinalIgnoreCase) == true);

            var statusCode = isLocked ? StatusCodes.Status423Locked : StatusCodes.Status401Unauthorized;
            var type = isLocked
                ? "https://tools.ietf.org/html/rfc4918#section-11.2"
                : "https://tools.ietf.org/html/rfc7235#section-3.1";
            var title = isLocked ? "Account Locked" : "Invalid Credentials";
            var errorCode = isLocked ? "ACCOUNT_LOCKED" : (result.ErrorCode ?? "INVALID_CREDENTIALS");

            var problem = new ProblemDetails
            {
                Type = type,
                Title = title,
                Status = statusCode,
                Detail = result.ErrorMessage ?? (isLocked ? "Account is locked." : "Invalid username or password."),
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["errorCode"] = errorCode;
            problem.Extensions["traceId"] = traceId;
            problem.Extensions["timestamp"] = DateTime.UtcNow;

            _logger.LogWarning(
                "Login attempt failed for '{Identifier}'. StatusCode: {StatusCode}, ErrorCode: {ErrorCode}, TraceId: {TraceId}",
                identifier, statusCode, errorCode, traceId);

            return ProblemResult(statusCode, problem);
        }

        // Set secure HttpOnly SameSite=Strict session cookie per Architecture §19.5
        var isSecure = Request.IsHttps || (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"));
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = isSecure,
            Expires = result.ExpiresAt,
            IsEssential = true,
            Path = "/"
        };

        Response.Cookies.Append(_cookieName, result.SessionToken!, cookieOptions);

        // Dynamically resolve active organizational roles
        IReadOnlyList<string> roles = Array.Empty<string>();
        if (result.PersonId.HasValue && _authorizationService != null)
        {
            roles = await _authorizationService.GetRolesForPersonAsync(result.PersonId.Value, cancellationToken);
        }

        _logger.LogInformation(
            "User '{Identifier}' successfully logged in. SessionToken issued. Roles: [{Roles}]. TraceId: {TraceId}",
            identifier, string.Join(", ", roles), traceId);

        var response = new LoginSuccessResponse(
            UserId: result.UserId!.Value,
            PersonId: result.PersonId!.Value,
            Username: identifier,
            Roles: roles,
            ExpiresAt: result.ExpiresAt,
            Message: "Login successful."
        );

        return Ok(response);
    }

    /// <summary>
    /// Invalidates the active server-side session and clears the session cookie per Architecture §14 and §19.5.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        string? sessionToken = null;

        if (Request.Cookies.TryGetValue(_cookieName, out var cookieVal) && !string.IsNullOrWhiteSpace(cookieVal))
        {
            sessionToken = cookieVal.Trim();
        }
        else if (Request.Headers.TryGetValue("X-Session-Token", out var headerVal) && !string.IsNullOrWhiteSpace(headerVal))
        {
            sessionToken = headerVal.ToString().Trim();
        }
        else if (User.FindFirst("session_token")?.Value is { } claimToken)
        {
            sessionToken = claimToken;
        }

        if (!string.IsNullOrWhiteSpace(sessionToken))
        {
            await _authenticationService.LogoutAsync(sessionToken, cancellationToken);
            _logger.LogInformation("Session successfully invalidated during logout.");
        }

        // Clear session cookie
        var isSecure = Request.IsHttps || (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"));
        Response.Cookies.Delete(_cookieName, new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = isSecure
        });

        return Ok(new { message = "Logged out successfully." });
    }

    /// <summary>
    /// Returns current user context (authenticated / unauthenticated) for frontend hydration.
    /// </summary>
    [HttpGet("me")]
    [AllowAnonymous]
    public IActionResult GetCurrentUser()
    {
        if (!_currentContext.IsAuthenticated || !_currentContext.CurrentUserId.HasValue)
        {
            return Ok(new
            {
                isAuthenticated = false,
                userId = (Guid?)null,
                personId = (Guid?)null,
                roles = Array.Empty<string>()
            });
        }

        return Ok(new
        {
            isAuthenticated = true,
            userId = _currentContext.CurrentUserId,
            personId = _currentContext.CurrentPersonId,
            roles = _currentContext.CurrentRoles
        });
    }

    private static IActionResult ProblemResult(int statusCode, ProblemDetails problem)
    {
        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }
}

/// <summary>
/// Login request payload supporting both username/password and usernameOrEmail/password keys.
/// </summary>
public record LoginRequest
{
    public string? UsernameOrEmail { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }

    [JsonIgnore]
    public string ResolvedIdentifier =>
        !string.IsNullOrWhiteSpace(UsernameOrEmail)
            ? UsernameOrEmail.Trim()
            : Username?.Trim() ?? string.Empty;
}

/// <summary>
/// Successful login response payload.
/// </summary>
public record LoginSuccessResponse(
    Guid UserId,
    Guid PersonId,
    string Username,
    IReadOnlyList<string> Roles,
    DateTime? ExpiresAt,
    string Message
);
