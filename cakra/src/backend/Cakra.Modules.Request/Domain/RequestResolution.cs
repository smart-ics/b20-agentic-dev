using Cakra.Core;

namespace Cakra.Modules.Request.Domain;

/// <summary>
/// Represents the recorded outcome when a Request is resolved, rejected, or completed (Architecture §17).
/// </summary>
public sealed class RequestResolution : EntityBase
{
    /// <summary>Unique identifier of the associated Request.</summary>
    public Guid RequestId { get; private set; }

    /// <summary>Resolution outcome classification (e.g. REJECTED, COMPLETED, RESOLVED).</summary>
    public string Outcome { get; private set; } = string.Empty;

    /// <summary>Detailed description, justification, or summary of the resolution.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>PersonId of the organizational actor who recorded or approved the resolution.</summary>
    public Guid ResolvedBy { get; private set; }

    /// <summary>UTC timestamp when the resolution was recorded.</summary>
    public DateTime ResolvedAt { get; private set; }

    private RequestResolution() { }

    public static RequestResolution Create(
        Guid requestId,
        string outcome,
        string description,
        Guid resolvedBy,
        DateTime resolvedAt)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("RequestId cannot be empty.", nameof(requestId));
        if (string.IsNullOrWhiteSpace(outcome))
            throw new ArgumentException("Outcome cannot be empty.", nameof(outcome));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty.", nameof(description));
        if (resolvedBy == Guid.Empty)
            throw new ArgumentException("ResolvedBy cannot be empty.", nameof(resolvedBy));

        return new RequestResolution
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            Outcome = outcome.Trim(),
            Description = description.Trim(),
            ResolvedBy = resolvedBy,
            ResolvedAt = resolvedAt,
            CreatedAt = resolvedAt
        };
    }

    /// <summary>
    /// Rehydrates a <see cref="RequestResolution"/> record from persistence storage.
    /// </summary>
    public static RequestResolution Rehydrate(
        Guid id,
        Guid requestId,
        string outcome,
        string description,
        Guid resolvedBy,
        DateTime resolvedAt,
        DateTime createdAt,
        DateTime? updatedAt = null)
    {
        return new RequestResolution
        {
            Id = id,
            RequestId = requestId,
            Outcome = outcome,
            Description = description,
            ResolvedBy = resolvedBy,
            ResolvedAt = resolvedAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
