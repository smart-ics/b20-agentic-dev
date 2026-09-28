namespace Cakra.Modules.WorkPackage.Domain;

/// <summary>
/// Lifecycle status of a Work Package aggregate (Architecture §11, Domain §9).
/// Lifecycle state machine: DRAFT -> ACTIVE -> CLOSED, or DRAFT -> CLOSED.
/// </summary>
public enum WorkPackageStatus
{
    /// <summary>Work Package has been created but is not yet active operational work.</summary>
    Draft = 1,

    /// <summary>Work Package represents active operational work.</summary>
    Active = 2,

    /// <summary>Work Package is closed (completed, cancelled, or retired). Terminal state.</summary>
    Closed = 3
}

/// <summary>
/// Canonical string representations of Work Package lifecycle statuses used in persistence and projections.
/// </summary>
public static class WorkPackageStatusNames
{
    public const string Draft = "DRAFT";
    public const string Active = "ACTIVE";
    public const string Closed = "CLOSED";

    public static string ToName(this WorkPackageStatus status) => status switch
    {
        WorkPackageStatus.Draft => Draft,
        WorkPackageStatus.Active => Active,
        WorkPackageStatus.Closed => Closed,
        _ => status.ToString().ToUpperInvariant()
    };

    public static WorkPackageStatus FromName(string statusName)
    {
        if (string.IsNullOrWhiteSpace(statusName))
        {
            throw new ArgumentException("Work package status name cannot be null or empty.", nameof(statusName));
        }

        return statusName.Trim().ToUpperInvariant() switch
        {
            Draft => WorkPackageStatus.Draft,
            Active => WorkPackageStatus.Active,
            Closed => WorkPackageStatus.Closed,
            _ when Enum.TryParse<WorkPackageStatus>(statusName.Trim(), ignoreCase: true, out var parsed) => parsed,
            _ => throw new ArgumentOutOfRangeException(nameof(statusName), statusName, $"Unknown work package status '{statusName}'.")
        };
    }

    public static WorkPackageStatus? FromNullableName(string? statusName) =>
        string.IsNullOrWhiteSpace(statusName) ? null : FromName(statusName);
}
