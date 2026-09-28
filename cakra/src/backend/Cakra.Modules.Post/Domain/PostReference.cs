using Cakra.Core;
using Cakra.Modules.Post.Domain.Exceptions;

namespace Cakra.Modules.Post.Domain;

/// <summary>
/// Represents a contextual link from a <see cref="Post"/> to an upstream operational object
/// (<c>REQUEST</c>, <c>WORK_PACKAGE</c>, <c>CUSTOMER</c>, <c>PRODUCT</c>, or <c>ORGANIZATION</c>)
/// without transferring ownership or altering the referenced object's lifecycle
/// (Post Domain §5, §7 Rules 8-14, §8; Architecture §6, §12, §15, §17).
/// </summary>
public sealed class PostReference : EntityBase
{
    public Guid PostReferenceId => Id;

    public Guid PostId { get; private set; }

    public string ReferenceType { get; private set; } = string.Empty;

    public Guid ReferenceId { get; private set; }

    public string? ReferenceDisplay { get; private set; }

    public DateTime? RemovedAt { get; private set; }

    public bool IsActive => RemovedAt is null;

    private PostReference()
    {
    }

    public static PostReference Create(
        Guid postId,
        string referenceType,
        Guid referenceId,
        string? referenceDisplay = null,
        DateTime? createdAtUtc = null)
    {
        if (postId == Guid.Empty)
        {
            throw new PostDomainValidationException("PostId cannot be empty.", nameof(postId));
        }

        if (referenceId == Guid.Empty)
        {
            throw new PostDomainValidationException("ReferenceId cannot be empty.", nameof(referenceId));
        }

        var normalizedType = PostReferenceTypes.NormalizeAndValidate(referenceType);
        var now = createdAtUtc ?? DateTime.UtcNow;

        return new PostReference
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            ReferenceType = normalizedType,
            ReferenceId = referenceId,
            ReferenceDisplay = string.IsNullOrWhiteSpace(referenceDisplay) ? null : referenceDisplay.Trim(),
            RemovedAt = null,
            CreatedAt = now,
            UpdatedAt = null
        };
    }

    public void UpdateDisplay(string? referenceDisplay, DateTime? updatedAtUtc = null)
    {
        ReferenceDisplay = string.IsNullOrWhiteSpace(referenceDisplay) ? null : referenceDisplay.Trim();
        UpdatedAt = updatedAtUtc ?? DateTime.UtcNow;
    }

    public void MarkRemoved(DateTime? removedAtUtc = null)
    {
        var now = removedAtUtc ?? DateTime.UtcNow;
        RemovedAt = now;
        UpdatedAt = now;
    }

    public static PostReference Rehydrate(
        Guid id,
        Guid postId,
        string referenceType,
        Guid referenceId,
        string? referenceDisplay,
        DateTime? removedAt,
        DateTime createdAt,
        DateTime? updatedAt = null)
    {
        return new PostReference
        {
            Id = id,
            PostId = postId,
            ReferenceType = PostReferenceTypes.NormalizeAndValidate(referenceType),
            ReferenceId = referenceId,
            ReferenceDisplay = string.IsNullOrWhiteSpace(referenceDisplay) ? null : referenceDisplay.Trim(),
            RemovedAt = removedAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
