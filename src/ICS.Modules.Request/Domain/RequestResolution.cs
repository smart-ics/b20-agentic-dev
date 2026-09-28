namespace ICS.Modules.Request.Domain;

using ICS.Core.Domain;

/// <summary>
/// Domain entity representing the authoritative recorded resolution outcome of a Request.
/// Architecture §6, §16, §17; request-domain.md §5.
/// </summary>
public class RequestResolution : Entity
{
    public const string OutcomeResolved = "RESOLVED";
    public const string OutcomeRejected = "REJECTED";
    public const string OutcomeCancelled = "CANCELLED";

    public Guid RequestId { get; private set; }
    public string Outcome { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public Guid ResolvedByPersonId { get; private set; }
    public DateTime ResolvedAt { get; private set; }

    /// <summary>
    /// Parameterless constructor for Dapper persistence mapping.
    /// </summary>
    protected RequestResolution() { }

    public RequestResolution(
        Guid id,
        Guid requestId,
        string outcome,
        string summary,
        Guid resolvedByPersonId,
        DateTime resolvedAt)
        : base(id)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("RequestId cannot be empty.", nameof(requestId));
        if (string.IsNullOrWhiteSpace(outcome))
            throw new ArgumentException("Outcome cannot be empty.", nameof(outcome));
        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("Summary/Reason cannot be empty.", nameof(summary));
        if (resolvedByPersonId == Guid.Empty)
            throw new ArgumentException("ResolvedByPersonId cannot be empty.", nameof(resolvedByPersonId));

        RequestId = requestId;
        Outcome = outcome.Trim().ToUpperInvariant();
        Summary = summary.Trim();
        ResolvedByPersonId = resolvedByPersonId;
        ResolvedAt = resolvedAt;
        CreatedAt = resolvedAt;
    }

    public static RequestResolution CreateResolved(
        Guid id,
        Guid requestId,
        string summary,
        Guid resolvedByPersonId,
        DateTime resolvedAt)
    {
        return new RequestResolution(id, requestId, OutcomeResolved, summary, resolvedByPersonId, resolvedAt);
    }

    public static RequestResolution CreateRejected(
        Guid id,
        Guid requestId,
        string reason,
        Guid rejectedByPersonId,
        DateTime rejectedAt)
    {
        return new RequestResolution(id, requestId, OutcomeRejected, reason, rejectedByPersonId, rejectedAt);
    }
}
