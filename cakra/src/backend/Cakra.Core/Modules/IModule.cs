using Microsoft.Extensions.DependencyInjection;

namespace Cakra.Core;

/// <summary>
/// Module bootstrapper contract. Every <c>Cakra.Modules.*</c> project implements
/// this interface to self-register its services, repositories, MediatR handlers,
/// and FluentValidation validators. The host discovers and invokes all
/// implementations at startup (Architecture 19.2).
/// </summary>
public interface IModule
{
    /// <summary>Unique module name used for diagnostics and registration ordering.</summary>
    string Name { get; }

    /// <summary>Registers the module's services with the application DI container.</summary>
    void RegisterServices(IServiceCollection services);
}
