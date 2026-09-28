namespace ICS.Modules.Request.Application.Commands;

using FluentValidation;
using ICS.Core.Domain;
using ICS.Core.Time;
using ICS.Modules.Customer;
using ICS.Modules.Organization;
using ICS.Modules.Product;
using ICS.Modules.Request.Application.DTOs;
using ICS.Modules.Request.Domain;
using ICS.Modules.Request.Domain.Events;
using ICS.Modules.Request.Domain.Exceptions;
using ICS.Modules.Request.Persistence;
using MediatR;

#region Helper Mapper

internal static class RequestDtoMapper
{
    public static async Task<RequestDto> ToDtoAsync(
        Request request,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        IReadOnlyList<RequestStateHistoryDto>? stateHistories = null,
        CancellationToken cancellationToken = default)
    {
        string? ownerName = null;
        if (request.OwnerPersonId.HasValue && request.OwnerPersonId.Value != Guid.Empty)
        {
            var owner = await organizationQueryService.GetPersonByIdAsync(request.OwnerPersonId.Value, cancellationToken);
            ownerName = owner?.Name;
        }

        string? customerName = null;
        string? customerCode = null;
        if (request.CustomerId.HasValue && request.CustomerId.Value != Guid.Empty)
        {
            var customer = await customerQueryService.GetCustomerByIdAsync(request.CustomerId.Value, cancellationToken);
            customerName = customer?.CustomerName;
            customerCode = customer?.CustomerCode;
        }

        string? productName = null;
        string? productCode = null;
        if (request.ProductId.HasValue && request.ProductId.Value != Guid.Empty)
        {
            var product = await productQueryService.GetProductByIdAsync(request.ProductId.Value, cancellationToken);
            productName = product?.Name;
            productCode = product?.Code;
        }

        string? escalatedByName = null;
        if (request.EscalatedByPersonId.HasValue && request.EscalatedByPersonId.Value != Guid.Empty)
        {
            var escPerson = await organizationQueryService.GetPersonByIdAsync(request.EscalatedByPersonId.Value, cancellationToken);
            escalatedByName = escPerson?.Name;
        }

        RequestResolutionDto? resolutionDto = null;
        if (request.Resolution != null)
        {
            var resPerson = await organizationQueryService.GetPersonByIdAsync(request.Resolution.ResolvedByPersonId, cancellationToken);
            resolutionDto = new RequestResolutionDto(
                request.Resolution.Id,
                request.Resolution.RequestId,
                request.Resolution.Outcome,
                request.Resolution.Summary,
                request.Resolution.ResolvedByPersonId,
                resPerson?.Name,
                request.Resolution.ResolvedAt);
        }

        var assignmentDtos = new List<RequestAssignmentDto>();
        foreach (var a in request.Assignments)
        {
            var aOwner = await organizationQueryService.GetPersonByIdAsync(a.OwnerPersonId, cancellationToken);
            var aAssigner = await organizationQueryService.GetPersonByIdAsync(a.AssignedByPersonId, cancellationToken);
            assignmentDtos.Add(new RequestAssignmentDto(
                a.Id,
                a.RequestId,
                a.OwnerPersonId,
                aOwner?.Name,
                a.AssignedByPersonId,
                aAssigner?.Name,
                a.AssignedAt,
                a.Note,
                a.IsActive));
        }

        return new RequestDto(
            request.Id,
            request.Title,
            request.Description,
            request.Type,
            request.Priority,
            request.Status,
            request.IsTerminal,
            request.IsActive,
            request.RequesterPersonId,
            request.RequesterContactId,
            request.RequesterName,
            request.OwnerPersonId,
            ownerName,
            request.CustomerId,
            customerName,
            customerCode,
            request.ProductId,
            productName,
            productCode,
            request.WorkPackageId,
            request.IsAwaitingManagementDecision,
            request.ManagementDecisionQuestion,
            request.ManagementDecisionOptions,
            request.ManagementDecisionImpact,
            request.ManagementDecisionRequestedAt,
            request.EscalationReason,
            request.EscalatedByPersonId,
            escalatedByName,
            request.EscalatedAt,
            request.CreatedAt,
            request.UpdatedAt,
            request.ClosedAt,
            resolutionDto,
            assignmentDtos,
            stateHistories);
    }
}

#endregion

#region RecordRequest (UC-REQ-001)

public sealed record RecordRequestCommand(
    string Title,
    string Description,
    string Type,
    string? Priority = null,
    Guid? RequesterPersonId = null,
    Guid? RequesterContactId = null,
    string? RequesterName = null,
    Guid? CustomerId = null,
    Guid? ProductId = null,
    Guid? WorkPackageId = null,
    Guid? InitialOwnerPersonId = null,
    Guid? AssignedByPersonId = null,
    Guid? RequestId = null) : IRequest<RequestDto>;

public sealed class RecordRequestCommandValidator : AbstractValidator<RecordRequestCommand>
{
    public RecordRequestCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Request type is required.");
    }
}

internal sealed class RecordRequestCommandHandler : IRequestHandler<RecordRequestCommand, RequestDto>
{
    private readonly IRequestRepository _requestRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public RecordRequestCommandHandler(
        IRequestRepository requestRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<RequestDto> Handle(RecordRequestCommand request, CancellationToken cancellationToken)
    {
        // 1. Cross-module validation: Customer
        if (request.CustomerId.HasValue && request.CustomerId.Value != Guid.Empty)
        {
            var customer = await _customerQueryService.GetCustomerByIdAsync(request.CustomerId.Value, cancellationToken);
            if (customer == null)
            {
                throw new InvalidOperationException($"Customer with ID '{request.CustomerId.Value}' does not exist.");
            }
            if (!string.Equals(customer.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Customer '{customer.CustomerName}' is not active.");
            }
        }

        // 2. Cross-module validation: Product
        if (request.ProductId.HasValue && request.ProductId.Value != Guid.Empty)
        {
            var product = await _productQueryService.GetProductByIdAsync(request.ProductId.Value, cancellationToken);
            if (product == null)
            {
                throw new InvalidOperationException($"Product with ID '{request.ProductId.Value}' does not exist.");
            }
            if (!string.Equals(product.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Product '{product.Name}' is not active.");
            }
        }

        // 3. Cross-module validation: Initial Owner
        if (request.InitialOwnerPersonId.HasValue && request.InitialOwnerPersonId.Value != Guid.Empty)
        {
            var owner = await _organizationQueryService.GetPersonByIdAsync(request.InitialOwnerPersonId.Value, cancellationToken);
            if (owner == null)
            {
                throw new InvalidOperationException($"Initial owner with PersonId '{request.InitialOwnerPersonId.Value}' does not exist in Organization.");
            }
            if (!string.Equals(owner.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Initial owner '{owner.Name}' is not active in Organization.");
            }
        }

        // 4. Cross-module validation: Requester Person
        if (request.RequesterPersonId.HasValue && request.RequesterPersonId.Value != Guid.Empty)
        {
            var requester = await _organizationQueryService.GetPersonByIdAsync(request.RequesterPersonId.Value, cancellationToken);
            if (requester == null)
            {
                throw new InvalidOperationException($"Requester with PersonId '{request.RequesterPersonId.Value}' does not exist in Organization.");
            }
        }

        var now = _clock.UtcNow;
        var requestId = request.RequestId ?? Guid.NewGuid();

        var aggregate = Request.Record(
            requestId,
            request.Title,
            request.Description,
            request.Type,
            request.Priority ?? Request.PriorityMedium,
            request.RequesterPersonId,
            request.RequesterContactId,
            request.RequesterName,
            request.CustomerId,
            request.ProductId,
            request.WorkPackageId,
            now,
            request.InitialOwnerPersonId,
            request.AssignedByPersonId);

        await _requestRepository.AddAsync(aggregate, cancellationToken);

        // Audit Logging (Architecture §18)
        var actorId = request.AssignedByPersonId ?? request.RequesterPersonId ?? request.InitialOwnerPersonId ?? Guid.Empty;
        await _requestRepository.AddStateHistoryAsync(
            aggregate.Id,
            fromStatus: null,
            toStatus: aggregate.Status,
            actorPersonId: actorId,
            reason: "Request recorded",
            changedAt: now,
            cancellationToken: cancellationToken);

        await _eventDispatcher.DispatchAndClearEventsAsync(aggregate, cancellationToken);

        var histories = await _requestRepository.GetStateHistoryAsync(aggregate.Id, cancellationToken);
        return await RequestDtoMapper.ToDtoAsync(
            aggregate,
            _organizationQueryService,
            _customerQueryService,
            _productQueryService,
            histories,
            cancellationToken);
    }
}

#endregion

#region AssignRequestOwner (UC-REQ-002)

public sealed record AssignRequestOwnerCommand(
    Guid RequestId,
    Guid NewOwnerPersonId,
    Guid AssignedByPersonId,
    string? Note = null) : IRequest<RequestDto>;

public sealed class AssignRequestOwnerCommandValidator : AbstractValidator<AssignRequestOwnerCommand>
{
    public AssignRequestOwnerCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("RequestId is required.");
        RuleFor(x => x.NewOwnerPersonId).NotEmpty().WithMessage("NewOwnerPersonId is required.");
        RuleFor(x => x.AssignedByPersonId).NotEmpty().WithMessage("AssignedByPersonId is required.");
    }
}

internal sealed class AssignRequestOwnerCommandHandler : IRequestHandler<AssignRequestOwnerCommand, RequestDto>
{
    private readonly IRequestRepository _requestRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public AssignRequestOwnerCommandHandler(
        IRequestRepository requestRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<RequestDto> Handle(AssignRequestOwnerCommand request, CancellationToken cancellationToken)
    {
        var aggregate = await _requestRepository.GetByIdAsync(request.RequestId, cancellationToken);
        if (aggregate == null)
        {
            throw new InvalidOperationException($"Request with ID '{request.RequestId}' does not exist.");
        }

        // Cross-module validation: Owner
        var newOwner = await _organizationQueryService.GetPersonByIdAsync(request.NewOwnerPersonId, cancellationToken);
        if (newOwner == null)
        {
            throw new InvalidOperationException($"Assignee with PersonId '{request.NewOwnerPersonId}' does not exist in Organization.");
        }
        if (!string.Equals(newOwner.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Assignee '{newOwner.Name}' is not active in Organization.");
        }

        var now = _clock.UtcNow;
        aggregate.AssignOwner(request.NewOwnerPersonId, request.AssignedByPersonId, now, request.Note);

        await _requestRepository.UpdateAsync(aggregate, cancellationToken);

        // Audit Logging
        await _requestRepository.AddStateHistoryAsync(
            aggregate.Id,
            fromStatus: aggregate.Status,
            toStatus: aggregate.Status,
            actorPersonId: request.AssignedByPersonId,
            reason: $"Ownership assigned to {newOwner.Name}" + (string.IsNullOrWhiteSpace(request.Note) ? "" : $": {request.Note}"),
            changedAt: now,
            cancellationToken: cancellationToken);

        await _eventDispatcher.DispatchAndClearEventsAsync(aggregate, cancellationToken);

        var histories = await _requestRepository.GetStateHistoryAsync(aggregate.Id, cancellationToken);
        return await RequestDtoMapper.ToDtoAsync(
            aggregate,
            _organizationQueryService,
            _customerQueryService,
            _productQueryService,
            histories,
            cancellationToken);
    }
}

#endregion

#region EvaluateRequest (UC-REQ-003)

public sealed record EvaluateRequestCommand(
    Guid RequestId,
    Guid EvaluatedByPersonId,
    string? Notes = null) : IRequest<RequestDto>;

public sealed class EvaluateRequestCommandValidator : AbstractValidator<EvaluateRequestCommand>
{
    public EvaluateRequestCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("RequestId is required.");
        RuleFor(x => x.EvaluatedByPersonId).NotEmpty().WithMessage("EvaluatedByPersonId is required.");
    }
}

internal sealed class EvaluateRequestCommandHandler : IRequestHandler<EvaluateRequestCommand, RequestDto>
{
    private readonly IRequestRepository _requestRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public EvaluateRequestCommandHandler(
        IRequestRepository requestRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<RequestDto> Handle(EvaluateRequestCommand request, CancellationToken cancellationToken)
    {
        var aggregate = await _requestRepository.GetByIdAsync(request.RequestId, cancellationToken);
        if (aggregate == null)
        {
            throw new InvalidOperationException($"Request with ID '{request.RequestId}' does not exist.");
        }

        var evaluator = await _organizationQueryService.GetPersonByIdAsync(request.EvaluatedByPersonId, cancellationToken);
        if (evaluator == null)
        {
            throw new InvalidOperationException($"Evaluator with PersonId '{request.EvaluatedByPersonId}' does not exist in Organization.");
        }

        var previousStatus = aggregate.Status;
        var now = _clock.UtcNow;

        aggregate.Evaluate(request.EvaluatedByPersonId, now, request.Notes);

        await _requestRepository.UpdateAsync(aggregate, cancellationToken);

        // Audit Logging (Architecture §18)
        await _requestRepository.AddStateHistoryAsync(
            aggregate.Id,
            fromStatus: previousStatus,
            toStatus: aggregate.Status,
            actorPersonId: request.EvaluatedByPersonId,
            reason: request.Notes ?? "Request evaluation started",
            changedAt: now,
            cancellationToken: cancellationToken);

        await _eventDispatcher.DispatchAndClearEventsAsync(aggregate, cancellationToken);

        var histories = await _requestRepository.GetStateHistoryAsync(aggregate.Id, cancellationToken);
        return await RequestDtoMapper.ToDtoAsync(
            aggregate,
            _organizationQueryService,
            _customerQueryService,
            _productQueryService,
            histories,
            cancellationToken);
    }
}

#endregion

#region AcceptRequestResponsibility (UC-REQ-004)

public sealed record AcceptRequestResponsibilityCommand(
    Guid RequestId,
    Guid AcceptedByPersonId,
    string? Notes = null) : IRequest<RequestDto>;

public sealed class AcceptRequestResponsibilityCommandValidator : AbstractValidator<AcceptRequestResponsibilityCommand>
{
    public AcceptRequestResponsibilityCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("RequestId is required.");
        RuleFor(x => x.AcceptedByPersonId).NotEmpty().WithMessage("AcceptedByPersonId is required.");
    }
}

internal sealed class AcceptRequestResponsibilityCommandHandler : IRequestHandler<AcceptRequestResponsibilityCommand, RequestDto>
{
    private readonly IRequestRepository _requestRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public AcceptRequestResponsibilityCommandHandler(
        IRequestRepository requestRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<RequestDto> Handle(AcceptRequestResponsibilityCommand request, CancellationToken cancellationToken)
    {
        var aggregate = await _requestRepository.GetByIdAsync(request.RequestId, cancellationToken);
        if (aggregate == null)
        {
            throw new InvalidOperationException($"Request with ID '{request.RequestId}' does not exist.");
        }

        var acceptor = await _organizationQueryService.GetPersonByIdAsync(request.AcceptedByPersonId, cancellationToken);
        if (acceptor == null)
        {
            throw new InvalidOperationException($"Actor with PersonId '{request.AcceptedByPersonId}' does not exist in Organization.");
        }

        var previousStatus = aggregate.Status;
        var now = _clock.UtcNow;

        aggregate.Accept(request.AcceptedByPersonId, now, request.Notes);

        await _requestRepository.UpdateAsync(aggregate, cancellationToken);

        // Audit Logging
        await _requestRepository.AddStateHistoryAsync(
            aggregate.Id,
            fromStatus: previousStatus,
            toStatus: aggregate.Status,
            actorPersonId: request.AcceptedByPersonId,
            reason: request.Notes ?? "Responsibility accepted",
            changedAt: now,
            cancellationToken: cancellationToken);

        await _eventDispatcher.DispatchAndClearEventsAsync(aggregate, cancellationToken);

        var histories = await _requestRepository.GetStateHistoryAsync(aggregate.Id, cancellationToken);
        return await RequestDtoMapper.ToDtoAsync(
            aggregate,
            _organizationQueryService,
            _customerQueryService,
            _productQueryService,
            histories,
            cancellationToken);
    }
}

#endregion

#region RejectRequest (UC-REQ-005)

public sealed record RejectRequestCommand(
    Guid RequestId,
    Guid RejectedByPersonId,
    string Reason) : IRequest<RequestDto>;

public sealed class RejectRequestCommandValidator : AbstractValidator<RejectRequestCommand>
{
    public RejectRequestCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("RequestId is required.");
        RuleFor(x => x.RejectedByPersonId).NotEmpty().WithMessage("RejectedByPersonId is required.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Rejection reason is required.");
    }
}

internal sealed class RejectRequestCommandHandler : IRequestHandler<RejectRequestCommand, RequestDto>
{
    private readonly IRequestRepository _requestRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public RejectRequestCommandHandler(
        IRequestRepository requestRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<RequestDto> Handle(RejectRequestCommand request, CancellationToken cancellationToken)
    {
        var aggregate = await _requestRepository.GetByIdAsync(request.RequestId, cancellationToken);
        if (aggregate == null)
        {
            throw new InvalidOperationException($"Request with ID '{request.RequestId}' does not exist.");
        }

        var rejector = await _organizationQueryService.GetPersonByIdAsync(request.RejectedByPersonId, cancellationToken);
        if (rejector == null)
        {
            throw new InvalidOperationException($"Rejector with PersonId '{request.RejectedByPersonId}' does not exist in Organization.");
        }

        var previousStatus = aggregate.Status;
        var now = _clock.UtcNow;

        aggregate.Reject(request.RejectedByPersonId, request.Reason, now);

        await _requestRepository.UpdateAsync(aggregate, cancellationToken);

        // Audit Logging
        await _requestRepository.AddStateHistoryAsync(
            aggregate.Id,
            fromStatus: previousStatus,
            toStatus: aggregate.Status,
            actorPersonId: request.RejectedByPersonId,
            reason: request.Reason,
            changedAt: now,
            cancellationToken: cancellationToken);

        await _eventDispatcher.DispatchAndClearEventsAsync(aggregate, cancellationToken);

        var histories = await _requestRepository.GetStateHistoryAsync(aggregate.Id, cancellationToken);
        return await RequestDtoMapper.ToDtoAsync(
            aggregate,
            _organizationQueryService,
            _customerQueryService,
            _productQueryService,
            histories,
            cancellationToken);
    }
}

#endregion

#region StartRequestProgress

public sealed record StartRequestProgressCommand(
    Guid RequestId,
    Guid ActorPersonId,
    string? Notes = null) : IRequest<RequestDto>;

public sealed class StartRequestProgressCommandValidator : AbstractValidator<StartRequestProgressCommand>
{
    public StartRequestProgressCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("RequestId is required.");
        RuleFor(x => x.ActorPersonId).NotEmpty().WithMessage("ActorPersonId is required.");
    }
}

internal sealed class StartRequestProgressCommandHandler : IRequestHandler<StartRequestProgressCommand, RequestDto>
{
    private readonly IRequestRepository _requestRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public StartRequestProgressCommandHandler(
        IRequestRepository requestRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<RequestDto> Handle(StartRequestProgressCommand request, CancellationToken cancellationToken)
    {
        var aggregate = await _requestRepository.GetByIdAsync(request.RequestId, cancellationToken);
        if (aggregate == null)
        {
            throw new InvalidOperationException($"Request with ID '{request.RequestId}' does not exist.");
        }

        var actor = await _organizationQueryService.GetPersonByIdAsync(request.ActorPersonId, cancellationToken);
        if (actor == null)
        {
            throw new InvalidOperationException($"Actor with PersonId '{request.ActorPersonId}' does not exist in Organization.");
        }

        var previousStatus = aggregate.Status;
        var now = _clock.UtcNow;

        aggregate.StartProgress(request.ActorPersonId, now, request.Notes);

        await _requestRepository.UpdateAsync(aggregate, cancellationToken);

        // Audit Logging
        await _requestRepository.AddStateHistoryAsync(
            aggregate.Id,
            fromStatus: previousStatus,
            toStatus: aggregate.Status,
            actorPersonId: request.ActorPersonId,
            reason: request.Notes ?? "Active progress started",
            changedAt: now,
            cancellationToken: cancellationToken);

        await _eventDispatcher.DispatchAndClearEventsAsync(aggregate, cancellationToken);

        var histories = await _requestRepository.GetStateHistoryAsync(aggregate.Id, cancellationToken);
        return await RequestDtoMapper.ToDtoAsync(
            aggregate,
            _organizationQueryService,
            _customerQueryService,
            _productQueryService,
            histories,
            cancellationToken);
    }
}

#endregion

#region EscalateRequest (UC-REQ-006)

public sealed record EscalateRequestCommand(
    Guid RequestId,
    Guid EscalatedByPersonId,
    string Reason,
    string? RequiredAssistance = null) : IRequest<RequestDto>;

public sealed class EscalateRequestCommandValidator : AbstractValidator<EscalateRequestCommand>
{
    public EscalateRequestCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("RequestId is required.");
        RuleFor(x => x.EscalatedByPersonId).NotEmpty().WithMessage("EscalatedByPersonId is required.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escalation reason is required.");
    }
}

internal sealed class EscalateRequestCommandHandler : IRequestHandler<EscalateRequestCommand, RequestDto>
{
    private readonly IRequestRepository _requestRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public EscalateRequestCommandHandler(
        IRequestRepository requestRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<RequestDto> Handle(EscalateRequestCommand request, CancellationToken cancellationToken)
    {
        var aggregate = await _requestRepository.GetByIdAsync(request.RequestId, cancellationToken);
        if (aggregate == null)
        {
            throw new InvalidOperationException($"Request with ID '{request.RequestId}' does not exist.");
        }

        var person = await _organizationQueryService.GetPersonByIdAsync(request.EscalatedByPersonId, cancellationToken);
        if (person == null)
        {
            throw new InvalidOperationException($"Person with ID '{request.EscalatedByPersonId}' does not exist in Organization.");
        }

        if (!aggregate.OwnerPersonId.HasValue || aggregate.OwnerPersonId.Value != request.EscalatedByPersonId)
        {
            throw new InvalidOperationException("Only the designated Request Owner can escalate the Request.");
        }

        var previousStatus = aggregate.Status;
        var now = _clock.UtcNow;

        aggregate.Escalate(request.EscalatedByPersonId, request.Reason, request.RequiredAssistance, now);

        await _requestRepository.UpdateAsync(aggregate, cancellationToken);

        // Audit Logging
        await _requestRepository.AddStateHistoryAsync(
            aggregate.Id,
            fromStatus: previousStatus,
            toStatus: aggregate.Status,
            actorPersonId: request.EscalatedByPersonId,
            reason: request.Reason,
            changedAt: now,
            cancellationToken: cancellationToken);

        await _eventDispatcher.DispatchAndClearEventsAsync(aggregate, cancellationToken);

        var histories = await _requestRepository.GetStateHistoryAsync(aggregate.Id, cancellationToken);
        return await RequestDtoMapper.ToDtoAsync(
            aggregate,
            _organizationQueryService,
            _customerQueryService,
            _productQueryService,
            histories,
            cancellationToken);
    }
}

#endregion

#region RequestManagementDecision (UC-REQ-007)

public sealed record RequestManagementDecisionCommand(
    Guid RequestId,
    Guid RequestedByPersonId,
    string Question,
    string? Options = null,
    string? Impact = null) : IRequest<RequestDto>;

public sealed class RequestManagementDecisionCommandValidator : AbstractValidator<RequestManagementDecisionCommand>
{
    public RequestManagementDecisionCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("RequestId is required.");
        RuleFor(x => x.RequestedByPersonId).NotEmpty().WithMessage("RequestedByPersonId is required.");
        RuleFor(x => x.Question).NotEmpty().WithMessage("Question is required.");
    }
}

internal sealed class RequestManagementDecisionCommandHandler : IRequestHandler<RequestManagementDecisionCommand, RequestDto>
{
    private readonly IRequestRepository _requestRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public RequestManagementDecisionCommandHandler(
        IRequestRepository requestRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<RequestDto> Handle(RequestManagementDecisionCommand request, CancellationToken cancellationToken)
    {
        var aggregate = await _requestRepository.GetByIdAsync(request.RequestId, cancellationToken);
        if (aggregate == null)
        {
            throw new InvalidOperationException($"Request with ID '{request.RequestId}' does not exist.");
        }

        var person = await _organizationQueryService.GetPersonByIdAsync(request.RequestedByPersonId, cancellationToken);
        if (person == null)
        {
            throw new InvalidOperationException($"Person with ID '{request.RequestedByPersonId}' does not exist in Organization.");
        }

        if (!aggregate.OwnerPersonId.HasValue || aggregate.OwnerPersonId.Value != request.RequestedByPersonId)
        {
            throw new InvalidOperationException("Only the designated Request Owner can request a management decision.");
        }

        var now = _clock.UtcNow;

        aggregate.RequestManagementDecision(request.RequestedByPersonId, request.Question, request.Options, request.Impact, now);

        await _requestRepository.UpdateAsync(aggregate, cancellationToken);

        // Audit Logging: Record condition change
        await _requestRepository.AddStateHistoryAsync(
            aggregate.Id,
            fromStatus: aggregate.Status,
            toStatus: aggregate.Status,
            actorPersonId: request.RequestedByPersonId,
            reason: $"Management Decision Requested: {request.Question}",
            changedAt: now,
            cancellationToken: cancellationToken);

        await _eventDispatcher.DispatchAndClearEventsAsync(aggregate, cancellationToken);

        var histories = await _requestRepository.GetStateHistoryAsync(aggregate.Id, cancellationToken);
        return await RequestDtoMapper.ToDtoAsync(
            aggregate,
            _organizationQueryService,
            _customerQueryService,
            _productQueryService,
            histories,
            cancellationToken);
    }
}

#endregion

#region ReviewRequestCompletion (UC-REQ-008)

public sealed record ReviewRequestCompletionCommand(
    Guid RequestId,
    Guid ReviewerPersonId,
    bool AcceptResolution,
    string SummaryOrFeedback,
    string? Outcome = null) : IRequest<RequestDto>;

public sealed class ReviewRequestCompletionCommandValidator : AbstractValidator<ReviewRequestCompletionCommand>
{
    public ReviewRequestCompletionCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("RequestId is required.");
        RuleFor(x => x.ReviewerPersonId).NotEmpty().WithMessage("ReviewerPersonId is required.");
        RuleFor(x => x.SummaryOrFeedback).NotEmpty().WithMessage("Summary or rework feedback is required.");
    }
}

internal sealed class ReviewRequestCompletionCommandHandler : IRequestHandler<ReviewRequestCompletionCommand, RequestDto>
{
    private readonly IRequestRepository _requestRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public ReviewRequestCompletionCommandHandler(
        IRequestRepository requestRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<RequestDto> Handle(ReviewRequestCompletionCommand request, CancellationToken cancellationToken)
    {
        var aggregate = await _requestRepository.GetByIdAsync(request.RequestId, cancellationToken);
        if (aggregate == null)
        {
            throw new InvalidOperationException($"Request with ID '{request.RequestId}' does not exist.");
        }

        var reviewer = await _organizationQueryService.GetPersonByIdAsync(request.ReviewerPersonId, cancellationToken);
        if (reviewer == null)
        {
            throw new InvalidOperationException($"Reviewer with PersonId '{request.ReviewerPersonId}' does not exist in Organization.");
        }

        var previousStatus = aggregate.Status;
        var now = _clock.UtcNow;

        if (request.AcceptResolution)
        {
            aggregate.Complete(
                request.ReviewerPersonId,
                request.SummaryOrFeedback,
                now,
                request.Outcome ?? RequestResolution.OutcomeResolved);
        }
        else
        {
            aggregate.RequestRework(
                request.ReviewerPersonId,
                request.SummaryOrFeedback,
                now);
        }

        await _requestRepository.UpdateAsync(aggregate, cancellationToken);

        // Audit Logging
        await _requestRepository.AddStateHistoryAsync(
            aggregate.Id,
            fromStatus: previousStatus,
            toStatus: aggregate.Status,
            actorPersonId: request.ReviewerPersonId,
            reason: request.SummaryOrFeedback,
            changedAt: now,
            cancellationToken: cancellationToken);

        await _eventDispatcher.DispatchAndClearEventsAsync(aggregate, cancellationToken);

        var histories = await _requestRepository.GetStateHistoryAsync(aggregate.Id, cancellationToken);
        return await RequestDtoMapper.ToDtoAsync(
            aggregate,
            _organizationQueryService,
            _customerQueryService,
            _productQueryService,
            histories,
            cancellationToken);
    }
}

#endregion

#region ReassignRequestOwnership (UC-MGT-001)

public sealed record ReassignRequestOwnershipCommand(
    Guid RequestId,
    Guid NewOwnerPersonId,
    Guid ReassignedByPersonId,
    string? Reason = null) : IRequest<RequestDto>;

public sealed class ReassignRequestOwnershipCommandValidator : AbstractValidator<ReassignRequestOwnershipCommand>
{
    public ReassignRequestOwnershipCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("RequestId is required.");
        RuleFor(x => x.NewOwnerPersonId).NotEmpty().WithMessage("NewOwnerPersonId is required.");
        RuleFor(x => x.ReassignedByPersonId).NotEmpty().WithMessage("ReassignedByPersonId is required.");
    }
}

internal sealed class ReassignRequestOwnershipCommandHandler : IRequestHandler<ReassignRequestOwnershipCommand, RequestDto>
{
    private readonly IRequestRepository _requestRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public ReassignRequestOwnershipCommandHandler(
        IRequestRepository requestRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<RequestDto> Handle(ReassignRequestOwnershipCommand request, CancellationToken cancellationToken)
    {
        var aggregate = await _requestRepository.GetByIdAsync(request.RequestId, cancellationToken);
        if (aggregate == null)
        {
            throw new InvalidOperationException($"Request with ID '{request.RequestId}' does not exist.");
        }

        if (aggregate.IsTerminal)
        {
            throw new InvalidOperationException($"Cannot reassign owner of a closed/terminal Request in status '{aggregate.Status}'.");
        }

        // Validate reassigner person exists in Organization
        var reassigner = await _organizationQueryService.GetPersonByIdAsync(request.ReassignedByPersonId, cancellationToken);
        if (reassigner == null)
        {
            throw new InvalidOperationException($"Reassigning actor with PersonId '{request.ReassignedByPersonId}' does not exist in Organization.");
        }

        // Validate new assignee exists and is active in Organization
        var newOwner = await _organizationQueryService.GetPersonByIdAsync(request.NewOwnerPersonId, cancellationToken);
        if (newOwner == null)
        {
            throw new InvalidOperationException($"Assignee with PersonId '{request.NewOwnerPersonId}' does not exist in Organization.");
        }
        if (!string.Equals(newOwner.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Assignee '{newOwner.Name}' is not active in Organization.");
        }

        // Reassignment to identical owner is rejected as redundant no-op (FEAT-REQ-002)
        if (aggregate.OwnerPersonId.HasValue && aggregate.OwnerPersonId.Value == request.NewOwnerPersonId)
        {
            throw new InvalidOperationException($"Request '{request.RequestId}' is already assigned to person '{newOwner.Name}'.");
        }

        var now = _clock.UtcNow;
        aggregate.AssignOwner(request.NewOwnerPersonId, request.ReassignedByPersonId, now, request.Reason);

        await _requestRepository.UpdateAsync(aggregate, cancellationToken);

        // Audit Logging (Architecture §18)
        await _requestRepository.AddStateHistoryAsync(
            aggregate.Id,
            fromStatus: aggregate.Status,
            toStatus: aggregate.Status,
            actorPersonId: request.ReassignedByPersonId,
            reason: $"Ownership reassigned to {newOwner.Name}" + (string.IsNullOrWhiteSpace(request.Reason) ? "" : $": {request.Reason}"),
            changedAt: now,
            cancellationToken: cancellationToken);

        await _eventDispatcher.DispatchAndClearEventsAsync(aggregate, cancellationToken);

        var histories = await _requestRepository.GetStateHistoryAsync(aggregate.Id, cancellationToken);
        return await RequestDtoMapper.ToDtoAsync(
            aggregate,
            _organizationQueryService,
            _customerQueryService,
            _productQueryService,
            histories,
            cancellationToken);
    }
}

#endregion

#region ResolveEscalation

public sealed record ResolveEscalationCommand(
    Guid RequestId,
    Guid ActorPersonId,
    string? Notes = null) : IRequest<RequestDto>;

public sealed class ResolveEscalationCommandValidator : AbstractValidator<ResolveEscalationCommand>
{
    public ResolveEscalationCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("RequestId is required.");
        RuleFor(x => x.ActorPersonId).NotEmpty().WithMessage("ActorPersonId is required.");
    }
}

internal sealed class ResolveEscalationCommandHandler : IRequestHandler<ResolveEscalationCommand, RequestDto>
{
    private readonly IRequestRepository _requestRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public ResolveEscalationCommandHandler(
        IRequestRepository requestRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<RequestDto> Handle(ResolveEscalationCommand request, CancellationToken cancellationToken)
    {
        var aggregate = await _requestRepository.GetByIdAsync(request.RequestId, cancellationToken);
        if (aggregate == null)
        {
            throw new InvalidOperationException($"Request with ID '{request.RequestId}' does not exist.");
        }

        var actor = await _organizationQueryService.GetPersonByIdAsync(request.ActorPersonId, cancellationToken);
        if (actor == null)
        {
            throw new InvalidOperationException($"Actor with PersonId '{request.ActorPersonId}' does not exist in Organization.");
        }

        var previousStatus = aggregate.Status;
        var now = _clock.UtcNow;

        aggregate.ResolveEscalation(request.ActorPersonId, now, request.Notes);

        await _requestRepository.UpdateAsync(aggregate, cancellationToken);

        // Audit Logging (Architecture §18)
        await _requestRepository.AddStateHistoryAsync(
            aggregate.Id,
            fromStatus: previousStatus,
            toStatus: aggregate.Status,
            actorPersonId: request.ActorPersonId,
            reason: request.Notes ?? "Escalation resolved",
            changedAt: now,
            cancellationToken: cancellationToken);

        await _eventDispatcher.DispatchAndClearEventsAsync(aggregate, cancellationToken);

        var histories = await _requestRepository.GetStateHistoryAsync(aggregate.Id, cancellationToken);
        return await RequestDtoMapper.ToDtoAsync(
            aggregate,
            _organizationQueryService,
            _customerQueryService,
            _productQueryService,
            histories,
            cancellationToken);
    }
}

#endregion

