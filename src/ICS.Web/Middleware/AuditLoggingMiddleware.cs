using System.Diagnostics;
using ICS.Core.Audit;
using ICS.Core.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ICS.Web.Middleware;

/// <summary>
/// Audit logging middleware establishing ambient client network and user-agent metadata in <see cref="IAuditContextAccessor"/>,
/// and capturing audit log traces for state-mutating HTTP requests per Architecture §18.
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IAuditContextAccessor auditAccessor,
        ICurrentContextProvider currentContext)
    {
        // 1. Extract client IP and user agent
        var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        var userAgent = context.Request.Headers["User-Agent"].ToString();
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            userAgent = "unknown";
        }

        auditAccessor.SetClientInfo(ipAddress, userAgent);

        var isStateMutating = HttpMethods.IsPost(context.Request.Method) ||
                             HttpMethods.IsPut(context.Request.Method) ||
                             HttpMethods.IsPatch(context.Request.Method) ||
                             HttpMethods.IsDelete(context.Request.Method);

        if (isStateMutating)
        {
            _logger.LogInformation(
                "Audited Operation Started: {Method} {Path} by Actor PersonId={PersonId}, UserId={UserId} from IP={IpAddress}",
                context.Request.Method,
                context.Request.Path,
                currentContext.CurrentPersonId,
                currentContext.CurrentUserId,
                ipAddress);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            if (isStateMutating)
            {
                _logger.LogInformation(
                    "Audited Operation Completed: {Method} {Path} -> {StatusCode} in {ElapsedMs}ms by Actor PersonId={PersonId}, UserId={UserId}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    currentContext.CurrentPersonId,
                    currentContext.CurrentUserId);
            }
        }
    }
}
