using System.Diagnostics;
using Cakra.Core;
using Cakra.Modules.Post;
using Cakra.Modules.Post.Domain;
using Cakra.Modules.Post.Domain.Exceptions;
using Cakra.Modules.Post.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;

namespace Cakra.Api.Controllers;

/// <summary>
/// REST API controller for the Post module (<c>SCR-POST-001</c>, <c>SCR-FEED-001</c>,
/// <c>UC-FCOL-001..004</c>, <c>UC-COL-001</c>, <c>FEAT-FCOL-001..003</c>;
/// Architecture §6, §7, §8, §9, §12, §15, §18, §19.5, §19.6, §20, §21).
/// Exposes <c>/api/v1/posts/*</c> endpoints protected by <see cref="AuthorizeAttribute"/>,
/// wiring <see cref="PostService"/> (<see cref="IPostService"/>) and
/// <see cref="PostQueryService"/> (<see cref="IPostQueryService"/>) to HTTP endpoints.
/// </summary>
[Authorize]
[Route("api/v1/posts")]
public sealed class PostsController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPostQueryService _postQueryService;
    private readonly ICurrentContextProvider _currentContextProvider;
    private readonly ILogger<PostsController> _logger;

    public PostsController(
        IMediator mediator,
        IPostQueryService postQueryService,
        ICurrentContextProvider currentContextProvider,
        ILogger<PostsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _postQueryService = postQueryService ?? throw new ArgumentNullException(nameof(postQueryService));
        _currentContextProvider = currentContextProvider ?? throw new ArgumentNullException(nameof(currentContextProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves full thread details for a Post, including active references, ordered comments,
    /// active reactions, and aggregated reaction counts
    /// (<c>PostQueryService.GetPostThreadDetails</c>; Architecture §7 — <c>SCR-POST-001</c>).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PostThreadDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PostThreadDetailsDto>> GetPostThreadDetails(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var post = await _mediator.Send(new GetPostThreadDetailsQuery(id), cancellationToken)
            ?? throw new KeyNotFoundException($"Post '{id}' was not found.");

        return Ok(post);
    }

    /// <summary>
    /// Retrieves all active comments for the specified Post in chronological order
    /// (<c>PostQueryService.GetFullComments</c>; Architecture §7 — <c>SCR-POST-001</c>, <c>UC-FCOL-001</c>).
    /// </summary>
    [HttpGet("{id:guid}/comments")]
    [ProducesResponseType(typeof(IReadOnlyList<CommentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CommentDto>>> GetFullComments(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var comments = await _mediator.Send(new GetFullCommentsQuery(id), cancellationToken);
        if (comments.Count == 0 && !await _postQueryService.PostExistsAsync(id, cancellationToken))
        {
            throw new KeyNotFoundException($"Post '{id}' was not found.");
        }

        return Ok(comments);
    }

    /// <summary>
    /// Retrieves all active (non-removed) reactions for the specified Post in chronological order
    /// (<c>PostQueryService.GetReactionList</c>; Architecture §7 — <c>SCR-POST-001</c>, <c>UC-FCOL-002</c>).
    /// </summary>
    [HttpGet("{id:guid}/reactions")]
    [ProducesResponseType(typeof(IReadOnlyList<ReactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ReactionDto>>> GetReactionList(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var reactions = await _mediator.Send(new GetReactionListQuery(id), cancellationToken);
        if (reactions.Count == 0 && !await _postQueryService.PostExistsAsync(id, cancellationToken))
        {
            throw new KeyNotFoundException($"Post '{id}' was not found.");
        }

        return Ok(reactions);
    }

    /// <summary>
    /// Retrieves active posts associated with a given contextual reference (<c>REQUEST</c>,
    /// <c>WORK_PACKAGE</c>, <c>CUSTOMER</c>, or <c>PRODUCT</c>) (<c>PostQueryService.GetPostsByReference</c>;
    /// Architecture §7, §8 — <c>UC-COL-001</c>).
    /// </summary>
    [HttpGet("by-reference")]
    [ProducesResponseType(typeof(IReadOnlyList<PostThreadDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPostsByReference(
        [FromQuery] string? referenceType = null,
        [FromQuery] Guid? referenceId = null,
        [FromQuery] Guid? requestId = null,
        [FromQuery] Guid? workPackageId = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] Guid? productId = null,
        CancellationToken cancellationToken = default)
    {
        string? resolvedType = FirstNonWhiteSpace(referenceType);
        Guid? resolvedId = FirstNonEmptyGuid(referenceId);

        if (!resolvedId.HasValue)
        {
            if (requestId.HasValue && requestId.Value != Guid.Empty)
            {
                resolvedType ??= PostReferenceTypes.Request;
                resolvedId = requestId.Value;
            }
            else if (workPackageId.HasValue && workPackageId.Value != Guid.Empty)
            {
                resolvedType ??= PostReferenceTypes.WorkPackage;
                resolvedId = workPackageId.Value;
            }
            else if (customerId.HasValue && customerId.Value != Guid.Empty)
            {
                resolvedType ??= PostReferenceTypes.Customer;
                resolvedId = customerId.Value;
            }
            else if (productId.HasValue && productId.Value != Guid.Empty)
            {
                resolvedType ??= PostReferenceTypes.Product;
                resolvedId = productId.Value;
            }
        }

        if (string.IsNullOrWhiteSpace(resolvedType) || !resolvedId.HasValue)
        {
            return CreateBadRequestProblem("Both referenceType and referenceId are required.");
        }

        try
        {
            var items = await _postQueryService.GetPostsByReferenceAsync(
                resolvedType,
                resolvedId.Value,
                cancellationToken);
            return Ok(items);
        }
        catch (Exception ex) when (ex is PostDomainException or InvalidOperationException or ArgumentException)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Creates a new human-authored operational Post and synchronously projects it into <c>post.FeedItems</c>
    /// (<c>PostService.CreateOperationalPost</c>; Architecture §7, §8, §12 — <c>UC-FCOL-003</c>, <c>UC-COL-001</c>).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PostThreadDetailsDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateOperationalPost(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateOperationalPostBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedAuthorPersonId = FirstNonEmptyGuid(
            request?.AuthorPersonId,
            request?.AuthorId,
            request?.PersonId,
            _currentContextProvider.CurrentPersonId);

        var resolvedReferences = request?.BuildReferencesList();

        var command = new CreateOperationalPostCommand(
            Title: request?.ResolvedTitle ?? string.Empty,
            Content: request?.ResolvedContent ?? string.Empty,
            AuthorPersonId: resolvedAuthorPersonId,
            CustomerId: request?.ResolvedCustomerId,
            ProductId: request?.ResolvedProductId,
            RequestId: request?.ResolvedRequestId,
            WorkPackageId: request?.ResolvedWorkPackageId,
            IsException: request?.IsException ?? false,
            ExceptionType: FirstNonWhiteSpace(request?.ExceptionType),
            References: resolvedReferences);

        try
        {
            var created = await _mediator.Send(command, cancellationToken);
            var enriched = await _postQueryService.GetPostThreadDetailsAsync(created.Id, cancellationToken) ?? created;

            _logger.LogInformation(
                "Created operational post '{PostId}' ({Title}) by author '{AuthorPersonId}' (RequestId={RequestId}, CustomerId={CustomerId}, ProductId={ProductId}).",
                enriched.Id,
                enriched.Title,
                enriched.AuthorPersonId,
                enriched.RequestId,
                enriched.CustomerId,
                enriched.ProductId);

            return CreatedAtAction(nameof(GetPostThreadDetails), new { id = enriched.Id }, enriched);
        }
        catch (Exception ex) when (ex is InvalidOperationException or PostDomainException or ArgumentException)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Adds a discussion comment to an active, visible Post and updates <c>post.FeedItems</c>
    /// (<c>PostService.PostComment</c>; Architecture §7, §8, §12 — <c>UC-FCOL-001</c>, <c>FEAT-FCOL-001</c>, <c>SCR-POST-001</c>).
    /// </summary>
    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PostComment(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PostCommentBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedAuthorPersonId = FirstNonEmptyGuid(
            request?.AuthorPersonId,
            request?.AuthorId,
            request?.PersonId,
            _currentContextProvider.CurrentPersonId);

        var command = new PostCommentCommand(
            PostId: id,
            Content: request?.ResolvedContent ?? string.Empty,
            AuthorPersonId: resolvedAuthorPersonId);

        try
        {
            var comment = await _mediator.Send(command, cancellationToken);

            _logger.LogInformation(
                "Added comment '{CommentId}' to post '{PostId}' by author '{AuthorPersonId}'.",
                comment.Id,
                id,
                comment.AuthorPersonId);

            return CreatedAtAction(nameof(GetFullComments), new { id }, comment);
        }
        catch (Exception ex) when (ex is InvalidOperationException or PostDomainException or ArgumentException)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Adds (or idempotently retains / reactivates) a structured operational reaction on an active, visible Post
    /// (<c>PostService.AddReaction</c>; Architecture §7, §8, §12 — <c>UC-FCOL-002</c>, <c>FEAT-FCOL-002</c>, <c>SCR-POST-001</c>).
    /// </summary>
    [HttpPost("{id:guid}/reactions")]
    [ProducesResponseType(typeof(ReactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddReaction(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AddReactionBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedPersonId = FirstNonEmptyGuid(
            request?.PersonId,
            request?.AuthorPersonId,
            request?.ActorPersonId,
            _currentContextProvider.CurrentPersonId);

        var command = new AddReactionCommand(
            PostId: id,
            ReactionType: request?.ResolvedReactionType ?? string.Empty,
            PersonId: resolvedPersonId,
            CommentId: FirstNonEmptyGuid(request?.CommentId));

        try
        {
            var reaction = await _mediator.Send(command, cancellationToken);

            _logger.LogInformation(
                "Added reaction '{ReactionType}' ({ReactionId}) on post '{PostId}' by person '{PersonId}'.",
                reaction.ReactionType,
                reaction.Id,
                id,
                reaction.PersonId);

            return Ok(reaction);
        }
        catch (Exception ex) when (ex is InvalidOperationException or PostDomainException or ArgumentException)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Soft-removes an active reaction from a Post without physical deletion
    /// (<c>PostService.RemoveReaction</c>; Architecture §7, §8, §12, §20, §21 — <c>UC-FCOL-002</c>, <c>SCR-POST-001</c>).
    /// </summary>
    [HttpDelete("{id:guid}/reactions/{reactionType}")]
    [ProducesResponseType(typeof(PostThreadDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveReaction(
        Guid id,
        string reactionType,
        [FromQuery] Guid? personId = null,
        [FromQuery] Guid? commentId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedPersonId = FirstNonEmptyGuid(
            personId,
            _currentContextProvider.CurrentPersonId);

        var command = new RemoveReactionCommand(
            PostId: id,
            ReactionType: reactionType ?? string.Empty,
            PersonId: resolvedPersonId,
            CommentId: FirstNonEmptyGuid(commentId));

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _postQueryService.GetPostThreadDetailsAsync(id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Soft-removed reaction '{ReactionType}' from post '{PostId}' by person '{PersonId}'.",
                reactionType,
                id,
                resolvedPersonId);

            return Ok(enriched);
        }
        catch (Exception ex) when (ex is InvalidOperationException or PostDomainException or ArgumentException)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Toggles a Post's visibility between <c>VISIBLE</c> and <c>HIDDEN</c> (or sets it to a specific target visibility)
    /// (<c>PostService.TogglePostVisibility</c>; Architecture §7, §12).
    /// </summary>
    [HttpPost("{id:guid}/visibility")]
    [HttpPut("{id:guid}/visibility")]
    [ProducesResponseType(typeof(PostThreadDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> TogglePostVisibility(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TogglePostVisibilityBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedActorPersonId = FirstNonEmptyGuid(
            request?.ActorPersonId,
            request?.PersonId,
            _currentContextProvider.CurrentPersonId);

        var command = new TogglePostVisibilityCommand(
            PostId: id,
            Visibility: FirstNonWhiteSpace(request?.Visibility),
            ActorPersonId: resolvedActorPersonId);

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _postQueryService.GetPostThreadDetailsAsync(id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Updated visibility of post '{PostId}' to '{Visibility}' by actor '{ActorPersonId}'.",
                id,
                enriched.Visibility,
                resolvedActorPersonId);

            return Ok(enriched);
        }
        catch (Exception ex) when (ex is InvalidOperationException or PostDomainException or ArgumentException)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Archives an active Post (<c>ACTIVE -&gt; ARCHIVED</c>) while preserving its discussion history
    /// (<c>PostService.ArchivePost</c>; Architecture §7, §12, §20, §21).
    /// </summary>
    [HttpPost("{id:guid}/archive")]
    [HttpPut("{id:guid}/archive")]
    [ProducesResponseType(typeof(PostThreadDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ArchivePost(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ArchivePostBody? request = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedActorPersonId = FirstNonEmptyGuid(
            request?.ActorPersonId,
            request?.PersonId,
            _currentContextProvider.CurrentPersonId);

        var command = new ArchivePostCommand(
            PostId: id,
            ActorPersonId: resolvedActorPersonId);

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var enriched = await _postQueryService.GetPostThreadDetailsAsync(id, cancellationToken) ?? updated;

            _logger.LogInformation(
                "Archived post '{PostId}' to status '{Status}' by actor '{ActorPersonId}'.",
                id,
                enriched.Status,
                resolvedActorPersonId);

            return Ok(enriched);
        }
        catch (Exception ex) when (ex is InvalidOperationException or PostDomainException or ArgumentException)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    private ObjectResult CreateBadRequestProblem(string detail)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        };
        problem.Extensions["errorCode"] = "BAD_REQUEST";
        problem.Extensions["traceId"] = traceId;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static Guid? FirstNonEmptyGuid(params Guid?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.HasValue && candidate.Value != Guid.Empty)
            {
                return candidate.Value;
            }
        }

        return null;
    }

    private static string? FirstNonWhiteSpace(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/posts</c> (<c>CreateOperationalPost</c>).
    /// Accepts both <c>content</c> and <c>body</c>, and resolves <c>authorPersonId</c> from payload
    /// or falls back to the authenticated person context.
    /// </summary>
    public sealed class CreateOperationalPostBody
    {
        public string? Title { get; set; }
        public string? Subject { get; set; }
        public string? Content { get; set; }
        public string? Body { get; set; }
        public string? Text { get; set; }
        public string? Description { get; set; }
        public string? Summary { get; set; }
        public Guid? AuthorPersonId { get; set; }
        public Guid? AuthorId { get; set; }
        public Guid? PersonId { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? Customer { get; set; }
        public Guid? ProductId { get; set; }
        public Guid? Product { get; set; }
        public Guid? RequestId { get; set; }
        public Guid? Request { get; set; }
        public Guid? WorkPackageId { get; set; }
        public Guid? WorkPackage { get; set; }
        public string? ReferenceType { get; set; }
        public Guid? ReferenceId { get; set; }
        public string? ReferenceDisplay { get; set; }
        public bool? IsException { get; set; }
        public string? ExceptionType { get; set; }
        public List<PostReferenceItemBody>? References { get; set; }

        public string ResolvedTitle =>
            FirstNonWhiteSpace(Title, Subject) ?? Title ?? Subject ?? string.Empty;

        public string ResolvedContent =>
            FirstNonWhiteSpace(Content, Body, Text, Description, Summary)
            ?? Content
            ?? Body
            ?? string.Empty;

        public Guid? ResolvedCustomerId =>
            FirstNonEmptyGuid(
                CustomerId,
                Customer,
                MatchesTopLevelReference(PostReferenceTypes.Customer) ? ReferenceId : null);

        public Guid? ResolvedProductId =>
            FirstNonEmptyGuid(
                ProductId,
                Product,
                MatchesTopLevelReference(PostReferenceTypes.Product) ? ReferenceId : null);

        public Guid? ResolvedRequestId =>
            FirstNonEmptyGuid(
                RequestId,
                Request,
                MatchesTopLevelReference(PostReferenceTypes.Request) ? ReferenceId : null);

        public Guid? ResolvedWorkPackageId =>
            FirstNonEmptyGuid(
                WorkPackageId,
                WorkPackage,
                MatchesTopLevelReference(PostReferenceTypes.WorkPackage) ? ReferenceId : null);

        public IReadOnlyList<PostReferenceInput>? BuildReferencesList()
        {
            var list = new List<PostReferenceInput>();

            if (!string.IsNullOrWhiteSpace(ReferenceType) &&
                ReferenceId.HasValue &&
                ReferenceId.Value != Guid.Empty)
            {
                list.Add(new PostReferenceInput(
                    ReferenceType.Trim(),
                    ReferenceId.Value,
                    FirstNonWhiteSpace(ReferenceDisplay)));
            }

            if (References is not null)
            {
                foreach (var item in References)
                {
                    var refType = FirstNonWhiteSpace(item?.ReferenceType, item?.Type);
                    var refId = FirstNonEmptyGuid(item?.ReferenceId, item?.Id);
                    if (!string.IsNullOrWhiteSpace(refType) && refId.HasValue)
                    {
                        list.Add(new PostReferenceInput(
                            refType,
                            refId.Value,
                            FirstNonWhiteSpace(item?.ReferenceDisplay, item?.Display)));
                    }
                }
            }

            return list.Count > 0 ? list : null;
        }

        private bool MatchesTopLevelReference(string expectedType)
        {
            if (string.IsNullOrWhiteSpace(ReferenceType) || !ReferenceId.HasValue || ReferenceId.Value == Guid.Empty)
            {
                return false;
            }

            var normalized = ReferenceType.Trim().Replace("-", "_", StringComparison.Ordinal).ToUpperInvariant();
            if (normalized == "WORKPACKAGE")
            {
                normalized = PostReferenceTypes.WorkPackage;
            }

            return string.Equals(normalized, expectedType, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Contextual reference item within <see cref="CreateOperationalPostBody.References"/>.
    /// </summary>
    public sealed class PostReferenceItemBody
    {
        public string? ReferenceType { get; set; }
        public string? Type { get; set; }
        public Guid? ReferenceId { get; set; }
        public Guid? Id { get; set; }
        public string? ReferenceDisplay { get; set; }
        public string? Display { get; set; }
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/posts/{id}/comments</c> (<c>PostComment</c>).
    /// Accepts both <c>content</c> and <c>body</c>, and resolves <c>authorPersonId</c> from payload
    /// or falls back to the authenticated person context.
    /// </summary>
    public sealed class PostCommentBody
    {
        public string? Content { get; set; }
        public string? Body { get; set; }
        public string? Text { get; set; }
        public string? Comment { get; set; }
        public Guid? AuthorPersonId { get; set; }
        public Guid? AuthorId { get; set; }
        public Guid? PersonId { get; set; }

        public string ResolvedContent =>
            FirstNonWhiteSpace(Content, Body, Text, Comment)
            ?? Content
            ?? Body
            ?? string.Empty;
    }

    /// <summary>
    /// Request payload for <c>POST /api/v1/posts/{id}/reactions</c> (<c>AddReaction</c>).
    /// Accepts <c>reactionType</c> or <c>type</c>, and resolves <c>personId</c> from payload
    /// or falls back to the authenticated person context.
    /// </summary>
    public sealed class AddReactionBody
    {
        public string? ReactionType { get; set; }
        public string? Type { get; set; }
        public string? Reaction { get; set; }
        public Guid? PersonId { get; set; }
        public Guid? AuthorPersonId { get; set; }
        public Guid? ActorPersonId { get; set; }
        public Guid? CommentId { get; set; }

        public string ResolvedReactionType =>
            FirstNonWhiteSpace(ReactionType, Type, Reaction)
            ?? ReactionType
            ?? Type
            ?? string.Empty;
    }

    /// <summary>
    /// Request payload for <c>POST/PUT /api/v1/posts/{id}/visibility</c> (<c>TogglePostVisibility</c>).
    /// </summary>
    public sealed class TogglePostVisibilityBody
    {
        public string? Visibility { get; set; }
        public Guid? ActorPersonId { get; set; }
        public Guid? PersonId { get; set; }
    }

    /// <summary>
    /// Request payload for <c>POST/PUT /api/v1/posts/{id}/archive</c> (<c>ArchivePost</c>).
    /// </summary>
    public sealed class ArchivePostBody
    {
        public Guid? ActorPersonId { get; set; }
        public Guid? PersonId { get; set; }
    }
}
