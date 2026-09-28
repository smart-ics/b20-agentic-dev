using System.Reflection;
using Cakra.Api.Infrastructure.Context;
using Cakra.Core;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Cakra.Api.Extensions;

/// <summary>
/// Registers the shared CAKRA core services and the MediatR/FluentValidation
/// application pipeline (Architecture §19.2). This is the single place where the
/// service lifetime conventions are applied — see
/// <c>docs/architecture/dependency-injection-conventions.md</c>.
/// </summary>
public static class CoreServicesExtensions
{
    /// <summary>
    /// Registers core cross-cutting services plus MediatR and FluentValidation,
    /// scanning the host, <c>Cakra.Core</c>, and every supplied module assembly so
    /// handlers and validators are auto-discovered per module.
    /// </summary>
    public static IServiceCollection AddCakraCore(
        this IServiceCollection services,
        IEnumerable<Assembly>? moduleAssemblies = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Service lifetime conventions:
        //   Singleton — stateless, thread-safe infrastructure (system clock).
        //   Scoped    — per-request ambient state (security/audit context).
        //   Transient — stateless per-resolution services, incl. MediatR handlers.
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddScoped<ICurrentContextProvider, CurrentContextProvider>();
        services.AddScoped<IAuditContext, AuditContext>();

        var assemblies = (moduleAssemblies ?? Enumerable.Empty<Assembly>())
            .Append(typeof(IModule).Assembly)                    // Cakra.Core
            .Append(typeof(CoreServicesExtensions).Assembly)     // Cakra.Api (host)
            .Distinct()
            .ToArray();

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssemblies(assemblies);
            // Architecture §19.2: validation runs before every handler.
            configuration.AddOpenBehavior(typeof(ValidationBehaviour<,>));
        });

        // P1-S05: in-process domain event bus. Registered after MediatR so the
        // handler-discovery registration above is reused (no duplicate MediatR).
        services.AddDomainEventBus();

        // Auto-discover FluentValidation validators across the host, Core, and
        // each module assembly so modules do not have to register them manually.
        services.AddValidatorsFromAssemblies(assemblies);

        return services;
    }
}
