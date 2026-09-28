using Cakra.Core;

namespace Cakra.Modules.Request.Domain.Events;

/// <summary>
/// Domain event emitted when triage evaluation notes are recorded on a Request (UC-REQ-003).
/// </summary>
public sealed record RequestEvaluated : IDomainEvent
{
    public Guid RequestId { get; init; }
    public Guid EvaluatedByPersonId { get; init; }
    public string EvaluationNotes { get; init; } = string.Empty;
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public RequestEvaluated(
        Guid requestId,
        Guid evaluatedByPersonId,
        string evaluationNotes,
        DateTime? occurredAtUtc = null,
        Guid? eventId = null)
    {
        RequestId = requestId;
        EvaluatedByPersonId = evaluatedByPersonId;
        EvaluationNotes = evaluationNotes;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        EventId = eventId ?? Guid.NewGuid();
    }
}
