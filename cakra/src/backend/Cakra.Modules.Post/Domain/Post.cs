using Cakra.Core;
using Cakra.Modules.Post.Domain.Events;
using Cakra.Modules.Post.Domain.Exceptions;

namespace Cakra.Modules.Post.Domain;

/// <summary>
/// Aggregate root for persistent operational communication, discussion comments, structured operational reactions,
/// and contextual references (Post Domain §5-§9; Architecture §6, §7, §8, §12, §17, §20, §21).
/// </summary>
public sealed class Post : EntityBase
{
    private readonly List<PostReference> _references = new();
    private readonly List<Comment> _comments = new();
    private readonly List<Reaction> _reactions = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid PostId => Id;

    public string Title { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    /// <summary>Convenience alias for <see cref="Content"/>.</summary>
    public string Body => Content;

    public Guid? AuthorPersonId { get; private set; }

    public PostSource Source { get; private set; } = PostSource.HumanAuthored;

    public string SourceName => Source.ToName();

    /// <summary>Convenience alias matching <c>FeedItems.PostType</c>.</summary>
    public string PostType => SourceName;

    public string? SourceEventType { get; private set; }

    public PostStatus Status { get; private set; } = PostStatus.Active;

    public string StatusName => Status.ToName();

    public PostVisibility Visibility { get; private set; } = PostVisibility.Visible;

    public string VisibilityName => Visibility.ToName();

    public bool IsException { get; private set; }

    public string? ExceptionType { get; private set; }

    public Guid? CustomerId { get; private set; }

    public Guid? ProductId { get; private set; }

    public Guid? RequestId { get; private set; }

    public Guid? WorkPackageId { get; private set; }

    public DateTime? ArchivedAt { get; private set; }

    public Guid? ArchivedByPersonId { get; private set; }

    public IReadOnlyList<PostReference> References => _references.AsReadOnly();

    public IReadOnlyList<PostReference> ActiveReferences =>
        _references.Where(r => r.IsActive).ToList().AsReadOnly();

    public IReadOnlyList<Comment> Comments => _comments.AsReadOnly();

    public IReadOnlyList<Comment> ActiveComments =>
        _comments.Where(c => c.IsActive).OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).ToList().AsReadOnly();

    public IReadOnlyList<Reaction> Reactions => _reactions.AsReadOnly();

    public IReadOnlyList<Reaction> ActiveReactions =>
        _reactions.Where(r => r.IsActive && r.RemovedAt is null).OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToList().AsReadOnly();

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Post()
    {
    }

    /// <summary>
    /// Creates a new human-authored operational Post in <c>ACTIVE</c> status and <c>VISIBLE</c> visibility
    /// (UC-FCOL-003, UC-COL-001, FEAT-FCOL-003).
    /// </summary>
    public static Post CreateOperationalPost(
        string title,
        string content,
        Guid authorPersonId,
        Guid? customerId = null,
        Guid? productId = null,
        Guid? requestId = null,
        Guid? workPackageId = null,
        bool isException = false,
        string? exceptionType = null,
        string? authorName = null,
        string? customerName = null,
        string? productName = null,
        string? primaryReferenceDisplay = null,
        IEnumerable<(string ReferenceType, Guid ReferenceId, string? ReferenceDisplay)>? additionalReferences = null,
        DateTime? createdAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new PostDomainValidationException("Post title cannot be null or whitespace.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new PostDomainValidationException("Post content cannot be null or whitespace.", nameof(content));
        }

        if (authorPersonId == Guid.Empty)
        {
            throw new PostDomainValidationException(
                "Human-authored operational post requires a non-empty AuthorPersonId.",
                nameof(authorPersonId));
        }

        var normalizedExceptionType = PostExceptionTypes.NormalizeAndValidate(exceptionType);
        var effectiveIsException = isException || normalizedExceptionType is not null;
        var now = createdAtUtc ?? DateTime.UtcNow;

        var post = new Post
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Content = content.Trim(),
            AuthorPersonId = authorPersonId,
            Source = PostSource.HumanAuthored,
            SourceEventType = null,
            Status = PostStatus.Active,
            Visibility = PostVisibility.Visible,
            IsException = effectiveIsException,
            ExceptionType = normalizedExceptionType,
            CustomerId = NormalizeOptionalGuid(customerId),
            ProductId = NormalizeOptionalGuid(productId),
            RequestId = NormalizeOptionalGuid(requestId),
            WorkPackageId = NormalizeOptionalGuid(workPackageId),
            CreatedAt = now,
            UpdatedAt = null
        };

        post.PopulateReferences(primaryReferenceDisplay, additionalReferences, now);
        post.RaisePostCreatedEvent(authorName, customerName, productName, now);

        return post;
    }

    /// <summary>
    /// Creates a new system-generated operational Post in <c>ACTIVE</c> status and <c>VISIBLE</c> visibility
    /// (Post Domain §5, §7 Rules 6-7; Architecture §7, §12).
    /// </summary>
    public static Post RecordSystemPost(
        string title,
        string content,
        Guid? authorPersonId = null,
        string? sourceEventType = null,
        Guid? customerId = null,
        Guid? productId = null,
        Guid? requestId = null,
        Guid? workPackageId = null,
        bool isException = false,
        string? exceptionType = null,
        string? authorName = null,
        string? customerName = null,
        string? productName = null,
        string? primaryReferenceDisplay = null,
        IEnumerable<(string ReferenceType, Guid ReferenceId, string? ReferenceDisplay)>? additionalReferences = null,
        DateTime? createdAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new PostDomainValidationException("System post title cannot be null or whitespace.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new PostDomainValidationException("System post content cannot be null or whitespace.", nameof(content));
        }

        var normalizedExceptionType = PostExceptionTypes.NormalizeAndValidate(exceptionType);
        var effectiveIsException = isException || normalizedExceptionType is not null;
        var now = createdAtUtc ?? DateTime.UtcNow;

        var post = new Post
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Content = content.Trim(),
            AuthorPersonId = NormalizeOptionalGuid(authorPersonId),
            Source = PostSource.SystemGenerated,
            SourceEventType = string.IsNullOrWhiteSpace(sourceEventType) ? null : sourceEventType.Trim(),
            Status = PostStatus.Active,
            Visibility = PostVisibility.Visible,
            IsException = effectiveIsException,
            ExceptionType = normalizedExceptionType,
            CustomerId = NormalizeOptionalGuid(customerId),
            ProductId = NormalizeOptionalGuid(productId),
            RequestId = NormalizeOptionalGuid(requestId),
            WorkPackageId = NormalizeOptionalGuid(workPackageId),
            CreatedAt = now,
            UpdatedAt = null
        };

        post.PopulateReferences(primaryReferenceDisplay, additionalReferences, now);
        post.RaisePostCreatedEvent(
            string.IsNullOrWhiteSpace(authorName) ? "SYSTEM" : authorName,
            customerName,
            productName,
            now);

        return post;
    }

    /// <summary>
    /// Appends a flat discussion <see cref="Comment"/> to the Post and emits <see cref="CommentAdded"/>
    /// (UC-FCOL-001, FEAT-FCOL-001; Post Domain Rules 17-20, 25).
    /// Requires the Post to be in <c>ACTIVE</c> status and <c>VISIBLE</c> visibility.
    /// </summary>
    public Comment AddComment(
        Guid authorPersonId,
        string content,
        string? authorName = null,
        DateTime? createdAtUtc = null)
    {
        EnsureCanAcceptDiscussion("comment on");

        var now = createdAtUtc ?? DateTime.UtcNow;
        var comment = Comment.Create(Id, authorPersonId, content, now);

        _comments.Add(comment);
        UpdatedAt = now;

        _domainEvents.Add(new CommentAdded(
            EventId: Guid.NewGuid(),
            CommentId: comment.Id,
            PostId: Id,
            AuthorPersonId: comment.AuthorPersonId,
            AuthorName: authorName,
            Content: comment.Content,
            CommentCount: ActiveComments.Count,
            OccurredAtUtc: now));

        return comment;
    }

    /// <summary>
    /// Adds (or idempotently retains / reactivates) a structured operational <see cref="Reaction"/> on the Post
    /// (UC-FCOL-002, FEAT-FCOL-002; Post Domain Rules 21-26).
    /// Duplicate active reactions for the same <c>(PostId, PersonId, ReactionType, CommentId)</c> are idempotent.
    /// </summary>
    public (Reaction Reaction, bool WasAddedOrReactivated) AddReaction(
        Guid personId,
        string reactionType,
        Guid? commentId = null,
        DateTime? createdAtUtc = null)
    {
        EnsureCanAcceptDiscussion("react to");

        if (personId == Guid.Empty)
        {
            throw new PostDomainValidationException("PersonId cannot be empty.", nameof(personId));
        }

        var normalizedType = PostReactionTypes.NormalizeAndValidate(reactionType);
        var now = createdAtUtc ?? DateTime.UtcNow;

        if (commentId.HasValue)
        {
            var targetComment = _comments.FirstOrDefault(c => c.Id == commentId.Value);
            if (targetComment is null || !targetComment.IsActive)
            {
                throw new InvalidPostStateException(
                    Id,
                    StatusName,
                    VisibilityName,
                    $"Target comment '{commentId.Value}' was not found or is not active on Post '{Id}'.");
            }
        }

        var existingActive = _reactions.FirstOrDefault(r =>
            r.PersonId == personId &&
            r.CommentId == commentId &&
            r.IsActive &&
            r.RemovedAt is null &&
            string.Equals(r.ReactionType, normalizedType, StringComparison.OrdinalIgnoreCase));

        if (existingActive is not null)
        {
            return (existingActive, false);
        }

        var existingRemoved = _reactions.FirstOrDefault(r =>
            r.PersonId == personId &&
            r.CommentId == commentId &&
            (!r.IsActive || r.RemovedAt is not null) &&
            string.Equals(r.ReactionType, normalizedType, StringComparison.OrdinalIgnoreCase));

        Reaction reaction;
        if (existingRemoved is not null)
        {
            existingRemoved.Reactivate(now);
            reaction = existingRemoved;
        }
        else
        {
            reaction = Reaction.Create(Id, personId, normalizedType, commentId, now);
            _reactions.Add(reaction);
        }

        UpdatedAt = now;

        _domainEvents.Add(new ReactionAdded(
            EventId: Guid.NewGuid(),
            ReactionId: reaction.Id,
            PostId: Id,
            PersonId: personId,
            ReactionType: normalizedType,
            ReactionCounts: ComputeReactionCounts(),
            OccurredAtUtc: now,
            CommentId: commentId));

        return (reaction, true);
    }

    /// <summary>
    /// Soft-removes an active <see cref="Reaction"/> on the Post without physical deletion
    /// (UC-FCOL-002, FEAT-FCOL-002; Architecture §20, §21 Permanent Data Retention).
    /// </summary>
    public Reaction? RemoveReaction(
        Guid personId,
        string reactionType,
        Guid? commentId = null,
        DateTime? removedAtUtc = null)
    {
        if (Status != PostStatus.Active)
        {
            throw new InvalidPostStateException(
                Id,
                StatusName,
                VisibilityName,
                $"Cannot modify reactions on Post '{Id}' because its status is '{StatusName}'.");
        }

        if (personId == Guid.Empty)
        {
            throw new PostDomainValidationException("PersonId cannot be empty.", nameof(personId));
        }

        var normalizedType = PostReactionTypes.NormalizeAndValidate(reactionType);
        var now = removedAtUtc ?? DateTime.UtcNow;

        var existingActive = _reactions.FirstOrDefault(r =>
            r.PersonId == personId &&
            r.CommentId == commentId &&
            r.IsActive &&
            r.RemovedAt is null &&
            string.Equals(r.ReactionType, normalizedType, StringComparison.OrdinalIgnoreCase));

        if (existingActive is null)
        {
            return null;
        }

        existingActive.MarkRemoved(now);
        UpdatedAt = now;

        _domainEvents.Add(new ReactionRemoved(
            EventId: Guid.NewGuid(),
            ReactionId: existingActive.Id,
            PostId: Id,
            PersonId: personId,
            ReactionType: normalizedType,
            ReactionCounts: ComputeReactionCounts(),
            OccurredAtUtc: now,
            CommentId: commentId));

        return existingActive;
    }

    /// <summary>
    /// Toggles or sets the <see cref="Visibility"/> of the Post between <c>VISIBLE</c> and <c>HIDDEN</c>
    /// without altering its <see cref="Status"/> or discussion history (Post Domain §7 Rule 27, §8; Architecture §7, §12).
    /// </summary>
    public PostVisibility ToggleVisibility(
        PostVisibility? targetVisibility = null,
        Guid? actorPersonId = null,
        DateTime? updatedAtUtc = null)
    {
        var previousVisibility = Visibility;
        var nextVisibility = targetVisibility ?? (Visibility == PostVisibility.Visible
            ? PostVisibility.Hidden
            : PostVisibility.Visible);

        var now = updatedAtUtc ?? DateTime.UtcNow;
        Visibility = nextVisibility;
        UpdatedAt = now;

        if (previousVisibility != nextVisibility || targetVisibility.HasValue)
        {
            _domainEvents.Add(new PostVisibilityChanged(
                EventId: Guid.NewGuid(),
                PostId: Id,
                PreviousVisibility: previousVisibility.ToName(),
                NewVisibility: nextVisibility.ToName(),
                ActorPersonId: NormalizeOptionalGuid(actorPersonId),
                OccurredAtUtc: now));
        }

        return Visibility;
    }

    /// <summary>
    /// Transitions the Post lifecycle status from <c>ACTIVE</c> to <c>ARCHIVED</c> while preserving
    /// all discussion comments, reactions, and references (Post Domain §7 Rules 28-30, §8; Architecture §7, §12, §20, §21).
    /// </summary>
    public void Archive(Guid? actorPersonId = null, DateTime? archivedAtUtc = null)
    {
        if (Status == PostStatus.Archived)
        {
            throw new InvalidPostStateException(
                Id,
                StatusName,
                VisibilityName,
                $"Post '{Id}' is already in '{StatusName}' status.");
        }

        var previousStatus = Status;
        var now = archivedAtUtc ?? DateTime.UtcNow;

        Status = PostStatus.Archived;
        ArchivedAt = now;
        ArchivedByPersonId = NormalizeOptionalGuid(actorPersonId);
        UpdatedAt = now;

        _domainEvents.Add(new PostArchived(
            EventId: Guid.NewGuid(),
            PostId: Id,
            PreviousStatus: previousStatus.ToName(),
            NewStatus: Status.ToName(),
            ActorPersonId: ArchivedByPersonId,
            OccurredAtUtc: now));
    }

    /// <summary>
    /// Computes aggregate counts of active reactions grouped by <see cref="Reaction.ReactionType"/>.
    /// </summary>
    public IReadOnlyDictionary<string, int> ComputeReactionCounts()
    {
        return ActiveReactions
            .GroupBy(r => r.ReactionType, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key.ToUpperInvariant(), g => g.Count(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves the primary contextual reference (<c>REQUEST</c> &gt; <c>WORK_PACKAGE</c> &gt; <c>CUSTOMER</c> &gt; <c>PRODUCT</c> &gt; <c>ORGANIZATION</c>)
    /// for feed projection cards (Architecture §12).
    /// </summary>
    public PostReference? GetPrimaryReference()
    {
        var active = ActiveReferences;
        if (active.Count == 0)
        {
            return null;
        }

        return active.FirstOrDefault(r => r.ReferenceType == PostReferenceTypes.Request)
            ?? active.FirstOrDefault(r => r.ReferenceType == PostReferenceTypes.WorkPackage)
            ?? active.FirstOrDefault(r => r.ReferenceType == PostReferenceTypes.Customer)
            ?? active.FirstOrDefault(r => r.ReferenceType == PostReferenceTypes.Product)
            ?? active[0];
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    public static Post Rehydrate(
        Guid id,
        string title,
        string content,
        Guid? authorPersonId,
        string source,
        string? sourceEventType,
        string status,
        string visibility,
        bool isException,
        string? exceptionType,
        Guid? customerId,
        Guid? productId,
        Guid? requestId,
        Guid? workPackageId,
        DateTime? archivedAt,
        Guid? archivedByPersonId,
        DateTime createdAt,
        DateTime? updatedAt = null,
        IEnumerable<PostReference>? references = null,
        IEnumerable<Comment>? comments = null,
        IEnumerable<Reaction>? reactions = null)
    {
        var post = new Post
        {
            Id = id,
            Title = title ?? string.Empty,
            Content = content ?? string.Empty,
            AuthorPersonId = authorPersonId,
            Source = PostSourceNames.FromName(string.IsNullOrWhiteSpace(source) ? PostSourceNames.HumanAuthored : source),
            SourceEventType = sourceEventType,
            Status = PostStatusNames.FromName(string.IsNullOrWhiteSpace(status) ? PostStatusNames.Active : status),
            Visibility = PostVisibilityNames.FromName(string.IsNullOrWhiteSpace(visibility) ? PostVisibilityNames.Visible : visibility),
            IsException = isException,
            ExceptionType = PostExceptionTypes.NormalizeAndValidate(exceptionType),
            CustomerId = customerId,
            ProductId = productId,
            RequestId = requestId,
            WorkPackageId = workPackageId,
            ArchivedAt = archivedAt,
            ArchivedByPersonId = archivedByPersonId,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        if (references is not null)
        {
            post._references.AddRange(references);
        }

        if (comments is not null)
        {
            post._comments.AddRange(comments);
        }

        if (reactions is not null)
        {
            post._reactions.AddRange(reactions);
        }

        return post;
    }

    private void EnsureCanAcceptDiscussion(string actionDescription)
    {
        if (Status != PostStatus.Active)
        {
            throw new InvalidPostStateException(
                Id,
                StatusName,
                VisibilityName,
                $"Cannot {actionDescription} Post '{Id}' because its status is '{StatusName}'.");
        }

        if (Visibility != PostVisibility.Visible)
        {
            throw new InvalidPostStateException(
                Id,
                StatusName,
                VisibilityName,
                $"Cannot {actionDescription} Post '{Id}' because its visibility is '{VisibilityName}'.");
        }
    }

    private void PopulateReferences(
        string? primaryReferenceDisplay,
        IEnumerable<(string ReferenceType, Guid ReferenceId, string? ReferenceDisplay)>? additionalReferences,
        DateTime now)
    {
        void AddUniqueReference(string refType, Guid refId, string? display)
        {
            var normalizedType = PostReferenceTypes.NormalizeAndValidate(refType);
            if (_references.Any(r => r.IsActive && r.ReferenceType == normalizedType && r.ReferenceId == refId))
            {
                return;
            }

            _references.Add(PostReference.Create(Id, normalizedType, refId, display, now));
        }

        if (RequestId.HasValue)
        {
            AddUniqueReference(PostReferenceTypes.Request, RequestId.Value, primaryReferenceDisplay);
        }

        if (WorkPackageId.HasValue)
        {
            AddUniqueReference(
                PostReferenceTypes.WorkPackage,
                WorkPackageId.Value,
                !RequestId.HasValue ? primaryReferenceDisplay : null);
        }

        if (CustomerId.HasValue)
        {
            AddUniqueReference(
                PostReferenceTypes.Customer,
                CustomerId.Value,
                !RequestId.HasValue && !WorkPackageId.HasValue ? primaryReferenceDisplay : null);
        }

        if (ProductId.HasValue)
        {
            AddUniqueReference(
                PostReferenceTypes.Product,
                ProductId.Value,
                !RequestId.HasValue && !WorkPackageId.HasValue && !CustomerId.HasValue ? primaryReferenceDisplay : null);
        }

        if (additionalReferences is not null)
        {
            foreach (var (refType, refId, display) in additionalReferences)
            {
                if (refId != Guid.Empty && !string.IsNullOrWhiteSpace(refType))
                {
                    AddUniqueReference(refType, refId, display);
                }
            }
        }
    }

    private void RaisePostCreatedEvent(
        string? authorName,
        string? customerName,
        string? productName,
        DateTime now)
    {
        var primaryRef = GetPrimaryReference();

        _domainEvents.Add(new PostCreated(
            EventId: Guid.NewGuid(),
            PostId: Id,
            Title: Title,
            Content: Content,
            AuthorPersonId: AuthorPersonId,
            AuthorName: authorName,
            Source: SourceName,
            Status: StatusName,
            Visibility: VisibilityName,
            IsException: IsException,
            ExceptionType: ExceptionType,
            ReferenceType: primaryRef?.ReferenceType,
            ReferenceId: primaryRef?.ReferenceId,
            ReferenceDisplay: primaryRef?.ReferenceDisplay,
            CustomerId: CustomerId,
            CustomerName: customerName,
            ProductId: ProductId,
            ProductName: productName,
            RequestId: RequestId,
            WorkPackageId: WorkPackageId,
            OccurredAtUtc: now,
            SourceEventType: SourceEventType));
    }

    private static Guid? NormalizeOptionalGuid(Guid? value)
        => value.HasValue && value.Value != Guid.Empty ? value.Value : null;
}
