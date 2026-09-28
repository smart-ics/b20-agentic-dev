using System.Diagnostics;
using ICS.Core.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace ICS.Web.Logging;

/// <summary>
/// Configures Serilog structured JSON logging per Architecture §19.9.
/// Formats all log entries with structured JSON and enriches events with:
/// - TraceId: Distributed tracing identifier (System.Diagnostics.Activity or HttpContext.TraceIdentifier)
/// - SpanId: Active span identifier within the trace
/// - UserId: Ambient authenticated user identifier
/// - PersonId: Ambient organizational person identifier
/// - SourceContext: Originating class or logger category
/// Configures stdout console sink and rolling file sinks.
/// </summary>
public static class SerilogConfiguration
{
    public const string DefaultLogDirectory = "logs";
    public const string DefaultLogFilePattern = "logs/ics-.json";

    /// <summary>
    /// Configures the bootstrap logger used during host initialization.
    /// </summary>
    public static Serilog.ILogger CreateBootstrapLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(new JsonFormatter(renderMessage: true))
            .CreateBootstrapLogger();
    }

    /// <summary>
    /// Configures Serilog on the web application builder.
    /// </summary>
    public static void ConfigureSerilog(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<AmbientContextLogEnricher>();

        builder.Host.UseSerilog((context, services, configuration) =>
        {
            var logPath = context.Configuration["Serilog:RollingFilePath"] ?? DefaultLogFilePattern;

            configuration
                .ReadFrom.Configuration(context.Configuration)
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.With(services.GetRequiredService<AmbientContextLogEnricher>())
                .WriteTo.Console(new JsonFormatter(renderMessage: true))
                .WriteTo.File(
                    formatter: new JsonFormatter(renderMessage: true),
                    path: logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    shared: true);
        }, preserveStaticLogger: true);
    }

    /// <summary>
    /// Enriches Serilog request completion diagnostic context with ambient security and tracing fields.
    /// </summary>
    public static void EnrichDiagnosticContext(IDiagnosticContext diagnosticContext, HttpContext httpContext)
    {
        var activity = Activity.Current;
        var traceId = activity?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        var spanId = activity?.SpanId.ToString() ?? string.Empty;

        diagnosticContext.Set("TraceId", traceId);
        diagnosticContext.Set("SpanId", spanId);

        var currentContext = httpContext.RequestServices.GetService<ICurrentContextProvider>();
        if (currentContext != null)
        {
            if (currentContext.CurrentUserId.HasValue)
            {
                diagnosticContext.Set("UserId", currentContext.CurrentUserId.Value.ToString());
            }
            if (currentContext.CurrentPersonId.HasValue)
            {
                diagnosticContext.Set("PersonId", currentContext.CurrentPersonId.Value.ToString());
            }
        }
    }
}
