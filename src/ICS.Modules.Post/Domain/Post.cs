namespace ICS.Modules.Post.Domain;

using ICS.Core.Domain;
using ICS.Modules.Post.Domain.Events;
using ICS.Modules.Post.Domain.Exceptions;

/// <summary>
/// Authoritative domain aggregate representing a persistent operational communication.
/// The Post Aggregate is the authoritative source of:
/// * Post identity;
/// * Post content;
/// * Post source (system-generated vs human-authored);
/// * Post status;
/// * Post visibility;
/// * Post references;
/// * Post discussion (comments and reactions);
/// * Post history.
/// post-domain.md §5 (Post), §6 (Aggregates), §7 (Business Rules 1-38).
/// </summary>
public class Post : Entity
{
    private readonly List<PostReference> _references = new();
    private readonly List<Comment> _comments = new();
    private readonly List<Reaction> _reactions = new();

    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public string Source { get; private set; } = PostSource.HumanAuthored;
    public string Status { get; private set; } = PostStatus.Active;
    public string Visibility { get; private set; } = PostVisibility.Visible;
    public Guid? AuthorPersonId { get; private set; }
    public DateTime? ArchivedAt { get; private set; }

    /// <summary>
    /// Read-only collections of associated entities.
    /// </summary>
    public IReadOnlyCollection<PostReference> References => _references.AsReadOnly();
    public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();
    public IReadOnlyCollection<Reaction> Reactions => _reactions.AsReadOnly();

    /// <summary>
    /// Parameterless constructor for Dapper persistence mapping.
    /// </summary>
    protected Post() { }

    /// <summary>
    /// Full constructor for hydrating the entity from persistence without raising domain events.
    /// </summary>
    public Post(
        Guid id,
        string title,
        string content,
        string source,
        Guid? authorPersonId,
        string status,
        string visibility,
        DateTime createdAt,
        DateTime? updatedAt = null,
        DateTime? archivedAt = null)
        : base(id)
    {
        Id = id;
        Title = title;
        Content = content;
        Source = source;
        AuthorPersonId = authorPersonId;
        Status = status;
        Visibility = visibility;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        ArchivedAt = archivedAt;
    }

    /// <summary>
    /// Factory method to create a new Post as human-authored and in ACTIVE/VISIBLE state.
    /// Emits <see cref="PostCreated"/>.
    /// post-domain.md Business Rule 3 (content sufficient to understand communication),
    /// Business Rule 6 (system-generated vs human-authored).
    /// </summary>
    public static Post CreateHumanAuthored(
        Guid id,
        string title,
        string content,
        Guid authorPersonId,
        IReadOnlyList<(string ReferenceType, Guid TargetId)>? references = null,
        DateTime? createdAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("PostId cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Post content cannot be empty.", nameof(content));
        if (authorPersonId == Guid.Empty)
            throw new ArgumentException("AuthorPersonId cannot be empty.", nameof(authorPersonId));

        var effectiveCreatedAt = createdAt ?? DateTime.UtcNow;

        var post = new Post
        {
            Id = id,
            Title = title.Trim(),
            Content = content.Trim(),
            Source = PostSource.HumanAuthored,
            AuthorPersonId = authorPersonId,
            Status = PostStatus.Active,
            Visibility = PostVisibility.Visible,
            CreatedAt = effectiveCreatedAt
        };

        // Apply references
        if (references != null)
        {
            foreach (var (refType, targetId) in references)
            {
                var refId = Guid.NewGuid();
                var reference = PostReference.Create(
                    refId,
                    post.Id,
                    refType,
                    targetId,
                    effectiveCreatedAt);
                post._references.Add(reference);
            }
        }

        post.AddDomainEvent(new PostCreated(
            post.Id,
            post.Title,
            post.Content,
            post.Source,
            post.AuthorPersonId,
            post.Status,
            post.Visibility,
            post._references.Select(r => new PostReferencePayload(
                r.ReferenceType,
                r.TargetId)).ToList()!,
            post.CreatedAt));

        return post;
    }

    /// <summary>
    /// Factory method to create a Post as system-generated and in ACTIVE/VISIBLE state.
    /// Emits <see cref="PostCreated"/>.
    /// post-domain.md Business Rule 7 (system-generated Post must identify source).
    /// </summary>
    public static Post CreateSystemAuthored(
        Guid id,
        string title,
        string content,
        string sourceEventOrObject,
        DateTime? createdAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("PostId cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Post content cannot be empty.", nameof(content));
        if (string.IsNullOrWhiteSpace(sourceEventOrObject))
            throw new ArgumentException("Source event or source object cannot be empty.", nameof(sourceEventOrObject));

        var effectiveCreatedAt = createdAt ?? DateTime.UtcNow;

        var post = new Post
        {
            Id = id,
            Title = title.Trim(),
            Content = content.Trim(),
            Source = PostSource.SystemGenerated,
            AuthorPersonId = null,
            Status = PostStatus.Active,
            Visibility = PostVisibility.Visible,
            CreatedAt = effectiveCreatedAt
        };

        post.AddDomainEvent(new PostCreated(
            post.Id,
            post.Title,
            post.Content,
            post.Source,
            post.AuthorPersonId,
            post.Status,
            post.Visibility,
            post._references.Select(r => new PostReferencePayload(
                r.ReferenceType,
                r.TargetId)).ToList()!,
            post.CreatedAt));

        return post;
    }

    /// <summary>
    /// Appends a Post Reference to the Post.
    /// post-domain.md Business Rule 9 (Post may reference operational objects only through references).
    /// </summary>
    public void AddReference(string referenceType, Guid targetId, DateTime? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(referenceType))
            throw new ArgumentException("Reference type cannot be empty.", nameof(referenceType));

        var refTypeUpper = referenceType.ToUpperInvariant();
        if (!PostReference.ReferenceTypes.All.Contains(refTypeUpper, StringComparer.OrdinalIgnoreCase))
            throw new InvalidPostReferenceException(
                $"Invalid reference type '{referenceType}'. Must be one of: {string.Join(", ", PostReference.ReferenceTypes.All)}.");

        var effectiveCreatedAt = createdAt ?? DateTime.UtcNow;

        var refId = Guid.NewGuid();
        var reference = PostReference.Create(
            refId,
            Id,
            refTypeUpper,
            targetId,
            effectiveCreatedAt);

        _references.Add(reference);
        UpdatedAt = reference.CreatedAt;
    }

    /// <summary>
    /// Removes a Post Reference by its target ID.
    /// </summary>
    public void RemoveReference(string referenceType, Guid targetId)
    {
        var refTypeUpper = referenceType?.ToUpperInvariant() ?? string.Empty;
        var refToRemove = _references.FirstOrDefault(r =>
            r.ReferenceType == refTypeUpper && r.TargetId == targetId);

        if (refToRemove == null)
            return;

        _references.Remove(refToRemove);
        UpdatedAt = DateTime.UtcNow;
    }

    /// </summary>
    /// Updates Post content.
    /// Does not create a new lifecycle state (Architecture §8).
    /// </summary>
    public void UpdateContent(string newContent, DateTime updatedAt)
    {
        if (string.IsNullOrWhiteSpace(newContent))
            throw new ArgumentException("Post content cannot be empty.", nameof(newContent));

        Content = newContent.Trim();
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Changes Post visibility (VISIBLE ↔ HIDDEN).
    /// Visibility is an independent property of the current Post state, not part of the lifecycle.
    /// post-domain.md Business Rule 27 (Hiding a Post changes visibility, not existence).
    /// Emits <see cref="PostVisibilityChanged"/>.
    /// </summary>
    public void ChangeVisibility(string newVisibility, Guid changedByPersonId, DateTime changedAt)
    {
        if (changedByPersonId == Guid.Empty)
            throw new ArgumentException("ChangedByPersonId cannot be empty.", nameof(changedByPersonId));
        if (!PostVisibility.IsValid(newVisibility))
            throw new PostDomainException($"Invalid Post visibility '{newVisibility}'.");

        var previousVisibility = Visibility;
        var newVisibilityNormalized = newVisibility.Equals(PostVisibility.Hidden, StringComparison.OrdinalIgnoreCase)
            ? PostVisibility.Hidden
            : PostVisibility.Visible;

        if (previousVisibility == newVisibilityNormalized)
            return;

        Visibility = newVisibilityNormalized;
        UpdatedAt = changedAt;

        AddDomainEvent(new PostVisibilityChanged(
            Id,
            previousVisibility,
            newVisibilityNormalized,
            changedByPersonId,
            changedAt));
    }

    /// <summary>
    /// Archives the Post (soft-delete, preserves discussion history).
    /// post-domain.md Business Rule 28 (Archiving does not delete the Post).
    /// Architecture §20 (Permanent Retention: soft-delete/archive only).
    /// Emits <see cref="PostArchived"/>.
    /// </summary>
    public void Archive(Guid archivedByPersonId, DateTime archivedAt)
    {
        if (archivedByPersonId == Guid.Empty)
            throw new ArgumentException("ArchivedByPersonId cannot be empty.", nameof(archivedByPersonId));

        if (PostStatus.IsArchived(Status))
            throw new PostDomainException($"Post '{Id}' is already archived.");

        Status = PostStatus.Archived;
        ArchivedAt = archivedAt;
        UpdatedAt = archivedAt;

        AddDomainEvent(new PostArchived(
            Id,
            archivedByPersonId,
            archivedAt));
    }

    /// <summary>
    /// Restores an archived Post to active lifecycle.
    /// </summary>
    public void Restore(Guid restoredByPersonId, DateTime restoredAt)
    {
        if (restoredByPersonId == Guid.Empty)
            throw new ArgumentException("RestoredByPersonId cannot be empty.", nameof(restoredByPersonId));

        if (PostStatus.IsActive(Status))
            throw new PostDomainException($"Post '{Id}' is not archived.");

        Status = PostStatus.Active;
        ArchivedAt = null;
        UpdatedAt = restoredAt;
    }

    /// <summary>
    /// Adds a Comment to the Post discussion.
    /// Emits <see cref="CommentAdded"/> via the Comment entity.
    /// post-domain.md Business Rule 17 (Comments belong to exactly one Post).
    /// </summary>
    public void AddComment(string content, Guid authorPersonId, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Comment content cannot be empty.", nameof(content));
        if (authorPersonId == Guid.Empty)
            throw new ArgumentException("AuthorPersonId cannot be empty.", nameof(authorPersonId));

        var comment = Comment.Add(
            Guid.NewGuid(),
            Id,
            authorPersonId,
            content,
            createdAt);

        _comments.Add(comment);
        UpdatedAt = comment.CreatedAt;

        AddDomainEvent(new CommentAdded(
            comment.Id,
            Id,
            authorPersonId,
            content,
            createdAt));
    }

    /// <summary>
    /// Adds a Reaction to the Post (or Comment via comment overload).
    /// Emits <see cref="ReactionAdded"/>.
    /// post-domain.md Business Rule 21 (Reaction must identify the Person).
    /// Business Rule 22 (at most one Reaction of same type on same Post).
    /// </summary>
    public void AddReaction(Guid personId, string type, DateTime createdAt)
    {
        if (personId == Guid.Empty)
            throw new ArgumentException("PersonId cannot be empty.", nameof(personId));

        var typeUpper = type.ToUpperInvariant();

        // Check for duplicate reaction on same Post
        var existingPostReaction = _reactions.FirstOrDefault(r =>
            r.PostId == Id &&
            r.PersonId == personId &&
            r.Type == typeUpper);

        if (existingPostReaction != null)
            throw new InvalidReactionException(
                $"Person '{personId}' has already expressed a '{typeUpper}' reaction on this Post.");

        var reaction = Reaction.AddToPost(
            Guid.NewGuid(),
            Id,
            personId,
            typeUpper,
            createdAt);

        _reactions.Add(reaction);
        UpdatedAt = reaction.CreatedAt;

        AddDomainEvent(new ReactionAdded(
            reaction.Id,
            Id,
            null,
            personId,
            typeUpper,
            createdAt));
    }

    /// <summary>
    /// Removes a Reaction from the Post.
    /// Emits <see cref="ReactionRemoved"/>.
    /// </summary>
    public void RemoveReaction(Guid personId, string type, DateTime removedAt)
    {
        var typeUpper = type?.ToUpperInvariant() ?? string.Empty;

        var reaction = _reactions.FirstOrDefault(r =>
            r.PostId == Id &&
            r.PersonId == personId &&
            r.Type == typeUpper);

        if (reaction == null)
            throw new InvalidReactionException(
                $"No matching '{typeUpper}' reaction found for Person '{personId}' on Post '{Id}'.");

        reaction.Remove(removedAt);
        _reactions.Remove(reaction);
        UpdatedAt = removedAt;

        AddDomainEvent(new ReactionRemoved(
            reaction.Id,
            Id,
            null,
            personId,
            typeUpper,
            removedAt));
    }

    /// <summary>
    /// Determines whether the Post is currently archived.
    /// </summary>
    public bool IsArchived => PostStatus.IsArchived(Status);

    /// <summary>
    /// Determines whether the Post is currently active (not archived).
    /// </summary>
    public bool IsActive => PostStatus.IsActive(Status);

    /// <summary>
    /// Determines whether the Post is visible in normal operational views.
    /// </summary>
    public bool IsVisible => PostVisibility.IsVisible(this.Visibility);

    /// <summary>
    /// Determines whether the Post is hidden.
    /// </value>
    public bool IsHidden => PostVisibility.IsHidden(Visibility);

    /// <summary>
    /// Determines whether the Post has any references.
    /// </summary>
    public bool HasReferences => _references.Count > 0;

    /// <summary>
    /// Determines whether the Post has any comments.
    /// </summary>
    public bool HasComments => _comments.Count > 0;

    /// <summary>
    /// Determines whether the Post has any reactions.
    /// </summary>
    public bool HasReactions => _reactions.Count > 0;
}