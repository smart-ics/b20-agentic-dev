using Cakra.Core;
using Cakra.Modules.Organization.Persistence;
using Cakra.Modules.Organization.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Cakra.Modules.Organization;

/// <summary>
/// Module bootstrapper for the Organization module (Architecture §6, §7, §19.2).
/// Discovered and invoked by the host application at startup.
/// </summary>
public sealed class OrganizationModule : IModule
{
    /// <inheritdoc />
    public string Name => "Organization";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Internal Repositories (Architecture §19.3, §20 - Scoped per request, not exposed across modules)
        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IResponsibilityRepository, ResponsibilityRepository>();
        services.AddScoped<ITeamMembershipRepository, TeamMembershipRepository>();
        services.AddScoped<IRoleAssignmentRepository, RoleAssignmentRepository>();
        services.AddScoped<IResponsibilityAssignmentRepository, ResponsibilityAssignmentRepository>();

        // Application Command Service (Architecture §7)
        services.AddScoped<OrganizationService>(sp => new OrganizationService(
            sp.GetRequiredService<IPersonRepository>(),
            sp.GetRequiredService<ITeamRepository>(),
            sp.GetRequiredService<IRoleRepository>(),
            sp.GetRequiredService<IResponsibilityRepository>(),
            sp.GetRequiredService<ITeamMembershipRepository>(),
            sp.GetRequiredService<IRoleAssignmentRepository>(),
            sp.GetRequiredService<IResponsibilityAssignmentRepository>(),
            sp.GetService<IDomainEventDispatcher>(),
            sp.GetService<ISystemClock>()));
        services.AddScoped<IOrganizationService>(sp => sp.GetRequiredService<OrganizationService>());

        // Published Cross-Module Query Service (Architecture §7, §14, §15, §20)
        services.AddScoped<OrganizationQueryService>();
        services.AddScoped<IOrganizationQueryService>(sp => sp.GetRequiredService<OrganizationQueryService>());
    }
}
