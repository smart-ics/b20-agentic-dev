using Cakra.Modules.Post.Domain;

namespace Cakra.Modules.Post;

/// <summary>
/// Input model for attaching an explicit contextual reference to a <see cref="Domain.Post"/>
/// (Post Domain §5; Architecture §12, §15).
/// </summary>
public sealed record PostReferenceInput(
    string ReferenceType,
    Guid ReferenceId,
    string? ReferenceDisplay = null);

/// <summary>
/// Read-only data transfer object representing a <see cref="Domain.PostReference"/> record
/// (Architecture §7, §12, §15).
/// </summary>
public sealed record PostReferenceDto
{
    public Guid Id { get; init; }

    public Guid PostReferenceId
    {
        get => Id;
        init => Id = value;
    }

    public Guid PostId { get; init; }

    public string ReferenceType { get; init; } = string.Empty;

    public Guid ReferenceId { get; init; }

    public string? ReferenceDisplay { get; init; }

    public DateTime? RemovedAt { get; init; }

    public bool IsActive => RemovedAt is null;

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    internal static PostReferenceDto FromDomain(PostReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return new PostReferenceDto
        {
            Id = reference.Id,
            PostId = reference.PostId,
            ReferenceType = reference.ReferenceType,
            ReferenceId = reference.ReferenceId,
            ReferenceDisplay = reference.ReferenceDisplay,
            RemovedAt = reference.RemovedAt,
            CreatedAt = reference.CreatedAt,
            UpdatedAt = reference.UpdatedAt
        };
    }
}

/// <summary>
/// Read-only data transfer object representing a flat discussion <see cref="Domain.Comment"/>
/// (Architecture §7, §8 — UC-FCOL-001, SCR-POST-001).
/// </summary>
public sealed record CommentDto
{
    public Guid Id { get; init; }

    public Guid CommentId
    {
        get => Id;
        init => Id = value;
    }

    public Guid PostId { get; init; }

    public Guid AuthorPersonId { get; init; }

    public string? AuthorName { get; init; }

    /// <summary>Convenience alias for <see cref="AuthorName"/>.</summary>
    public string? Author => AuthorName;

    public string Content { get; init; } = string.Empty;

    public string Status { get; init; } = Comment.StatusActive;

    public bool IsActive => string.Equals(Status, Comment.StatusActive, StringComparison.OrdinalIgnoreCase);

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    internal static CommentDto FromDomain(Comment comment, string? authorName = null)
    {
        ArgumentNullException.ThrowIfNull(comment);

        return new CommentDto
        {
            Id = comment.Id,
            PostId = comment.PostId,
            AuthorPersonId = comment.AuthorPersonId,
            AuthorName = authorName,
            Content = comment.Content,
            Status = comment.Status,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt
        };
    }
}

/// <summary>
/// Read-only data transfer object representing a structured operational <see cref="Domain.Reaction"/>
/// (Architecture §7, §8 — UC-FCOL-002, SCR-POST-001).
/// </summary>
public sealed record ReactionDto
{
    public Guid Id { get; init; }

    public Guid ReactionId
    {
        get => Id;
        init => Id = value;
    }

    public Guid PostId { get; init; }

    public Guid? CommentId { get; init; }

    public Guid PersonId { get; init; }

    public string? PersonName { get; init; }

    public string ReactionType { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;

    public DateTime? RemovedAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    internal static ReactionDto FromDomain(Reaction reaction, string? personName = null)
    {
        ArgumentNullException.ThrowIfNull(reaction);

        return new ReactionDto
        {
            Id = reaction.Id,
            PostId = reaction.PostId,
            CommentId = reaction.CommentId,
            PersonId = reaction.PersonId,
            PersonName = personName,
            ReactionType = reaction.ReactionType,
            IsActive = reaction.IsActive && reaction.RemovedAt is null,
            RemovedAt = reaction.RemovedAt,
            CreatedAt = reaction.CreatedAt,
            UpdatedAt = reaction.UpdatedAt
        };
    }
}

/// <summary>
/// Read-only data transfer object representing a <see cref="Domain.Post"/> and its thread discussion
/// (Architecture §7, §8, §12 — SCR-POST-001, SCR-FEED-001).
/// </summary>
public record PostDto
{
    public Guid Id { get; init; }

    public Guid PostId
    {
        get => Id;
        init => Id = value;
    }

    public string Title { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    /// <summary>Convenience alias for <see cref="Content"/> (SCR-POST-001).</summary>
    public string Body => Content;

    public Guid? AuthorPersonId { get; init; }

    public string? AuthorName { get; init; }

    /// <summary>Convenience alias for <see cref="AuthorName"/>.</summary>
    public string? Author => AuthorName;

    public string Source { get; init; } = PostSourceNames.HumanAuthored;

    /// <summary>Convenience alias matching <c>FeedItems.PostType</c>.</summary>
    public string PostType => Source;

    public string? SourceEventType { get; init; }

    public string Status { get; init; } = PostStatusNames.Active;

    public string Visibility { get; init; } = PostVisibilityNames.Visible;

    public bool IsException { get; init; }

    public string? ExceptionType { get; init; }

    public Guid? CustomerId { get; init; }

    public string? CustomerName { get; init; }

    public string? CustomerCode { get; init; }

    public Guid? ProductId { get; init; }

    public string? ProductName { get; init; }

    public string? ProductCode { get; init; }

    public Guid? RequestId { get; init; }

    public string? RequestTitle { get; init; }

    public Guid? WorkPackageId { get; init; }

    public string? WorkPackageName { get; init; }

    public string? ReferenceType { get; init; }

    public Guid? ReferenceId { get; init; }

    public string? ReferenceDisplay { get; init; }

    public DateTime? ArchivedAt { get; init; }

    public Guid? ArchivedByPersonId { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public IReadOnlyList<PostReferenceDto> References { get; init; } = Array.Empty<PostReferenceDto>();

    public IReadOnlyList<CommentDto> Comments { get; init; } = Array.Empty<CommentDto>();

    public IReadOnlyList<ReactionDto> Reactions { get; init; } = Array.Empty<ReactionDto>();

    public IReadOnlyDictionary<string, int> ReactionCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public int CommentCount => Comments.Count;

    public int ReactionCount => Reactions.Count;

    internal static PostThreadDetailsDto FromDomain(
        Domain.Post post,
        string? authorName = null,
        string? customerName = null,
        string? productName = null,
        string? requestTitle = null,
        string? workPackageName = null)
    {
        ArgumentNullException.ThrowIfNull(post);

        var primaryRef = post.GetPrimaryReference();
        var references = post.ActiveReferences.Select(PostReferenceDto.FromDomain).ToList();
        var comments = post.ActiveComments.Select(c => CommentDto.FromDomain(c)).ToList();
        var reactions = post.ActiveReactions.Select(r => ReactionDto.FromDomain(r)).ToList();

        return new PostThreadDetailsDto
        {
            Id = post.Id,
            Title = post.Title,
            Content = post.Content,
            AuthorPersonId = post.AuthorPersonId,
            AuthorName = authorName ?? (post.Source == PostSource.SystemGenerated ? "SYSTEM" : null),
            Source = post.SourceName,
            SourceEventType = post.SourceEventType,
            Status = post.StatusName,
            Visibility = post.VisibilityName,
            IsException = post.IsException,
            ExceptionType = post.ExceptionType,
            CustomerId = post.CustomerId,
            CustomerName = customerName,
            ProductId = post.ProductId,
            ProductName = productName,
            RequestId = post.RequestId,
            RequestTitle = requestTitle,
            WorkPackageId = post.WorkPackageId,
            WorkPackageName = workPackageName,
            ReferenceType = primaryRef?.ReferenceType,
            ReferenceId = primaryRef?.ReferenceId,
            ReferenceDisplay = primaryRef?.ReferenceDisplay,
            ArchivedAt = post.ArchivedAt,
            ArchivedByPersonId = post.ArchivedByPersonId,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt,
            References = references,
            Comments = comments,
            Reactions = reactions,
            ReactionCounts = post.ComputeReactionCounts()
        };
    }
}

/// <summary>
/// Complete post thread detail projection returned by <see cref="IPostQueryService.GetPostThreadDetailsAsync"/>
/// (Architecture §7 — SCR-POST-001).
/// </summary>
public sealed record PostThreadDetailsDto : PostDto;
