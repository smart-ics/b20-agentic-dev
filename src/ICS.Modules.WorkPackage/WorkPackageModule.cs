using ICS.Core.Modules;
using ICS.Modules.WorkPackage.Application;
using ICS.Modules.WorkPackage.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ICS.Modules.WorkPackage;

/// <summary>
/// Module registration bootstrapper for the Work Package bounded context.
/// Configures Work Package application services, queries, handlers, and repositories.
/// Per Architecture §11, §16, and §17.
/// </summary>
public sealed class WorkPackageModule : IModule
{
    /// <inheritdoc />
    public string Name => "WorkPackage";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // 1. Published cross-module interfaces
        services.AddScoped<IWorkPackageService, WorkPackageService>();
        services.AddScoped<IWorkPackageQueryService, WorkPackageQueryService>();

        // 2. Cross-module reference validation (Organization, Customer, Product, Request)
        services.AddScoped<WorkPackageReferenceValidator>();

        // 3. Module-internal Dapper repository (not exposed across the module boundary)
        services.AddScoped<IWorkPackageRepository, WorkPackageRepository>();

        // 4. MediatR command handlers and FluentValidation validators are discovered
        //    automatically by the MediatR and FluentValidation registrations performed
        //    during host composition (see Program.cs / CoreServiceExtensions).
    }
}
