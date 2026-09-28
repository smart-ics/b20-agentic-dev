using Cakra.Core;
using Cakra.Modules.Post.Domain.Exceptions;

namespace Cakra.Modules.Post.Domain;

/// <summary>
/// Represents a contribution to the flat discussion attached to a <see cref="Post"/>
/// (Post Domain §5, §7 Rules 17-20, §8; FEAT-FCOL-001; Architecture §6, §17).
/// </summary>
public sealed class Comment : EntityBase
{
    public const string StatusActive = "ACTIVE";
    public const string StatusHidden = "HIDDEN";

    public Guid CommentId => Id;

    public Guid PostId { get; private set; }

    public Guid AuthorPersonId { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public string Status { get; private set; } = StatusActive;

    public bool IsActive => string.Equals(Status, StatusActive, StringComparison.OrdinalIgnoreCase);

    private Comment()
    {
    }

    public static Comment Create(
        Guid postId,
        Guid authorPersonId,
        string content,
        DateTime? createdAtUtc = null)
    {
        if (postId == Guid.Empty)
        {
            throw new PostDomainValidationException("PostId cannot be empty.", nameof(postId));
        }

        if (authorPersonId == Guid.Empty)
        {
            throw new PostDomainValidationException("Comment AuthorPersonId cannot be empty.", nameof(authorPersonId));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new PostDomainValidationException("Comment content cannot be null or whitespace.", nameof(content));
        }

        var now = createdAtUtc ?? DateTime.UtcNow;

        return new Comment
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            AuthorPersonId = authorPersonId,
            Content = content.Trim(),
            Status = StatusActive,
            CreatedAt = now,
            UpdatedAt = null
        };
    }

    public void Hide(DateTime? updatedAtUtc = null)
    {
        Status = StatusHidden;
        UpdatedAt = updatedAtUtc ?? DateTime.UtcNow;
    }

    public void Restore(DateTime? updatedAtUtc = null)
    {
        Status = StatusActive;
        UpdatedAt = updatedAtUtc ?? DateTime.UtcNow;
    }

    public static Comment Rehydrate(
        Guid id,
        Guid postId,
        Guid authorPersonId,
        string content,
        string status,
        DateTime createdAt,
        DateTime? updatedAt = null)
    {
        return new Comment
        {
            Id = id,
            PostId = postId,
            AuthorPersonId = authorPersonId,
            Content = content ?? string.Empty,
            Status = string.IsNullOrWhiteSpace(status) ? StatusActive : status.Trim().ToUpperInvariant(),
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
