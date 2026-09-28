using Cakra.Core;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Analytics.Services;
using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Cakra.Modules.Request;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cakra.Modules.Analytics;

/// <summary>
/// Module bootstrapper for the Management Analytics module (Architecture §6, §7, §13, §19.2, §19.7, §22).
/// Discovered and invoked by the host application at startup.
/// </summary>
public sealed class AnalyticsModule : IModule
{
    /// <inheritdoc />
    public string Name => "Analytics";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Management Analytics Application Service (Architecture §7, §13, §15)
        services.AddScoped<ManagementAnalyticsService>(sp => new ManagementAnalyticsService(
            sp.GetRequiredService<IDbConnectionFactory>(),
            sp.GetRequiredService<IRequestQueryService>(),
            sp.GetRequiredService<IOrganizationQueryService>(),
            sp.GetRequiredService<ICustomerQueryService>(),
            sp.GetService<ISystemClock>(),
            sp.GetService<ILogger<ManagementAnalyticsService>>()));
        services.AddScoped<IManagementAnalyticsService>(sp => sp.GetRequiredService<ManagementAnalyticsService>());

        // Scheduled Background Worker for Daily & Monthly Snapshots (Architecture §7, §13, §19.7)
        services.AddSingleton<AnalyticsSnapshotJob>(sp => new AnalyticsSnapshotJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetService<ISystemClock>(),
            sp.GetService<ILogger<AnalyticsSnapshotJob>>()));
        services.AddHostedService<AnalyticsSnapshotJob>(sp => sp.GetRequiredService<AnalyticsSnapshotJob>());
    }
}
