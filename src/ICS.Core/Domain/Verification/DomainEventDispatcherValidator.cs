using ICS.Core.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ICS.Core.Domain.Verification;

/// <summary>
/// Diagnostic validator executing runtime assertions on <see cref="DomainEventDispatcher"/>,
/// verifying handler discovery, multi-handler dispatch, exception propagation, and entity event clearing.
/// Per Architecture §12, §18, and §19.2.
/// </summary>
public static class DomainEventDispatcherValidator
{
    /// <summary>
    /// Executes all behavioral assertions against the domain event bus.
    /// Throws <see cref="InvalidOperationException"/> if any architectural assertion fails.
    /// </summary>
    /// <param name="rootProvider">The root service provider.</param>
    /// <param name="logger">Optional logger for operational logging.</param>
    public static void Verify(IServiceProvider rootProvider, ILogger? logger = null)
    {
        using var scope = rootProvider.CreateScope();
        var sp = scope.ServiceProvider;

        logger?.LogInformation("Beginning Domain Event Bus verification...");

        // 1. Verify DI Resolution: IDomainEventDispatcher resolves to DomainEventDispatcher
        var dispatcher = sp.GetService<IDomainEventDispatcher>();
        if (dispatcher is null)
        {
            throw new InvalidOperationException("IDomainEventDispatcher is not registered in the DI container.");
        }

        if (dispatcher is not DomainEventDispatcher)
        {
            throw new InvalidOperationException(
                $"IDomainEventDispatcher resolved to unexpected type '{dispatcher.GetType().FullName}'. Expected DomainEventDispatcher.");
        }

        var journal = sp.GetRequiredService<IEventExecutionJournal>();
        journal.Clear();

        // 2. Verify Multiple Handlers: All registered handlers for the same event are invoked
        var multiEvent = new SampleMultiHandledDomainEvent("Multi-Handler-Test");
        dispatcher.PublishAsync(multiEvent).GetAwaiter().GetResult();

        var invocations = journal.GetInvocations(multiEvent.EventId);
        if (!invocations.Contains(nameof(SampleMultiHandlerOne)) || !invocations.Contains(nameof(SampleMultiHandlerTwo)))
        {
            throw new InvalidOperationException(
                $"Domain event dispatch failed to invoke all registered handlers. Expected: [SampleMultiHandlerOne, SampleMultiHandlerTwo]. Actual: [{string.Join(", ", invocations)}]");
        }

        // 3. Verify Missing Handler: Unhandled event does NOT throw
        var unhandledEvent = new SampleUnhandledDomainEvent("No-Handler-Test");
        try
        {
            dispatcher.PublishAsync(unhandledEvent).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Publishing an unhandled domain event unexpectedly threw an exception: {ex.Message}", ex);
        }

        // 4. Verify Exception Propagation: Handler exception propagates for transactional consistency
        var faultyEvent = new SampleFaultyDomainEvent("Simulated transactional failure for verification.");
        bool exceptionPropagated = false;
        try
        {
            dispatcher.PublishAsync(faultyEvent).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Simulated transactional failure"))
        {
            exceptionPropagated = true;
        }

        if (!exceptionPropagated)
        {
            throw new InvalidOperationException(
                "Handler exception failed to propagate from DomainEventDispatcher. Transactional consistency guarantee violated.");
        }

        // 5. Verify Entity Event Dispatch and Clearing
        var entity = new SampleOrderEntity();
        entity.Complete("Entity-Order-Completed");

        if (entity.DomainEvents.Count != 1)
        {
            throw new InvalidOperationException("SampleOrderEntity failed to stage domain event.");
        }

        var entityEventId = entity.DomainEvents.First().EventId;
        dispatcher.DispatchAndClearEventsAsync(entity).GetAwaiter().GetResult();

        if (entity.DomainEvents.Count != 0)
        {
            throw new InvalidOperationException("DispatchAndClearEventsAsync failed to clear domain events on entity.");
        }

        var entityInvocations = journal.GetInvocations(entityEventId);
        if (!entityInvocations.Contains(nameof(SampleMultiHandlerOne)) || !entityInvocations.Contains(nameof(SampleMultiHandlerTwo)))
        {
            throw new InvalidOperationException("Entity domain event dispatch failed to invoke registered handlers.");
        }

        // 6. Verify Argument Validation
        try
        {
            dispatcher.PublishAsync((IDomainEvent)null!).GetAwaiter().GetResult();
            throw new InvalidOperationException("PublishAsync(null) should have thrown ArgumentNullException.");
        }
        catch (ArgumentNullException)
        {
            // Expected
        }

        logger?.LogInformation(
            "Domain Event Bus verification passed cleanly. All assertions (multi-handler dispatch, unhandled tolerance, exception propagation, entity event clearing) succeeded.");
    }
}
