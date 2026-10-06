using Cakra.Core;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Cakra.Modules.Product;
using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Domain.Exceptions;
using Cakra.Modules.Request.Persistence;
using MediatR;

namespace Cakra.Modules.Request.Services;

/// <summary>
/// Application command service and MediatR command handler for Request lifecycle operations
/// (Architecture §6, §7, §8, §15, §18, §19.2).
/// Validates cross-module references via <see cref="IOrganizationQueryService"/>,
/// <see cref="ICustomerQueryService"/>, and <see cref="IProductQueryService"/> without cross-schema writes.
/// Records every state change in <c>request.RequestAssignments</c> (Architecture §18 Audit Logging).
/// </summary>
public sealed partial class RequestService :
    IRequestService,
    IRequestHandler<RecordRequestCommand, RequestDto>,
    IRequestHandler<AssignRequestOwnerCommand, RequestDto>,
    IRequestHandler<StartWorkCommand, RequestDto>,
    IRequestHandler<PauseWorkCommand, RequestDto>,
    IRequestHandler<CancelRequestCommand, RequestDto>,
    IRequestHandler<ReassignRequestOwnershipCommand, RequestDto>,
    IRequestHandler<ReviewRequestCompletionCommand, RequestDto>,
    IRequestHandler<CompleteRequestCommand, RequestDto>,
    IRequestHandler<UpdateRequestComplexityCommand, RequestDto>,
    IRequestHandler<AddRequestSubTaskCommand, RequestDto>,
    IRequestHandler<CompleteRequestSubTaskCommand, RequestDto>,
    IRequestHandler<ReopenRequestSubTaskCommand, RequestDto>,
    IRequestHandler<RemoveRequestSubTaskCommand, RequestDto>,
    IRequestHandler<UpdateRequestCoreAttributesCommand, RequestDto>
{
    private static readonly HashSet<string> AuthorizedComplexityRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Programmer",
        "Administrator",
        "Admin",
        "Developer",
        "Team Lead",
        "Manager"
    };

    private static readonly HashSet<string> AuthorizedSubTaskRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Programmer",
        "Administrator",
        "Admin",
        "Developer",
        "Team Lead",
        "Manager"
    };

    private static readonly HashSet<string> AuthorizedCoreAttributesRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Administrator",
        "Admin",
        "Manager"
    };

    /// <summary>
    /// Fallback system actor PersonId used only when commands execute outside an authenticated
    /// HTTP request context and no explicit <c>ActorPersonId</c> is supplied.
    /// </summary>
    internal static readonly Guid SystemActorPersonId = new("00000000-0000-0000-0000-000000000001");

    private readonly IRequestRepository _requestRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly IDomainEventDispatcher? _eventDispatcher;
    private readonly ICurrentContextProvider? _currentContextProvider;
    private readonly IAuditContext? _auditContext;
    private readonly ISystemClock? _clock;

    internal async Task ValidateComplexityAuthorizationAsync(Guid actorPersonId, CancellationToken cancellationToken)
    {
        if (actorPersonId == SystemActorPersonId)
        {
            return;
        }

        var roles = await _organizationQueryService.GetPersonRolesAsync(actorPersonId, cancellationToken);
        if (!roles.Any(r => AuthorizedComplexityRoles.Contains(r)))
        {
            throw new UnauthorizedAccessException(
                $"Actor '{actorPersonId}' does not possess an authorized role to modify request complexity.");
        }
    }

    internal async Task ValidateSubTaskManagementAuthorizationAsync(
        Domain.Request request,
        Guid actorPersonId,
        CancellationToken cancellationToken)
    {
        if (actorPersonId == SystemActorPersonId)
        {
            return;
        }

        if (request.OwnerPersonId.HasValue && request.OwnerPersonId.Value == actorPersonId)
        {
            return;
        }

        var roles = await _organizationQueryService.GetPersonRolesAsync(actorPersonId, cancellationToken);
        if (roles.Any(r => AuthorizedSubTaskRoles.Contains(r)))
        {
            return;
        }

        throw new UnauthorizedAccessException(
            $"Actor '{actorPersonId}' does not possess an authorized role or request ownership to manage sub-tasks on request '{request.Id}'.");
    }

    internal async Task ValidateSubTaskCompletionAuthorizationAsync(
        Domain.Request request,
        RequestSubTask subTask,
        Guid actorPersonId,
        CancellationToken cancellationToken)
    {
        if (actorPersonId == SystemActorPersonId)
        {
            return;
        }

        if (subTask.AssigneePersonId.HasValue && subTask.AssigneePersonId.Value == actorPersonId)
        {
            return;
        }

        if (request.OwnerPersonId.HasValue && request.OwnerPersonId.Value == actorPersonId)
        {
            return;
        }

        var roles = await _organizationQueryService.GetPersonRolesAsync(actorPersonId, cancellationToken);
        if (roles.Any(r => AuthorizedSubTaskRoles.Contains(r)))
        {
            return;
        }

        throw new UnauthorizedAccessException(
            $"Actor '{actorPersonId}' does not possess an authorized role, request ownership, or sub-task assignment to complete or reopen sub-task '{subTask.Id}' on request '{request.Id}'.");
    }

    internal async Task ValidateCoreAttributesAuthorizationAsync(
        Domain.Request request,
        Guid actorPersonId,
        CancellationToken cancellationToken)
    {
        if (actorPersonId == SystemActorPersonId)
        {
            return;
        }

        if (request.Status == RequestStatus.Captured && !request.OwnerPersonId.HasValue)
        {
            return;
        }

        if (request.OwnerPersonId.HasValue && request.OwnerPersonId.Value == actorPersonId)
        {
            return;
        }

        var roles = await _organizationQueryService.GetPersonRolesAsync(actorPersonId, cancellationToken);
        if (roles.Any(r => AuthorizedCoreAttributesRoles.Contains(r)))
        {
            return;
        }

        throw new UnauthorizedAccessException(
            $"Actor '{actorPersonId}' does not possess an authorized role or request ownership to edit core attributes on request '{request.Id}'.");
    }

    public RequestService(
        IDbConnectionFactory connectionFactory,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        IDomainEventDispatcher? eventDispatcher = null,
        ICurrentContextProvider? currentContextProvider = null,
        IAuditContext? auditContext = null,
        ISystemClock? clock = null)
        : this(
            new RequestRepository(connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory))),
            organizationQueryService,
            customerQueryService,
            productQueryService,
            eventDispatcher,
            currentContextProvider,
            auditContext,
            clock)
    {
    }

    internal RequestService(
        IRequestRepository requestRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        IDomainEventDispatcher? eventDispatcher = null,
        ICurrentContextProvider? currentContextProvider = null,
        IAuditContext? auditContext = null,
        ISystemClock? clock = null)
    {
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _eventDispatcher = eventDispatcher;
        _currentContextProvider = currentContextProvider;
        _auditContext = auditContext;
        _clock = clock;
    }

    private DateTime UtcNow => _clock?.UtcNow ?? DateTime.UtcNow;

    /// <inheritdoc />
    public async Task<RequestDto> RecordRequestAsync(
        string title,
        string? description = null,
        Guid? customerId = null,
        Guid? productId = null,
        string requestType = "GENERAL",
        string priority = "NORMAL",
        Guid? actorPersonId = null,
        Guid? workPackageId = null,
        int? complexity = null,
        IReadOnlyList<InitialSubTaskDto>? initialSubTasks = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new RequestDomainValidationException("Title cannot be empty.", nameof(title));
        }

        if (customerId.HasValue)
        {
            await ValidateCustomerAsync(customerId.Value, cancellationToken);
        }

        if (productId.HasValue)
        {
            await ValidateProductAsync(productId.Value, cancellationToken);
        }

        var resolvedActorId = ResolveActorPersonId(actorPersonId);
        if (complexity.HasValue && complexity.Value != 1)
        {
            await ValidateComplexityAuthorizationAsync(resolvedActorId, cancellationToken);
        }

        var normalizedType = string.IsNullOrWhiteSpace(requestType) ? "GENERAL" : requestType.Trim();
        var normalizedPriority = string.IsNullOrWhiteSpace(priority) ? "NORMAL" : priority.Trim().ToUpperInvariant();
        var now = UtcNow;

        var request = Domain.Request.Record(
            id: Guid.NewGuid(),
            title: title,
            description: description ?? string.Empty,
            requestType: normalizedType,
            actorPersonId: resolvedActorId,
            customerId: customerId,
            productId: productId,
            workPackageId: workPackageId,
            priority: normalizedPriority,
            complexity: complexity,
            utcNow: now);

        if (initialSubTasks is not null && initialSubTasks.Count > 0)
        {
            foreach (var initialSubTask in initialSubTasks)
            {
                if (initialSubTask.AssigneePersonId.HasValue)
                {
                    await ValidateAssigneeAsync(initialSubTask.AssigneePersonId.Value, cancellationToken);
                }

                request.AddSubTask(initialSubTask.Title, initialSubTask.AssigneePersonId, resolvedActorId, now);
            }
        }

        await _requestRepository.AddAsync(request, cancellationToken);

        foreach (var assignment in request.Assignments)
        {
            await RecordStateChangeAuditAsync(assignment, cancellationToken);
        }

        await DispatchDomainEventsAsync(request, cancellationToken);

        return RequestDto.FromDomain(request);
    }

    /// <inheritdoc />
    public async Task<RequestDto> AssignRequestOwnerAsync(
        Guid requestId,
        Guid ownerPersonId,
        string? notes = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(requestId));
        }

        if (ownerPersonId == Guid.Empty)
        {
            throw new RequestDomainValidationException("OwnerPersonId cannot be empty.", nameof(ownerPersonId));
        }

        var request = await GetRequiredRequestAsync(requestId, cancellationToken);

        await ValidateAssigneeAsync(ownerPersonId, cancellationToken);

        var resolvedActorId = ResolveActorPersonId(actorPersonId, fallbackPersonId: ownerPersonId);
        var existingAssignmentIds = SnapshotAssignmentIds(request);
        var hadResolution = request.Resolution is not null;
        var now = GetNextMonotonicTimestamp(request);

        request.AssignOwner(ownerPersonId, resolvedActorId, notes, now);

        await PersistStateChangesAndDispatchAsync(
            request,
            existingAssignmentIds,
            hadResolution,
            cancellationToken);

        return RequestDto.FromDomain(request);
    }

    /// <inheritdoc />
    public async Task<RequestDto> StartWorkAsync(
        Guid requestId,
        string? notes = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(requestId));
        }

        var request = await GetRequiredRequestAsync(requestId, cancellationToken);

        var resolvedActorId = ResolveActorPersonId(actorPersonId, fallbackPersonId: request.OwnerPersonId);
        var existingAssignmentIds = SnapshotAssignmentIds(request);
        var hadResolution = request.Resolution is not null;
        var now = GetNextMonotonicTimestamp(request);

        request.StartWork(resolvedActorId, notes, now);

        await PersistStateChangesAndDispatchAsync(
            request,
            existingAssignmentIds,
            hadResolution,
            cancellationToken);

        return RequestDto.FromDomain(request);
    }

    /// <inheritdoc />
    public Task<RequestDto> StartWork(
        Guid requestId,
        string? notes = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => StartWorkAsync(requestId, notes, actorPersonId, cancellationToken);

    /// <inheritdoc />
    public Task<RequestDto> RecordRequest(
        string title,
        string? description = null,
        Guid? customerId = null,
        Guid? productId = null,
        string requestType = "GENERAL",
        string priority = "NORMAL",
        Guid? actorPersonId = null,
        Guid? workPackageId = null,
        int? complexity = null,
        IReadOnlyList<InitialSubTaskDto>? initialSubTasks = null,
        CancellationToken cancellationToken = default)
        => RecordRequestAsync(
            title,
            description,
            customerId,
            productId,
            requestType,
            priority,
            actorPersonId,
            workPackageId,
            complexity,
            initialSubTasks,
            cancellationToken);

    /// <inheritdoc />
    public Task<RequestDto> AssignRequestOwner(
        Guid requestId,
        Guid ownerPersonId,
        string? notes = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => AssignRequestOwnerAsync(requestId, ownerPersonId, notes, actorPersonId, cancellationToken);

    /// <inheritdoc />
    public async Task<RequestDto> PauseWorkAsync(
        Guid requestId,
        string? note = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(requestId));
        }

        var request = await GetRequiredRequestAsync(requestId, cancellationToken);

        var resolvedActorId = ResolveActorPersonId(actorPersonId, fallbackPersonId: request.OwnerPersonId);
        var existingAssignmentIds = SnapshotAssignmentIds(request);
        var hadResolution = request.Resolution is not null;
        var now = GetNextMonotonicTimestamp(request);

        request.PauseWork(resolvedActorId, note, now);

        await PersistStateChangesAndDispatchAsync(
            request,
            existingAssignmentIds,
            hadResolution,
            cancellationToken);

        return RequestDto.FromDomain(request);
    }

    /// <inheritdoc />
    public Task<RequestDto> PauseWork(
        Guid requestId,
        string? note = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => PauseWorkAsync(requestId, note, actorPersonId, cancellationToken);

    /// <inheritdoc />
    public async Task<RequestDto> CancelRequestAsync(
        Guid requestId,
        string reason,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new RequestDomainValidationException("Cancellation reason cannot be empty.", nameof(reason));
        }

        var request = await GetRequiredRequestAsync(requestId, cancellationToken);

        var resolvedActorId = ResolveActorPersonId(actorPersonId, fallbackPersonId: request.OwnerPersonId);
        var existingAssignmentIds = SnapshotAssignmentIds(request);
        var hadResolution = request.Resolution is not null;
        var now = GetNextMonotonicTimestamp(request);

        request.Cancel(reason, resolvedActorId, now);

        await PersistStateChangesAndDispatchAsync(
            request,
            existingAssignmentIds,
            hadResolution,
            cancellationToken);

        return RequestDto.FromDomain(request);
    }

    /// <inheritdoc />
    public Task<RequestDto> CancelRequest(
        Guid requestId,
        string reason,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => CancelRequestAsync(requestId, reason, actorPersonId, cancellationToken);

    /// <inheritdoc />
    public Task<RequestDto> Cancel(
        Guid requestId,
        string reason,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => CancelRequestAsync(requestId, reason, actorPersonId, cancellationToken);

    /// <inheritdoc />
    public Task<RequestDto> ReassignRequestOwnershipAsync(
        Guid requestId,
        Guid newOwnerPersonId,
        string? notes = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => AssignRequestOwnerAsync(requestId, newOwnerPersonId, notes, actorPersonId, cancellationToken);

    /// <inheritdoc />
    public Task<RequestDto> ReassignRequestOwnership(
        Guid requestId,
        Guid newOwnerPersonId,
        string? notes = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => AssignRequestOwnerAsync(requestId, newOwnerPersonId, notes, actorPersonId, cancellationToken);

    /// <inheritdoc />
    public async Task<RequestDto> ReviewRequestCompletionAsync(
        Guid requestId,
        string resolutionDescription,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(resolutionDescription))
        {
            throw new RequestDomainValidationException("Resolution description cannot be empty.", nameof(resolutionDescription));
        }

        var request = await GetRequiredRequestAsync(requestId, cancellationToken);

        var resolvedActorId = ResolveActorPersonId(actorPersonId, fallbackPersonId: request.OwnerPersonId);
        var existingAssignmentIds = SnapshotAssignmentIds(request);
        var hadResolution = request.Resolution is not null;
        var now = GetNextMonotonicTimestamp(request);

        request.Complete(resolutionDescription, resolvedActorId, now);

        await PersistStateChangesAndDispatchAsync(
            request,
            existingAssignmentIds,
            hadResolution,
            cancellationToken);

        return RequestDto.FromDomain(request);
    }

    /// <inheritdoc />
    public Task<RequestDto> ReviewRequestCompletion(
        Guid requestId,
        string resolutionDescription,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => ReviewRequestCompletionAsync(requestId, resolutionDescription, actorPersonId, cancellationToken);

    /// <inheritdoc />
    public Task<RequestDto> CompleteRequestAsync(
        Guid requestId,
        string resolutionDescription,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => ReviewRequestCompletionAsync(requestId, resolutionDescription, actorPersonId, cancellationToken);

    /// <inheritdoc />
    public Task<RequestDto> CompleteRequest(
        Guid requestId,
        string resolutionDescription,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => ReviewRequestCompletionAsync(requestId, resolutionDescription, actorPersonId, cancellationToken);

    /// <inheritdoc />
    public async Task<RequestDto> UpdateRequestComplexityAsync(
        Guid requestId,
        int complexity,
        string? reason = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(requestId));
        }

        if (complexity < 1 || complexity > 5)
        {
            throw new RequestDomainValidationException("Complexity must be an integer between 1 and 5.", nameof(complexity));
        }

        if (reason is not null && reason.Length > 500)
        {
            throw new RequestDomainValidationException("Reason must not exceed 500 characters.", nameof(reason));
        }

        var request = await GetRequiredRequestAsync(requestId, cancellationToken);

        var resolvedActorId = ResolveActorPersonId(actorPersonId, fallbackPersonId: request.OwnerPersonId);
        await ValidateComplexityAuthorizationAsync(resolvedActorId, cancellationToken);

        var existingAssignmentIds = SnapshotAssignmentIds(request);
        var hadResolution = request.Resolution is not null;
        var now = GetNextMonotonicTimestamp(request);

        request.SetComplexity(complexity, resolvedActorId, reason, now);

        await PersistStateChangesAndDispatchAsync(
            request,
            existingAssignmentIds,
            hadResolution,
            cancellationToken);

        return RequestDto.FromDomain(request);
    }

    /// <inheritdoc />
    public Task<RequestDto> UpdateRequestComplexity(
        Guid requestId,
        int complexity,
        string? reason = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => UpdateRequestComplexityAsync(requestId, complexity, reason, actorPersonId, cancellationToken);

    /// <inheritdoc />
    public async Task<RequestDto> AddRequestSubTaskAsync(
        Guid requestId,
        string title,
        Guid? assigneePersonId = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new RequestDomainValidationException("Sub-task title cannot be empty.", nameof(title));
        }

        var request = await GetRequiredRequestAsync(requestId, cancellationToken);
        var resolvedActorId = ResolveActorPersonId(actorPersonId);
        await ValidateSubTaskManagementAuthorizationAsync(request, resolvedActorId, cancellationToken);

        if (assigneePersonId.HasValue)
        {
            await ValidateAssigneeAsync(assigneePersonId.Value, cancellationToken);
        }

        var now = UtcNow;
        request.AddSubTask(title, assigneePersonId, resolvedActorId, now);

        await _requestRepository.UpdateAsync(request, cancellationToken);
        await DispatchDomainEventsAsync(request, cancellationToken);

        return RequestDto.FromDomain(request);
    }

    /// <inheritdoc />
    public Task<RequestDto> AddRequestSubTask(
        Guid requestId,
        string title,
        Guid? assigneePersonId = null,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => AddRequestSubTaskAsync(requestId, title, assigneePersonId, actorPersonId, cancellationToken);

    /// <inheritdoc />
    public async Task<RequestDto> CompleteRequestSubTaskAsync(
        Guid requestId,
        Guid subTaskId,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(requestId));
        }

        if (subTaskId == Guid.Empty)
        {
            throw new RequestDomainValidationException("SubTaskId cannot be empty.", nameof(subTaskId));
        }

        var request = await GetRequiredRequestAsync(requestId, cancellationToken);
        var subTask = request.SubTasks.FirstOrDefault(t => t.Id == subTaskId)
            ?? throw new KeyNotFoundException($"Sub-task '{subTaskId}' not found on Request '{requestId}'.");

        var resolvedActorId = ResolveActorPersonId(actorPersonId, fallbackPersonId: subTask.AssigneePersonId);
        await ValidateSubTaskCompletionAuthorizationAsync(request, subTask, resolvedActorId, cancellationToken);

        var now = UtcNow;
        request.CompleteSubTask(subTaskId, resolvedActorId, now);

        await _requestRepository.UpdateAsync(request, cancellationToken);
        await DispatchDomainEventsAsync(request, cancellationToken);

        return RequestDto.FromDomain(request);
    }

    /// <inheritdoc />
    public Task<RequestDto> CompleteRequestSubTask(
        Guid requestId,
        Guid subTaskId,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => CompleteRequestSubTaskAsync(requestId, subTaskId, actorPersonId, cancellationToken);

    /// <inheritdoc />
    public async Task<RequestDto> ReopenRequestSubTaskAsync(
        Guid requestId,
        Guid subTaskId,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(requestId));
        }

        if (subTaskId == Guid.Empty)
        {
            throw new RequestDomainValidationException("SubTaskId cannot be empty.", nameof(subTaskId));
        }

        var request = await GetRequiredRequestAsync(requestId, cancellationToken);
        var subTask = request.SubTasks.FirstOrDefault(t => t.Id == subTaskId)
            ?? throw new KeyNotFoundException($"Sub-task '{subTaskId}' not found on Request '{requestId}'.");

        var resolvedActorId = ResolveActorPersonId(actorPersonId, fallbackPersonId: subTask.AssigneePersonId);
        await ValidateSubTaskCompletionAuthorizationAsync(request, subTask, resolvedActorId, cancellationToken);

        var now = UtcNow;
        request.ReopenSubTask(subTaskId, resolvedActorId, now);

        await _requestRepository.UpdateAsync(request, cancellationToken);
        await DispatchDomainEventsAsync(request, cancellationToken);

        return RequestDto.FromDomain(request);
    }

    /// <inheritdoc />
    public Task<RequestDto> ReopenRequestSubTask(
        Guid requestId,
        Guid subTaskId,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => ReopenRequestSubTaskAsync(requestId, subTaskId, actorPersonId, cancellationToken);

    /// <inheritdoc />
    public async Task<RequestDto> RemoveRequestSubTaskAsync(
        Guid requestId,
        Guid subTaskId,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(requestId));
        }

        if (subTaskId == Guid.Empty)
        {
            throw new RequestDomainValidationException("SubTaskId cannot be empty.", nameof(subTaskId));
        }

        var request = await GetRequiredRequestAsync(requestId, cancellationToken);
        var subTask = request.SubTasks.FirstOrDefault(t => t.Id == subTaskId)
            ?? throw new KeyNotFoundException($"Sub-task '{subTaskId}' not found on Request '{requestId}'.");

        var resolvedActorId = ResolveActorPersonId(actorPersonId);
        await ValidateSubTaskManagementAuthorizationAsync(request, resolvedActorId, cancellationToken);

        var now = UtcNow;
        request.RemoveSubTask(subTaskId, resolvedActorId, now);

        await _requestRepository.UpdateAsync(request, cancellationToken);
        await DispatchDomainEventsAsync(request, cancellationToken);

        return RequestDto.FromDomain(request);
    }

    /// <inheritdoc />
    public Task<RequestDto> RemoveRequestSubTask(
        Guid requestId,
        Guid subTaskId,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => RemoveRequestSubTaskAsync(requestId, subTaskId, actorPersonId, cancellationToken);

    /// <inheritdoc />
    public async Task<RequestDto> UpdateRequestCoreAttributesAsync(
        Guid requestId,
        string title,
        string description,
        string priority,
        string requestType,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new RequestDomainValidationException("Title cannot be empty.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new RequestDomainValidationException("Description cannot be empty.", nameof(description));
        }

        var request = await GetRequiredRequestAsync(requestId, cancellationToken);

        var resolvedActorId = ResolveActorPersonId(actorPersonId, fallbackPersonId: request.OwnerPersonId);
        await ValidateCoreAttributesAuthorizationAsync(request, resolvedActorId, cancellationToken);

        var existingAssignmentIds = SnapshotAssignmentIds(request);
        var hadResolution = request.Resolution is not null;
        var now = GetNextMonotonicTimestamp(request);

        request.UpdateCoreAttributes(title, description, priority, requestType, resolvedActorId, now);

        await PersistStateChangesAndDispatchAsync(
            request,
            existingAssignmentIds,
            hadResolution,
            cancellationToken);

        return RequestDto.FromDomain(request);
    }

    /// <inheritdoc />
    public Task<RequestDto> UpdateRequestCoreAttributes(
        Guid requestId,
        string title,
        string description,
        string priority,
        string requestType,
        Guid? actorPersonId = null,
        CancellationToken cancellationToken = default)
        => UpdateRequestCoreAttributesAsync(requestId, title, description, priority, requestType, actorPersonId, cancellationToken);


    internal DateTime GetNextMonotonicTimestamp(Domain.Request request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = UtcNow;
        if (request.Assignments.Count == 0)
        {
            return now;
        }

        var maxAssignedAt = request.Assignments.Max(a => a.AssignedAtUtc);
        var minNext = maxAssignedAt.AddMilliseconds(10);
        return now > minNext ? now : minNext;
    }

    /// <summary>
    /// Reusable audit logging method recording a state transition or ownership assignment
    /// into <c>request.RequestAssignments</c> via Dapper parameterized SQL (Architecture §18).
    /// Used by P4-S18, P4-S19, and P4-S20.
    /// </summary>
    internal Task RecordStateChangeAuditAsync(
        RequestAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        return _requestRepository.AddAssignmentAsync(assignment, cancellationToken);
    }

    /// <summary>
    /// Reusable persistence &amp; event dispatch helper for request state transitions
    /// (reusable across P4-S18, P4-S19, and P4-S20):
    /// 1. Updates <c>request.Requests</c>
    /// 2. Inserts any newly recorded <see cref="RequestAssignment"/> audit entries into <c>request.RequestAssignments</c>
    /// 3. Inserts a newly created <see cref="RequestResolution"/> into <c>request.RequestResolutions</c> (if any)
    /// 4. Dispatches all raised domain events synchronously via <see cref="IDomainEventDispatcher"/>
    /// </summary>
    internal async Task PersistStateChangesAndDispatchAsync(
        Domain.Request request,
        IReadOnlySet<Guid> existingAssignmentIds,
        bool hadResolution,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(existingAssignmentIds);

        await _requestRepository.UpdateAsync(request, cancellationToken);

        foreach (var assignment in request.Assignments)
        {
            if (!existingAssignmentIds.Contains(assignment.Id))
            {
                await RecordStateChangeAuditAsync(assignment, cancellationToken);
            }
        }

        if (!hadResolution && request.Resolution is not null)
        {
            await _requestRepository.AddResolutionAsync(request.Resolution, cancellationToken);
        }

        await DispatchDomainEventsAsync(request, cancellationToken);
    }

    internal static HashSet<Guid> SnapshotAssignmentIds(Domain.Request request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Assignments.Select(a => a.Id).ToHashSet();
    }

    internal async Task<Domain.Request> GetRequiredRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        return await _requestRepository.GetByIdAsync(requestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Request '{requestId}' was not found.");
    }

    internal Guid ResolveActorPersonId(Guid? explicitActorPersonId, Guid? fallbackPersonId = null)
    {
        if (explicitActorPersonId.HasValue)
        {
            if (explicitActorPersonId.Value == Guid.Empty)
            {
                throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(explicitActorPersonId));
            }

            return explicitActorPersonId.Value;
        }

        if (_currentContextProvider?.CurrentPersonId is { } currentPersonId && currentPersonId != Guid.Empty)
        {
            return currentPersonId;
        }

        if (_auditContext?.ActorPersonId is { } auditPersonId && auditPersonId != Guid.Empty)
        {
            return auditPersonId;
        }

        if (fallbackPersonId.HasValue && fallbackPersonId.Value != Guid.Empty)
        {
            return fallbackPersonId.Value;
        }

        return SystemActorPersonId;
    }

    internal async Task ValidateAssigneeAsync(Guid assigneePersonId, CancellationToken cancellationToken)
    {
        if (assigneePersonId == Guid.Empty)
        {
            throw new ArgumentException("Assignee PersonId cannot be empty.", nameof(assigneePersonId));
        }

        var isActive = await _organizationQueryService.IsPersonActiveAsync(assigneePersonId, cancellationToken);
        if (isActive)
        {
            return;
        }

        var person = await _organizationQueryService.GetPersonByIdAsync(assigneePersonId, cancellationToken);
        if (person is not null)
        {
            if (person.IsActive)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Person '{assigneePersonId}' is not active in Organization and cannot be assigned as request owner.");
        }

        throw new KeyNotFoundException(
            $"Assignee person '{assigneePersonId}' was not found in Organization.");
    }

    internal async Task ValidateCustomerAsync(Guid customerId, CancellationToken cancellationToken)
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
                $"Customer '{customerId}' is not active and cannot be associated with a request.");
        }

        throw new KeyNotFoundException(
            $"Customer '{customerId}' was not found.");
    }

    internal async Task ValidateProductAsync(Guid productId, CancellationToken cancellationToken)
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
                $"Product '{productId}' is not active and cannot be associated with a request.");
        }

        throw new KeyNotFoundException(
            $"Product '{productId}' was not found.");
    }

    private async Task DispatchDomainEventsAsync(
        Domain.Request request,
        CancellationToken cancellationToken)
    {
        if (_eventDispatcher is not null)
        {
            foreach (var domainEvent in request.DomainEvents.ToList())
            {
                await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
            }
        }

        request.ClearDomainEvents();
    }

    // MediatR command handler entry points
    public Task<RequestDto> Handle(RecordRequestCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RecordRequestAsync(
            request.Title,
            request.Description,
            request.CustomerId,
            request.ProductId,
            request.RequestType,
            request.Priority,
            request.ActorPersonId,
            request.WorkPackageId,
            request.Complexity,
            request.InitialSubTasks,
            cancellationToken);
    }

    public Task<RequestDto> Handle(AssignRequestOwnerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return AssignRequestOwnerAsync(
            request.RequestId,
            request.OwnerPersonId,
            request.Notes,
            request.ActorPersonId,
            cancellationToken);
    }

    public Task<RequestDto> Handle(StartWorkCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return StartWorkAsync(
            request.RequestId,
            request.Notes,
            request.ActorPersonId,
            cancellationToken);
    }

    public Task<RequestDto> Handle(PauseWorkCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PauseWorkAsync(
            request.RequestId,
            request.Note,
            request.ActorPersonId,
            cancellationToken);
    }

    public Task<RequestDto> Handle(CancelRequestCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CancelRequestAsync(
            request.RequestId,
            request.Reason,
            request.ActorPersonId,
            cancellationToken);
    }

    public Task<RequestDto> Handle(ReassignRequestOwnershipCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return AssignRequestOwnerAsync(
            request.RequestId,
            request.NewOwnerPersonId,
            request.Notes,
            request.ActorPersonId,
            cancellationToken);
    }

    public Task<RequestDto> Handle(ReviewRequestCompletionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ReviewRequestCompletionAsync(
            request.RequestId,
            request.ResolutionDescription,
            request.ActorPersonId,
            cancellationToken);
    }

    public Task<RequestDto> Handle(CompleteRequestCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ReviewRequestCompletionAsync(
            request.RequestId,
            request.ResolutionDescription,
            request.ActorPersonId,
            cancellationToken);
    }

    public Task<RequestDto> Handle(UpdateRequestComplexityCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return UpdateRequestComplexityAsync(
            request.RequestId,
            request.Complexity,
            request.Reason,
            request.ActorPersonId,
            cancellationToken);
    }

    public Task<RequestDto> Handle(AddRequestSubTaskCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return AddRequestSubTaskAsync(
            request.RequestId,
            request.Title,
            request.AssigneePersonId,
            request.ActorPersonId,
            cancellationToken);
    }

    public Task<RequestDto> Handle(CompleteRequestSubTaskCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CompleteRequestSubTaskAsync(
            request.RequestId,
            request.SubTaskId,
            request.ActorPersonId,
            cancellationToken);
    }

    public Task<RequestDto> Handle(ReopenRequestSubTaskCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ReopenRequestSubTaskAsync(
            request.RequestId,
            request.SubTaskId,
            request.ActorPersonId,
            cancellationToken);
    }

    public Task<RequestDto> Handle(RemoveRequestSubTaskCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RemoveRequestSubTaskAsync(
            request.RequestId,
            request.SubTaskId,
            request.ActorPersonId,
            cancellationToken);
    }

    public Task<RequestDto> Handle(UpdateRequestCoreAttributesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return UpdateRequestCoreAttributesAsync(
            request.RequestId,
            request.Title,
            request.Description,
            request.Priority,
            request.RequestType,
            request.ActorPersonId,
            cancellationToken);
    }
}

