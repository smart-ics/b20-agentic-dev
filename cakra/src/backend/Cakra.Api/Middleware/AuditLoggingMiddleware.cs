using System.Diagnostics;
using Cakra.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Cakra.Api.Middleware;

/// <summary>
/// Audit logging hook in the HTTP pipeline (Architecture §18).
/// Emits structured audit telemetry for mutating operations (POST, PUT, PATCH, DELETE)
/// and API requests, capturing ActorPersonId, ActorUserId, timestamp, HTTP method,
/// path, and status code.
/// </summary>
public sealed class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context, IAuditContext auditContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(auditContext);

        var stopwatch = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;

        await _next(context);

        stopwatch.Stop();

        // Emit audit entry for state changes or API requests
        if (HttpMethods.IsPost(method) ||
            HttpMethods.IsPut(method) ||
            HttpMethods.IsPatch(method) ||
            HttpMethods.IsDelete(method) ||
            path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Audit: {HttpMethod} {HttpPath} resulted in {StatusCode} in {ElapsedMs}ms. ActorPersonId: {ActorPersonId}, ActorUserId: {ActorUserId}, RecordedAtUtc: {RecordedAtUtc}",
                method,
                path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                auditContext.ActorPersonId,
                auditContext.ActorUserId,
                auditContext.RecordedAtUtc);
        }
    }
}
