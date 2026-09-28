namespace ICS.Modules.Post.Application;

using MediatR;
using ICS.Modules.Post.Application.Commands;

/// <summary>
/// Concrete implementation of <see cref="IPostService"/> executing commands through MediatR pipeline.
/// Architecture §7, §12, §19.2.
/// </summary>
public sealed class PostService : IPostService
{
    private readonly ISender _sender;

    public PostService(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public Task<PostThreadDto> CreateOperationalPostAsync(
        string title,
        string content,
        Guid authorPersonId,
        IReadOnlyList<(string ReferenceType, Guid TargetId)>? references = null,
        Guid? postId = null,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new CreateOperationalPostCommand(
            title,
            content,
            authorPersonId,
            references,
            postId), cancellationToken);
    }

    public Task<PostThreadDto> RecordSystemPostAsync(
        string title,
        string content,
        string sourceEventOrObject,
        Guid? postId = null,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new RecordSystemPostCommand(
            title,
            content,
            sourceEventOrObject,
            postId), cancellationToken);
    }

    public Task<PostThreadDto> PostCommentAsync(
        Guid postId,
        string content,
        Guid authorPersonId,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new PostCommentCommand(
            postId,
            content,
            authorPersonId), cancellationToken);
    }

    public Task<PostThreadDto> AddReactionAsync(
        Guid postId,
        Guid personId,
        string reactionType,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new AddReactionCommand(
            postId,
            personId,
            reactionType), cancellationToken);
    }

    public Task<PostThreadDto> RemoveReactionAsync(
        Guid postId,
        Guid personId,
        string reactionType,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new RemoveReactionCommand(
            postId,
            personId,
            reactionType), cancellationToken);
    }

    public Task<PostThreadDto> TogglePostVisibilityAsync(
        Guid postId,
        string newVisibility,
        Guid changedByPersonId,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new TogglePostVisibilityCommand(
            postId,
            newVisibility,
            changedByPersonId), cancellationToken);
    }

    public Task<PostThreadDto> ArchivePostAsync(
        Guid postId,
        Guid archivedByPersonId,
        CancellationToken cancellationToken = default)
    {
        return _sender.Send(new ArchivePostCommand(
            postId,
            archivedByPersonId), cancellationToken);
    }
}