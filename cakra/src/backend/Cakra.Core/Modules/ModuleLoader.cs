using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Cakra.Core;

/// <summary>
/// Discovers and invokes <see cref="IModule"/> bootstrappers so each module can
/// self-register its services, repositories, MediatR handlers, and
/// FluentValidation validators (Architecture §19.2). The host supplies the
/// assemblies to scan; this type has no knowledge of where those assemblies
/// live so it stays free of host/deployment concerns.
/// </summary>
public static class ModuleLoader
{
    /// <summary>
    /// Instantiates every concrete, parameterless <see cref="IModule"/>
    /// implementation found in <paramref name="assemblies"/>, ordered by
    /// <see cref="IModule.Name"/> for deterministic registration.
    /// </summary>
    public static IReadOnlyList<IModule> DiscoverModules(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return assemblies
            .SelectMany(GetLoadableTypes)
            .Where(type => typeof(IModule).IsAssignableFrom(type) && type is { IsAbstract: false, IsInterface: false })
            .Select(type => (IModule)Activator.CreateInstance(type)!)
            .OrderBy(module => module.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Discovers all <see cref="IModule"/> implementations and invokes
    /// <see cref="IModule.RegisterServices"/> on each, returning the modules that
    /// were registered.
    /// </summary>
    public static IReadOnlyList<IModule> RegisterModules(IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        var modules = DiscoverModules(assemblies);
        foreach (var module in modules)
        {
            module.RegisterServices(services);
        }

        return modules;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null)!;
        }
    }
}
