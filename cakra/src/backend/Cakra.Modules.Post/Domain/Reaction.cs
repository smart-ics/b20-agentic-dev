using Cakra.Core;
using Cakra.Modules.Post.Domain.Exceptions;

namespace Cakra.Modules.Post.Domain;

/// <summary>
/// Represents a structured operational reaction expressed by a Person on a <see cref="Post"/> or <see cref="Comment"/>
/// (Post Domain §5, §7 Rules 21-26; FEAT-FCOL-002; Architecture §6, §17, §20, §21).
/// Uses soft-removal (<see cref="IsActive"/> / <see cref="RemovedAt"/>) rather than physical deletion
/// to preserve permanent operational history.
/// </summary>
public sealed class Reaction : EntityBase
{
    public Guid ReactionId => Id;

    public Guid PostId { get; private set; }

    public Guid? CommentId { get; private set; }

    public Guid PersonId { get; private set; }

    public string ReactionType { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    public DateTime? RemovedAt { get; private set; }

    private Reaction()
    {
    }

    public static Reaction Create(
        Guid postId,
        Guid personId,
        string reactionType,
        Guid? commentId = null,
        DateTime? createdAtUtc = null)
    {
        if (postId == Guid.Empty)
        {
            throw new PostDomainValidationException("PostId cannot be empty.", nameof(postId));
        }

        if (personId == Guid.Empty)
        {
            throw new PostDomainValidationException("PersonId cannot be empty.", nameof(personId));
        }

        if (commentId.HasValue && commentId.Value == Guid.Empty)
        {
            throw new PostDomainValidationException("CommentId cannot be an empty GUID when specified.", nameof(commentId));
        }

        var normalizedReactionType = PostReactionTypes.NormalizeAndValidate(reactionType);
        var now = createdAtUtc ?? DateTime.UtcNow;

        return new Reaction
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            CommentId = commentId,
            PersonId = personId,
            ReactionType = normalizedReactionType,
            IsActive = true,
            RemovedAt = null,
            CreatedAt = now,
            UpdatedAt = null
        };
    }

    public void MarkRemoved(DateTime? removedAtUtc = null)
    {
        var now = removedAtUtc ?? DateTime.UtcNow;
        IsActive = false;
        RemovedAt = now;
        UpdatedAt = now;
    }

    public void Reactivate(DateTime? reactivatedAtUtc = null)
    {
        var now = reactivatedAtUtc ?? DateTime.UtcNow;
        IsActive = true;
        RemovedAt = null;
        UpdatedAt = now;
    }

    public static Reaction Rehydrate(
        Guid id,
        Guid postId,
        Guid? commentId,
        Guid personId,
        string reactionType,
        bool isActive,
        DateTime? removedAt,
        DateTime createdAt,
        DateTime? updatedAt = null)
    {
        return new Reaction
        {
            Id = id,
            PostId = postId,
            CommentId = commentId,
            PersonId = personId,
            ReactionType = PostReactionTypes.NormalizeAndValidate(reactionType),
            IsActive = isActive && removedAt is null,
            RemovedAt = removedAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
