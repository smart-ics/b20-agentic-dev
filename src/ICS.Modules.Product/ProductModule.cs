namespace ICS.Modules.Product;

using ICS.Core.Modules;
using ICS.Modules.Product.Application;
using ICS.Modules.Product.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Module registration bootstrapper for the Product bounded context.
/// Configures Product services, catalog queries, handlers, and repositories.
/// Per Architecture §10, §16, §17, and §20.
/// </summary>
public sealed class ProductModule : IModule
{
    /// <inheritdoc />
    public string Name => "Product";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // 1. Published cross-module interfaces
        services.AddScoped<IProductQueryService, ProductQueryService>();
        services.AddScoped<IProductService, ProductService>();

        // 2. Module-internal Dapper repository (not exposed across boundary)
        services.AddScoped<IProductRepository, ProductRepository>();
    }
}
