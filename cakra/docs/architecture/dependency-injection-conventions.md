# Dependency Injection & Module Registration Conventions

Authoritative source: Architecture §19.2 (Application Architecture) and §18
(Cross-Cutting Concerns). This document records how those decisions are applied
in the CAKRA composition root (P1-S04).

## Composition root

`Cakra.Api/Program.cs` is the single composition root. At startup it:

1. Discovers every deployed `Cakra.Modules.*` assembly
   (`ModuleRegistrationExtensions.DiscoverModuleAssemblies`).
2. Calls `builder.Services.AddCakraCore(moduleAssemblies)` to register the shared
   core services and the MediatR + FluentValidation pipeline.
3. Calls `builder.Services.AddCakraModules(moduleAssemblies)` to discover all
   `IModule` implementations and invoke `RegisterServices` on each.

`Cakra.Core.ModuleLoader` performs assembly scanning so the mechanism stays free
of host/deployment concerns.

## Module registration pattern

Every module project contains a single `IModule` implementation (for example
`IdentityModule`) that self-registers its services, repositories, MediatR
handlers, and FluentValidation validators.

```csharp
public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<StubRegistrationProbe>();
        // Feature vertical slices register their services/repositories here.
    }
}
```

Modules are registered in ascending `Name` order for determinism. MediatR
handlers (`IRequestHandler<,>`, `INotificationHandler<>`) and FluentValidation
validators (`AbstractValidator<T>`) do **not** need to be registered manually:
`AddCakraCore` scans the host, `Cakra.Core`, and every module assembly, so each
module's handlers and validators are auto-discovered.

## MediatR & validation pipeline

- MediatR is registered once in `AddCakraCore` with
  `RegisterServicesFromAssemblies` over the host, Core, and module assemblies.
- `ValidationBehaviour<,>` is registered as an open behavior, so every MediatR
  request executes all registered `IValidator<TRequest>` implementations before
  its handler runs (Architecture §19.2). A failed validation throws
  `ValidationException` and the handler is never invoked.

## Service lifetime conventions

| Lifetime | Use for | Examples |
|---|---|---|
| **Singleton** | Stateless, thread-safe infrastructure shared by the whole process | `ISystemClock`, `IDbConnectionFactory`, `IDomainEventDispatcher`'s dependencies, module probes |
| **Scoped** | State that is specific to one HTTP request / MediatR request scope | `ICurrentContextProvider`, `IAuditContext`, `IDomainEventDispatcher`, FluentValidation validators (MediatR handlers are transient by default) |
| **Transient** | Lightweight, stateless services that must not be shared | MediatR handlers and pipeline behaviors |

Rules:

- Never inject a Scoped or Transient service into a Singleton.
- Ambient request state (`ICurrentContextProvider`, `IAuditContext`) is Scoped so
  a single instance flows through the request's MediatR handlers and data access.
- The domain event dispatcher is Scoped so handlers execute within the
  originating command's transaction scope (Architecture §18).

## Core services resolved at startup

`AddCakraCore` registers default implementations for the foundation contracts
defined in `Cakra.Core`:

| Interface | Implementation | Lifetime |
|---|---|---|
| `ISystemClock` | `Cakra.Api.Infrastructure.Context.SystemClock` | Singleton |
| `ICurrentContextProvider` | `Cakra.Api.Infrastructure.Context.CurrentContextProvider` | Scoped |
| `IAuditContext` | `Cakra.Api.Infrastructure.Context.AuditContext` | Scoped |

`CurrentContextProvider` exposes `Initialize(...)`; the security-context
population middleware (P1-S06 placeholder, concrete wiring in P2-S10) calls it
once per request.
