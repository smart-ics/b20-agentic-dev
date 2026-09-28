namespace ICS.Modules.WorkPackage.Domain;

/// <summary>
/// Authoritative lifecycle statuses for a Work Package.
/// State machine sequence: DRAFT → ACTIVE → CLOSED (or DRAFT → CLOSED).
/// Architecture §11; work-package-domain.md §8, §9.
/// </summary>
public static class WorkPackageStatus
{
    public const string Draft = "DRAFT";
    public const string Active = "ACTIVE";
    public const string Closed = "CLOSED";

    public static readonly IReadOnlyList<string> All =
    [
        Draft,
        Active,
        Closed
    ];

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) &&
        All.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool IsTerminal(string? status) =>
        string.Equals(status, Closed, StringComparison.OrdinalIgnoreCase);

    public static bool IsActive(string? status) =>
        string.Equals(status, Active, StringComparison.OrdinalIgnoreCase);

    public static bool IsDraft(string? status) =>
        string.Equals(status, Draft, StringComparison.OrdinalIgnoreCase);
}
