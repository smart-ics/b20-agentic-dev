using System.Diagnostics;
using ICS.Core.Auth;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace ICS.Web.Middleware;

/// <summary>
/// Scoped logging context middleware pushing ambient correlation identifiers and identity metadata
/// into Serilog's <see cref="LogContext"/> for the duration of the HTTP request lifecycle.
/// Per Architecture §19.9.
/// </summary>
public class LoggingContextMiddleware
{
    private readonly RequestDelegate _next;

    public LoggingContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentContextProvider currentContext)
    {
        var activity = Activity.Current;
        var traceId = activity?.TraceId.ToString() ?? context.TraceIdentifier;
        var spanId = activity?.SpanId.ToString() ?? string.Empty;
        var userId = currentContext.CurrentUserId?.ToString() ?? "anonymous";
        var personId = currentContext.CurrentPersonId?.ToString() ?? "unassigned";

        using (LogContext.PushProperty("TraceId", traceId))
        using (LogContext.PushProperty("SpanId", spanId))
        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("PersonId", personId))
        {
            await _next(context);
        }
    }
}
