namespace ICS.Modules.Post.Application.Commands;

using FluentValidation;
using ICS.Core.Domain;
using ICS.Core.Time;
using ICS.Modules.Customer;
using ICS.Modules.Organization;
using ICS.Modules.Product;
using ICS.Modules.Post.Domain;
using ICS.Modules.Post.Domain.Events;
using ICS.Modules.Post.Domain.Exceptions;
using ICS.Modules.Post.Persistence;
using ICS.Modules.Request;
using ICS.Modules.WorkPackage;
using MediatR;

#region Helper Mapper

internal static class PostDtoMapper
{
    public static PostThreadDto ToDtoAsync(
        Post post,
        IReadOnlyList<PostCommentDto> comments,
        IReadOnlyList<PostReactionDto> reactions,
        CancellationToken cancellationToken = default)
    {
        return new PostThreadDto(
            post.Id,
            post.Title,
            post.Content,
            post.Source,
            post.Status,
            post.Visibility,
            post.AuthorPersonId,
            post.CreatedAt,
            post.UpdatedAt,
            post.ArchivedAt,
            comments,
            reactions);
    }
}

#endregion

#region CreateOperationalPost (UC-FCOL-003: Create Operational Post - Human Authored)

public sealed record CreateOperationalPostCommand(
    string Title,
    string Content,
    Guid AuthorPersonId,
    IReadOnlyList<(string ReferenceType, Guid TargetId)>? References = null,
    Guid? PostId = null) : IRequest<PostThreadDto>;

public sealed class CreateOperationalPostCommandValidator : AbstractValidator<CreateOperationalPostCommand>
{
    public CreateOperationalPostCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Post content is required.");

        RuleFor(x => x.AuthorPersonId)
            .NotEmpty().WithMessage("AuthorPersonId is required.");
    }
}

internal sealed class CreateOperationalPostCommandHandler
    : IRequestHandler<CreateOperationalPostCommand, PostThreadDto>
{
    private readonly IPostRepository _postRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly ICS.Modules.Request.IRequestQueryService _requestQueryService;
    private readonly ICS.Modules.WorkPackage.IWorkPackageQueryService _workPackageQueryService;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public CreateOperationalPostCommandHandler(
        IPostRepository postRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        ICS.Modules.Request.IRequestQueryService requestQueryService,
        ICS.Modules.WorkPackage.IWorkPackageQueryService workPackageQueryService,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _requestQueryService = requestQueryService ?? throw new ArgumentNullException(nameof(requestQueryService));
        _workPackageQueryService = workPackageQueryService ?? throw new ArgumentNullException(nameof(workPackageQueryService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<PostThreadDto> Handle(CreateOperationalPostCommand command, CancellationToken cancellationToken)
    {
        // Cross-domain validation: Author exists in Organization
        var author = await _organizationQueryService.GetPersonByIdAsync(command.AuthorPersonId, cancellationToken);
        if (author is null)
        {
            throw new PostReferenceValidationException("Person",
                $"Author with PersonId '{command.AuthorPersonId}' does not exist in Organization.");
        }

        // Cross-domain validation: References (Request, WorkPackage, Customer, Product)
        var validatedReferences = new List<(string ReferenceType, Guid TargetId)>();
        if (command.References != null)
        {
            foreach (var (refType, targetId) in command.References)
            {
                await ValidateReferenceAsync(refType, targetId, cancellationToken);
                validatedReferences.Add((refType, targetId));
            }
        }

        var now = _clock.UtcNow;
        var postId = command.PostId ?? Guid.NewGuid();

        var post = Post.CreateHumanAuthored(
            postId,
            command.Title,
            command.Content,
            command.AuthorPersonId,
            validatedReferences,
            now);

        await _postRepository.AddAsync(post, cancellationToken);

        await _eventDispatcher.DispatchAndClearEventsAsync(post, cancellationToken);

        var comments = await _postRepository.GetFullCommentsAsync(postId, cancellationToken);
        var reactions = await _postRepository.GetReactionListAsync(postId, cancellationToken: cancellationToken);

        return PostDtoMapper.ToDtoAsync(post, comments, reactions, cancellationToken);
    }

    private async Task ValidateReferenceAsync(string refType, Guid targetId, CancellationToken cancellationToken)
    {
        switch (refType.ToUpperInvariant())
        {
            case PostReference.ReferenceTypes.Request:
                var request = await _requestQueryService.GetRequestByIdAsync(targetId, cancellationToken);
                if (request is null)
                    throw new PostReferenceValidationException("Request",
                        $"Request with ID '{targetId}' does not exist.");
                break;
            case PostReference.ReferenceTypes.WorkPackage:
                var wp = await _workPackageQueryService.GetWorkPackageByIdAsync(targetId, cancellationToken);
                if (wp is null)
                    throw new PostReferenceValidationException("WorkPackage",
                        $"Work Package with ID '{targetId}' does not exist.");
                break;
            case PostReference.ReferenceTypes.Customer:
                var customer = await _customerQueryService.GetCustomerByIdAsync(targetId, cancellationToken);
                if (customer is null)
                    throw new PostReferenceValidationException("Customer",
                        $"Customer with ID '{targetId}' does not exist.");
                break;
            case PostReference.ReferenceTypes.Product:
                var product = await _productQueryService.GetProductByIdAsync(targetId, cancellationToken);
                if (product is null)
                    throw new PostReferenceValidationException("Product",
                        $"Product with ID '{targetId}' does not exist.");
                break;
            case PostReference.ReferenceTypes.Organization:
                var orgPerson = await _organizationQueryService.GetPersonByIdAsync(targetId, cancellationToken);
                if (orgPerson is null)
                    throw new PostReferenceValidationException("Organization",
                        $"Organization Person with ID '{targetId}' does not exist.");
                break;
        }
    }
}

#endregion

#region RecordSystemPost (UC-FCOL-003: Create Operational Post - System Generated)

public sealed record RecordSystemPostCommand(
    string Title,
    string Content,
    string SourceEventOrObject,
    Guid? PostId = null) : IRequest<PostThreadDto>;

public sealed class RecordSystemPostCommandValidator : AbstractValidator<RecordSystemPostCommand>
{
    public RecordSystemPostCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Post content is required.");

        RuleFor(x => x.SourceEventOrObject)
            .NotEmpty().WithMessage("Source event or object is required for system-generated posts.");
    }
}

internal sealed class RecordSystemPostCommandHandler
    : IRequestHandler<RecordSystemPostCommand, PostThreadDto>
{
    private readonly IPostRepository _postRepository;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public RecordSystemPostCommandHandler(
        IPostRepository postRepository,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<PostThreadDto> Handle(RecordSystemPostCommand command, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var postId = command.PostId ?? Guid.NewGuid();

        var post = Post.CreateSystemAuthored(
            postId,
            command.Title,
            command.Content,
            command.SourceEventOrObject,
            now);

        await _postRepository.AddAsync(post, cancellationToken);

        await _eventDispatcher.DispatchAndClearEventsAsync(post, cancellationToken);

        var comments = await _postRepository.GetFullCommentsAsync(postId, cancellationToken);
        var reactions = await _postRepository.GetReactionListAsync(postId, cancellationToken: cancellationToken);

        return PostDtoMapper.ToDtoAsync(post, comments, reactions, cancellationToken);
    }
}

#endregion

#region PostComment (UC-FCOL-001: Comment on Post)

public sealed record PostCommentCommand(
    Guid PostId,
    string Content,
    Guid AuthorPersonId) : IRequest<PostThreadDto>;

public sealed class PostCommentCommandValidator : AbstractValidator<PostCommentCommand>
{
    public PostCommentCommandValidator()
    {
        RuleFor(x => x.PostId).NotEmpty().WithMessage("PostId is required.");
        RuleFor(x => x.Content).NotEmpty().WithMessage("Comment content is required.");
        RuleFor(x => x.AuthorPersonId).NotEmpty().WithMessage("AuthorPersonId is required.");
    }
}

internal sealed class PostCommentCommandHandler
    : IRequestHandler<PostCommentCommand, PostThreadDto>
{
    private readonly IPostRepository _postRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly ISystemClock _clock;

    public PostCommentCommandHandler(
        IPostRepository postRepository,
        IDomainEventDispatcher eventDispatcher,
        ISystemClock clock)
    {
        _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<PostThreadDto> Handle(PostCommentCommand command, CancellationToken cancellationToken)
    {
        var post = await _postRepository.GetByIdAsync(command.PostId, cancellationToken);
        if (post is null)
        {
            throw new PostNotFoundException(command.PostId);
        }

        if (!post.IsActive)
        {
            throw new PostDomainException($"Cannot comment on a Post in status '{post.Status}'.");
        }

        var now = _clock.UtcNow;
        var comment = Comment.Add(
            Guid.NewGuid(),
            command.PostId,
            command.AuthorPersonId,
            command.Content,
            now);

        await _postRepository.AddCommentAsync(comment, cancellationToken);

        await _eventDispatcher.DispatchAndClearEventsAsync(comment, cancellationToken);

        var comments = await _postRepository.GetFullCommentsAsync(command.PostId, cancellationToken);
        var reactions = await _postRepository.GetReactionListAsync(command.PostId, cancellationToken: cancellationToken);

        return PostDtoMapper.ToDtoAsync(post, comments, reactions, cancellationToken);
    }
}

#endregion

#region AddReaction

public sealed record AddReactionCommand(
    Guid PostId,
    Guid PersonId,
    string ReactionType) : IRequest<PostThreadDto>;

public sealed class AddReactionCommandValidator : AbstractValidator<AddReactionCommand>
{
    public AddReactionCommandValidator()
    {
        RuleFor(x => x.PostId).NotEmpty().WithMessage("PostId is required.");
        RuleFor(x => x.PersonId).NotEmpty().WithMessage("PersonId is required.");
        RuleFor(x => x.ReactionType).NotEmpty().WithMessage("ReactionType is required.");
    }
}

internal sealed class AddReactionCommandHandler
    : IRequestHandler<AddReactionCommand, PostThreadDto>
{
    private readonly IPostRepository _postRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public AddReactionCommandHandler(
        IPostRepository postRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<PostThreadDto> Handle(AddReactionCommand command, CancellationToken cancellationToken)
    {
        var post = await _postRepository.GetByIdAsync(command.PostId, cancellationToken);
        if (post is null)
        {
            throw new PostNotFoundException(command.PostId);
        }

        if (!post.IsActive)
        {
            throw new PostDomainException($"Cannot react to a Post in status '{post.Status}'.");
        }

        var now = DateTime.UtcNow;
        var reaction = Reaction.AddToPost(
            Guid.NewGuid(),
            command.PostId,
            command.PersonId,
            command.ReactionType,
            now);

        await _postRepository.AddReactionAsync(reaction, cancellationToken);

        await _eventDispatcher.DispatchAndClearEventsAsync(reaction, cancellationToken);

        var comments = await _postRepository.GetFullCommentsAsync(command.PostId, cancellationToken);
        var reactions = await _postRepository.GetReactionListAsync(command.PostId, cancellationToken: cancellationToken);

        return PostDtoMapper.ToDtoAsync(post, comments, reactions, cancellationToken);
    }
}

#endregion

#region RemoveReaction

public sealed record RemoveReactionCommand(
    Guid PostId,
    Guid PersonId,
    string ReactionType) : IRequest<PostThreadDto>;

public sealed class RemoveReactionCommandValidator : AbstractValidator<RemoveReactionCommand>
{
    public RemoveReactionCommandValidator()
    {
        RuleFor(x => x.PostId).NotEmpty().WithMessage("PostId is required.");
        RuleFor(x => x.PersonId).NotEmpty().WithMessage("PersonId is required.");
        RuleFor(x => x.ReactionType).NotEmpty().WithMessage("ReactionType is required.");
    }
}

internal sealed class RemoveReactionCommandHandler
    : IRequestHandler<RemoveReactionCommand, PostThreadDto>
{
    private readonly IPostRepository _postRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public RemoveReactionCommandHandler(
        IPostRepository postRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<PostThreadDto> Handle(RemoveReactionCommand command, CancellationToken cancellationToken)
    {
        var post = await _postRepository.GetByIdAsync(command.PostId, cancellationToken);
        if (post is null)
        {
            throw new PostNotFoundException(command.PostId);
        }

        var reaction = post.Reactions.FirstOrDefault(r =>
            r.PostId == command.PostId &&
            r.PersonId == command.PersonId &&
            r.Type == command.ReactionType.ToUpperInvariant());

        if (reaction != null)
            {
                reaction.Remove(DateTime.UtcNow);
                await _postRepository.RemoveReactionAsync(reaction, cancellationToken);
                await _eventDispatcher.DispatchAndClearEventsAsync(reaction, cancellationToken);
            }

        var comments = await _postRepository.GetFullCommentsAsync(command.PostId, cancellationToken);
        var reactions = await _postRepository.GetReactionListAsync(command.PostId, cancellationToken: cancellationToken);

        return PostDtoMapper.ToDtoAsync(post, comments, reactions, cancellationToken);
    }
}

#endregion

#region TogglePostVisibility

public sealed record TogglePostVisibilityCommand(
    Guid PostId,
    string NewVisibility,
    Guid ChangedByPersonId) : IRequest<PostThreadDto>;

public sealed class TogglePostVisibilityCommandValidator : AbstractValidator<TogglePostVisibilityCommand>
{
    public TogglePostVisibilityCommandValidator()
    {
        RuleFor(x => x.PostId).NotEmpty().WithMessage("PostId is required.");
        RuleFor(x => x.NewVisibility).NotEmpty().WithMessage("NewVisibility is required.");
        RuleFor(x => x.ChangedByPersonId).NotEmpty().WithMessage("ChangedByPersonId is required.");
    }
}

internal sealed class TogglePostVisibilityCommandHandler
    : IRequestHandler<TogglePostVisibilityCommand, PostThreadDto>
{
    private readonly IPostRepository _postRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public TogglePostVisibilityCommandHandler(
        IPostRepository postRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<PostThreadDto> Handle(TogglePostVisibilityCommand command, CancellationToken cancellationToken)
    {
        var post = await _postRepository.GetByIdAsync(command.PostId, cancellationToken);
        if (post is null)
        {
            throw new PostNotFoundException(command.PostId);
        }

        post.ChangeVisibility(command.NewVisibility, command.ChangedByPersonId, DateTime.UtcNow);

        await _postRepository.UpdateAsync(post, cancellationToken);

        await _eventDispatcher.DispatchAndClearEventsAsync(post, cancellationToken);

        var comments = await _postRepository.GetFullCommentsAsync(command.PostId, cancellationToken);
        var reactions = await _postRepository.GetReactionListAsync(command.PostId, cancellationToken: cancellationToken);

        return PostDtoMapper.ToDtoAsync(post, comments, reactions, cancellationToken);
    }
}

#endregion

#region ArchivePost

public sealed record ArchivePostCommand(
    Guid PostId,
    Guid ArchivedByPersonId) : IRequest<PostThreadDto>;

public sealed class ArchivePostCommandValidator : AbstractValidator<ArchivePostCommand>
{
    public ArchivePostCommandValidator()
    {
        RuleFor(x => x.PostId).NotEmpty().WithMessage("PostId is required.");
        RuleFor(x => x.ArchivedByPersonId).NotEmpty().WithMessage("ArchivedByPersonId is required.");
    }
}

internal sealed class ArchivePostCommandHandler
    : IRequestHandler<ArchivePostCommand, PostThreadDto>
{
    private readonly IPostRepository _postRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public ArchivePostCommandHandler(
        IPostRepository postRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<PostThreadDto> Handle(ArchivePostCommand command, CancellationToken cancellationToken)
    {
        var post = await _postRepository.GetByIdAsync(command.PostId, cancellationToken);
        if (post is null)
        {
            throw new PostNotFoundException(command.PostId);
        }

        post.Archive(command.ArchivedByPersonId, DateTime.UtcNow);

        await _postRepository.UpdateAsync(post, cancellationToken);

        await _eventDispatcher.DispatchAndClearEventsAsync(post, cancellationToken);

        var comments = await _postRepository.GetFullCommentsAsync(command.PostId, cancellationToken);
        var reactions = await _postRepository.GetReactionListAsync(command.PostId, cancellationToken: cancellationToken);

        return PostDtoMapper.ToDtoAsync(post, comments, reactions, cancellationToken);
    }
}

#endregion