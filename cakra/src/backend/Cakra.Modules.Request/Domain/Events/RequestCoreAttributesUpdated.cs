using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when core attributes (Title, Description, Priority, RequestType) of a Request are updated (CR-018).
/// </summary>
public sealed record RequestCoreAttributesUpdated(
    Guid RequestId,
    string Title,
    string Description,
    string Priority,
    string RequestType,
    Guid ActorPersonId,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public RequestCoreAttributesUpdated(
        Guid requestId,
        string title,
        string description,
        string priority,
        string requestType,
        Guid actorPersonId,
        DateTime occurredAtUtc,
        Guid? eventId = null)
        : this(requestId, title, description, priority, requestType, actorPersonId, occurredAtUtc)
    {
        if (eventId.HasValue)
        {
            EventId = eventId.Value;
        }
    }
}
