using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cakra.Core;

/// <summary>
/// Self-contained DI registration for the in-process domain event bus
/// (Architecture §18, §19.2). This helper exists so the event bus can be wired
/// independently of any module bootstrapper; the module registration pattern
/// (P1-S04) may call <see cref="AddDomainEventBus"/> from an <c>IModule</c>.
/// </summary>
public static class DomainEventBusServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="IDomainEventDispatcher"/> implementation. MediatR
    /// is registered as a fallback when the host has not already done so, using
    /// this assembly so <c>INotificationHandler&lt;T&gt;</c> implementations in
    /// <c>Cakra.Core</c> are discovered. Module assemblies register their own
    /// handlers through the module registration pattern.
    /// </summary>
    public static IServiceCollection AddDomainEventBus(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Scoped lifetime: the dispatcher (and its publisher/handlers) share the
        // originating command's scope, preserving transactional consistency.
        services.TryAddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IMediator)))
        {
            services.AddMediatR(configuration =>
                configuration.RegisterServicesFromAssembly(typeof(DomainEventDispatcher).Assembly));
        }

        return services;
    }
}
