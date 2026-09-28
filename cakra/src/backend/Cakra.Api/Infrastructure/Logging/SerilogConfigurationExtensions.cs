using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Formatting.Json;

namespace Cakra.Api.Infrastructure.Logging;

/// <summary>
/// Configures Serilog structured JSON logging with console (stdout) and rolling file sinks,
/// enriched with TraceId, SpanId, UserId, PersonId, and SourceContext (Architecture §19.9).
/// </summary>
public static class SerilogConfigurationExtensions
{
    /// <summary>
    /// Configures the host with Serilog structured logging and required enrichers and sinks.
    /// </summary>
    public static WebApplicationBuilder ConfigureCakraSerilog(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHttpContextAccessor();

        builder.Host.UseSerilog((context, services, configuration) =>
        {
            var httpContextAccessor = services.GetService<IHttpContextAccessor>();

            configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "Cakra")
                .Enrich.With(new CakraLogEnricher(httpContextAccessor))
                .WriteTo.Console(new JsonFormatter())
                .WriteTo.File(
                    new JsonFormatter(),
                    path: context.Configuration["Logging:File:Path"] ?? "logs/cakra-.json",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    shared: true);

            foreach (var sink in services.GetServices<ILogEventSink>())
            {
                configuration.WriteTo.Sink(sink);
            }
        });

        return builder;
    }
}
