namespace Cakra.Modules.Request.Domain;

/// <summary>
/// Authoritative lifecycle status of a Request (Architecture §7, §8, §17).
/// </summary>
public enum RequestStatus
{
    /// <summary>The Request has been recorded and is queued for ownership triage.</summary>
    Captured = 1,

    /// <summary>The Request has been assigned to an owner and is undergoing triage evaluation.</summary>
    Evaluating = 2,

    /// <summary>The Request has been accepted by the owner for active execution.</summary>
    Accepted = 3,

    /// <summary>The Request has been rejected during evaluation and closed with justification.</summary>
    Rejected = 4,

    /// <summary>Active work on the Request is underway.</summary>
    InProgress = 5,

    /// <summary>The Request has been escalated due to authority, technical, or resource barriers.</summary>
    Escalated = 6,

    /// <summary>Work on the Request has concluded and the resolution has been verified.</summary>
    Completed = 7
}

/// <summary>
/// Canonical string representation of request statuses used in persistence and projections.
/// </summary>
public static class RequestStatusNames
{
    public const string Captured = "CAPTURED";
    public const string Evaluating = "EVALUATING";
    public const string Accepted = "ACCEPTED";
    public const string Rejected = "REJECTED";
    public const string InProgress = "IN_PROGRESS";
    public const string Escalated = "ESCALATED";
    public const string Completed = "COMPLETED";

    public static string ToName(this RequestStatus status) => status switch
    {
        RequestStatus.Captured => Captured,
        RequestStatus.Evaluating => Evaluating,
        RequestStatus.Accepted => Accepted,
        RequestStatus.Rejected => Rejected,
        RequestStatus.InProgress => InProgress,
        RequestStatus.Escalated => Escalated,
        RequestStatus.Completed => Completed,
        _ => status.ToString().ToUpperInvariant()
    };

    public static RequestStatus FromName(string statusName)
    {
        if (string.IsNullOrWhiteSpace(statusName))
            throw new ArgumentException("Status name cannot be null or empty.", nameof(statusName));

        return statusName.Trim().ToUpperInvariant() switch
        {
            Captured => RequestStatus.Captured,
            Evaluating => RequestStatus.Evaluating,
            Accepted => RequestStatus.Accepted,
            Rejected => RequestStatus.Rejected,
            InProgress or "INPROGRESS" => RequestStatus.InProgress,
            Escalated => RequestStatus.Escalated,
            Completed => RequestStatus.Completed,
            _ when Enum.TryParse<RequestStatus>(statusName.Trim(), ignoreCase: true, out var parsed) => parsed,
            _ => throw new ArgumentOutOfRangeException(nameof(statusName), statusName, $"Unknown request status '{statusName}'.")
        };
    }

    public static RequestStatus? FromNullableName(string? statusName) =>
        string.IsNullOrWhiteSpace(statusName) ? null : FromName(statusName);
}
