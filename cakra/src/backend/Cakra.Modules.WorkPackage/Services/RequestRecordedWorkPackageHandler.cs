using Cakra.Modules.Request.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cakra.Modules.WorkPackage.Services;

/// <summary>
/// In-process MediatR notification handler subscribing to <see cref="RequestRecorded"/>
/// (Architecture §7, §8, §18; CR-019 §7).
/// Automatically links newly recorded requests to their parent Work Package in
/// <c>workpackage.WorkPackageRequests</c> when <c>WorkPackageId</c> is specified.
/// </summary>
public sealed class RequestRecordedWorkPackageHandler : INotificationHandler<RequestRecorded>
{
    private readonly IWorkPackageService _workPackageService;
    private readonly ILogger<RequestRecordedWorkPackageHandler> _logger;

    public RequestRecordedWorkPackageHandler(
        IWorkPackageService workPackageService,
        ILogger<RequestRecordedWorkPackageHandler>? logger = null)
    {
        _workPackageService = workPackageService ?? throw new ArgumentNullException(nameof(workPackageService));
        _logger = logger ?? NullLogger<RequestRecordedWorkPackageHandler>.Instance;
    }

    public async Task Handle(RequestRecorded notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!notification.WorkPackageId.HasValue || notification.WorkPackageId.Value == Guid.Empty)
        {
            return;
        }

        _logger.LogInformation(
            "Handling RequestRecorded event for RequestId: {RequestId}, linking to WorkPackage: {WorkPackageId}",
            notification.RequestId,
            notification.WorkPackageId.Value);

        await _workPackageService.AddRequestToWorkPackageAsync(
            notification.WorkPackageId.Value,
            notification.RequestId,
            cancellationToken);

        _logger.LogInformation(
            "Successfully linked RequestId: {RequestId} to WorkPackage: {WorkPackageId}",
            notification.RequestId,
            notification.WorkPackageId.Value);
    }
}
