using Cakra.Core;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Cakra.Modules.Product;
using Cakra.Modules.Request.Persistence;
using Cakra.Modules.Request.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Cakra.Modules.Request;

/// <summary>
/// Module bootstrapper for the Request module (Architecture §6, §7, §8, §19.2).
/// Discovered and invoked by the host application at startup.
/// </summary>
public sealed class RequestModule : IModule
{
    /// <inheritdoc />
    public string Name => "Request";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Internal repositories (Architecture §19.3, §21 - Scoped per request, not exposed outside module)
        services.AddScoped<IRequestRepository, RequestRepository>();

        // Application Command Service (Architecture §7, §8, §15, §18)
        services.AddScoped<RequestService>(sp => new RequestService(
            sp.GetRequiredService<IRequestRepository>(),
            sp.GetRequiredService<IOrganizationQueryService>(),
            sp.GetRequiredService<ICustomerQueryService>(),
            sp.GetRequiredService<IProductQueryService>(),
            sp.GetService<IDomainEventDispatcher>(),
            sp.GetService<ICurrentContextProvider>(),
            sp.GetService<IAuditContext>(),
            sp.GetService<ISystemClock>()));
        services.AddScoped<IRequestService>(sp => sp.GetRequiredService<RequestService>());

        // Published Cross-Module Query Service (Architecture §7, §8, §15, §20, §21)
        services.AddScoped<RequestQueryService>(sp => new RequestQueryService(
            sp.GetRequiredService<IDbConnectionFactory>(),
            sp.GetService<ICurrentContextProvider>(),
            sp.GetService<IOrganizationQueryService>(),
            sp.GetService<ICustomerQueryService>(),
            sp.GetService<IProductQueryService>(),
            sp.GetService<IRequestRepository>(),
            sp.GetService<ISystemClock>()));
        services.AddScoped<IRequestQueryService>(sp => sp.GetRequiredService<RequestQueryService>());
    }
}
