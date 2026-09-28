namespace ICS.Modules.WorkPackage.Application.Commands;

using FluentValidation;
using ICS.Core.Domain;
using ICS.Core.Time;
using ICS.Modules.Request;
using ICS.Modules.WorkPackage.Application;
using ICS.Modules.WorkPackage.Domain;
using ICS.Modules.WorkPackage.Domain.Events;
using ICS.Modules.WorkPackage.Domain.Exceptions;
using ICS.Modules.WorkPackage.Infrastructure;
using MediatR;

#region Shared handler plumbing

/// <summary>
/// Shared dependencies for Work Package command handlers.
/// </summary>
internal abstract class WorkPackageCommandHandlerBase
{
    protected WorkPackageCommandHandlerBase(
        IWorkPackageRepository repository,
        IWorkPackageQueryService queryService,
        WorkPackageReferenceValidator referenceValidator,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        QueryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        ReferenceValidator = referenceValidator ?? throw new ArgumentNullException(nameof(referenceValidator));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        EventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    protected IWorkPackageRepository Repository { get; }
    protected IWorkPackageQueryService QueryService { get; }
    protected WorkPackageReferenceValidator ReferenceValidator { get; }
    protected ISystemClock Clock { get; }
    protected IDomainEventDispatcher EventDispatcher { get; }

    /// <summary>
    /// Loads the aggregate or raises <see cref="WorkPackageNotFoundException"/>.
    /// </summary>
    protected async Task<WorkPackage> LoadAsync(Guid workPackageId, CancellationToken cancellationToken)
    {
        var workPackage = await Repository.GetByIdAsync(workPackageId, cancellationToken);
        if (workPackage is null)
        {
            throw new WorkPackageNotFoundException(workPackageId);
        }

        return workPackage;
    }

    /// <summary>
    /// Projects the aggregate to its DTO after events have been dispatched.
    /// </summary>
    protected async Task<WorkPackageDto> ToDtoAsync(WorkPackage workPackage, CancellationToken cancellationToken)
    {
        var dto = await QueryService.GetWorkPackageByIdAsync(workPackage.Id, cancellationToken);
        return dto ?? throw new WorkPackageNotFoundException(workPackage.Id);
    }
}

#endregion

#region CreateWorkPackage (UC-WP-001)

public sealed record CreateWorkPackageCommand(
    string Name,
    string Objective,
    Guid OwnerPersonId,
    Guid? CustomerId = null,
    Guid? ProductId = null,
    Guid? WorkPackageId = null) : IRequest<WorkPackageDto>;

public sealed class CreateWorkPackageCommandValidator : AbstractValidator<CreateWorkPackageCommand>
{
    public CreateWorkPackageCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Objective)
            .NotEmpty().WithMessage("Objective is required.")
            .MaximumLength(500).WithMessage("Objective must not exceed 500 characters.");

        RuleFor(x => x.OwnerPersonId)
            .NotEmpty().WithMessage("OwnerPersonId is required.");
    }
}

internal sealed class CreateWorkPackageCommandHandler
    : WorkPackageCommandHandlerBase,
      IRequestHandler<CreateWorkPackageCommand, WorkPackageDto>
{
    public CreateWorkPackageCommandHandler(
        IWorkPackageRepository repository,
        IWorkPackageQueryService queryService,
        WorkPackageReferenceValidator referenceValidator,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
        : base(repository, queryService, referenceValidator, clock, eventDispatcher)
    {
    }

    public async Task<WorkPackageDto> Handle(CreateWorkPackageCommand request, CancellationToken cancellationToken)
    {
        // 1. Cross-module validation: owner must be an active Person
        await ReferenceValidator.EnsureActivePersonAsync(request.OwnerPersonId, "Work Package owner", cancellationToken);

        // 2. Cross-module validation: optional customer and product references
        await ReferenceValidator.EnsureCustomerAsync(request.CustomerId, cancellationToken);
        await ReferenceValidator.EnsureProductAsync(request.ProductId, cancellationToken);

        var now = Clock.UtcNow;
        var workPackageId = request.WorkPackageId ?? Guid.NewGuid();

        var workPackage = WorkPackage.Create(
            workPackageId,
            request.Name,
            request.Objective,
            request.OwnerPersonId,
            request.CustomerId,
            request.ProductId,
            now);

        await Repository.AddAsync(workPackage, cancellationToken);

        // Audit the initial lifecycle state (Architecture §18)
        await Repository.AddStateHistoryAsync(
            workPackage.Id,
            fromStatus: null,
            toStatus: workPackage.Status,
            actorPersonId: workPackage.OwnerPersonId,
            reason: "Work Package created",
            changedAt: now,
            cancellationToken: cancellationToken);

        // Emits WorkPackageCreated
        await EventDispatcher.DispatchAndClearEventsAsync(workPackage, cancellationToken);

        return await ToDtoAsync(workPackage, cancellationToken);
    }
}

#endregion

#region UpdateObjective

public sealed record UpdateWorkPackageObjectiveCommand(
    Guid WorkPackageId,
    string Name,
    string Objective) : IRequest<WorkPackageDto>;

public sealed class UpdateWorkPackageObjectiveCommandValidator : AbstractValidator<UpdateWorkPackageObjectiveCommand>
{
    public UpdateWorkPackageObjectiveCommandValidator()
    {
        RuleFor(x => x.WorkPackageId).NotEmpty().WithMessage("WorkPackageId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Objective)
            .NotEmpty().WithMessage("Objective is required.")
            .MaximumLength(500).WithMessage("Objective must not exceed 500 characters.");
    }
}

internal sealed class UpdateWorkPackageObjectiveCommandHandler
    : WorkPackageCommandHandlerBase,
      IRequestHandler<UpdateWorkPackageObjectiveCommand, WorkPackageDto>
{
    public UpdateWorkPackageObjectiveCommandHandler(
        IWorkPackageRepository repository,
        IWorkPackageQueryService queryService,
        WorkPackageReferenceValidator referenceValidator,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
        : base(repository, queryService, referenceValidator, clock, eventDispatcher)
    {
    }

    public async Task<WorkPackageDto> Handle(UpdateWorkPackageObjectiveCommand request, CancellationToken cancellationToken)
    {
        var workPackage = await LoadAsync(request.WorkPackageId, cancellationToken);

        // The aggregate rejects updates to a closed Work Package.
        workPackage.UpdateObjective(request.Name, request.Objective, Clock.UtcNow);

        await Repository.UpdateAsync(workPackage, cancellationToken);
        await EventDispatcher.DispatchAndClearEventsAsync(workPackage, cancellationToken);

        return await ToDtoAsync(workPackage, cancellationToken);
    }
}

#endregion

#region AssignOwner

public sealed record AssignWorkPackageOwnerCommand(
    Guid WorkPackageId,
    Guid NewOwnerPersonId) : IRequest<WorkPackageDto>;

public sealed class AssignWorkPackageOwnerCommandValidator : AbstractValidator<AssignWorkPackageOwnerCommand>
{
    public AssignWorkPackageOwnerCommandValidator()
    {
        RuleFor(x => x.WorkPackageId).NotEmpty().WithMessage("WorkPackageId is required.");
        RuleFor(x => x.NewOwnerPersonId).NotEmpty().WithMessage("NewOwnerPersonId is required.");
    }
}

internal sealed class AssignWorkPackageOwnerCommandHandler
    : WorkPackageCommandHandlerBase,
      IRequestHandler<AssignWorkPackageOwnerCommand, WorkPackageDto>
{
    public AssignWorkPackageOwnerCommandHandler(
        IWorkPackageRepository repository,
        IWorkPackageQueryService queryService,
        WorkPackageReferenceValidator referenceValidator,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
        : base(repository, queryService, referenceValidator, clock, eventDispatcher)
    {
    }

    public async Task<WorkPackageDto> Handle(AssignWorkPackageOwnerCommand request, CancellationToken cancellationToken)
    {
        // Cross-module validation: the new owner must be an active Person
        await ReferenceValidator.EnsureActivePersonAsync(
            request.NewOwnerPersonId,
            "new Work Package owner",
            cancellationToken);

        var workPackage = await LoadAsync(request.WorkPackageId, cancellationToken);

        // The aggregate rejects reassignment on a closed Work Package and is a no-op for the same owner.
        workPackage.AssignOwner(request.NewOwnerPersonId, Clock.UtcNow);

        await Repository.UpdateAsync(workPackage, cancellationToken);

        // Emits WorkPackageOwnerChanged when the owner actually changed
        await EventDispatcher.DispatchAndClearEventsAsync(workPackage, cancellationToken);

        return await ToDtoAsync(workPackage, cancellationToken);
    }
}

#endregion

#region AddRequestToWorkPackage (Business Rule 9)

public sealed record AddRequestToWorkPackageCommand(
    Guid WorkPackageId,
    Guid RequestId) : IRequest<WorkPackageDto>;

public sealed class AddRequestToWorkPackageCommandValidator : AbstractValidator<AddRequestToWorkPackageCommand>
{
    public AddRequestToWorkPackageCommandValidator()
    {
        RuleFor(x => x.WorkPackageId).NotEmpty().WithMessage("WorkPackageId is required.");
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("RequestId is required.");
    }
}

internal sealed class AddRequestToWorkPackageCommandHandler
    : WorkPackageCommandHandlerBase,
      IRequestHandler<AddRequestToWorkPackageCommand, WorkPackageDto>
{
    public AddRequestToWorkPackageCommandHandler(
        IWorkPackageRepository repository,
        IWorkPackageQueryService queryService,
        WorkPackageReferenceValidator referenceValidator,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
        : base(repository, queryService, referenceValidator, clock, eventDispatcher)
    {
    }

    public async Task<WorkPackageDto> Handle(AddRequestToWorkPackageCommand request, CancellationToken cancellationToken)
    {
        // 1. Cross-module validation: the Request must exist (Request module owns the Request schema)
        await ReferenceValidator.EnsureRequestExistsAsync(request.RequestId, cancellationToken);

        var workPackage = await LoadAsync(request.WorkPackageId, cancellationToken);
        var now = Clock.UtcNow;

        // 2. Business Rule 9, cross-aggregate half: the request must not already be an active
        //    member of another Work Package that still holds it. A CLOSED Work Package releases
        //    the request, so it never blocks reuse.
        var conflict = await Repository.GetActiveMembershipForRequestAsync(
            request.RequestId,
            cancellationToken);

        if (conflict is not null &&
            conflict.Value.Membership.WorkPackageId != workPackage.Id &&
            !WorkPackageStatus.IsTerminal(conflict.Value.HolderStatus))
        {
            var holderId = conflict.Value.Membership.WorkPackageId;

            // Distinguish "blocked by a Work Package already in progress" from "held by a draft".
            var blocksByActivePackage = await Repository.IsRequestInActiveWorkPackageAsync(
                request.RequestId,
                excludingWorkPackageId: workPackage.Id,
                cancellationToken: cancellationToken);

            if (blocksByActivePackage)
            {
                throw new BusinessRule9ViolationException(
                    request.RequestId,
                    workPackage.Id,
                    $"Business Rule 9 violation: Request '{request.RequestId}' is already an active member of Work Package '{holderId}'.");
            }

            // A DRAFT Work Package still holds the request. Enforce single active membership across
            // the whole in-flight package set so the scope of a Work Package stays unambiguous.
            throw new BusinessRule9ViolationException(
                request.RequestId,
                workPackage.Id,
                $"Business Rule 9 violation: Request '{request.RequestId}' already belongs to Work Package '{holderId}'.");
        }

        // 3. Business Rule 9, in-aggregate half: the aggregate rejects duplicates within itself.
        var membership = workPackage.AddRequest(request.RequestId, now);

        var inserted = await Repository.AddRequestMembershipAsync(workPackage, membership, cancellationToken);
        if (inserted == 0)
        {
            throw new BusinessRule9ViolationException(
                request.RequestId,
                workPackage.Id,
                $"Business Rule 9 violation: Request '{request.RequestId}' is already an active member of Work Package '{workPackage.Id}'.");
        }

        // Emits RequestAddedToWorkPackage
        await EventDispatcher.DispatchAndClearEventsAsync(workPackage, cancellationToken);

        return await ToDtoAsync(workPackage, cancellationToken);
    }
}

#endregion

#region RemoveRequestFromWorkPackage

public sealed record RemoveRequestFromWorkPackageCommand(
    Guid WorkPackageId,
    Guid RequestId) : IRequest<WorkPackageDto>;

public sealed class RemoveRequestFromWorkPackageCommandValidator : AbstractValidator<RemoveRequestFromWorkPackageCommand>
{
    public RemoveRequestFromWorkPackageCommandValidator()
    {
        RuleFor(x => x.WorkPackageId).NotEmpty().WithMessage("WorkPackageId is required.");
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("RequestId is required.");
    }
}

internal sealed class RemoveRequestFromWorkPackageCommandHandler
    : WorkPackageCommandHandlerBase,
      IRequestHandler<RemoveRequestFromWorkPackageCommand, WorkPackageDto>
{
    public RemoveRequestFromWorkPackageCommandHandler(
        IWorkPackageRepository repository,
        IWorkPackageQueryService queryService,
        WorkPackageReferenceValidator referenceValidator,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
        : base(repository, queryService, referenceValidator, clock, eventDispatcher)
    {
    }

    public async Task<WorkPackageDto> Handle(RemoveRequestFromWorkPackageCommand request, CancellationToken cancellationToken)
    {
        var workPackage = await LoadAsync(request.WorkPackageId, cancellationToken);
        var now = Clock.UtcNow;

        // The aggregate rejects removal from a closed Work Package and of non-member requests.
        workPackage.RemoveRequest(request.RequestId, now);

        var affected = await Repository.DeactivateRequestMembershipAsync(
            workPackage.Id,
            request.RequestId,
            now,
            cancellationToken);

        if (affected == 0)
        {
            throw new WorkPackageDomainException(
                $"Request '{request.RequestId}' is not an active member of Work Package '{workPackage.Id}'.");
        }

        // Emits RequestRemovedFromWorkPackage. The Request's own lifecycle state is untouched.
        await EventDispatcher.DispatchAndClearEventsAsync(workPackage, cancellationToken);

        return await ToDtoAsync(workPackage, cancellationToken);
    }
}

#endregion

#region ActivateWorkPackage

public sealed record ActivateWorkPackageCommand(
    Guid WorkPackageId) : IRequest<WorkPackageDto>;

public sealed class ActivateWorkPackageCommandValidator : AbstractValidator<ActivateWorkPackageCommand>
{
    public ActivateWorkPackageCommandValidator()
    {
        RuleFor(x => x.WorkPackageId).NotEmpty().WithMessage("WorkPackageId is required.");
    }
}

internal sealed class ActivateWorkPackageCommandHandler
    : WorkPackageCommandHandlerBase,
      IRequestHandler<ActivateWorkPackageCommand, WorkPackageDto>
{
    public ActivateWorkPackageCommandHandler(
        IWorkPackageRepository repository,
        IWorkPackageQueryService queryService,
        WorkPackageReferenceValidator referenceValidator,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
        : base(repository, queryService, referenceValidator, clock, eventDispatcher)
    {
    }

    public async Task<WorkPackageDto> Handle(ActivateWorkPackageCommand request, CancellationToken cancellationToken)
    {
        var workPackage = await LoadAsync(request.WorkPackageId, cancellationToken);
        var fromStatus = workPackage.Status;
        var now = Clock.UtcNow;

        // The state machine permits only DRAFT -> ACTIVE.
        workPackage.Activate(now);

        await Repository.UpdateAsync(workPackage, cancellationToken);
        await Repository.AddStateHistoryAsync(
            workPackage.Id,
            fromStatus,
            workPackage.Status,
            workPackage.OwnerPersonId,
            "Work Package activated",
            now,
            cancellationToken);

        // Emits WorkPackageActivated
        await EventDispatcher.DispatchAndClearEventsAsync(workPackage, cancellationToken);

        return await ToDtoAsync(workPackage, cancellationToken);
    }
}

#endregion

#region CloseWorkPackage

public sealed record CloseWorkPackageCommand(
    Guid WorkPackageId,
    string? Reason = null) : IRequest<WorkPackageDto>;

public sealed class CloseWorkPackageCommandValidator : AbstractValidator<CloseWorkPackageCommand>
{
    public CloseWorkPackageCommandValidator()
    {
        RuleFor(x => x.WorkPackageId).NotEmpty().WithMessage("WorkPackageId is required.");

        RuleFor(x => x.Reason)
            .MaximumLength(200).WithMessage("Reason must not exceed 200 characters.");
    }
}

internal sealed class CloseWorkPackageCommandHandler
    : WorkPackageCommandHandlerBase,
      IRequestHandler<CloseWorkPackageCommand, WorkPackageDto>
{
    public CloseWorkPackageCommandHandler(
        IWorkPackageRepository repository,
        IWorkPackageQueryService queryService,
        WorkPackageReferenceValidator referenceValidator,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
        : base(repository, queryService, referenceValidator, clock, eventDispatcher)
    {
    }

    public async Task<WorkPackageDto> Handle(CloseWorkPackageCommand request, CancellationToken cancellationToken)
    {
        var workPackage = await LoadAsync(request.WorkPackageId, cancellationToken);
        var fromStatus = workPackage.Status;
        var now = Clock.UtcNow;

        // The state machine permits DRAFT -> CLOSED and ACTIVE -> CLOSED; CLOSED is terminal.
        workPackage.Close(request.Reason, now);

        await Repository.UpdateAsync(workPackage, cancellationToken);
        await Repository.AddStateHistoryAsync(
            workPackage.Id,
            fromStatus,
            workPackage.Status,
            workPackage.OwnerPersonId,
            request.Reason ?? "Work Package closed",
            now,
            cancellationToken);

        // Constituent Request lifecycle states are deliberately left untouched: closing a Work
        // Package does not complete, reject, or otherwise transition its member Requests.
        // Emits WorkPackageClosed
        await EventDispatcher.DispatchAndClearEventsAsync(workPackage, cancellationToken);

        return await ToDtoAsync(workPackage, cancellationToken);
    }
}

#endregion
