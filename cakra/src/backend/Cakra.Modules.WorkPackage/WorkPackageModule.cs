using Cakra.Core;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Cakra.Modules.Product;
using Cakra.Modules.Request;
using Cakra.Modules.WorkPackage.Domain;
using Cakra.Modules.WorkPackage.Persistence;
using Cakra.Modules.WorkPackage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cakra.Modules.WorkPackage;

/// <summary>
/// Module bootstrapper for the Work Package module (Architecture §6, §7, §8, §11, §19.2).
/// Discovered and invoked by the host application at startup.
/// </summary>
public sealed class WorkPackageModule : IModule
{
    /// <inheritdoc />
    public string Name => "WorkPackage";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Internal repositories (Architecture §19.3, §21 - Scoped per request, not exposed outside module)
        services.AddScoped<IWorkPackageRepository, WorkPackageRepository>();
        services.AddScoped<IActiveWorkPackageChecker>(sp => sp.GetRequiredService<IWorkPackageRepository>());

        // Application Command Service (Architecture §7, §11, §15, §18)
        services.AddScoped<WorkPackageService>(sp => new WorkPackageService(
            sp.GetRequiredService<IWorkPackageRepository>(),
            sp.GetRequiredService<IOrganizationQueryService>(),
            sp.GetRequiredService<ICustomerQueryService>(),
            sp.GetRequiredService<IProductQueryService>(),
            sp.GetRequiredService<IRequestQueryService>(),
            sp.GetService<IDomainEventDispatcher>(),
            sp.GetService<ICurrentContextProvider>(),
            sp.GetService<IAuditContext>(),
            sp.GetService<ISystemClock>(),
            sp.GetService<ILogger<WorkPackageService>>()));
        services.AddScoped<IWorkPackageService>(sp => sp.GetRequiredService<WorkPackageService>());

        // Published Cross-Module Query Service (Architecture §7, §11, §15, §20, §21)
        services.AddScoped<WorkPackageQueryService>(sp => new WorkPackageQueryService(
            sp.GetRequiredService<IDbConnectionFactory>(),
            sp.GetService<IOrganizationQueryService>(),
            sp.GetService<ICustomerQueryService>(),
            sp.GetService<IProductQueryService>(),
            sp.GetService<IRequestQueryService>()));
        services.AddScoped<IWorkPackageQueryService>(sp => sp.GetRequiredService<WorkPackageQueryService>());
    }
}
