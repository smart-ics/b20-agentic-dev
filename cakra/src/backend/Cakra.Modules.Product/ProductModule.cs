using Cakra.Core;
using Cakra.Modules.Organization;
using Cakra.Modules.Product.Persistence;
using Cakra.Modules.Product.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Cakra.Modules.Product;

/// <summary>
/// Module bootstrapper for the Product module (Architecture §6, §7, §10, §19.2).
/// Discovered and invoked by the host application at startup.
/// </summary>
public sealed class ProductModule : IModule
{
    /// <inheritdoc />
    public string Name => "Product";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Internal repositories (Architecture §19.3, §21 - Scoped per request, not exposed outside module)
        services.AddScoped<IProductRepository, ProductRepository>();

        // Application Command & Query Services (Architecture §7, §10, §15)
        services.AddScoped<ProductService>(sp => new ProductService(
            sp.GetRequiredService<IProductRepository>(),
            sp.GetRequiredService<IOrganizationQueryService>(),
            sp.GetService<IDomainEventDispatcher>(),
            sp.GetService<ISystemClock>()));
        services.AddScoped<IProductService>(sp => sp.GetRequiredService<ProductService>());

        services.AddScoped<ProductQueryService>();
        services.AddScoped<IProductQueryService>(sp => sp.GetRequiredService<ProductQueryService>());
    }
}
