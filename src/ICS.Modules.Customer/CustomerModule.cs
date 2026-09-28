namespace ICS.Modules.Customer;

using ICS.Core.Modules;
using ICS.Modules.Customer.Application;
using ICS.Modules.Customer.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Module registration bootstrapper for the Customer bounded context.
/// Configures Customer and CustomerContact services, handlers, and repositories.
/// Per Architecture §7, §16, §17, and §20.
/// </summary>
public sealed class CustomerModule : IModule
{
    /// <inheritdoc />
    public string Name => "Customer";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // 1. Published cross-module interfaces
        services.AddScoped<ICustomerQueryService, CustomerQueryService>();
        services.AddScoped<ICustomerService, CustomerService>();

        // 2. Module-internal Dapper repositories (not exposed across boundary)
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerContactRepository, CustomerContactRepository>();
    }
}
