using Cakra.Modules.Post.Persistence;
using Cakra.Modules.Request.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cakra.Modules.Post.Services;

/// <summary>
/// In-process MediatR notification handler subscribing to <see cref="RequestCoreAttributesUpdated"/>
/// (CR-018 / P2-S03; Architecture §4 TD-005).
/// Synchronizes the root operational post title and content when request core attributes change.
/// </summary>
public sealed class RequestCoreAttributesUpdatedPostHandler : INotificationHandler<RequestCoreAttributesUpdated>
{
    private readonly IPostRepository _postRepository;
    private readonly ILogger<RequestCoreAttributesUpdatedPostHandler> _logger;

    public RequestCoreAttributesUpdatedPostHandler(
        IPostRepository postRepository,
        ILogger<RequestCoreAttributesUpdatedPostHandler>? logger = null)
    {
        _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
        _logger = logger ?? NullLogger<RequestCoreAttributesUpdatedPostHandler>.Instance;
    }

    public async Task Handle(RequestCoreAttributesUpdated notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        _logger.LogInformation(
            "Handling RequestCoreAttributesUpdated event for RequestId: {RequestId}, Title: {Title}",
            notification.RequestId,
            notification.Title);

        // Find root operational post for this request where SourceEventType is "RequestRecorded"
        var posts = await _postRepository.GetByRequestIdAsync(notification.RequestId, cancellationToken);
        var rootPost = posts.FirstOrDefault(p => p.SourceEventType == "RequestRecorded");

        if (rootPost is null)
        {
            _logger.LogWarning(
                "No root operational post found for RequestId {RequestId} to synchronize.",
                notification.RequestId);
            return;
        }

        rootPost.UpdateContent(
            title: $"Request: {notification.Title}",
            content: notification.Description,
            updatedAtUtc: notification.OccurredAtUtc);

        await _postRepository.UpdateAsync(rootPost, cancellationToken);

        _logger.LogInformation(
            "Successfully synchronized root operational post {PostId} for Request {RequestId}.",
            rootPost.Id,
            notification.RequestId);
    }
}
