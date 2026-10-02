using Cakra.Core;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Cakra.Modules.Post.Domain;
using Cakra.Modules.Post.Domain.Exceptions;
using Cakra.Modules.Post.Persistence;
using Cakra.Modules.Product;
using Cakra.Modules.Request;
using Cakra.Modules.WorkPackage;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cakra.Modules.Post.Services;

/// <summary>
/// Application command service and MediatR command handler for Post authoring, system post recording,
/// discussion comments, structured operational reactions, visibility toggling, and archiving
/// (Architecture §6, §7, §8, §12, §15, §18, §19.2, §20, §21).
/// Validates cross-module references via <see cref="IOrganizationQueryService"/>,
/// <see cref="ICustomerQueryService"/>, <see cref="IProductQueryService"/>,
/// <see cref="IRequestQueryService"/>, and <see cref="IWorkPackageQueryService"/> without cross-schema writes.
/// </summary>
public sealed class PostService :
    IPostService,
    IRequestHandler<RecordSystemPostCommand, PostThreadDetailsDto>,
    IRequestHandler<PostCommentCommand, CommentDto>,
    IRequestHandler<AddReactionCommand, ReactionDto>,
    IRequestHandler<RemoveReactionCommand, PostThreadDetailsDto>,
    IRequestHandler<TogglePostVisibilityCommand, PostThreadDetailsDto>,
    IRequestHandler<ArchivePostCommand, PostThreadDetailsDto>
{
    private readonly IPostRepository _postRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly IRequestQueryService _requestQueryService;
    private readonly IWorkPackageQueryService _workPackageQueryService;
    private readonly IDomainEventDispatcher? _eventDispatcher;
    private readonly ICurrentContextProvider? _currentContextProvider;
    private readonly IAuditContext? _auditContext;
    private readonly ISystemClock? _clock;
    private readonly ILogger<PostService> _logger;

    public PostService(
        IDbConnectionFactory connectionFactory,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        IRequestQueryService requestQueryService,
        IWorkPackageQueryService workPackageQueryService,
        IDomainEventDispatcher? eventDispatcher = null,
        ICurrentContextProvider? currentContextProvider = null,
        IAuditContext? auditContext = null,
        ISystemClock? clock = null)
        : this(
            new PostRepository(connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory))),
            organizationQueryService,
            customerQueryService,
            productQueryService,
            requestQueryService,
            workPackageQueryService,
            eventDispatcher,
            currentContextProvider,
            auditContext,
            clock,
            null)
    {
    }

    internal PostService(
        IPostRepository postRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        IRequestQueryService requestQueryService,
        IWorkPackageQueryService workPackageQueryService,
        IDomainEventDispatcher? eventDispatcher = null,
        ICurrentContextProvider? currentContextProvider = null,
        IAuditContext? auditContext = null,
        ISystemClock? clock = null,
        ILogger<PostService>? logger = null)
    {
        _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _requestQueryService = requestQueryService ?? throw new ArgumentNullException(nameof(requestQueryService));
        _workPackageQueryService = workPackageQueryService ?? throw new ArgumentNullException(nameof(workPackageQueryService));
        _eventDispatcher = eventDispatcher;
        _currentContextProvider = currentContextProvider;
        _auditContext = auditContext;
        _clock = clock;
        _logger = logger ?? NullLogger<PostService>.Instance;
    }

    private DateTime UtcNow => _clock?.UtcNow ?? DateTime.UtcNow;


    /// <inheritdoc />
    public async Task<PostThreadDetailsDto> RecordSystemPostAsync(
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
        IReadOnlyList<PostReferenceInput>? references = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new PostDomainValidationException("System post title cannot be null or whitespace.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new PostDomainValidationException("System post content cannot be null or whitespace.", nameof(content));
        }

        var effectiveAuthorId = NormalizeOptionalGuid(authorPersonId)
            ?? NormalizeOptionalGuid(_currentContextProvider?.CurrentPersonId);

        string? authorName = "SYSTEM";
        if (effectiveAuthorId.HasValue)
        {
            var person = await _organizationQueryService.GetPersonByIdAsync(effectiveAuthorId.Value, cancellationToken);
            if (person is not null && !string.IsNullOrWhiteSpace(person.FullName))
            {
                authorName = person.FullName;
            }
        }

        var resolvedReferences = await ValidateAndResolveReferencesAsync(
            customerId,
            productId,
            requestId,
            workPackageId,
            references,
            requireActiveMasterData: false,
            cancellationToken);

        var now = UtcNow;
        var post = Domain.Post.RecordSystemPost(
            title: title,
            content: content,
            authorPersonId: effectiveAuthorId,
            sourceEventType: sourceEventType,
            customerId: resolvedReferences.EffectiveCustomerId,
            productId: resolvedReferences.EffectiveProductId,
            requestId: resolvedReferences.EffectiveRequestId,
            workPackageId: resolvedReferences.EffectiveWorkPackageId,
            isException: isException,
            exceptionType: exceptionType,
            authorName: authorName,
            customerName: resolvedReferences.CustomerName,
            productName: resolvedReferences.ProductName,
            primaryReferenceDisplay: resolvedReferences.PrimaryReferenceDisplay,
            additionalReferences: resolvedReferences.AdditionalReferences,
            createdAtUtc: now);

        await _postRepository.AddAsync(post, cancellationToken);
        await DispatchDomainEventsAsync(post, cancellationToken);

        _logger.LogInformation(
            "System Post {PostId} recorded with SourceEventType {SourceEventType} (IsException={IsException}, ExceptionType={ExceptionType})",
            post.Id,
            post.SourceEventType,
            post.IsException,
            post.ExceptionType);

        return PostDto.FromDomain(
            post,
            authorName: authorName,
            customerName: resolvedReferences.CustomerName,
            productName: resolvedReferences.ProductName,
            requestTitle: resolvedReferences.RequestTitle,
            workPackageName: resolvedReferences.WorkPackageName);
    }

    /// <inheritdoc />
    public async Task<CommentDto> PostCommentAsync(
        Guid postId,
        string content,
        Guid? authorPersonId = null,
        CancellationToken cancellationToken = default)
    {
        if (postId == Guid.Empty)
        {
            throw new PostDomainValidationException("PostId cannot be empty.", nameof(postId));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new PostDomainValidationException("Comment content cannot be null or whitespace.", nameof(content));
        }

        var post = await GetRequiredPostAsync(postId, cancellationToken);
        var resolvedAuthorId = ResolveRequiredActorPersonId(authorPersonId, nameof(authorPersonId));
        var authorName = await ValidateAndResolvePersonNameAsync(resolvedAuthorId, cancellationToken);

        var now = UtcNow;
        var comment = post.AddComment(resolvedAuthorId, content, authorName, now);

        await _postRepository.AddCommentAsync(comment, now, cancellationToken);
        await DispatchDomainEventsAsync(post, cancellationToken);

        _logger.LogInformation(
            "Comment {CommentId} added to Post {PostId} by AuthorPersonId {AuthorPersonId}",
            comment.Id,
            post.Id,
            resolvedAuthorId);

        return CommentDto.FromDomain(comment, authorName);
    }

    /// <inheritdoc />
    public async Task<ReactionDto> AddReactionAsync(
        Guid postId,
        string reactionType,
        Guid? personId = null,
        Guid? commentId = null,
        CancellationToken cancellationToken = default)
    {
        if (postId == Guid.Empty)
        {
            throw new PostDomainValidationException("PostId cannot be empty.", nameof(postId));
        }

        var normalizedReactionType = PostReactionTypes.NormalizeAndValidate(reactionType);
        var post = await GetRequiredPostAsync(postId, cancellationToken);
        var resolvedPersonId = ResolveRequiredActorPersonId(personId, nameof(personId));
        var personName = await ValidateAndResolvePersonNameAsync(resolvedPersonId, cancellationToken);

        var existingReactionIds = post.Reactions.Select(r => r.Id).ToHashSet();
        var now = UtcNow;

        var (reaction, wasAddedOrReactivated) = post.AddReaction(
            resolvedPersonId,
            normalizedReactionType,
            commentId,
            now);

        if (wasAddedOrReactivated)
        {
            if (existingReactionIds.Contains(reaction.Id))
            {
                await _postRepository.UpdateReactionAsync(reaction, now, cancellationToken);
            }
            else
            {
                await _postRepository.AddReactionAsync(reaction, now, cancellationToken);
            }

            await DispatchDomainEventsAsync(post, cancellationToken);

            _logger.LogInformation(
                "Reaction {ReactionId} ({ReactionType}) added to Post {PostId} by PersonId {PersonId}",
                reaction.Id,
                reaction.ReactionType,
                post.Id,
                resolvedPersonId);
        }

        return ReactionDto.FromDomain(reaction, personName);
    }

    /// <inheritdoc />
    public async Task<PostThreadDetailsDto> RemoveReactionAsync(
        Guid postId,
        string reactionType,
        Guid? personId = null,
        Guid? commentId = null,
        CancellationToken cancellationToken = default)
    {
        if (postId == Guid.Empty)
        {
            throw new PostDomainValidationException("PostId cannot be empty.", nameof(postId));
        }

        var normalizedReactionType = PostReactionTypes.NormalizeAndValidate(reactionType);
        var post = await GetRequiredPostAsync(postId, cancellationToken);
        var resolvedPersonId = ResolveRequiredActorPersonId(personId, nameof(personId));

        var now = UtcNow;
        var removedReaction = post.RemoveReaction(
            resolvedPersonId,
            normalizedReactionType,
            commentId,
            now);

        if (removedReaction is not null)
        {
            await _postRepository.UpdateReactionAsync(removedReaction, now, cancellationToken);
            await DispatchDomainEventsAsync(post, cancellationToken);

            _logger.LogInformation(
                "Reaction {ReactionId} ({ReactionType}) soft-removed from Post {PostId} by PersonId {PersonId}",
                removedReaction.Id,
                removedReaction.ReactionType,
                post.Id,
                resolvedPersonId);
        }

        return PostDto.FromDomain(post);
    }

    /// <inheritdoc />
    public async Task<PostThreadDetailsDto> TogglePostVisibilityAsync(
        Guid postId,
        string? visibility = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
    {
        if (postId == Guid.Empty)
        {
            throw new PostDomainValidationException("PostId cannot be empty.", nameof(postId));
        }

        var post = await GetRequiredPostAsync(postId, cancellationToken);
        var targetVisibility = PostVisibilityNames.FromNullableName(visibility);
        var resolvedActorId = NormalizeOptionalGuid(actorPersonId)
            ?? NormalizeOptionalGuid(_currentContextProvider?.CurrentPersonId)
            ?? NormalizeOptionalGuid(_auditContext?.ActorPersonId)
            ?? post.AuthorPersonId;

        var previousVisibility = post.VisibilityName;
        var now = UtcNow;
        post.ToggleVisibility(targetVisibility, resolvedActorId, now);

        await _postRepository.UpdateAsync(post, cancellationToken);
        await DispatchDomainEventsAsync(post, cancellationToken);

        _logger.LogInformation(
            "Post {PostId} visibility changed from {PreviousVisibility} to {NewVisibility} by ActorPersonId {ActorPersonId}",
            post.Id,
            previousVisibility,
            post.VisibilityName,
            resolvedActorId);

        return PostDto.FromDomain(post);
    }

    /// <inheritdoc />
    public async Task<PostThreadDetailsDto> ArchivePostAsync(
        Guid postId,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
    {
        if (postId == Guid.Empty)
        {
            throw new PostDomainValidationException("PostId cannot be empty.", nameof(postId));
        }

        var post = await GetRequiredPostAsync(postId, cancellationToken);
        var resolvedActorId = NormalizeOptionalGuid(actorPersonId)
            ?? NormalizeOptionalGuid(_currentContextProvider?.CurrentPersonId)
            ?? NormalizeOptionalGuid(_auditContext?.ActorPersonId)
            ?? post.AuthorPersonId;

        var previousStatus = post.StatusName;
        var now = UtcNow;
        post.Archive(resolvedActorId, now);

        await _postRepository.UpdateAsync(post, cancellationToken);
        await DispatchDomainEventsAsync(post, cancellationToken);

        _logger.LogInformation(
            "Post {PostId} transitioned from {PreviousStatus} to {NewStatus} by ActorPersonId {ActorPersonId}",
            post.Id,
            previousStatus,
            post.StatusName,
            resolvedActorId);

        return PostDto.FromDomain(post);
    }

    // MediatR command handler entry points

    public Task<PostThreadDetailsDto> Handle(
        RecordSystemPostCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RecordSystemPostAsync(
            request.Title,
            request.Content,
            request.AuthorPersonId,
            request.SourceEventType,
            request.CustomerId,
            request.ProductId,
            request.RequestId,
            request.WorkPackageId,
            request.IsException,
            request.ExceptionType,
            request.References,
            cancellationToken);
    }

    public Task<CommentDto> Handle(
        PostCommentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostCommentAsync(
            request.PostId,
            request.Content,
            request.AuthorPersonId,
            cancellationToken);
    }

    public Task<ReactionDto> Handle(
        AddReactionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return AddReactionAsync(
            request.PostId,
            request.ReactionType,
            request.PersonId,
            request.CommentId,
            cancellationToken);
    }

    public Task<PostThreadDetailsDto> Handle(
        RemoveReactionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RemoveReactionAsync(
            request.PostId,
            request.ReactionType,
            request.PersonId,
            request.CommentId,
            cancellationToken);
    }

    public Task<PostThreadDetailsDto> Handle(
        TogglePostVisibilityCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TogglePostVisibilityAsync(
            request.PostId,
            request.Visibility,
            request.ActorPersonId,
            cancellationToken);
    }

    public Task<PostThreadDetailsDto> Handle(
        ArchivePostCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ArchivePostAsync(
            request.PostId,
            request.ActorPersonId,
            cancellationToken);
    }

    private async Task<Domain.Post> GetRequiredPostAsync(Guid postId, CancellationToken cancellationToken)
    {
        return await _postRepository.GetByIdAsync(postId, cancellationToken)
            ?? throw new KeyNotFoundException($"Post '{postId}' was not found.");
    }

    private Guid ResolveRequiredActorPersonId(Guid? explicitPersonId, string paramName)
    {
        if (explicitPersonId.HasValue && explicitPersonId.Value != Guid.Empty)
        {
            return explicitPersonId.Value;
        }

        if (_currentContextProvider?.CurrentPersonId is { } currentPersonId && currentPersonId != Guid.Empty)
        {
            return currentPersonId;
        }

        if (_auditContext?.ActorPersonId is { } auditPersonId && auditPersonId != Guid.Empty)
        {
            return auditPersonId;
        }

        throw new PostDomainValidationException(
            "An authenticated PersonId or explicit PersonId is required for this Post operation.",
            paramName);
    }

    private async Task<string?> ValidateAndResolvePersonNameAsync(
        Guid personId,
        CancellationToken cancellationToken)
    {
        if (personId == Guid.Empty)
        {
            throw new PostDomainValidationException("PersonId cannot be empty.", nameof(personId));
        }

        PersonDto? person = await _organizationQueryService.GetPersonByIdAsync(personId, cancellationToken);
        if (person is not null)
        {
            if (!person.IsActive)
            {
                throw new InvalidOperationException(
                    $"Person '{personId}' is not active in Organization and cannot perform this Post action.");
            }

            return person.FullName;
        }

        var isActive = await _organizationQueryService.IsPersonActiveAsync(personId, cancellationToken);
        if (isActive)
        {
            return null;
        }

        throw new KeyNotFoundException($"Person '{personId}' was not found in Organization.");
    }

    private async Task<ResolvedPostReferences> ValidateAndResolveReferencesAsync(
        Guid? customerId,
        Guid? productId,
        Guid? requestId,
        Guid? workPackageId,
        IReadOnlyList<PostReferenceInput>? explicitReferences,
        bool requireActiveMasterData,
        CancellationToken cancellationToken)
    {
        var effectiveCustomerId = NormalizeOptionalGuid(customerId);
        var effectiveProductId = NormalizeOptionalGuid(productId);
        var effectiveRequestId = NormalizeOptionalGuid(requestId);
        var effectiveWorkPackageId = NormalizeOptionalGuid(workPackageId);

        if (explicitReferences is not null)
        {
            foreach (var input in explicitReferences)
            {
                if (input is null || input.ReferenceId == Guid.Empty)
                {
                    continue;
                }

                var refType = PostReferenceTypes.NormalizeAndValidate(input.ReferenceType);
                switch (refType)
                {
                    case PostReferenceTypes.Request:
                        effectiveRequestId ??= input.ReferenceId;
                        break;
                    case PostReferenceTypes.WorkPackage:
                        effectiveWorkPackageId ??= input.ReferenceId;
                        break;
                    case PostReferenceTypes.Customer:
                        effectiveCustomerId ??= input.ReferenceId;
                        break;
                    case PostReferenceTypes.Product:
                        effectiveProductId ??= input.ReferenceId;
                        break;
                }
            }
        }

        string? requestTitle = null;
        if (effectiveRequestId.HasValue)
        {
            var reqDto = await _requestQueryService.GetRequestByIdAsync(effectiveRequestId.Value, cancellationToken);
            if (reqDto is not null)
            {
                requestTitle = reqDto.Title;
                effectiveCustomerId ??= reqDto.CustomerId;
                effectiveProductId ??= reqDto.ProductId;
                effectiveWorkPackageId ??= reqDto.WorkPackageId;
            }
            else
            {
                var exists = await _requestQueryService.RequestExistsAsync(effectiveRequestId.Value, cancellationToken);
                if (!exists)
                {
                    throw new KeyNotFoundException($"Referenced Request '{effectiveRequestId.Value}' was not found.");
                }
            }
        }

        string? workPackageName = null;
        if (effectiveWorkPackageId.HasValue)
        {
            var wpDto = await _workPackageQueryService.GetWorkPackageByIdAsync(effectiveWorkPackageId.Value, cancellationToken);
            if (wpDto is not null)
            {
                workPackageName = wpDto.Name;
                effectiveCustomerId ??= wpDto.CustomerId;
                effectiveProductId ??= wpDto.ProductId;
            }
            else
            {
                var exists = await _workPackageQueryService.WorkPackageExistsAsync(effectiveWorkPackageId.Value, cancellationToken);
                if (!exists)
                {
                    throw new KeyNotFoundException($"Referenced WorkPackage '{effectiveWorkPackageId.Value}' was not found.");
                }
            }
        }

        string? customerName = null;
        if (effectiveCustomerId.HasValue)
        {
            var customer = await _customerQueryService.GetCustomerByIdAsync(effectiveCustomerId.Value, cancellationToken);
            if (customer is not null)
            {
                if (requireActiveMasterData && !customer.IsActive)
                {
                    throw new InvalidOperationException(
                        $"Customer '{effectiveCustomerId.Value}' is not active and cannot be referenced by a new operational post.");
                }

                customerName = customer.CustomerName;
            }
            else
            {
                var isActive = await _customerQueryService.IsCustomerActiveAsync(effectiveCustomerId.Value, cancellationToken);
                if (!isActive)
                {
                    throw new KeyNotFoundException($"Referenced Customer '{effectiveCustomerId.Value}' was not found.");
                }
            }
        }

        string? productName = null;
        if (effectiveProductId.HasValue)
        {
            var product = await _productQueryService.GetProductByIdAsync(effectiveProductId.Value, cancellationToken);
            if (product is not null)
            {
                if (requireActiveMasterData && !product.IsActive)
                {
                    throw new InvalidOperationException(
                        $"Product '{effectiveProductId.Value}' is not active and cannot be referenced by a new operational post.");
                }

                productName = product.Name;
            }
            else
            {
                var isActive = await _productQueryService.IsProductActiveAsync(effectiveProductId.Value, cancellationToken);
                if (!isActive)
                {
                    throw new KeyNotFoundException($"Referenced Product '{effectiveProductId.Value}' was not found.");
                }
            }
        }

        var additionalRefs = new List<(string ReferenceType, Guid ReferenceId, string? ReferenceDisplay)>();

        if (effectiveRequestId.HasValue)
        {
            additionalRefs.Add((PostReferenceTypes.Request, effectiveRequestId.Value, requestTitle));
        }

        if (effectiveWorkPackageId.HasValue)
        {
            additionalRefs.Add((PostReferenceTypes.WorkPackage, effectiveWorkPackageId.Value, workPackageName));
        }

        if (effectiveCustomerId.HasValue)
        {
            additionalRefs.Add((PostReferenceTypes.Customer, effectiveCustomerId.Value, customerName));
        }

        if (effectiveProductId.HasValue)
        {
            additionalRefs.Add((PostReferenceTypes.Product, effectiveProductId.Value, productName));
        }

        if (explicitReferences is not null)
        {
            foreach (var input in explicitReferences)
            {
                if (input is null || input.ReferenceId == Guid.Empty)
                {
                    continue;
                }

                var refType = PostReferenceTypes.NormalizeAndValidate(input.ReferenceType);
                string? display = input.ReferenceDisplay;

                switch (refType)
                {
                    case PostReferenceTypes.Request:
                        if (input.ReferenceId != effectiveRequestId)
                        {
                            var r = await _requestQueryService.GetRequestByIdAsync(input.ReferenceId, cancellationToken);
                            if (r is null && !await _requestQueryService.RequestExistsAsync(input.ReferenceId, cancellationToken))
                            {
                                throw new KeyNotFoundException($"Referenced Request '{input.ReferenceId}' was not found.");
                            }
                            display ??= r?.Title;
                        }
                        else
                        {
                            display ??= requestTitle;
                        }
                        break;

                    case PostReferenceTypes.WorkPackage:
                        if (input.ReferenceId != effectiveWorkPackageId)
                        {
                            var wp = await _workPackageQueryService.GetWorkPackageByIdAsync(input.ReferenceId, cancellationToken);
                            if (wp is null && !await _workPackageQueryService.WorkPackageExistsAsync(input.ReferenceId, cancellationToken))
                            {
                                throw new KeyNotFoundException($"Referenced WorkPackage '{input.ReferenceId}' was not found.");
                            }
                            display ??= wp?.Name;
                        }
                        else
                        {
                            display ??= workPackageName;
                        }
                        break;

                    case PostReferenceTypes.Customer:
                        if (input.ReferenceId != effectiveCustomerId)
                        {
                            var c = await _customerQueryService.GetCustomerByIdAsync(input.ReferenceId, cancellationToken);
                            if (c is null && !await _customerQueryService.IsCustomerActiveAsync(input.ReferenceId, cancellationToken))
                            {
                                throw new KeyNotFoundException($"Referenced Customer '{input.ReferenceId}' was not found.");
                            }
                            display ??= c?.CustomerName;
                        }
                        else
                        {
                            display ??= customerName;
                        }
                        break;

                    case PostReferenceTypes.Product:
                        if (input.ReferenceId != effectiveProductId)
                        {
                            var p = await _productQueryService.GetProductByIdAsync(input.ReferenceId, cancellationToken);
                            if (p is null && !await _productQueryService.IsProductActiveAsync(input.ReferenceId, cancellationToken))
                            {
                                throw new KeyNotFoundException($"Referenced Product '{input.ReferenceId}' was not found.");
                            }
                            display ??= p?.Name;
                        }
                        else
                        {
                            display ??= productName;
                        }
                        break;

                    case PostReferenceTypes.Organization:
                        var person = await _organizationQueryService.GetPersonByIdAsync(input.ReferenceId, cancellationToken);
                        if (person is null && !await _organizationQueryService.IsPersonActiveAsync(input.ReferenceId, cancellationToken))
                        {
                            throw new KeyNotFoundException($"Referenced Organization Person '{input.ReferenceId}' was not found.");
                        }
                        display ??= person?.FullName;
                        break;
                }

                additionalRefs.Add((refType, input.ReferenceId, display));
            }
        }

        var primaryDisplay = requestTitle ?? workPackageName ?? customerName ?? productName;

        return new ResolvedPostReferences(
            EffectiveCustomerId: effectiveCustomerId,
            EffectiveProductId: effectiveProductId,
            EffectiveRequestId: effectiveRequestId,
            EffectiveWorkPackageId: effectiveWorkPackageId,
            CustomerName: customerName,
            ProductName: productName,
            RequestTitle: requestTitle,
            WorkPackageName: workPackageName,
            PrimaryReferenceDisplay: primaryDisplay,
            AdditionalReferences: additionalRefs);
    }

    private async Task DispatchDomainEventsAsync(Domain.Post post, CancellationToken cancellationToken)
    {
        if (_eventDispatcher is not null)
        {
            foreach (var domainEvent in post.DomainEvents.ToList())
            {
                await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
            }
        }

        post.ClearDomainEvents();
    }

    private static Guid? NormalizeOptionalGuid(Guid? value)
        => value.HasValue && value.Value != Guid.Empty ? value.Value : null;

    private sealed record ResolvedPostReferences(
        Guid? EffectiveCustomerId,
        Guid? EffectiveProductId,
        Guid? EffectiveRequestId,
        Guid? EffectiveWorkPackageId,
        string? CustomerName,
        string? ProductName,
        string? RequestTitle,
        string? WorkPackageName,
        string? PrimaryReferenceDisplay,
        IReadOnlyList<(string ReferenceType, Guid ReferenceId, string? ReferenceDisplay)> AdditionalReferences);
}
