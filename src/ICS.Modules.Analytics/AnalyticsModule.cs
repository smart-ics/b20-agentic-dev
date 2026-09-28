using ICS.Core.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ICS.Modules.Analytics;

/// <summary>
/// Module registration bootstrapper for the Analytics bounded context.
/// Configures Management Analytics services, queries, and snapshot background jobs.
/// Per Architecture §7, §13, §16, and §17.
/// </summary>
public sealed class AnalyticsModule : IModule
{
    /// <inheritdoc />
    public string Name => "Analytics";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Module-specific services, repositories, and handlers will be registered here as vertical slices are implemented
    }
}
