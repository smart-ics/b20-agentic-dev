using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when a management decision is requested on a Request (UC-REQ-007).
/// </summary>
public sealed record ManagementDecisionRequested : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid RequestedByPersonId { get; init; }
    public string DecisionDetails { get; init; } = string.Empty;
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public ManagementDecisionRequested(
        Guid requestId,
        Guid requestedByPersonId,
        string decisionDetails,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        RequestedByPersonId = requestedByPersonId;
        DecisionDetails = decisionDetails;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}
