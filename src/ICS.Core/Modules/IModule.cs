using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ICS.Core.Modules;

/// <summary>
/// Contract for module bootstrapper registration within the modular monolith.
/// Each module implements this interface to self-register its services, handlers, repositories, and validators.
/// </summary>
public interface IModule
{
    /// <summary>
    /// The unique name of the module (e.g. "Identity", "Organization", "Customer").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Registers module-specific services, repositories, handlers, and configuration into the DI container.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">The application configuration root.</param>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);
}
