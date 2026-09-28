using System.Reflection;
using Cakra.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Cakra.Api.Extensions;

/// <summary>
/// Host-side discovery of module bootstrappers. The host owns the knowledge of
/// where module assemblies live; <see cref="ModuleLoader"/> owns the actual
/// scanning and registration (Architecture §19.2).
/// </summary>
public static class ModuleRegistrationExtensions
{
    private const string ModuleAssemblyPrefix = "Cakra.Modules.";

    /// <summary>
    /// Locates every <c>Cakra.Modules.*</c> assembly deployed alongside the host.
    /// Using the application base directory (rather than the entry assembly's
    /// reference list) guarantees modules are discovered even when the compiler
    /// drops unused assembly references.
    /// </summary>
    public static IReadOnlyList<Assembly> DiscoverModuleAssemblies()
    {
        return Directory
            .EnumerateFiles(AppContext.BaseDirectory, $"{ModuleAssemblyPrefix}*.dll", SearchOption.TopDirectoryOnly)
            .Select(AssemblyName.GetAssemblyName)
            .Select(Assembly.Load)
            .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Discovers and invokes every <see cref="IModule"/> implementation in the
    /// supplied module assemblies. When no assemblies are supplied the deployed
    /// <c>Cakra.Modules.*</c> assemblies are scanned.
    /// </summary>
    public static IServiceCollection AddCakraModules(
        this IServiceCollection services,
        IEnumerable<Assembly>? moduleAssemblies = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assemblies = moduleAssemblies?.ToList() ?? DiscoverModuleAssemblies();
        ModuleLoader.RegisterModules(services, assemblies);

        return services;
    }
}
