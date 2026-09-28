using System.Diagnostics;
using Cakra.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace Cakra.Api.Infrastructure.Logging;

/// <summary>
/// Serilog log event enricher that populates structured diagnostic properties
/// (Architecture §19.9):
/// <c>TraceId</c>, <c>SpanId</c>, <c>UserId</c>, <c>PersonId</c>, and <c>SourceContext</c>.
/// </summary>
public sealed class CakraLogEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public CakraLogEnricher(IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        // Enrich TraceId and SpanId from Activity.Current if distributed tracing is active
        var activity = Activity.Current;
        if (activity != null)
        {
            var traceId = activity.TraceId.ToHexString();
            var spanId = activity.SpanId.ToHexString();

            if (!string.IsNullOrEmpty(traceId))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", traceId));
            }

            if (!string.IsNullOrEmpty(spanId))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", spanId));
            }
        }

        var httpContext = _httpContextAccessor?.HttpContext;
        if (httpContext != null)
        {
            // If Activity was not active, fall back to the ASP.NET Core request TraceIdentifier
            if (activity == null && !string.IsNullOrEmpty(httpContext.TraceIdentifier))
            {
                logEvent.AddPropertyIfAbsent(
                    propertyFactory.CreateProperty("TraceId", httpContext.TraceIdentifier));
            }

            // Enrich authenticated identity from the ambient ICurrentContextProvider
            var contextProvider = httpContext.RequestServices?.GetService<ICurrentContextProvider>();
            if (contextProvider != null)
            {
                if (contextProvider.CurrentUserId.HasValue)
                {
                    logEvent.AddPropertyIfAbsent(
                        propertyFactory.CreateProperty("UserId", contextProvider.CurrentUserId.Value));
                }

                if (contextProvider.CurrentPersonId.HasValue)
                {
                    logEvent.AddPropertyIfAbsent(
                        propertyFactory.CreateProperty("PersonId", contextProvider.CurrentPersonId.Value));
                }
            }
        }
    }
}
