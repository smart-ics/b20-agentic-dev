using Cakra.Modules.Request.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cakra.Modules.Post.Services;

/// <summary>
/// In-process MediatR notification handler subscribing to <see cref="RequestRecorded"/> (Architecture §5, §6; CR-001 / P1-S01).
/// Automatically records a system-generated operational post via <see cref="IPostService.RecordSystemPostAsync"/>
/// whenever a new request is recorded.
/// </summary>
public sealed class RequestRecordedPostHandler : INotificationHandler<RequestRecorded>
{
    private readonly IPostService _postService;
    private readonly ILogger<RequestRecordedPostHandler> _logger;

    public RequestRecordedPostHandler(
        IPostService postService,
        ILogger<RequestRecordedPostHandler>? logger = null)
    {
        _postService = postService ?? throw new ArgumentNullException(nameof(postService));
        _logger = logger ?? NullLogger<RequestRecordedPostHandler>.Instance;
    }

    public async Task Handle(RequestRecorded notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        _logger.LogInformation(
            "Handling RequestRecorded event for RequestId: {RequestId}, Title: {Title}",
            notification.RequestId,
            notification.Title);

        var title = $"Request: {notification.Title}";
        var content = notification.Description;
        var authorPersonId = notification.ActorPersonId;
        const string sourceEventType = "RequestRecorded";

        await _postService.RecordSystemPostAsync(
            title: title,
            content: content,
            authorPersonId: authorPersonId,
            sourceEventType: sourceEventType,
            customerId: notification.CustomerId,
            productId: notification.ProductId,
            requestId: notification.RequestId,
            workPackageId: notification.WorkPackageId,
            isException: false,
            exceptionType: null,
            references: null,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Successfully recorded operational system post for RequestId: {RequestId}",
            notification.RequestId);
    }
}
