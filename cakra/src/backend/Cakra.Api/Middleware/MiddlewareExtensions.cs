using Microsoft.AspNetCore.Builder;

namespace Cakra.Api.Middleware;

/// <summary>
/// Pipeline extension methods for registering CAKRA API middlewares in canonical order.
/// </summary>
public static class MiddlewareExtensions
{
    /// <summary>
    /// Registers the centralized RFC 7807 ProblemDetails exception handling middleware.
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
    }

    /// <summary>
    /// Registers the security context population middleware placeholder (Architecture §18).
    /// </summary>
    public static IApplicationBuilder UseSecurityContext(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityContextMiddleware>();
    }

    /// <summary>
    /// Registers the audit logging hook middleware (Architecture §18).
    /// </summary>
    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuditLoggingMiddleware>();
    }
}
