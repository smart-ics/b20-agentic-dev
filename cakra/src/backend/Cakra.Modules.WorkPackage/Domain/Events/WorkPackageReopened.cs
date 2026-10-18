using Cakra.Core;

namespace Cakra.Modules.WorkPackage.Domain.Events;

/// <summary>
/// Domain event published when a closed Work Package is re-opened into DRAFT state (Architecture CR-035 §4 TD-002).
/// </summary>
public sealed record WorkPackageReopened(
    Guid WorkPackageId,
    string Reason,
    string? PreviousClosedReason,
    DateTime? PreviousClosedAt,
    DateTime ReopenedAtUtc,
    Guid EventId) : IDomainEvent
{
    public WorkPackageReopened(
        Guid workPackageId,
        string reason,
        string? previousClosedReason,
        DateTime? previousClosedAt,
        DateTime reopenedAtUtc)
        : this(workPackageId, reason, previousClosedReason, previousClosedAt, reopenedAtUtc, Guid.NewGuid())
    {
    }

    public DateTime OccurredAtUtc => ReopenedAtUtc;
}
