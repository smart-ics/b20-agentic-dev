namespace Cakra.Modules.Request.Domain;

/// <summary>
/// Authoritative lifecycle status of a Request (Architecture §7, §8, CR-016 TD-001).
/// </summary>
public enum RequestStatus
{
    /// <summary>The Request has been recorded and is queued for ownership triage.</summary>
    Captured = 1,

    /// <summary>The Request has been assigned to an owner and is awaiting owner initiation.</summary>
    Assigned = 2,

    /// <summary>Active work on the Request is underway by the designated owner.</summary>
    InProgress = 3,

    /// <summary>Work on the Request is temporarily suspended due to priority, blocker, or management direction.</summary>
    Paused = 4,

    /// <summary>Work on the Request has concluded and the resolution has been verified.</summary>
    Completed = 5,

    /// <summary>The Request has been abandoned, rejected, or cancelled without completion.</summary>
    Cancelled = 6
}

/// <summary>
/// Canonical string representation of request statuses used in persistence and projections.
/// </summary>
public static class RequestStatusNames
{
    public const string Captured = "CAPTURED";
    public const string Assigned = "ASSIGNED";
    public const string InProgress = "IN_PROGRESS";
    public const string Paused = "PAUSED";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";

    // Legacy status constants retained for backward compatibility during CR-016 migration
    [Obsolete("Evaluating is deprecated in CR-016.")]
    public const string Evaluating = "EVALUATING";

    [Obsolete("Accepted is deprecated in CR-016.")]
    public const string Accepted = "ACCEPTED";

    [Obsolete("Rejected is deprecated in CR-016.")]
    public const string Rejected = "REJECTED";

    [Obsolete("Escalated is deprecated in CR-016.")]
    public const string Escalated = "ESCALATED";

    public static string ToName(this RequestStatus status) => status switch
    {
        RequestStatus.Captured => Captured,
        RequestStatus.Assigned => Assigned,
        RequestStatus.InProgress => InProgress,
        RequestStatus.Paused => Paused,
        RequestStatus.Completed => Completed,
        RequestStatus.Cancelled => Cancelled,
        _ => status.ToString().ToUpperInvariant()
    };

    public static RequestStatus FromName(string statusName)
    {
        if (string.IsNullOrWhiteSpace(statusName))
            throw new ArgumentException("Status name cannot be null or empty.", nameof(statusName));

        return statusName.Trim().ToUpperInvariant() switch
        {
            Captured => RequestStatus.Captured,
            Assigned => RequestStatus.Assigned,
            InProgress or "INPROGRESS" => RequestStatus.InProgress,
            Paused => RequestStatus.Paused,
            Completed => RequestStatus.Completed,
            Cancelled or "CANCELED" => RequestStatus.Cancelled,
            "EVALUATING" or "ACCEPTED" => RequestStatus.Assigned,
            "ESCALATED" => RequestStatus.Paused,
            "REJECTED" => RequestStatus.Cancelled,
            _ when Enum.TryParse<RequestStatus>(statusName.Trim(), ignoreCase: true, out var parsed) => parsed,
            _ => throw new ArgumentOutOfRangeException(nameof(statusName), statusName, $"Unknown request status '{statusName}'.")
        };
    }

    public static RequestStatus? FromNullableName(string? statusName) =>
        string.IsNullOrWhiteSpace(statusName) ? null : FromName(statusName);
}
