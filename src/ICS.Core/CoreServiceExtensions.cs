using System.Reflection;
using FluentValidation;
using ICS.Core.Audit;
using ICS.Core.Auth;
using ICS.Core.Domain;
using ICS.Core.Domain.Verification;
using ICS.Core.Time;
using ICS.Core.Validation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ICS.Core;

/// <summary>
/// Service collection extensions establishing core architectural services, lifetimes, MediatR, and FluentValidation.
/// Per Architecture §7, §14, §18, §19.2, and §20.
/// </summary>
public static class CoreServiceExtensions
{
    /// <summary>
    /// Registers core domain and infrastructure abstractions, MediatR with validation behavior,
    /// FluentValidation validators, and establishes baseline service lifetimes.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="assembliesToScan">Assemblies to scan for MediatR handlers and FluentValidation validators.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCoreServices(
        this IServiceCollection services,
        params Assembly[] assembliesToScan)
    {
        // 1. Service Lifetime Convention: Singleton for stateless infrastructure
        services.AddSingleton<ISystemClock, SystemClock>();

        // 2. Service Lifetime Convention: Scoped for ambient execution and identity context
        services.AddScoped<CurrentContextProvider>();
        services.AddScoped<ICurrentContextProvider>(sp => sp.GetRequiredService<CurrentContextProvider>());
        services.AddScoped<ICurrentContextAccessor>(sp => sp.GetRequiredService<CurrentContextProvider>());

        services.AddScoped<AuditContext>();
        services.AddScoped<IAuditContext>(sp => sp.GetRequiredService<AuditContext>());
        services.AddScoped<IAuditContextAccessor>(sp => sp.GetRequiredService<AuditContext>());

        // 3. Domain Event Bus: In-process synchronous event dispatcher with transactional consistency (Architecture §12, §18)
        services.AddScoped<DomainEventDispatcher>();
        services.AddScoped<IDomainEventDispatcher>(sp => sp.GetRequiredService<DomainEventDispatcher>());
        services.AddSingleton<IEventExecutionJournal, EventExecutionJournal>();

        // 3. Scan assemblies for MediatR handlers and FluentValidation validators
        var scanAssemblies = assembliesToScan.Length > 0
            ? assembliesToScan
            : [typeof(CoreServiceExtensions).Assembly];

        // 4. MediatR registration with ValidationBehavior pipeline behavior
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(scanAssemblies);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        // 5. FluentValidation auto-discovery across scanned assemblies
        services.AddValidatorsFromAssemblies(scanAssemblies, ServiceLifetime.Scoped, includeInternalTypes: true);

        return services;
    }
}
