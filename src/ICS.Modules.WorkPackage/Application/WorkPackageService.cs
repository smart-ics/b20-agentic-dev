namespace ICS.Modules.WorkPackage.Application;

using ICS.Modules.WorkPackage.Application.Commands;
using MediatR;

/// <summary>
/// Concrete implementation of <see cref="IWorkPackageService"/>. Each operation is dispatched as a
/// MediatR command and executed by the corresponding handler in the MediatR pipeline.
/// Architecture §11, §16, §19.2, §20.
/// </summary>
public sealed class WorkPackageService : IWorkPackageService
{
    private readonly ISender _sender;

    public WorkPackageService(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public Task<WorkPackageDto> CreateWorkPackageAsync(
        string name,
        string objective,
        Guid ownerPersonId,
        Guid? customerId = null,
        Guid? productId = null,
        Guid? workPackageId = null,
        CancellationToken cancellationToken = default) =>
        _sender.Send(
            new CreateWorkPackageCommand(name, objective, ownerPersonId, customerId, productId, workPackageId),
            cancellationToken);

    public Task<WorkPackageDto> UpdateObjectiveAsync(
        Guid workPackageId,
        string name,
        string objective,
        CancellationToken cancellationToken = default) =>
        _sender.Send(
            new UpdateWorkPackageObjectiveCommand(workPackageId, name, objective),
            cancellationToken);

    public Task<WorkPackageDto> AssignOwnerAsync(
        Guid workPackageId,
        Guid newOwnerPersonId,
        CancellationToken cancellationToken = default) =>
        _sender.Send(
            new AssignWorkPackageOwnerCommand(workPackageId, newOwnerPersonId),
            cancellationToken);

    public Task<WorkPackageDto> AddRequestToWorkPackageAsync(
        Guid workPackageId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        _sender.Send(
            new AddRequestToWorkPackageCommand(workPackageId, requestId),
            cancellationToken);

    public Task<WorkPackageDto> RemoveRequestFromWorkPackageAsync(
        Guid workPackageId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        _sender.Send(
            new RemoveRequestFromWorkPackageCommand(workPackageId, requestId),
            cancellationToken);

    public Task<WorkPackageDto> ActivateWorkPackageAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default) =>
        _sender.Send(
            new ActivateWorkPackageCommand(workPackageId),
            cancellationToken);

    public Task<WorkPackageDto> CloseWorkPackageAsync(
        Guid workPackageId,
        string? reason = null,
        CancellationToken cancellationToken = default) =>
        _sender.Send(
            new CloseWorkPackageCommand(workPackageId, reason),
            cancellationToken);
}
