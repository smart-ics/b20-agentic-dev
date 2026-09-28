namespace ICS.Core;

/// <summary>
/// Architectural conventions for service lifetimes in the ICS Modular Monolith.
/// Reference: Architecture §18, §19.2, and §20.
/// </summary>
public static class ServiceLifetimeConventions
{
    /// <summary>
    /// Singleton Lifetime:
    /// Applied to stateless, thread-safe infrastructure components and registry descriptors.
    /// Examples:
    /// - <see cref="ICS.Core.Time.ISystemClock"/>
    /// - <see cref="ICS.Core.Data.IDbConnectionFactory"/>
    /// - <see cref="ICS.Core.Modules.IModule"/> registrations
    /// </summary>
    public const string Singleton = "Singleton";

    /// <summary>
    /// Scoped Lifetime:
    /// Applied to per-request/per-execution-unit state, ambient contexts, domain/application services,
    /// repositories, and FluentValidation validators.
    /// Examples:
    /// - <see cref="ICS.Core.Auth.ICurrentContextProvider"/>
    /// - <see cref="ICS.Core.Audit.IAuditContext"/>
    /// - <see cref="ICS.Core.Domain.IDomainEventDispatcher"/>
    /// - FluentValidation <see cref="FluentValidation.IValidator{T}"/>
    /// - Domain repositories implementing <see cref="ICS.Core.Domain.IRepository{T}"/>
    /// - Domain and Application services (e.g. ProductService, RequestService)
    /// </summary>
    public const string Scoped = "Scoped";

    /// <summary>
    /// Transient Lifetime:
    /// Applied to lightweight, non-shared operations and message handlers.
    /// Examples:
    /// - MediatR <see cref="MediatR.IRequestHandler{TRequest, TResponse}"/>
    /// - MediatR <see cref="MediatR.INotificationHandler{TNotification}"/>
    /// - MediatR pipeline behaviors (<see cref="ICS.Core.Validation.ValidationBehavior{TRequest, TResponse}"/>)
    /// </summary>
    public const string Transient = "Transient";
}
