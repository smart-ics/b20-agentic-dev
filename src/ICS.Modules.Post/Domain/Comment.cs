namespace ICS.Modules.Post.Domain;

using ICS.Core.Domain;
using ICS.Modules.Post.Domain.Events;
using ICS.Modules.Post.Domain.Exceptions;

/// <summary>
/// Represents a contribution to the discussion of a Post.
/// Comments belong to exactly one Post and use a flat discussion model.
/// A Comment cannot exist independently of its Post.
/// post-domain.md §5 (Comment), §7 (Business Rules 17-20), §8 (Comment Lifecycle).
/// </summary>
public class Comment : Entity
{
    private const int MaxContentLength = 2000;

    public Guid PostId { get; private set; } = Guid.Empty;
    public Guid AuthorPersonId { get; private set; } = Guid.Empty;
    public string Content { get; private set; } = string.Empty;
    public string Status { get; private set; } = CommentStatus.Active;

    /// <summary>
    /// Parameterless constructor for Dapper persistence mapping.
    /// </summary>
    protected Comment() { }

    /// <summary>
    /// Full constructor for hydrating the entity from persistence without raising domain events.
    /// </summary>
    public Comment(
        Guid id,
        Guid postId,
        Guid authorPersonId,
        string content,
        string status,
        DateTime createdAt,
        DateTime? updatedAt = null)
        : base(id)
    {
        Id = id;
        PostId = postId;
        AuthorPersonId = authorPersonId;
        Content = content;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Factory method to create a new Comment on a Post.
    /// Emits <see cref="CommentAdded"/>.
    /// </summary>
    public static Comment Add(
        Guid id,
        Guid postId,
        Guid authorPersonId,
        string content,
        DateTime createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("CommentId cannot be empty.", nameof(id));
        if (postId == Guid.Empty)
            throw new ArgumentException("PostId cannot be empty.", nameof(postId));
        if (authorPersonId == Guid.Empty)
            throw new ArgumentException("AuthorPersonId cannot be empty.", nameof(authorPersonId));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Comment content cannot be empty.", nameof(content));

        var trimmedContent = content.Trim();
        if (trimmedContent.Length > MaxContentLength)
            throw new ArgumentException($"Comment content must not exceed {MaxContentLength} characters.", nameof(content));

        var comment = new Comment
        {
            Id = id,
            PostId = postId,
            AuthorPersonId = authorPersonId,
            Content = trimmedContent,
            Status = CommentStatus.Active,
            CreatedAt = createdAt
        };

        comment.AddDomainEvent(new CommentAdded(
            comment.Id,
            comment.PostId,
            comment.AuthorPersonId,
            comment.Content,
            createdAt));

        return comment;
    }

    /// <summary>
    /// Updates the content of the Comment.
    /// Emits <see cref="CommentChanged"/>.
    /// </summary>
    public void UpdateContent(string content, DateTime updatedAt)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Comment content cannot be empty.", nameof(content));

        var trimmedContent = content.Trim();
        if (trimmedContent.Length > MaxContentLength)
            throw new ArgumentException($"Comment content must not exceed {MaxContentLength} characters.", nameof(content));

        Content = trimmedContent;
        UpdatedAt = updatedAt;

        AddDomainEvent(new CommentChanged(Id, PostId, AuthorPersonId, updatedAt));
    }

    /// <summary>
    /// Hides the Comment from normal discussion display.
    /// HIDDEN is a visibility condition, not a lifecycle state.
    /// Emits <see cref="CommentHidden"/>.
    /// </section>
    public void Hide(DateTime hiddenAt)
    {
        if (CommentStatus.IsHidden(Status))
            return;

        Status = CommentStatus.Hidden;
        UpdatedAt = hiddenAt;

        AddDomainEvent(new CommentHidden(Id, PostId, AuthorPersonId, hiddenAt));
    }

    /// <summary>
    /// Restores the Comment to normal discussion display.
    /// Emits <see cref="CommentShown"/>.
    /// </summary>
    public void Show(DateTime shownAt)
    {
        if (!CommentStatus.IsHidden(Status))
            return;

        Status = CommentStatus.Active;
        UpdatedAt = shownAt;

        AddDomainEvent(new CommentShown(Id, PostId, AuthorPersonId, shownAt));
    }

    /// <summary>
    /// Determines whether the Comment is currently active.
    /// </summary>
    public bool IsActive => CommentStatus.IsActive(Status);

    /// <summary>
    /// Determines whether the Comment is currently hidden.
    /// </summary>
    public bool IsHidden => CommentStatus.IsHidden(Status);
}