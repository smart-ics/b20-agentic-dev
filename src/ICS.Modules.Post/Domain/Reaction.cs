namespace ICS.Modules.Post.Domain;

using ICS.Core.Domain;
using ICS.Modules.Post.Domain.Events;
using ICS.Modules.Post.Domain.Exceptions;

/// <summary>
/// Represents a structured response to a Post or Comment indicating an operational meaning.
/// A Reaction records that a Person expressed a defined operational response.
/// Generic social-media reaction concepts such as Like, Love, or Haha are not part of the domain language.
/// post-domain.md §5 (Reaction), §7 (Business Rules 21-26).
/// </summary>
public class Reaction : Entity
{
    /// <summary>
    /// Valid operational reaction types.
    /// </summary>
    public static class Types
    {
        public const string Seen = "SEEN";
        public const string Experienced = "EXPERIENCED";
        public const string HaveIdea = "HAVE_IDEA";
        public const string SimilarIssue = "SIMILAR_ISSUE";
        public const string Duplicate = "DUPLICATE";
        public const string NeedClarification = "NEED_CLARIFICATION";

        public static readonly IReadOnlyList<string> All =
            [Seen, Experienced, HaveIdea, SimilarIssue, Duplicate, NeedClarification];
    }

    public Guid? PostId { get; private set; }
    public Guid? CommentId { get; private set; }
    public Guid PersonId { get; private set; } = Guid.Empty;
    public string Type { get; private set; } = string.Empty;
    public string Status { get; private set; } = "ACTIVE";

    /// <summary>
    /// Parameterless constructor for Dapper persistence mapping.
    /// </summary>
    protected Reaction() { }

    /// <summary>
    /// Full constructor for hydrating the entity from persistence without raising domain events.
    /// </summary>
    public Reaction(
        Guid id,
        Guid? postId,
        Guid? commentId,
        Guid personId,
        string type,
        string status,
        DateTime createdAt)
        : base(id)
    {
        Id = id;
        PostId = postId;
        CommentId = commentId;
        PersonId = personId;
        Type = type;
        Status = status;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Factory method to create a Reaction on a Post.
    /// Emits <see cref="ReactionAdded"/>.
    /// </summary>
    public static Reaction AddToPost(
        Guid id,
        Guid postId,
        Guid personId,
        string type,
        DateTime createdAt)
    {
        ValidateReaction(id, postId, null, personId, type, createdAt);

        var reaction = new Reaction
        {
            Id = id,
            PostId = postId,
            CommentId = null,
            PersonId = personId,
            Type = type.ToUpperInvariant(),
            Status = "ACTIVE",
            CreatedAt = createdAt
        };

        reaction.AddDomainEvent(new ReactionAdded(
            reaction.Id,
            postId,
            null,
            reaction.PersonId,
            reaction.Type,
            createdAt));

        return reaction;
    }

    /// <summary>
    /// Factory method to create a Reaction on a Comment.
    /// Emits <see cref="ReactionAdded"/>.
    /// </summary>
    public static Reaction AddToComment(
        Guid id,
        Guid commentId,
        Guid personId,
        string type,
        DateTime createdAt)
    {
        ValidateReaction(id, null, commentId, personId, type, createdAt);

        var reaction = new Reaction
        {
            Id = id,
            PostId = null,
            CommentId = commentId,
            PersonId = personId,
            Type = type.ToUpperInvariant(),
            Status = "ACTIVE",
            CreatedAt = createdAt
        };

        reaction.AddDomainEvent(new ReactionAdded(
            reaction.Id,
            null,
            commentId,
            reaction.PersonId,
            reaction.Type,
            createdAt));

        return reaction;
    }

    /// <summary>
    /// Removes the Reaction.
    /// Emits <see cref="ReactionRemoved"/>.
    /// </summary>
    public void Remove(DateTime removedAt)
    {
        Status = "DELETED";
        UpdatedAt = removedAt;

        AddDomainEvent(new ReactionRemoved(
            Id,
            PostId,
            CommentId,
            PersonId,
            Type,
            removedAt));
    }

    /// <summary>
    /// Gets the target identifier (PostId or CommentId) for this reaction.
    /// </summary>
    public Guid? TargetId => PostId ?? CommentId;

    /// <summary>
    /// Determines whether this reaction targets a Post.
    /// </summary>
    public bool IsForPost => PostId.HasValue;

    /// <summary>
    /// Determines whether this reaction targets a Comment.
    /// </summary>
    public bool IsForComment => CommentId.HasValue;

    private static void ValidateReaction(
        Guid id,
        Guid? postId,
        Guid? commentId,
        Guid personId,
        string type,
        DateTime createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("ReactionId cannot be empty.", nameof(id));
        if (!postId.HasValue && !commentId.HasValue)
            throw new InvalidReactionException("A Reaction must target either a Post or a Comment.");
        if (postId.HasValue && commentId.HasValue)
            throw new InvalidReactionException("A Reaction cannot target both a Post and a Comment simultaneously.");
        if (personId == Guid.Empty)
            throw new ArgumentException("PersonId cannot be empty.", nameof(personId));
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Reaction type cannot be empty.", nameof(type));
        if (!Types.All.Contains(type, StringComparer.OrdinalIgnoreCase))
            throw new InvalidReactionException(
                $"Invalid reaction type '{type}'. Must be one of: {string.Join(", ", Types.All)}.");
    }
}