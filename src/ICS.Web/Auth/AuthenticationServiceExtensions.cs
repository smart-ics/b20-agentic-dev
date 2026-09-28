namespace ICS.Web.Auth;

using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods configuring ASP.NET Core Cookie Authentication and Authorization
/// per Architecture §14, §18, §19.5, and §19.6.
/// </summary>
public static class AuthenticationServiceExtensions
{
    /// <summary>
    /// Default cookie name used for session authentication.
    /// </summary>
    public const string DefaultCookieName = "ICS_SESSION";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Configures ASP.NET Core Cookie Authentication with secure defaults and RFC 7807 401/403 responses.
    /// </summary>
    public static IServiceCollection AddIcsAuthentication(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var cookieName = configuration?["Authentication:CookieName"] ?? DefaultCookieName;

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.Cookie.Name = cookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.IsEssential = true;

            // In an API / SPA environment, respond directly with HTTP 401 Unauthorized instead of 302 redirecting to login page
            options.Events.OnRedirectToLogin = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json; charset=utf-8";

                var problem = new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                    Title = "Unauthorized",
                    Status = StatusCodes.Status401Unauthorized,
                    Detail = "Authentication is required to access this resource.",
                    Instance = context.Request.Path
                };
                problem.Extensions["errorCode"] = "UNAUTHORIZED";

                await JsonSerializer.SerializeAsync(context.Response.Body, problem, JsonOptions);
            };

            // Respond directly with HTTP 403 Forbidden instead of 302 redirecting to access denied page
            options.Events.OnRedirectToAccessDenied = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json; charset=utf-8";

                var problem = new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                    Title = "Forbidden",
                    Status = StatusCodes.Status403Forbidden,
                    Detail = "Access denied: insufficient permissions or roles.",
                    Instance = context.Request.Path
                };
                problem.Extensions["errorCode"] = "FORBIDDEN";

                await JsonSerializer.SerializeAsync(context.Response.Body, problem, JsonOptions);
            };
        });

        services.AddAuthorization();

        return services;
    }
}
