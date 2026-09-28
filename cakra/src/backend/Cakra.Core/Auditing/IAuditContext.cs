namespace Cakra.Core;

/// <summary>
/// Ambient audit context describing the actor and time associated with the
/// current operation, used to record audit information on state changes
/// (Architecture 18).
/// </summary>
public interface IAuditContext
{
    /// <summary>Authenticated user identifier of the acting user, or <c>null</c> when anonymous.</summary>
    Guid? ActorUserId { get; }

    /// <summary>Organization person identifier of the acting user, or <c>null</c> when anonymous.</summary>
    Guid? ActorPersonId { get; }

    /// <summary>UTC timestamp captured for the current operation.</summary>
    DateTime RecordedAtUtc { get; }
}
