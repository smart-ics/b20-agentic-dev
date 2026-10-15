using Cakra.Modules.Request.Domain;

namespace Cakra.Modules.Request;

/// <summary>
/// Read-only data transfer object representing a <see cref="RequestBlocker"/> entity
/// (Architecture CR-032 §4 TD-001, TD-007).
/// </summary>
public sealed record RequestBlockerDto(
    Guid Id,
    Guid RequestId,
    string Reason,
    Guid CreatedByPersonId,
    string? CreatedByName,
    DateTime CreatedAtUtc,
    bool IsCleared,
    DateTime? ClearedAtUtc,
    Guid? ClearedByPersonId,
    string? ClearedByName,
    string? ClearanceReason)
{
    /// <summary>Convenience alias for <see cref="CreatedAtUtc"/>.</summary>
    public DateTime CreatedAt => CreatedAtUtc;

    /// <summary>Convenience alias for <see cref="ClearedAtUtc"/>.</summary>
    public DateTime? ClearedAt => ClearedAtUtc;
    internal static RequestBlockerDto FromDomain(RequestBlocker blocker)
    {
        ArgumentNullException.ThrowIfNull(blocker);

        return new RequestBlockerDto(
            blocker.Id,
            blocker.RequestId,
            blocker.Reason,
            blocker.CreatedByPersonId,
            null,
            blocker.CreatedAtUtc,
            blocker.IsCleared,
            blocker.ClearedAtUtc,
            blocker.ClearedByPersonId,
            null,
            blocker.ClearanceReason);
    }
}
