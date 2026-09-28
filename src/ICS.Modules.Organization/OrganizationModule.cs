using ICS.Core.Modules;
using ICS.Modules.Organization.Application;
using ICS.Modules.Organization.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ICS.Modules.Organization;

/// <summary>
/// Module registration bootstrapper for the Organization bounded context.
/// Configures Person, Team, Role, and Responsibility services, handlers, and repositories.
/// Per Architecture §7, §16, §17, and §20.
/// </summary>
public sealed class OrganizationModule : IModule
{
    /// <inheritdoc />
    public string Name => "Organization";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // 1. Published cross-module interfaces
        services.AddScoped<IOrganizationQueryService, OrganizationQueryService>();
        services.AddScoped<IOrganizationService, OrganizationService>();

        // 2. Module-internal Dapper repositories (not exposed across boundary)
        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IResponsibilityRepository, ResponsibilityRepository>();
    }
}
