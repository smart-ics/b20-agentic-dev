using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ICS.Core.Modules;

/// <summary>
/// Extension methods for discovering and registering modular monolith modules into Microsoft.Extensions.DependencyInjection.
/// Per Architecture §19.2 and §20.
/// </summary>
public static class ModuleRegistrationExtensions
{
    /// <summary>
    /// Discovers all concrete implementations of <see cref="IModule"/> in the specified assemblies
    /// (or loaded application assemblies if none specified), registers their services, and registers
    /// the module instances as <see cref="IModule"/> in the DI container.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <param name="assemblies">Explicit assemblies to scan for <see cref="IModule"/> implementations.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddModules(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] assemblies)
    {
        var targetAssemblies = assemblies.Length > 0
            ? assemblies
            : AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && a.GetName().Name?.StartsWith("ICS") == true)
                .ToArray();

        var moduleTypes = targetAssemblies
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(t => t != null).Select(t => t!);
                }
            })
            .Where(t => t != null && typeof(IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Cast<Type>()
            .Distinct()
            .ToList();

        foreach (var moduleType in moduleTypes)
        {
            if (Activator.CreateInstance(moduleType) is IModule module)
            {
                module.RegisterServices(services, configuration);
                services.AddSingleton<IModule>(module);
            }
        }

        return services;
    }

    /// <summary>
    /// Explicitly registers a specific module type into the service collection.
    /// </summary>
    /// <typeparam name="TModule">The module implementation type.</typeparam>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddModule<TModule>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TModule : class, IModule, new()
    {
        var module = new TModule();
        module.RegisterServices(services, configuration);
        services.AddSingleton<IModule>(module);
        return services;
    }

    /// <summary>
    /// Resolves all registered <see cref="IModule"/> instances from the service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve from.</param>
    /// <returns>A read-only collection of registered modules.</returns>
    public static IReadOnlyList<IModule> GetRegisteredModules(this IServiceProvider serviceProvider)
    {
        return serviceProvider.GetServices<IModule>().ToList().AsReadOnly();
    }
}
