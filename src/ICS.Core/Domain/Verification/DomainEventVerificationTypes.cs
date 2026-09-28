using System.Collections.Concurrent;
using MediatR;

namespace ICS.Core.Domain.Verification;

/// <summary>
/// Journal tracking execution of domain event handlers during verification and runtime testing.
/// </summary>
public interface IEventExecutionJournal
{
    void RecordInvocation(string handlerName, Guid eventId);
    IReadOnlyList<string> GetInvocations(Guid eventId);
    void Clear();
}

/// <summary>
/// In-memory thread-safe implementation of <see cref="IEventExecutionJournal"/>.
/// </summary>
public sealed class EventExecutionJournal : IEventExecutionJournal
{
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<string>> _invocations = new();

    public void RecordInvocation(string handlerName, Guid eventId)
    {
        var bag = _invocations.GetOrAdd(eventId, _ => new ConcurrentBag<string>());
        bag.Add(handlerName);
    }

    public IReadOnlyList<string> GetInvocations(Guid eventId)
    {
        return _invocations.TryGetValue(eventId, out var bag)
            ? bag.ToList()
            : [];
    }

    public void Clear()
    {
        _invocations.Clear();
    }
}

/// <summary>
/// Verification domain event having multiple registered notification handlers.
/// </summary>
public sealed record SampleMultiHandledDomainEvent(string Payload) : DomainEvent;

/// <summary>
/// Verification domain event having zero registered handlers.
/// </summary>
public sealed record SampleUnhandledDomainEvent(string Reason) : DomainEvent;

/// <summary>
/// Verification domain event designed to test failure propagation.
/// </summary>
public sealed record SampleFaultyDomainEvent(string FailureMessage) : DomainEvent;

/// <summary>
/// First handler for <see cref="SampleMultiHandledDomainEvent"/>.
/// </summary>
public sealed class SampleMultiHandlerOne : INotificationHandler<SampleMultiHandledDomainEvent>
{
    private readonly IEventExecutionJournal _journal;

    public SampleMultiHandlerOne(IEventExecutionJournal journal)
    {
        _journal = journal;
    }

    public Task Handle(SampleMultiHandledDomainEvent notification, CancellationToken cancellationToken)
    {
        _journal.RecordInvocation(nameof(SampleMultiHandlerOne), notification.EventId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Second handler for <see cref="SampleMultiHandledDomainEvent"/>.
/// </summary>
public sealed class SampleMultiHandlerTwo : INotificationHandler<SampleMultiHandledDomainEvent>
{
    private readonly IEventExecutionJournal _journal;

    public SampleMultiHandlerTwo(IEventExecutionJournal journal)
    {
        _journal = journal;
    }

    public Task Handle(SampleMultiHandledDomainEvent notification, CancellationToken cancellationToken)
    {
        _journal.RecordInvocation(nameof(SampleMultiHandlerTwo), notification.EventId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Handler for <see cref="SampleFaultyDomainEvent"/> that simulates a processing failure.
/// </summary>
public sealed class SampleFaultyEventHandler : INotificationHandler<SampleFaultyDomainEvent>
{
    public const string ExpectedExceptionMessage = "Simulated domain event handler transactional failure.";

    public Task Handle(SampleFaultyDomainEvent notification, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(notification.FailureMessage ?? ExpectedExceptionMessage);
    }
}

/// <summary>
/// Concrete entity for verifying entity domain event dispatching and clearing.
/// </summary>
public sealed class SampleOrderEntity : Entity<Guid>
{
    public SampleOrderEntity()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }

    public void Complete(string payload)
    {
        AddDomainEvent(new SampleMultiHandledDomainEvent(payload));
    }
}
