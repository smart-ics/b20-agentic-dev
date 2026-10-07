using Cakra.Core;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Cakra.Modules.Product;
using Cakra.Modules.Request;
using Cakra.Modules.WorkPackage.Domain;
using Cakra.Modules.WorkPackage.Domain.Exceptions;
using Cakra.Modules.WorkPackage.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cakra.Modules.WorkPackage.Services;

/// <summary>
/// Application command service and MediatR command handler for Work Package lifecycle and scope operations
/// (Architecture §6, §7, §8, §11, §15, §18, §19.2).
/// Validates cross-module references via <see cref="IOrganizationQueryService"/>,
/// <see cref="ICustomerQueryService"/>, <see cref="IProductQueryService"/>, and
/// <see cref="IRequestQueryService"/> without cross-schema writes.
/// </summary>
public sealed class WorkPackageService :
    IWorkPackageService,
    IRequestHandler<CreateWorkPackageCommand, WorkPackageDto>,
    IRequestHandler<UpdateWorkPackageDeadlineCommand, WorkPackageDto>,
    IRequestHandler<UpdateObjectiveCommand, WorkPackageDto>,
    IRequestHandler<AssignOwnerCommand, WorkPackageDto>,
    IRequestHandler<AssignWorkPackageOwnerCommand, WorkPackageDto>,
    IRequestHandler<AddRequestToWorkPackageCommand, WorkPackageDto>,
    IRequestHandler<RemoveRequestFromWorkPackageCommand, WorkPackageDto>,
    IRequestHandler<ActivateWorkPackageCommand, WorkPackageDto>,
    IRequestHandler<CloseWorkPackageCommand, WorkPackageDto>,
    IRequestHandler<ReorderWorkPackageRequestsCommand, WorkPackageDto>
{
    private readonly IWorkPackageRepository _workPackageRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly IRequestQueryService _requestQueryService;
    private readonly IDomainEventDispatcher? _eventDispatcher;
    private readonly ICurrentContextProvider? _currentContextProvider;
    private readonly IAuditContext? _auditContext;
    private readonly ISystemClock? _clock;
    private readonly ILogger<WorkPackageService> _logger;

    public WorkPackageService(
        IDbConnectionFactory connectionFactory,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        IRequestQueryService requestQueryService,
        IDomainEventDispatcher? eventDispatcher = null,
        ICurrentContextProvider? currentContextProvider = null,
        IAuditContext? auditContext = null,
        ISystemClock? clock = null)
        : this(
            new WorkPackageRepository(connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory))),
            organizationQueryService,
            customerQueryService,
            productQueryService,
            requestQueryService,
            eventDispatcher,
            currentContextProvider,
            auditContext,
            clock,
            null)
    {
    }

    internal WorkPackageService(
        IWorkPackageRepository workPackageRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        IRequestQueryService requestQueryService,
        IDomainEventDispatcher? eventDispatcher = null,
        ICurrentContextProvider? currentContextProvider = null,
        IAuditContext? auditContext = null,
        ISystemClock? clock = null,
        ILogger<WorkPackageService>? logger = null)
    {
        _workPackageRepository = workPackageRepository ?? throw new ArgumentNullException(nameof(workPackageRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _requestQueryService = requestQueryService ?? throw new ArgumentNullException(nameof(requestQueryService));
        _eventDispatcher = eventDispatcher;
        _currentContextProvider = currentContextProvider;
        _auditContext = auditContext;
        _clock = clock;
        _logger = logger ?? NullLogger<WorkPackageService>.Instance;
    }

    private DateTime UtcNow => _clock?.UtcNow ?? DateTime.UtcNow;

    /// <inheritdoc />
    public async Task<WorkPackageDto> CreateWorkPackageAsync(
        string name,
        string objective,
        Guid ownerPersonId,
        Guid? customerId = null,
        Guid? productId = null,
        DateTime? deadline = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Work package name cannot be null or empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(objective))
        {
            throw new ArgumentException("Work package objective cannot be null or empty.", nameof(objective));
        }

        if (ownerPersonId == Guid.Empty)
        {
            throw new ArgumentException("Owner PersonId cannot be empty.", nameof(ownerPersonId));
        }

        await ValidateOwnerAsync(ownerPersonId, cancellationToken);

        if (customerId.HasValue)
        {
            await ValidateCustomerAsync(customerId.Value, cancellationToken);
        }

        if (productId.HasValue)
        {
            await ValidateProductAsync(productId.Value, cancellationToken);
        }

        var now = UtcNow;
        var workPackage = Domain.WorkPackage.Create(
            name: name,
            objective: objective,
            ownerPersonId: ownerPersonId,
            customerId: customerId,
            productId: productId,
            createdAtUtc: now,
            deadline: deadline);

        await _workPackageRepository.AddAsync(workPackage, cancellationToken);
        await DispatchDomainEventsAsync(workPackage, cancellationToken);

        _logger.LogInformation(
            "WorkPackage {WorkPackageId} created in {Status} state by ActorPersonId {ActorPersonId} with OwnerPersonId {OwnerPersonId}",
            workPackage.Id,
            workPackage.Status.ToName(),
            ResolveActorPersonId(ownerPersonId),
            ownerPersonId);

        return WorkPackageDto.FromDomain(workPackage);
    }

    /// <inheritdoc />
    public async Task<WorkPackageDto> UpdateObjectiveAsync(
        Guid workPackageId,
        string name,
        string objective,
        CancellationToken cancellationToken = default)
    {
        if (workPackageId == Guid.Empty)
        {
            throw new ArgumentException("WorkPackageId cannot be empty.", nameof(workPackageId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Work package name cannot be null or empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(objective))
        {
            throw new ArgumentException("Work package objective cannot be null or empty.", nameof(objective));
        }

        var workPackage = await GetRequiredWorkPackageAsync(workPackageId, cancellationToken);

        var now = UtcNow;
        workPackage.UpdateObjective(name, objective, now);

        await _workPackageRepository.UpdateAsync(workPackage, cancellationToken);
        await DispatchDomainEventsAsync(workPackage, cancellationToken);

        _logger.LogInformation(
            "WorkPackage {WorkPackageId} objective updated by ActorPersonId {ActorPersonId}",
            workPackage.Id,
            ResolveActorPersonId(workPackage.OwnerPersonId));

        return WorkPackageDto.FromDomain(workPackage);
    }

    /// <inheritdoc />
    public async Task<WorkPackageDto> UpdateDeadlineAsync(
        Guid workPackageId,
        DateTime? deadline,
        CancellationToken cancellationToken = default)
    {
        if (workPackageId == Guid.Empty)
        {
            throw new ArgumentException("WorkPackageId cannot be empty.", nameof(workPackageId));
        }

        var workPackage = await GetRequiredWorkPackageAsync(workPackageId, cancellationToken);

        var now = UtcNow;
        workPackage.UpdateDeadline(deadline, now);

        await _workPackageRepository.UpdateAsync(workPackage, cancellationToken);
        await DispatchDomainEventsAsync(workPackage, cancellationToken);

        _logger.LogInformation(
            "WorkPackage {WorkPackageId} deadline updated to {Deadline} by ActorPersonId {ActorPersonId}",
            workPackage.Id,
            workPackage.Deadline,
            ResolveActorPersonId(workPackage.OwnerPersonId));

        return WorkPackageDto.FromDomain(workPackage);
    }

    /// <inheritdoc />
    public async Task<WorkPackageDto> AssignOwnerAsync(
        Guid workPackageId,
        Guid newOwnerPersonId,
        CancellationToken cancellationToken = default)
    {
        if (workPackageId == Guid.Empty)
        {
            throw new ArgumentException("WorkPackageId cannot be empty.", nameof(workPackageId));
        }

        if (newOwnerPersonId == Guid.Empty)
        {
            throw new ArgumentException("New owner PersonId cannot be empty.", nameof(newOwnerPersonId));
        }

        var workPackage = await GetRequiredWorkPackageAsync(workPackageId, cancellationToken);

        await ValidateOwnerAsync(newOwnerPersonId, cancellationToken);

        var previousOwnerId = workPackage.OwnerPersonId;
        var now = UtcNow;
        workPackage.AssignOwner(newOwnerPersonId, now);

        await _workPackageRepository.UpdateAsync(workPackage, cancellationToken);
        await DispatchDomainEventsAsync(workPackage, cancellationToken);

        _logger.LogInformation(
            "WorkPackage {WorkPackageId} owner reassigned from {PreviousOwnerPersonId} to {NewOwnerPersonId} by ActorPersonId {ActorPersonId}",
            workPackage.Id,
            previousOwnerId,
            newOwnerPersonId,
            ResolveActorPersonId(newOwnerPersonId));

        return WorkPackageDto.FromDomain(workPackage);
    }

    /// <inheritdoc />
    public async Task<WorkPackageDto> AddRequestToWorkPackageAsync(
        Guid workPackageId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (workPackageId == Guid.Empty)
        {
            throw new ArgumentException("WorkPackageId cannot be empty.", nameof(workPackageId));
        }

        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("RequestId cannot be empty.", nameof(requestId));
        }

        var workPackage = await GetRequiredWorkPackageAsync(workPackageId, cancellationToken);

        await ValidateRequestExistsAsync(requestId, cancellationToken);

        var isInAnotherActivePackage = await _workPackageRepository.IsRequestInActiveWorkPackageAsync(
            requestId,
            workPackage.Id,
            cancellationToken);

        var now = UtcNow;
        var checker = new PrecomputedActiveWorkPackageChecker(isInAnotherActivePackage);
        var membership = workPackage.AddRequest(requestId, checker, now);

        await _workPackageRepository.AddRequestMembershipAsync(membership, cancellationToken);
        await _workPackageRepository.UpdateAsync(workPackage, cancellationToken);
        await DispatchDomainEventsAsync(workPackage, cancellationToken);

        _logger.LogInformation(
            "Request {RequestId} added to WorkPackage {WorkPackageId} by ActorPersonId {ActorPersonId}",
            requestId,
            workPackage.Id,
            ResolveActorPersonId(workPackage.OwnerPersonId));

        return WorkPackageDto.FromDomain(workPackage);
    }

    /// <inheritdoc />
    public async Task<WorkPackageDto> RemoveRequestFromWorkPackageAsync(
        Guid workPackageId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (workPackageId == Guid.Empty)
        {
            throw new ArgumentException("WorkPackageId cannot be empty.", nameof(workPackageId));
        }

        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("RequestId cannot be empty.", nameof(requestId));
        }

        var workPackage = await GetRequiredWorkPackageAsync(workPackageId, cancellationToken);

        var activeMembership = workPackage.ActiveRequests.FirstOrDefault(r => r.RequestId == requestId);
        var now = UtcNow;
        if (activeMembership is not null && now < activeMembership.AddedAt)
        {
            now = activeMembership.AddedAt;
        }

        workPackage.RemoveRequest(requestId, now);

        if (activeMembership is not null)
        {
            await _workPackageRepository.UpdateRequestMembershipAsync(activeMembership, cancellationToken);
        }

        await _workPackageRepository.UpdateAsync(workPackage, cancellationToken);
        await DispatchDomainEventsAsync(workPackage, cancellationToken);

        _logger.LogInformation(
            "Request {RequestId} removed from WorkPackage {WorkPackageId} by ActorPersonId {ActorPersonId}",
            requestId,
            workPackage.Id,
            ResolveActorPersonId(workPackage.OwnerPersonId));

        return WorkPackageDto.FromDomain(workPackage);
    }

    /// <inheritdoc />
    public async Task<WorkPackageDto> ActivateWorkPackageAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default)
    {
        if (workPackageId == Guid.Empty)
        {
            throw new ArgumentException("WorkPackageId cannot be empty.", nameof(workPackageId));
        }

        var workPackage = await GetRequiredWorkPackageAsync(workPackageId, cancellationToken);

        foreach (var activeRequest in workPackage.ActiveRequests)
        {
            var isInAnotherActivePackage = await _workPackageRepository.IsRequestInActiveWorkPackageAsync(
                activeRequest.RequestId,
                workPackage.Id,
                cancellationToken);

            if (isInAnotherActivePackage)
            {
                throw new BusinessRuleViolationException(
                    9,
                    $"Request '{activeRequest.RequestId}' already belongs to another active Work Package.");
            }
        }

        var previousStatus = workPackage.Status;
        var now = UtcNow;
        workPackage.Activate(now);

        await _workPackageRepository.UpdateAsync(workPackage, cancellationToken);
        await DispatchDomainEventsAsync(workPackage, cancellationToken);

        _logger.LogInformation(
            "WorkPackage {WorkPackageId} transitioned from {PreviousStatus} to {NewStatus} by ActorPersonId {ActorPersonId}",
            workPackage.Id,
            previousStatus.ToName(),
            workPackage.Status.ToName(),
            ResolveActorPersonId(workPackage.OwnerPersonId));

        return WorkPackageDto.FromDomain(workPackage);
    }

    /// <inheritdoc />
    public async Task<WorkPackageDto> CloseWorkPackageAsync(
        Guid workPackageId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (workPackageId == Guid.Empty)
        {
            throw new ArgumentException("WorkPackageId cannot be empty.", nameof(workPackageId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Close reason cannot be null or empty.", nameof(reason));
        }

        var workPackage = await GetRequiredWorkPackageAsync(workPackageId, cancellationToken);

        var previousStatus = workPackage.Status;
        var now = UtcNow;
        workPackage.Close(reason, now);

        await _workPackageRepository.UpdateAsync(workPackage, cancellationToken);
        await DispatchDomainEventsAsync(workPackage, cancellationToken);

        _logger.LogInformation(
            "WorkPackage {WorkPackageId} transitioned from {PreviousStatus} to {NewStatus} with reason '{Reason}' by ActorPersonId {ActorPersonId}",
            workPackage.Id,
            previousStatus.ToName(),
            workPackage.Status.ToName(),
            workPackage.ClosedReason,
            ResolveActorPersonId(workPackage.OwnerPersonId));

        return WorkPackageDto.FromDomain(workPackage);
    }

    /// <inheritdoc />
    public async Task<WorkPackageDto> ReorderRequestsAsync(
        Guid workPackageId,
        IReadOnlyList<Guid> orderedRequestIds,
        CancellationToken cancellationToken = default)
    {
        if (workPackageId == Guid.Empty)
        {
            throw new ArgumentException("WorkPackageId cannot be empty.", nameof(workPackageId));
        }

        ArgumentNullException.ThrowIfNull(orderedRequestIds);

        var workPackage = await GetRequiredWorkPackageAsync(workPackageId, cancellationToken);

        var now = UtcNow;
        workPackage.ReorderRequests(orderedRequestIds, now);

        await _workPackageRepository.SaveAsync(workPackage, cancellationToken);
        await DispatchDomainEventsAsync(workPackage, cancellationToken);

        _logger.LogInformation(
            "Reordered {Count} requests in WorkPackage {WorkPackageId} by ActorPersonId {ActorPersonId}",
            orderedRequestIds.Count,
            workPackage.Id,
            ResolveActorPersonId(workPackage.OwnerPersonId));

        return WorkPackageDto.FromDomain(workPackage);
    }

    // MediatR command handler entry points
    public Task<WorkPackageDto> Handle(CreateWorkPackageCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CreateWorkPackageAsync(
            request.Name,
            request.Objective,
            request.OwnerPersonId,
            request.CustomerId,
            request.ProductId,
            request.Deadline,
            cancellationToken);
    }

    public Task<WorkPackageDto> Handle(UpdateWorkPackageDeadlineCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return UpdateDeadlineAsync(
            request.WorkPackageId,
            request.Deadline,
            cancellationToken);
    }

    public Task<WorkPackageDto> Handle(UpdateObjectiveCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return UpdateObjectiveAsync(
            request.WorkPackageId,
            request.Name,
            request.Objective,
            cancellationToken);
    }

    public Task<WorkPackageDto> Handle(AssignOwnerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return AssignOwnerAsync(
            request.WorkPackageId,
            request.NewOwnerPersonId,
            cancellationToken);
    }

    public Task<WorkPackageDto> Handle(AssignWorkPackageOwnerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return AssignOwnerAsync(
            request.WorkPackageId,
            request.NewOwnerPersonId,
            cancellationToken);
    }

    public Task<WorkPackageDto> Handle(AddRequestToWorkPackageCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return AddRequestToWorkPackageAsync(
            request.WorkPackageId,
            request.RequestId,
            cancellationToken);
    }

    public Task<WorkPackageDto> Handle(RemoveRequestFromWorkPackageCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RemoveRequestFromWorkPackageAsync(
            request.WorkPackageId,
            request.RequestId,
            cancellationToken);
    }

    public Task<WorkPackageDto> Handle(ActivateWorkPackageCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ActivateWorkPackageAsync(request.WorkPackageId, cancellationToken);
    }

    public Task<WorkPackageDto> Handle(CloseWorkPackageCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CloseWorkPackageAsync(
            request.WorkPackageId,
            request.Reason,
            cancellationToken);
    }

    public Task<WorkPackageDto> Handle(ReorderWorkPackageRequestsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ReorderRequestsAsync(
            request.WorkPackageId,
            request.OrderedRequestIds,
            cancellationToken);
    }

    private async Task<Domain.WorkPackage> GetRequiredWorkPackageAsync(
        Guid workPackageId,
        CancellationToken cancellationToken)
    {
        return await _workPackageRepository.GetByIdAsync(workPackageId, cancellationToken)
            ?? throw new KeyNotFoundException($"WorkPackage '{workPackageId}' was not found.");
    }

    private async Task ValidateOwnerAsync(Guid ownerPersonId, CancellationToken cancellationToken)
    {
        if (ownerPersonId == Guid.Empty)
        {
            throw new ArgumentException("Owner PersonId cannot be empty.", nameof(ownerPersonId));
        }

        var isActive = await _organizationQueryService.IsPersonActiveAsync(ownerPersonId, cancellationToken);
        if (isActive)
        {
            return;
        }

        var person = await _organizationQueryService.GetPersonByIdAsync(ownerPersonId, cancellationToken);
        if (person is not null)
        {
            if (person.IsActive)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Person '{ownerPersonId}' is not active in Organization and cannot be assigned as work package owner.");
        }

        throw new KeyNotFoundException(
            $"Owner person '{ownerPersonId}' was not found in Organization.");
    }

    private async Task ValidateCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId cannot be empty.", nameof(customerId));
        }

        var isActive = await _customerQueryService.IsCustomerActiveAsync(customerId, cancellationToken);
        if (isActive)
        {
            return;
        }

        var customer = await _customerQueryService.GetCustomerByIdAsync(customerId, cancellationToken);
        if (customer is not null)
        {
            if (customer.IsActive)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Customer '{customerId}' is not active and cannot be associated with a work package.");
        }

        throw new KeyNotFoundException(
            $"Customer '{customerId}' was not found.");
    }

    private async Task ValidateProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId cannot be empty.", nameof(productId));
        }

        var isActive = await _productQueryService.IsProductActiveAsync(productId, cancellationToken);
        if (isActive)
        {
            return;
        }

        var product = await _productQueryService.GetProductByIdAsync(productId, cancellationToken);
        if (product is not null)
        {
            if (product.IsActive)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Product '{productId}' is not active and cannot be associated with a work package.");
        }

        throw new KeyNotFoundException(
            $"Product '{productId}' was not found.");
    }

    private async Task ValidateRequestExistsAsync(Guid requestId, CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("RequestId cannot be empty.", nameof(requestId));
        }

        var exists = await _requestQueryService.RequestExistsAsync(requestId, cancellationToken);
        if (exists)
        {
            return;
        }

        var request = await _requestQueryService.GetRequestByIdAsync(requestId, cancellationToken);
        if (request is not null)
        {
            return;
        }

        throw new KeyNotFoundException(
            $"Request '{requestId}' was not found.");
    }

    private Guid ResolveActorPersonId(Guid fallbackPersonId)
    {
        if (_currentContextProvider?.CurrentPersonId is { } currentPersonId && currentPersonId != Guid.Empty)
        {
            return currentPersonId;
        }

        if (_auditContext?.ActorPersonId is { } auditPersonId && auditPersonId != Guid.Empty)
        {
            return auditPersonId;
        }

        return fallbackPersonId;
    }

    private async Task DispatchDomainEventsAsync(
        Domain.WorkPackage workPackage,
        CancellationToken cancellationToken)
    {
        if (_eventDispatcher is not null)
        {
            foreach (var domainEvent in workPackage.DomainEvents.ToList())
            {
                await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
            }
        }

        workPackage.ClearDomainEvents();
    }

    private sealed class PrecomputedActiveWorkPackageChecker : IActiveWorkPackageChecker
    {
        private readonly bool _isInActiveWorkPackage;

        public PrecomputedActiveWorkPackageChecker(bool isInActiveWorkPackage)
        {
            _isInActiveWorkPackage = isInActiveWorkPackage;
        }

        public bool IsRequestInActiveWorkPackage(Guid requestId, Guid? excludingWorkPackageId = null)
            => _isInActiveWorkPackage;
    }
}
