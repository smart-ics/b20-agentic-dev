using Cakra.Core;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cakra.Tests.Unit.Events;

/// <summary>
/// P1-S05 — verifies the in-process domain event bus (Architecture §18, §19.2):
/// all registered handlers are invoked, a missing handler is a no-op, and a
/// handler exception propagates to the caller (so the enclosing command
/// transaction rolls back).
/// </summary>
public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_InvokesAllRegisteredHandlers()
    {
        using var provider = BuildProvider();
        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();
        var log = provider.GetRequiredService<HandlerCallLog>();

        await dispatcher.DispatchAsync(new TestDomainEvent());

        log.Calls.Should().BeEquivalentTo(
            new[] { nameof(FirstTestHandler), nameof(SecondTestHandler) },
            "every INotificationHandler registered for the event must be invoked");
    }

    [Fact]
    public async Task DispatchAsync_WithNoRegisteredHandler_DoesNotThrow()
    {
        using var provider = BuildProvider();
        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();

        var publish = async () => await dispatcher.DispatchAsync(new UnhandledDomainEvent());

        await publish.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DispatchAsync_WhenHandlerThrows_PropagatesException()
    {
        using var provider = BuildProvider();
        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();

        var publish = async () => await dispatcher.DispatchAsync(new ThrowingDomainEvent());

        await publish.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("handler failure");
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        // Handlers live in this test assembly; the module registration pattern
        // (P1-S04) performs equivalent scanning for production assemblies.
        services.AddSingleton<HandlerCallLog>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<DomainEventDispatcherTests>());
        services.AddDomainEventBus();

        return services.BuildServiceProvider();
    }
}

/// <summary>Records handler invocation order for assertions.</summary>
internal sealed class HandlerCallLog
{
    public List<string> Calls { get; } = new();
}

internal sealed class TestDomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}

internal sealed class UnhandledDomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}

internal sealed class ThrowingDomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}

internal sealed class FirstTestHandler : INotificationHandler<TestDomainEvent>
{
    private readonly HandlerCallLog _log;

    public FirstTestHandler(HandlerCallLog log) => _log = log;

    public Task Handle(TestDomainEvent notification, CancellationToken cancellationToken)
    {
        _log.Calls.Add(nameof(FirstTestHandler));
        return Task.CompletedTask;
    }
}

internal sealed class SecondTestHandler : INotificationHandler<TestDomainEvent>
{
    private readonly HandlerCallLog _log;

    public SecondTestHandler(HandlerCallLog log) => _log = log;

    public Task Handle(TestDomainEvent notification, CancellationToken cancellationToken)
    {
        _log.Calls.Add(nameof(SecondTestHandler));
        return Task.CompletedTask;
    }
}

internal sealed class ThrowingTestHandler : INotificationHandler<ThrowingDomainEvent>
{
    public Task Handle(ThrowingDomainEvent notification, CancellationToken cancellationToken)
        => throw new InvalidOperationException("handler failure");
}
