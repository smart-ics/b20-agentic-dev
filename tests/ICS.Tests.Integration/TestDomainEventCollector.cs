namespace ICS.Tests.Integration;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ICS.Core.Domain;
using ICS.Modules.Customer.Domain.Events;
using ICS.Modules.Organization.Domain.Events;
using ICS.Modules.Product.Domain.Events;
using ICS.Modules.Request.Domain.Events;
using MediatR;

/// <summary>
/// In-process event collector for capturing domain events during integration testing.
/// </summary>
public class TestDomainEventCollector :
    INotificationHandler<PersonCreated>,
    INotificationHandler<PersonDeactivated>,
    INotificationHandler<RoleAssigned>,
    INotificationHandler<RoleRevoked>,
    INotificationHandler<CustomerCreated>,
    INotificationHandler<CustomerDeactivated>,
    INotificationHandler<CustomerActivated>,
    INotificationHandler<CustomerMasterDataUpdated>,
    INotificationHandler<CustomerContactAdded>,
    INotificationHandler<CustomerContactUpdated>,
    INotificationHandler<CustomerContactDeactivated>,
    INotificationHandler<CustomerContactActivated>,
    INotificationHandler<ProductCreated>,
    INotificationHandler<ProductOwnerChanged>,
    INotificationHandler<ProductActivated>,
    INotificationHandler<ProductDeactivated>,
    INotificationHandler<RequestRecorded>,
    INotificationHandler<RequestAssigned>,
    INotificationHandler<RequestEvaluated>,
    INotificationHandler<RequestAccepted>,
    INotificationHandler<RequestRejected>,
    INotificationHandler<RequestEscalated>,
    INotificationHandler<ManagementDecisionRequested>,
    INotificationHandler<RequestCompleted>
{
    private static readonly ConcurrentBag<IDomainEvent> _events = new();

    public static IReadOnlyList<IDomainEvent> PublishedEvents => _events.ToList();

    public static void Clear()
    {
        _events.Clear();
    }

    public Task Handle(PersonCreated notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(PersonDeactivated notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(RoleAssigned notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(RoleRevoked notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(CustomerCreated notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(CustomerDeactivated notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(CustomerActivated notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(CustomerMasterDataUpdated notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(CustomerContactAdded notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(CustomerContactUpdated notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(CustomerContactDeactivated notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(CustomerContactActivated notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(ProductCreated notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(ProductOwnerChanged notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(ProductActivated notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(ProductDeactivated notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(RequestRecorded notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(RequestAssigned notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(RequestEvaluated notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(RequestAccepted notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(RequestRejected notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(RequestEscalated notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(ManagementDecisionRequested notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }

    public Task Handle(RequestCompleted notification, CancellationToken cancellationToken)
    {
        _events.Add(notification);
        return Task.CompletedTask;
    }
}
