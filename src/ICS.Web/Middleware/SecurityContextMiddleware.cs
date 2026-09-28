namespace ICS.Web.Middleware;

using System.Security.Claims;
using ICS.Core.Auth;
using ICS.Web.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IIcsAuthenticationService = ICS.Modules.Identity.Application.IAuthenticationService;
using IIcsAuthorizationService = ICS.Modules.Identity.Application.IAuthorizationService;

/// <summary>
/// HTTP authentication middleware and ambient security context pipeline component.
/// Intercepts incoming requests, validates session cookies against identity.UserSessions via AuthenticationService,
/// dynamically resolves active organizational roles via AuthorizationService, populates CurrentContextProvider,
/// and synchronizes ClaimsPrincipal for RBAC authorization per Architecture §7, §14, §18, and §19.5.
/// </summary>
public class SecurityContextMiddleware
{
    public const string CookieName = AuthenticationServiceExtensions.DefaultCookieName;
    public const string UserIdHeader = "X-User-Id";
    public const string PersonIdHeader = "X-Person-Id";
    public const string RolesHeader = "X-Roles";
    public const string SessionTokenHeader = "X-Session-Token";

    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityContextMiddleware> _logger;

    public SecurityContextMiddleware(RequestDelegate next, ILogger<SecurityContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentContextAccessor contextAccessor,
        IIcsAuthenticationService? authenticationService = null,
        IIcsAuthorizationService? authorizationService = null)
    {
        authenticationService ??= context.RequestServices?.GetService<IIcsAuthenticationService>();
        authorizationService ??= context.RequestServices?.GetService<IIcsAuthorizationService>();

        string? sessionToken = null;

        // 1. Extract session token from cookie (Architecture §19.5: secure, HttpOnly, SameSite=Strict session cookie)
        if (context.Request.Cookies.TryGetValue(CookieName, out var cookieVal) && !string.IsNullOrWhiteSpace(cookieVal))
        {
            sessionToken = cookieVal.Trim();
        }
        else if (context.Request.Headers.TryGetValue(SessionTokenHeader, out var headerVal) && !string.IsNullOrWhiteSpace(headerVal))
        {
            sessionToken = headerVal.ToString().Trim();
        }
        else if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var authStr = authHeader.ToString().Trim();
            if (authStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                sessionToken = authStr.Substring("Bearer ".Length).Trim();
            }
        }

        // 2. Validate session against identity.UserSessions if token is provided
        if (!string.IsNullOrWhiteSpace(sessionToken) && authenticationService != null)
        {
            var securityContext = await authenticationService.ValidateSessionAsync(sessionToken, context.RequestAborted);
            if (securityContext.IsAuthenticated && securityContext.UserId.HasValue && securityContext.PersonId.HasValue)
            {
                var userId = securityContext.UserId.Value;
                var personId = securityContext.PersonId.Value;

                // Resolve active roles dynamically via AuthorizationService backed by OrganizationQueryService (Architecture §14, §15)
                IReadOnlyList<string> roles = Array.Empty<string>();
                if (authorizationService != null)
                {
                    roles = await authorizationService.GetRolesForPersonAsync(personId, context.RequestAborted);
                }

                // Construct ClaimsIdentity and ClaimsPrincipal for ASP.NET Core RBAC authorization
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, userId.ToString()),
                    new("person_id", personId.ToString()),
                    new("session_token", securityContext.SessionToken ?? sessionToken)
                };

                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                context.User = principal;
                contextAccessor.SetContext(userId, personId, roles);

                _logger.LogDebug(
                    "Security context established for session: UserId={UserId}, PersonId={PersonId}, Roles=[{Roles}]",
                    userId, personId, string.Join(", ", roles));

                await _next(context);
                return;
            }
            else
            {
                _logger.LogWarning(
                    "Session validation failed: {Reason}",
                    securityContext.FailureReason ?? "Invalid or expired session token.");

                contextAccessor.Clear();
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
                await _next(context);
                return;
            }
        }

        // 3. Fallback: Check HTTP header overrides (for local dev, pipeline validator, and integration test harnesses)
        Guid? fallbackUserId = null;
        Guid? fallbackPersonId = null;
        var fallbackRoles = new List<string>();

        if (context.Request.Headers.TryGetValue(UserIdHeader, out var userIdHeader) &&
            Guid.TryParse(userIdHeader.ToString(), out var parsedUserId))
        {
            fallbackUserId = parsedUserId;
        }

        if (context.Request.Headers.TryGetValue(PersonIdHeader, out var personIdHeader) &&
            Guid.TryParse(personIdHeader.ToString(), out var parsedPersonId))
        {
            fallbackPersonId = parsedPersonId;
        }

        if (context.Request.Headers.TryGetValue(RolesHeader, out var rolesHeader))
        {
            var headerRoles = rolesHeader.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            fallbackRoles.AddRange(headerRoles);
        }

        if (fallbackUserId.HasValue || fallbackPersonId.HasValue || fallbackRoles.Count > 0)
        {
            var claims = new List<Claim>();
            if (fallbackUserId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, fallbackUserId.Value.ToString()));
            }
            if (fallbackPersonId.HasValue)
            {
                claims.Add(new Claim("person_id", fallbackPersonId.Value.ToString()));
            }
            foreach (var r in fallbackRoles)
            {
                claims.Add(new Claim(ClaimTypes.Role, r));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            context.User = new ClaimsPrincipal(identity);
            contextAccessor.SetContext(fallbackUserId, fallbackPersonId, fallbackRoles);

            _logger.LogDebug(
                "Security context populated from test headers: UserId={UserId}, PersonId={PersonId}, Roles=[{Roles}]",
                fallbackUserId, fallbackPersonId, string.Join(", ", fallbackRoles));
        }
        else
        {
            // Anonymous / Unauthenticated request
            contextAccessor.Clear();
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
        }

        await _next(context);
    }
}
