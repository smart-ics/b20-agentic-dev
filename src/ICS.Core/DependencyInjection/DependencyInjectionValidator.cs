using FluentValidation;
using ICS.Core.Audit;
using ICS.Core.Auth;
using ICS.Core.Data;
using ICS.Core.Modules;
using ICS.Core.Time;
using ICS.Core.Validation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ICS.Core;

/// <summary>
/// Diagnostic validator for runtime DI container configuration and MediatR/FluentValidation pipeline verification.
/// </summary>
public static class DependencyInjectionValidator
{
    /// <summary>
    /// Verifies that all core domain and infrastructure abstractions, modules, MediatR handlers,
    /// and validation pipeline behaviors are correctly registered and resolvable in the DI container.
    /// </summary>
    /// <param name="rootProvider">The root service provider.</param>
    /// <param name="logger">Optional logger for operational logging.</param>
    public static void VerifyContainer(IServiceProvider rootProvider, ILogger? logger = null)
    {
        using var scope = rootProvider.CreateScope();
        var sp = scope.ServiceProvider;

        // 1. Verify Core Singletons
        var clock = sp.GetRequiredService<ISystemClock>();
        if (clock.UtcNow == default)
        {
            throw new InvalidOperationException("ISystemClock failed to return a valid UtcNow.");
        }

        // 2. Verify Scoped Ambient Contexts and Accessors
        var authContext = sp.GetRequiredService<ICurrentContextProvider>();
        var authMutator = sp.GetRequiredService<ICurrentContextAccessor>();
        var auditContext = sp.GetRequiredService<IAuditContext>();
        var auditMutator = sp.GetRequiredService<IAuditContextAccessor>();

        // 3. Verify Database Infrastructure
        var dbConnectionFactory = sp.GetRequiredService<IDbConnectionFactory>();

        // 4. Verify MediatR Dispatcher
        var mediator = sp.GetRequiredService<IMediator>();

        // 5. Verify IModule Registrations
        var modules = sp.GetServices<IModule>().ToList();
        if (modules.Count == 0)
        {
            throw new InvalidOperationException("No IModule instances were registered in the DI container.");
        }

        // 6. Verify FluentValidation Validator Auto-Discovery
        var validator = sp.GetService<IValidator<PingCommand>>();
        if (validator == null)
        {
            throw new InvalidOperationException("IValidator<PingCommand> was not auto-discovered in the DI container.");
        }

        // 7. Verify Pipeline Interception: Invalid Request MUST throw ValidationException
        bool validationCaught = false;
        try
        {
            mediator.Send(new PingCommand("")).GetAwaiter().GetResult();
        }
        catch (ValidationException ex)
        {
            validationCaught = true;
            logger?.LogInformation("ValidationBehavior intercepted invalid PingCommand as expected: {ErrorCount} errors.", ex.Errors.Count());
        }

        if (!validationCaught)
        {
            throw new InvalidOperationException("ValidationBehavior failed to intercept invalid PingCommand.");
        }

        // 8. Verify Pipeline Execution: Valid Request MUST return valid PingResult
        var pingResult = mediator.Send(new PingCommand("DI-Verification-Echo")).GetAwaiter().GetResult();
        if (pingResult.Echo != "DI-Verification-Echo")
        {
            throw new InvalidOperationException($"PingCommandHandler returned unexpected echo: {pingResult.Echo}");
        }

        // 9. Verify Domain Event Dispatcher & Bus (Architecture §12, §18, §19.2)
        ICS.Core.Domain.Verification.DomainEventDispatcherValidator.Verify(rootProvider, logger);

        logger?.LogInformation(
            "DI Container, Domain Event Bus, and MediatR/FluentValidation pipeline verified cleanly. Registered {Count} modules: {Modules}",
            modules.Count,
            string.Join(", ", modules.Select(m => m.Name)));
    }
}
