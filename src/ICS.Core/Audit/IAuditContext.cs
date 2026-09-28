namespace ICS.Core.Audit;

/// <summary>
/// Context abstraction providing audit metadata for tracking mutations across domain entities.
/// </summary>
public interface IAuditContext
{
    /// <summary>
    /// The authenticated user account identifier, or null if unauthenticated or system-triggered.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// The linked organizational person identifier performing the action.
    /// </summary>
    Guid? PersonId { get; }

    /// <summary>
    /// The UTC timestamp when the auditable operation occurred.
    /// </summary>
    DateTime Timestamp { get; }

    /// <summary>
    /// Client IP address initiating the request.
    /// </summary>
    string? IpAddress { get; }

    /// <summary>
    /// Client User-Agent string.
    /// </summary>
    string? UserAgent { get; }
}
