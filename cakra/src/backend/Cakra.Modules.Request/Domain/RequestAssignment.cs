using Cakra.Core;

namespace Cakra.Modules.Request.Domain;

/// <summary>
/// Audit trail entity capturing ownership assignments and lifecycle state changes
/// on a Request (Architecture §17, §18).
/// </summary>
public sealed class RequestAssignment : EntityBase
{
    /// <summary>Unique identifier of the associated Request.</summary>
    public Guid RequestId { get; private set; }

    /// <summary>PersonId of the previous owner, or <c>null</c> if initially unassigned.</summary>
    public Guid? PreviousOwnerPersonId { get; private set; }

    /// <summary>PersonId of the assigned owner, or <c>null</c> if unassigned.</summary>
    public Guid? AssignedOwnerPersonId { get; private set; }

    /// <summary>PersonId of the actor who performed the assignment or state transition.</summary>
    public Guid ActorPersonId { get; private set; }

    /// <summary>Lifecycle status prior to this transition, or <c>null</c> for initial capture.</summary>
    public RequestStatus? PreviousStatus { get; private set; }

    /// <summary>New lifecycle status following this transition.</summary>
    public RequestStatus NewStatus { get; private set; }

    /// <summary>UTC timestamp when the transition or assignment occurred.</summary>
    public DateTime AssignedAtUtc { get; private set; }

    /// <summary>Optional notes, justifications, or context for this transition.</summary>
    public string? Notes { get; private set; }

    private RequestAssignment() { }

    public static RequestAssignment Create(
        Guid requestId,
        Guid? previousOwnerPersonId,
        Guid? assignedOwnerPersonId,
        Guid actorPersonId,
        RequestStatus? previousStatus,
        RequestStatus newStatus,
        DateTime assignedAtUtc,
        string? notes = null)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("RequestId cannot be empty.", nameof(requestId));
        if (actorPersonId == Guid.Empty)
            throw new ArgumentException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        return new RequestAssignment
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            PreviousOwnerPersonId = previousOwnerPersonId,
            AssignedOwnerPersonId = assignedOwnerPersonId,
            ActorPersonId = actorPersonId,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            AssignedAtUtc = assignedAtUtc,
            Notes = notes?.Trim(),
            CreatedAt = assignedAtUtc
        };
    }

    /// <summary>
    /// Rehydrates a <see cref="RequestAssignment"/> audit record from persistence storage.
    /// </summary>
    public static RequestAssignment Rehydrate(
        Guid id,
        Guid requestId,
        Guid? previousOwnerPersonId,
        Guid? assignedOwnerPersonId,
        Guid actorPersonId,
        RequestStatus? previousStatus,
        RequestStatus newStatus,
        DateTime assignedAtUtc,
        string? notes,
        DateTime createdAt,
        DateTime? updatedAt = null)
    {
        return new RequestAssignment
        {
            Id = id,
            RequestId = requestId,
            PreviousOwnerPersonId = previousOwnerPersonId,
            AssignedOwnerPersonId = assignedOwnerPersonId,
            ActorPersonId = actorPersonId,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            AssignedAtUtc = assignedAtUtc,
            Notes = notes,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
