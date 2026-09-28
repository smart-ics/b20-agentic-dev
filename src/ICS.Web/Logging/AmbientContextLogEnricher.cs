using System.Diagnostics;
using ICS.Core.Auth;
using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace ICS.Web.Logging;

/// <summary>
/// Serilog log event enricher that dynamically attaches TraceId, SpanId, UserId, and PersonId
/// to every structured log event per Architecture §19.9.
/// </summary>
public class AmbientContextLogEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AmbientContextLogEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var activity = Activity.Current;

        var traceId = activity?.TraceId.ToString()
            ?? httpContext?.TraceIdentifier
            ?? string.Empty;

        var spanId = activity?.SpanId.ToString() ?? string.Empty;

        if (!string.IsNullOrEmpty(traceId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", traceId));
        }

        if (!string.IsNullOrEmpty(spanId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", spanId));
        }

        if (httpContext != null)
        {
            var currentContext = httpContext.RequestServices?.GetService<ICurrentContextProvider>();
            if (currentContext != null)
            {
                if (currentContext.CurrentUserId.HasValue)
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", currentContext.CurrentUserId.Value.ToString()));
                }

                if (currentContext.CurrentPersonId.HasValue)
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("PersonId", currentContext.CurrentPersonId.Value.ToString()));
                }
            }
        }
    }
}
