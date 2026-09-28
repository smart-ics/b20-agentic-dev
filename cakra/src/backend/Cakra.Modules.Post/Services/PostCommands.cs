using Cakra.Modules.Post.Domain;
using FluentValidation;
using MediatR;

namespace Cakra.Modules.Post.Services;

/// <summary>
/// MediatR command to author a new human-authored operational Post (Architecture §7, §8 — UC-FCOL-003, UC-COL-001).
/// </summary>
public sealed record CreateOperationalPostCommand(
    string Title,
    string Content,
    Guid? AuthorPersonId = null,
    Guid? CustomerId = null,
    Guid? ProductId = null,
    Guid? RequestId = null,
    Guid? WorkPackageId = null,
    bool IsException = false,
    string? ExceptionType = null,
    IReadOnlyList<PostReferenceInput>? References = null) : IRequest<PostThreadDetailsDto>;

/// <summary>
/// FluentValidation validator for <see cref="CreateOperationalPostCommand"/> (Architecture §19.2).
/// </summary>
public sealed class CreateOperationalPostCommandValidator : AbstractValidator<CreateOperationalPostCommand>
{
    public CreateOperationalPostCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Post title is required.")
            .MaximumLength(255).WithMessage("Post title cannot exceed 255 characters.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Post content is required.");

        RuleFor(x => x.ExceptionType)
            .Must(type => string.IsNullOrWhiteSpace(type) || PostExceptionTypes.All.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage($"ExceptionType must be one of: {string.Join(", ", PostExceptionTypes.All)}.");
    }
}

/// <summary>
/// MediatR command to record a system-generated operational Post (Architecture §7, §12; Post Domain §5, §7).
/// </summary>
public sealed record RecordSystemPostCommand(
    string Title,
    string Content,
    Guid? AuthorPersonId = null,
    string? SourceEventType = null,
    Guid? CustomerId = null,
    Guid? ProductId = null,
    Guid? RequestId = null,
    Guid? WorkPackageId = null,
    bool IsException = false,
    string? ExceptionType = null,
    IReadOnlyList<PostReferenceInput>? References = null) : IRequest<PostThreadDetailsDto>;

/// <summary>
/// FluentValidation validator for <see cref="RecordSystemPostCommand"/> (Architecture §19.2).
/// </summary>
public sealed class RecordSystemPostCommandValidator : AbstractValidator<RecordSystemPostCommand>
{
    public RecordSystemPostCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("System post title is required.")
            .MaximumLength(255).WithMessage("System post title cannot exceed 255 characters.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("System post content is required.");

        RuleFor(x => x.ExceptionType)
            .Must(type => string.IsNullOrWhiteSpace(type) || PostExceptionTypes.All.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage($"ExceptionType must be one of: {string.Join(", ", PostExceptionTypes.All)}.");
    }
}

/// <summary>
/// MediatR command to add a comment to an active, visible Post (Architecture §7, §8 — UC-FCOL-001, FEAT-FCOL-001).
/// </summary>
public sealed record PostCommentCommand(
    Guid PostId,
    string Content,
    Guid? AuthorPersonId = null) : IRequest<CommentDto>;

/// <summary>
/// FluentValidation validator for <see cref="PostCommentCommand"/> (Architecture §19.2).
/// </summary>
public sealed class PostCommentCommandValidator : AbstractValidator<PostCommentCommand>
{
    public PostCommentCommandValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty().WithMessage("PostId is required.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Comment content is required.");
    }
}

/// <summary>
/// MediatR command to add (or idempotently retain / reactivate) a structured operational reaction on a Post
/// (Architecture §7, §8 — UC-FCOL-002, FEAT-FCOL-002).
/// </summary>
public sealed record AddReactionCommand(
    Guid PostId,
    string ReactionType,
    Guid? PersonId = null,
    Guid? CommentId = null) : IRequest<ReactionDto>;

/// <summary>
/// FluentValidation validator for <see cref="AddReactionCommand"/> (Architecture §19.2).
/// </summary>
public sealed class AddReactionCommandValidator : AbstractValidator<AddReactionCommand>
{
    public AddReactionCommandValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty().WithMessage("PostId is required.");

        RuleFor(x => x.ReactionType)
            .NotEmpty().WithMessage("ReactionType is required.")
            .Must(PostReactionTypes.IsValid)
            .WithMessage($"ReactionType must be one of: {string.Join(", ", PostReactionTypes.All)}.");
    }
}

/// <summary>
/// MediatR command to soft-remove an active reaction on a Post without physical deletion
/// (Architecture §7, §8, §20, §21 — UC-FCOL-002, FEAT-FCOL-002).
/// </summary>
public sealed record RemoveReactionCommand(
    Guid PostId,
    string ReactionType,
    Guid? PersonId = null,
    Guid? CommentId = null) : IRequest<PostThreadDetailsDto>;

/// <summary>
/// FluentValidation validator for <see cref="RemoveReactionCommand"/> (Architecture §19.2).
/// </summary>
public sealed class RemoveReactionCommandValidator : AbstractValidator<RemoveReactionCommand>
{
    public RemoveReactionCommandValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty().WithMessage("PostId is required.");

        RuleFor(x => x.ReactionType)
            .NotEmpty().WithMessage("ReactionType is required.")
            .Must(PostReactionTypes.IsValid)
            .WithMessage($"ReactionType must be one of: {string.Join(", ", PostReactionTypes.All)}.");
    }
}

/// <summary>
/// MediatR command to toggle a Post's visibility between <c>VISIBLE</c> and <c>HIDDEN</c>
/// (or set it to a specific target visibility) (Architecture §7, §12).
/// </summary>
public sealed record TogglePostVisibilityCommand(
    Guid PostId,
    string? Visibility = null,
    Guid? ActorPersonId = null) : IRequest<PostThreadDetailsDto>;

/// <summary>
/// FluentValidation validator for <see cref="TogglePostVisibilityCommand"/> (Architecture §19.2).
/// </summary>
public sealed class TogglePostVisibilityCommandValidator : AbstractValidator<TogglePostVisibilityCommand>
{
    public TogglePostVisibilityCommandValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty().WithMessage("PostId is required.");

        RuleFor(x => x.Visibility)
            .Must(v => string.IsNullOrWhiteSpace(v) ||
                       string.Equals(v.Trim(), PostVisibilityNames.Visible, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(v.Trim(), PostVisibilityNames.Hidden, StringComparison.OrdinalIgnoreCase))
            .WithMessage($"Visibility must be '{PostVisibilityNames.Visible}' or '{PostVisibilityNames.Hidden}' when specified.");
    }
}

/// <summary>
/// MediatR command to archive an active Post (<c>ACTIVE -&gt; ARCHIVED</c>) (Architecture §7, §12, §20, §21).
/// </summary>
public sealed record ArchivePostCommand(
    Guid PostId,
    Guid? ActorPersonId = null) : IRequest<PostThreadDetailsDto>;

/// <summary>
/// FluentValidation validator for <see cref="ArchivePostCommand"/> (Architecture §19.2).
/// </summary>
public sealed class ArchivePostCommandValidator : AbstractValidator<ArchivePostCommand>
{
    public ArchivePostCommandValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty().WithMessage("PostId is required.");
    }
}
