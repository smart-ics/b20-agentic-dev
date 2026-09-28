using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when a new Request is recorded in CAPTURED state (UC-REQ-001).
/// </summary>
public sealed record RequestRecorded : IDomainEvent
{
    public Guid RequestId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string RequestType { get; init; } = string.Empty;
    public Guid ActorPersonId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? ProductId { get; init; }
    public Guid? WorkPackageId { get; init; }
    public string? Priority { get; init; }
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestRecorded(
        Guid requestId,
        string title,
        string description,
        string requestType,
        Guid actorPersonId,
        Guid? customerId = null,
        Guid? productId = null,
        Guid? workPackageId = null,
        string? priority = null,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        Title = title;
        Description = description;
        RequestType = requestType;
        ActorPersonId = actorPersonId;
        CustomerId = customerId;
        ProductId = productId;
        WorkPackageId = workPackageId;
        Priority = priority;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}
