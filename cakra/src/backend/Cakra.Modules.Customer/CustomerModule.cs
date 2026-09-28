using Cakra.Core;
using Cakra.Modules.Customer.Persistence;
using Cakra.Modules.Customer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Cakra.Modules.Customer;

/// <summary>
/// Module bootstrapper for the Customer module (Architecture §6, §7, §19.2).
/// Discovered and invoked by the host application at startup.
/// </summary>
public sealed class CustomerModule : IModule
{
    /// <inheritdoc />
    public string Name => "Customer";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Internal repositories (Architecture §19.3, §21 - Scoped per request)
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerContactRepository, CustomerContactRepository>();

        // Application Command & Query Services (Architecture §7, §15)
        services.AddScoped<CustomerService>(sp => new CustomerService(
            sp.GetRequiredService<ICustomerRepository>(),
            sp.GetRequiredService<ICustomerContactRepository>(),
            sp.GetService<IDomainEventDispatcher>(),
            sp.GetService<ISystemClock>()));
        services.AddScoped<ICustomerService>(sp => sp.GetRequiredService<CustomerService>());
        services.AddScoped<CustomerQueryService>();
        services.AddScoped<ICustomerQueryService>(sp => sp.GetRequiredService<CustomerQueryService>());
    }
}
