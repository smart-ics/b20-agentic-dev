namespace ICS.Modules.WorkPackage.Domain;

using ICS.Modules.WorkPackage.Domain.Exceptions;

/// <summary>
/// State machine enforcing valid lifecycle transitions for a Work Package.
/// Allowed transitions:
/// - DRAFT → ACTIVE
/// - DRAFT → CLOSED
/// - ACTIVE → CLOSED
/// CLOSED is terminal.
/// Architecture §11; work-package-domain.md §9.
/// </summary>
public static class WorkPackageStateMachine
{
    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        [WorkPackageStatus.Draft] = new(StringComparer.OrdinalIgnoreCase)
        {
            WorkPackageStatus.Active,
            WorkPackageStatus.Closed
        },
        [WorkPackageStatus.Active] = new(StringComparer.OrdinalIgnoreCase)
        {
            WorkPackageStatus.Closed
        },
        [WorkPackageStatus.Closed] = new(StringComparer.OrdinalIgnoreCase)
    };

    /// <summary>
    /// Checks whether transitioning from <paramref name="currentStatus"/> to <paramref name="targetStatus"/> is valid.
    /// </summary>
    public static bool CanTransition(string currentStatus, string targetStatus)
    {
        if (string.IsNullOrWhiteSpace(currentStatus) || string.IsNullOrWhiteSpace(targetStatus))
            return false;

        if (AllowedTransitions.TryGetValue(currentStatus.Trim(), out var allowedTargets))
        {
            return allowedTargets.Contains(targetStatus.Trim());
        }

        return false;
    }

    /// <summary>
    /// Enforces that the transition from <paramref name="currentStatus"/> to <paramref name="targetStatus"/> is valid,
    /// throwing an <see cref="InvalidWorkPackageStateTransitionException"/> if invalid.
    /// </summary>
    public static void EnsureValidTransition(Guid workPackageId, string currentStatus, string targetStatus)
    {
        if (!CanTransition(currentStatus, targetStatus))
        {
            throw new InvalidWorkPackageStateTransitionException(
                workPackageId,
                currentStatus,
                targetStatus,
                $"Invalid Work Package state transition: cannot transition Work Package '{workPackageId}' from status '{currentStatus}' to '{targetStatus}'.");
        }
    }

    /// <summary>
    /// Gets all allowed target statuses from the specified <paramref name="currentStatus"/>.
    /// </summary>
    public static IReadOnlyCollection<string> GetAllowedTransitions(string currentStatus)
    {
        if (!string.IsNullOrWhiteSpace(currentStatus) &&
            AllowedTransitions.TryGetValue(currentStatus.Trim(), out var allowedTargets))
        {
            return allowedTargets.ToList().AsReadOnly();
        }

        return Array.Empty<string>();
    }
}
