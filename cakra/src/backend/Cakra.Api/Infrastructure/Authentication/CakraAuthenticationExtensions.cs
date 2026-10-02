using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Identity.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using IIdentityAuthService = Cakra.Modules.Identity.Domain.IAuthenticationService;

namespace Cakra.Api.Infrastructure.Authentication;

/// <summary>
/// Service collection extension methods for registering ASP.NET Core Cookie Authentication
/// wired into server-side session validation and dynamic role resolution (Architecture §14, §18, §19.5).
/// </summary>
public static class CakraAuthenticationExtensions
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Configures ASP.NET Core Cookie Authentication per Architecture §19.5:
    /// - Scheme: <see cref="CookieAuthenticationDefaults.AuthenticationScheme"/> ("Cookies")
    /// - Cookie options: HttpOnly = true, SameSite = Strict, Secure = true
    /// - Custom ticket format: <see cref="SessionCookieTicketFormat"/> extracting server-side session token
    /// - Ticket validation: verifies session validity via <see cref="IIdentityAuthService.ValidateSessionAsync"/>
    /// - RBAC: dynamically resolves active roles via <see cref="IAuthorizationService.ResolveRolesAsync"/>
    /// - API challenge/forbid handlers: emits RFC 7807 ProblemDetails on HTTP 401 and HTTP 403
    /// </summary>
    public static IServiceCollection AddCakraAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.Cookie.Name = CakraAuthenticationDefaults.CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.TicketDataFormat = new SessionCookieTicketFormat();
            options.LoginPath = PathString.Empty;
            options.AccessDeniedPath = PathString.Empty;

            options.Events = new CookieAuthenticationEvents
            {
                OnValidatePrincipal = async validateContext =>
                {
                    var sessionToken = validateContext.Properties.GetString("SessionToken")
                        ?? validateContext.Principal?.FindFirst("session_token")?.Value
                        ?? validateContext.Request.Cookies[options.Cookie.Name!];

                    if (string.IsNullOrWhiteSpace(sessionToken))
                    {
                        validateContext.RejectPrincipal();
                        return;
                    }

                    var authService = validateContext.HttpContext.RequestServices.GetRequiredService<IIdentityAuthService>();
                    var secContext = await authService.ValidateSessionAsync(sessionToken, validateContext.HttpContext.RequestAborted);

                    if (!secContext.IsValid)
                    {
                        validateContext.RejectPrincipal();
                        return;
                    }

                    var authzService = validateContext.HttpContext.RequestServices.GetService<IAuthorizationService>();
                    var roles = authzService != null
                        ? await authzService.ResolveRolesAsync(secContext.PersonId, validateContext.HttpContext.RequestAborted)
                        : Array.Empty<string>();

                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, secContext.UserId.ToString()),
                        new Claim("userId", secContext.UserId.ToString()),
                        new Claim("personId", secContext.PersonId.ToString()),
                        new Claim("session_token", sessionToken)
                    };

                    foreach (var role in roles)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, role));
                    }

                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    validateContext.Principal = new ClaimsPrincipal(identity);
                    validateContext.ShouldRenew = false;
                },
                OnRedirectToLogin = async redirectContext =>
                {
                    if (!redirectContext.Response.HasStarted)
                    {
                        redirectContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        redirectContext.Response.ContentType = "application/problem+json";
                        var problem = new ProblemDetails
                        {
                            Status = StatusCodes.Status401Unauthorized,
                            Title = "Unauthorized",
                            Detail = "Authentication is required to access this resource.",
                            Instance = redirectContext.Request.Path
                        };
                        problem.Extensions["errorCode"] = "UNAUTHORIZED";
                        problem.Extensions["traceId"] = Activity.Current?.Id ?? redirectContext.HttpContext.TraceIdentifier;
                        await redirectContext.Response.WriteAsJsonAsync(problem, s_jsonOptions, "application/problem+json");
                    }
                },
                OnRedirectToAccessDenied = async redirectContext =>
                {
                    if (!redirectContext.Response.HasStarted)
                    {
                        redirectContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                        redirectContext.Response.ContentType = "application/problem+json";
                        var problem = new ProblemDetails
                        {
                            Status = StatusCodes.Status403Forbidden,
                            Title = "Forbidden",
                            Detail = "You do not have permission to access this resource.",
                            Instance = redirectContext.Request.Path
                        };
                        problem.Extensions["errorCode"] = "FORBIDDEN";
                        problem.Extensions["traceId"] = Activity.Current?.Id ?? redirectContext.HttpContext.TraceIdentifier;
                        await redirectContext.Response.WriteAsJsonAsync(problem, s_jsonOptions, "application/problem+json");
                    }
                }
            };
        });

        return services;
    }
}
