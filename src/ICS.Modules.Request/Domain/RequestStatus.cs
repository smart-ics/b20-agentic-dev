namespace ICS.Modules.Request.Domain;

/// <summary>
/// Authoritative lifecycle statuses for a Request.
/// State machine sequence: CAPTURED → EVALUATING → ACCEPTED / REJECTED → IN_PROGRESS → ESCALATED → COMPLETED.
/// Architecture §7, §8, §13; request-domain.md §9.
/// </summary>
public static class RequestStatus
{
    public const string Captured = "CAPTURED";
    public const string Evaluating = "EVALUATING";
    public const string Accepted = "ACCEPTED";
    public const string Rejected = "REJECTED";
    public const string InProgress = "IN_PROGRESS";
    public const string Escalated = "ESCALATED";
    public const string Completed = "COMPLETED";

    public static readonly IReadOnlyList<string> All =
    [
        Captured,
        Evaluating,
        Accepted,
        Rejected,
        InProgress,
        Escalated,
        Completed
    ];

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) &&
        All.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool IsTerminal(string? status) =>
        string.Equals(status, Completed, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, Rejected, StringComparison.OrdinalIgnoreCase);

    public static bool IsActive(string? status) =>
        string.Equals(status, Evaluating, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, Accepted, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, InProgress, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, Escalated, StringComparison.OrdinalIgnoreCase);
}
