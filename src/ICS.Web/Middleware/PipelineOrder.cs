using ICS.Web.Logging;
using Microsoft.AspNetCore.Builder;
using Serilog;

namespace ICS.Web.Middleware;

/// <summary>
/// Documents and registers the authoritative HTTP Request Pipeline Middleware Order
/// per Architecture §5, §14, §18, §19.5, §19.6, and §19.9.
/// </summary>
public static class PipelineOrder
{
    /*
     * AUTHORITATIVE MIDDLEWARE REGISTRATION ORDER:
     * -----------------------------------------------------------------------------------------
     * 1. Serilog HTTP Request Logging:
     *    Captures incoming HTTP requests, duration, status codes, and enriches Serilog diagnostics.
     *
     * 2. Centralized Exception Handling Middleware:
     *    Catches all downstream unhandled exceptions and writes RFC 7807 ProblemDetails JSON responses.
     *
     * 3. Logging Context Middleware:
     *    Pushes ambient TraceId, SpanId, UserId, and PersonId properties into Serilog's LogContext
     *    for the lifetime of the request.
     *
     * 4. Static Files & Default Files (Architecture §19.4, §19.10):
     *    Serves compiled Vue 3 Single Page Application static assets from wwwroot.
     *
     * 5. Routing (Architecture §18):
     *    Matches incoming request paths to target endpoints, enabling endpoint authorization metadata inspection.
     *
     * 6. Authentication & Security Context Population (Architecture §14, §18, §19.5):
     *    Validates session cookies against identity.UserSessions via AuthenticationService,
     *    dynamically resolves active roles via AuthorizationService, populates CurrentContextProvider,
     *    and synchronizes ClaimsPrincipal for RBAC authorization.
     *
     * 7. Authorization (RBAC) (Architecture §14, §18, §19.5):
     *    Enforces endpoint authorization policies and role requirements ([Authorize(Roles = "...")]),
     *    returning RFC 7807 401 Unauthorized or 403 Forbidden on authorization failures.
     *
     * 8. Audit Logging Middleware Hook (Architecture §18):
     *    Extracts client IP and User-Agent, populates IAuditContextAccessor, and logs audit traces
     *    for state-mutating requests (POST, PUT, PATCH, DELETE).
     *
     * 9. Endpoints & Controllers:
     *    Dispatches controllers, minimal APIs, health checks, and falls back to index.html for SPA routes.
     * -----------------------------------------------------------------------------------------
     */

    /// <summary>
    /// Configures the ICS HTTP request pipeline with strict middleware ordering.
    /// </summary>
    public static IApplicationBuilder UseIcsApplicationPipeline(this WebApplication app)
    {
        // 1. Serilog HTTP Request Logging with Diagnostic Context Enrichment
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = SerilogConfiguration.EnrichDiagnosticContext;
        });

        // 2. Centralized Exception Handling (RFC 7807 ProblemDetails)
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        // 3. Ambient Logging Context Enrichment
        app.UseMiddleware<LoggingContextMiddleware>();

        // 4. SPA Default & Static Files (Architecture §19.4, §19.10)
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // 5. Routing
        app.UseRouting();

        // 6. Cookie Authentication & Security Context Population (Architecture §14, §18, §19.5)
        app.UseAuthentication();
        app.UseMiddleware<SecurityContextMiddleware>();

        // 7. Role-Based Access Control (RBAC) Authorization (Architecture §14, §18, §19.5)
        app.UseAuthorization();

        // 8. Audit Logging Middleware Hook
        app.UseMiddleware<AuditLoggingMiddleware>();

        return app;
    }
}
