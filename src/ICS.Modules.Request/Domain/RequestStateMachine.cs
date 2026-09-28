namespace ICS.Modules.Request.Domain;

using ICS.Modules.Request.Domain.Exceptions;

/// <summary>
/// State machine enforcing valid lifecycle transitions for a Request.
/// Formal transitions: CAPTURED → EVALUATING → ACCEPTED/REJECTED → IN_PROGRESS → ESCALATED → COMPLETED.
/// Architecture §7, §8; request-domain.md §9.
/// </summary>
public static class RequestStateMachine
{
    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        [RequestStatus.Captured] = new(StringComparer.OrdinalIgnoreCase)
        {
            RequestStatus.Evaluating,
            RequestStatus.Rejected
        },
        [RequestStatus.Evaluating] = new(StringComparer.OrdinalIgnoreCase)
        {
            RequestStatus.Accepted,
            RequestStatus.Rejected
        },
        [RequestStatus.Accepted] = new(StringComparer.OrdinalIgnoreCase)
        {
            RequestStatus.InProgress,
            RequestStatus.Escalated
        },
        [RequestStatus.InProgress] = new(StringComparer.OrdinalIgnoreCase)
        {
            RequestStatus.Escalated,
            RequestStatus.Completed
        },
        [RequestStatus.Escalated] = new(StringComparer.OrdinalIgnoreCase)
        {
            RequestStatus.InProgress,
            RequestStatus.Completed,
            RequestStatus.Rejected
        },
        [RequestStatus.Completed] = new(StringComparer.OrdinalIgnoreCase),
        [RequestStatus.Rejected] = new(StringComparer.OrdinalIgnoreCase)
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
    /// throwing an <see cref="InvalidRequestStateTransitionException"/> if invalid.
    /// </summary>
    public static void EnsureValidTransition(Guid requestId, string currentStatus, string targetStatus)
    {
        if (!CanTransition(currentStatus, targetStatus))
        {
            throw new InvalidRequestStateTransitionException(
                requestId,
                currentStatus,
                targetStatus,
                $"Invalid Request state transition: cannot transition Request '{requestId}' from status '{currentStatus}' to '{targetStatus}'.");
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
