using Cakra.Core;
using Cakra.Modules.Request.Domain.Exceptions;

namespace Cakra.Modules.Request.Domain;

/// <summary>
/// Child entity representing a granular operational impediment/blocker within
/// the <see cref="Request"/> aggregate root boundary (Architecture CR-032 §4 TD-001).
/// </summary>
public sealed class RequestBlocker : EntityBase
{
    /// <summary>Identifier of the parent Request.</summary>
    public Guid RequestId { get; private set; }

    /// <summary>Detailed impediment description and context explaining why progress is halted.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>PersonId of the collaborator who reported/recorded this blocker.</summary>
    public Guid CreatedByPersonId { get; private set; }

    /// <summary>Timestamp when the blocker was recorded in UTC.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>True if the blocker has been resolved/cleared; otherwise false.</summary>
    public bool IsCleared { get; private set; }

    /// <summary>Timestamp when the blocker was cleared in UTC, or null if active.</summary>
    public DateTime? ClearedAtUtc { get; private set; }

    /// <summary>PersonId of the actor who cleared this blocker, or null if active.</summary>
    public Guid? ClearedByPersonId { get; private set; }

    /// <summary>Optional resolution/clearance notes or rationale.</summary>
    public string? ClearanceReason { get; private set; }

    private RequestBlocker() { }

    internal static RequestBlocker Create(
        Guid id,
        Guid requestId,
        string reason,
        Guid createdByPersonId,
        DateTime utcNow)
    {
        if (id == Guid.Empty)
            throw new RequestDomainValidationException("BlockerId cannot be empty.", nameof(id));
        if (requestId == Guid.Empty)
            throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(requestId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new RequestDomainValidationException("Blocker reason cannot be empty.", nameof(reason));
        if (createdByPersonId == Guid.Empty)
            throw new RequestDomainValidationException("CreatedByPersonId cannot be empty.", nameof(createdByPersonId));

        return new RequestBlocker
        {
            Id = id,
            RequestId = requestId,
            Reason = reason.Trim(),
            CreatedByPersonId = createdByPersonId,
            CreatedAtUtc = utcNow,
            IsCleared = false,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    internal void Clear(Guid clearedByPersonId, string? clearanceReason, DateTime utcNow)
    {
        if (clearedByPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ClearedByPersonId cannot be empty.", nameof(clearedByPersonId));
        if (IsCleared)
            throw new InvalidOperationException("Blocker is already cleared.");

        IsCleared = true;
        ClearedAtUtc = utcNow;
        ClearedByPersonId = clearedByPersonId;
        ClearanceReason = string.IsNullOrWhiteSpace(clearanceReason) ? null : clearanceReason.Trim();
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Rehydrates a <see cref="RequestBlocker"/> entity from persistence storage.
    /// </summary>
    public static RequestBlocker Rehydrate(
        Guid id,
        Guid requestId,
        string reason,
        Guid createdByPersonId,
        DateTime createdAtUtc,
        bool isCleared,
        DateTime? clearedAtUtc,
        Guid? clearedByPersonId,
        string? clearanceReason,
        DateTime createdAt,
        DateTime? updatedAt = null)
    {
        return new RequestBlocker
        {
            Id = id,
            RequestId = requestId,
            Reason = reason,
            CreatedByPersonId = createdByPersonId,
            CreatedAtUtc = createdAtUtc,
            IsCleared = isCleared,
            ClearedAtUtc = clearedAtUtc,
            ClearedByPersonId = clearedByPersonId,
            ClearanceReason = clearanceReason,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
