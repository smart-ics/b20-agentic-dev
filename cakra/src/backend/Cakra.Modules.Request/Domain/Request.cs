using Cakra.Core;
using Cakra.Modules.Request.Domain.Events;
using Cakra.Modules.Request.Domain.Exceptions;

namespace Cakra.Modules.Request.Domain;

/// <summary>
/// Authoritative Request aggregate root managing the operational demand lifecycle,
/// state transitions, ownership assignments, and resolution (Architecture §7, §8, CR-016).
/// </summary>
public sealed class Request : EntityBase
{
    private readonly List<RequestAssignment> _assignments = new();
    private readonly List<RequestSubTask> _subTasks = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>Brief summary or subject of the operational demand.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Detailed description and operational context of the demand.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Classification of the demand (e.g., Bug, Feature, Support, Investigation).</summary>
    public string RequestType { get; private set; } = string.Empty;

    /// <summary>Current authoritative lifecycle state.</summary>
    public RequestStatus Status { get; private set; }

    /// <summary>Priority level (e.g. LOW, NORMAL, HIGH, URGENT).</summary>
    public string Priority { get; private set; } = "NORMAL";

    /// <summary>Authoritative numerical complexity rating (1 to 5).</summary>
    public int Complexity { get; private set; } = 1;

    /// <summary>Total count of checklist sub-tasks defined on this request.</summary>
    public int TotalSubTasksCount { get; private set; }

    /// <summary>Count of sub-tasks marked completed on this request.</summary>
    public int CompletedSubTasksCount { get; private set; }

    /// <summary>Authoritative completion percentage (0 to 100) calculated from sub-tasks or status.</summary>
    public int CompletionPercentage { get; private set; }

    /// <summary>Granular operational sub-task checklist items belonging to this request.</summary>
    public IReadOnlyCollection<RequestSubTask> SubTasks => _subTasks.AsReadOnly();

    /// <summary>PersonId of the assigned Request Owner in Organization domain, or <c>null</c> if unassigned.</summary>
    public Guid? OwnerPersonId { get; private set; }

    /// <summary>Optional CustomerId from Customer domain associated with this request.</summary>
    public Guid? CustomerId { get; private set; }

    /// <summary>Optional ProductId from Product domain associated with this request.</summary>
    public Guid? ProductId { get; private set; }

    /// <summary>Optional WorkPackageId from Work Package domain grouping this request.</summary>
    public Guid? WorkPackageId { get; private set; }

    /// <summary>Evaluation notes recorded during triage assessment.</summary>
    public string? EvaluationNotes { get; private set; }

    /// <summary>Recorded resolution outcome when the request is closed (completed or cancelled).</summary>
    public RequestResolution? Resolution { get; private set; }

    /// <summary>Audit trail of ownership and lifecycle state transitions.</summary>
    public IReadOnlyCollection<RequestAssignment> Assignments => _assignments.AsReadOnly();

    /// <summary>In-process domain events emitted by state changes on this aggregate.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Clears all recorded domain events after dispatch.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    // Parameterless constructor for Dapper / persistence hydration
    private Request() { }

    /// <summary>
    /// Records a new customer or internal operational demand into CAPTURED state (UC-REQ-001).
    /// </summary>
    public static Request Record(
        Guid id,
        string title,
        string description,
        string requestType,
        Guid actorPersonId,
        Guid? customerId = null,
        Guid? productId = null,
        Guid? workPackageId = null,
        string? priority = null,
        int? complexity = null,
        DateTime? utcNow = null)
    {
        if (id == Guid.Empty)
            throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(title))
            throw new RequestDomainValidationException("Title cannot be empty.", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new RequestDomainValidationException("Description cannot be empty.", nameof(description));
        if (string.IsNullOrWhiteSpace(requestType))
            throw new RequestDomainValidationException("RequestType cannot be empty.", nameof(requestType));
        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        var initialComplexity = complexity ?? 1;
        if (initialComplexity < 1 || initialComplexity > 5)
            throw new RequestDomainValidationException("Complexity must be an integer between 1 and 5.", nameof(complexity));

        var now = utcNow ?? DateTime.UtcNow;

        var request = new Request
        {
            Id = id,
            Title = title.Trim(),
            Description = description.Trim(),
            RequestType = requestType.Trim(),
            Status = RequestStatus.Captured,
            Priority = string.IsNullOrWhiteSpace(priority) ? "NORMAL" : priority.Trim().ToUpperInvariant(),
            Complexity = initialComplexity,
            CustomerId = customerId,
            ProductId = productId,
            WorkPackageId = workPackageId,
            CreatedAt = now,
            UpdatedAt = null
        };

        var initialAssignment = RequestAssignment.Create(
            requestId: id,
            previousOwnerPersonId: null,
            assignedOwnerPersonId: null,
            actorPersonId: actorPersonId,
            previousStatus: null,
            newStatus: RequestStatus.Captured,
            assignedAtUtc: now,
            notes: "Request recorded in CAPTURED state.");

        request._assignments.Add(initialAssignment);

        request._domainEvents.Add(new RequestRecorded(
            requestId: id,
            title: request.Title,
            description: request.Description,
            requestType: request.RequestType,
            actorPersonId: actorPersonId,
            customerId: customerId,
            productId: productId,
            workPackageId: workPackageId,
            priority: request.Priority,
            occurredAtUtc: now));

        return request;
    }

    /// <summary>
    /// Assigns or reassigns the operational owner of the Request (Architecture CR-016 TD-003).
    /// Transitions CAPTURED -> ASSIGNED, preserves ASSIGNED, or resets IN_PROGRESS / PAUSED -> ASSIGNED.
    /// </summary>
    public void AssignOwner(
        Guid ownerPersonId,
        Guid actorPersonId,
        string? notes = null,
        DateTime? utcNow = null)
    {
        if (ownerPersonId == Guid.Empty)
            throw new RequestDomainValidationException("OwnerPersonId cannot be empty.", nameof(ownerPersonId));
        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        if (Status == RequestStatus.Completed || Status == RequestStatus.Cancelled)
        {
            throw new InvalidRequestStateTransitionException(
                Id, Status, nameof(AssignOwner), reason: "Closed requests cannot be assigned or reassigned.");
        }

        var now = utcNow ?? DateTime.UtcNow;
        var prevOwner = OwnerPersonId;
        var prevStatus = Status;

        RequestStatus newStatus;

        switch (Status)
        {
            case RequestStatus.Captured:
                newStatus = RequestStatus.Assigned;
                break;

            case RequestStatus.Assigned:
                if (OwnerPersonId.HasValue && OwnerPersonId.Value == ownerPersonId)
                {
                    throw new InvalidOperationException("Request is already assigned to this person.");
                }
                newStatus = RequestStatus.Assigned;
                break;

            case RequestStatus.InProgress:
            case RequestStatus.Paused:
                if (OwnerPersonId.HasValue && OwnerPersonId.Value == ownerPersonId)
                {
                    throw new InvalidOperationException("Request is already assigned to this person.");
                }
                // TD-003: Active work reassignment resets status to ASSIGNED
                newStatus = RequestStatus.Assigned;
                break;

            default:
                throw new InvalidRequestStateTransitionException(
                    Id, Status, nameof(AssignOwner), reason: $"Cannot assign owner in status '{Status}'.");
        }

        OwnerPersonId = ownerPersonId;
        Status = newStatus;
        UpdatedAt = now;

        var assignment = RequestAssignment.Create(
            requestId: Id,
            previousOwnerPersonId: prevOwner,
            assignedOwnerPersonId: ownerPersonId,
            actorPersonId: actorPersonId,
            previousStatus: prevStatus,
            newStatus: newStatus,
            assignedAtUtc: now,
            notes: notes ?? (prevStatus == RequestStatus.Captured
                ? "Initial owner assignment -> ASSIGNED"
                : prevStatus == RequestStatus.InProgress || prevStatus == RequestStatus.Paused
                    ? "Active owner reassignment -> ASSIGNED"
                    : "Owner reassigned"));

        _assignments.Add(assignment);

        _domainEvents.Add(new RequestAssigned(
            requestId: Id,
            ownerPersonId: ownerPersonId,
            previousOwnerPersonId: prevOwner,
            actorPersonId: actorPersonId,
            previousStatus: prevStatus,
            newStatus: newStatus,
            notes: notes,
            occurredAtUtc: now));
    }

    /// <summary>
    /// Starts active work on the Request, transitioning ASSIGNED -> IN_PROGRESS or PAUSED -> IN_PROGRESS (CR-016 TD-002).
    /// Strictly executable by the assigned owner.
    /// </summary>
    public void StartWork(
        Guid actorPersonId,
        string? notes = null,
        DateTime? utcNow = null)
    {
        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        if (!OwnerPersonId.HasValue || actorPersonId != OwnerPersonId.Value)
        {
            throw new InvalidOperationException("Only the assigned owner can start work on this request.");
        }

        if (Status != RequestStatus.Assigned && Status != RequestStatus.Paused)
        {
            throw new InvalidRequestStateTransitionException(
                Id, Status, nameof(StartWork), RequestStatus.InProgress,
                reason: "Request can only transition to IN_PROGRESS from ASSIGNED or PAUSED state.");
        }

        var now = utcNow ?? DateTime.UtcNow;
        var prevStatus = Status;

        Status = RequestStatus.InProgress;
        UpdatedAt = now;

        var assignment = RequestAssignment.Create(
            requestId: Id,
            previousOwnerPersonId: OwnerPersonId,
            assignedOwnerPersonId: OwnerPersonId,
            actorPersonId: actorPersonId,
            previousStatus: prevStatus,
            newStatus: RequestStatus.InProgress,
            assignedAtUtc: now,
            notes: notes ?? (prevStatus == RequestStatus.Paused ? "Resumed work from PAUSED" : "Started work on request"));

        _assignments.Add(assignment);

        _domainEvents.Add(new RequestWorkStarted(
            requestId: Id,
            ownerPersonId: OwnerPersonId.Value,
            actorPersonId: actorPersonId,
            previousStatus: prevStatus,
            notes: notes,
            occurredAtUtc: now));
    }

    /// <summary>
    /// Suspends active work on the Request, transitioning IN_PROGRESS -> PAUSED (CR-016 TD-002).
    /// </summary>
    public void PauseWork(
        Guid actorPersonId,
        string? note = null,
        DateTime? utcNow = null)
    {
        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        if (Status != RequestStatus.InProgress)
        {
            throw new InvalidRequestStateTransitionException(
                Id, Status, nameof(PauseWork), RequestStatus.Paused,
                reason: "Request can only transition to PAUSED from IN_PROGRESS state.");
        }

        var now = utcNow ?? DateTime.UtcNow;
        var prevStatus = Status;

        Status = RequestStatus.Paused;
        UpdatedAt = now;

        var assignment = RequestAssignment.Create(
            requestId: Id,
            previousOwnerPersonId: OwnerPersonId,
            assignedOwnerPersonId: OwnerPersonId,
            actorPersonId: actorPersonId,
            previousStatus: prevStatus,
            newStatus: RequestStatus.Paused,
            assignedAtUtc: now,
            notes: note ?? "Work paused on request");

        _assignments.Add(assignment);

        _domainEvents.Add(new RequestWorkPaused(
            requestId: Id,
            ownerPersonId: OwnerPersonId,
            actorPersonId: actorPersonId,
            notes: note,
            occurredAtUtc: now));
    }

    /// <summary>
    /// Cancels the Request, transitioning any non-terminal state -> CANCELLED (CR-016 TD-002).
    /// Permitted regardless of unfinished subtasks.
    /// </summary>
    public void Cancel(
        string reason,
        Guid actorPersonId,
        DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new RequestDomainValidationException("Cancellation reason cannot be empty.", nameof(reason));
        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        if (Status == RequestStatus.Completed || Status == RequestStatus.Cancelled)
        {
            throw new InvalidRequestStateTransitionException(
                Id, Status, nameof(Cancel), RequestStatus.Cancelled,
                reason: "Closed requests cannot be cancelled.");
        }

        var now = utcNow ?? DateTime.UtcNow;
        var prevStatus = Status;

        Status = RequestStatus.Cancelled;
        Resolution = RequestResolution.Create(
            requestId: Id,
            outcome: ResolutionOutcomeNames.Cancelled,
            description: reason.Trim(),
            resolvedBy: actorPersonId,
            resolvedAt: now);
        UpdatedAt = now;

        var assignment = RequestAssignment.Create(
            requestId: Id,
            previousOwnerPersonId: OwnerPersonId,
            assignedOwnerPersonId: OwnerPersonId,
            actorPersonId: actorPersonId,
            previousStatus: prevStatus,
            newStatus: RequestStatus.Cancelled,
            assignedAtUtc: now,
            notes: $"Request cancelled: {reason.Trim()}");

        _assignments.Add(assignment);

        _domainEvents.Add(new RequestCancelled(
            requestId: Id,
            cancelledByPersonId: actorPersonId,
            cancellationReason: reason.Trim(),
            occurredAtUtc: now));
    }

    /// <summary>
    /// Concludes work on the Request, transitioning IN_PROGRESS -> COMPLETED (UC-REQ-008, CR-016 TD-002).
    /// Requires all checklist sub-tasks to be completed.
    /// </summary>
    public void Complete(
        string resolutionDescription,
        Guid actorPersonId,
        DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(resolutionDescription))
            throw new RequestDomainValidationException("Resolution description cannot be empty.", nameof(resolutionDescription));
        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        if (Status != RequestStatus.InProgress)
        {
            throw new InvalidRequestStateTransitionException(
                Id, Status, nameof(Complete), RequestStatus.Completed, reason: "Request must be in IN_PROGRESS state to be completed.");
        }

        if (_subTasks.Any(t => !t.IsCompleted))
        {
            var unfinishedCount = _subTasks.Count(t => !t.IsCompleted);
            throw new RequestHasUnfinishedSubTasksException(
                Id,
                unfinishedCount,
                $"Cannot complete request '{Id}' because it has {unfinishedCount} unfinished sub-task(s). All sub-tasks must be completed or removed before closure.");
        }

        var now = utcNow ?? DateTime.UtcNow;
        var prevStatus = Status;

        Status = RequestStatus.Completed;
        RecalculateProgress();
        Resolution = RequestResolution.Create(
            requestId: Id,
            outcome: ResolutionOutcomeNames.Completed,
            description: resolutionDescription.Trim(),
            resolvedBy: actorPersonId,
            resolvedAt: now);
        UpdatedAt = now;

        var assignment = RequestAssignment.Create(
            requestId: Id,
            previousOwnerPersonId: OwnerPersonId,
            assignedOwnerPersonId: OwnerPersonId,
            actorPersonId: actorPersonId,
            previousStatus: prevStatus,
            newStatus: RequestStatus.Completed,
            assignedAtUtc: now,
            notes: $"Request completed: {resolutionDescription.Trim()}");

        _assignments.Add(assignment);

        _domainEvents.Add(new RequestCompleted(
            requestId: Id,
            completedByPersonId: actorPersonId,
            resolutionDescription: resolutionDescription.Trim(),
            occurredAtUtc: now));
    }

    /// <summary>
    /// Updates the complexity rating while the request is in an active lifecycle state.
    /// </summary>
    public void SetComplexity(
        int newComplexity,
        Guid actorPersonId,
        string? reason = null,
        DateTime? utcNow = null)
    {
        if (newComplexity < 1 || newComplexity > 5)
            throw new RequestDomainValidationException("Complexity must be an integer between 1 and 5.", nameof(newComplexity));

        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        if (Status == RequestStatus.Completed || Status == RequestStatus.Cancelled)
        {
            throw new InvalidRequestStateTransitionException(
                Id, Status, nameof(SetComplexity), reason: "Cannot change complexity on a closed request.");
        }

        if (Complexity == newComplexity)
            return;

        var previousComplexity = Complexity;
        var now = utcNow ?? DateTime.UtcNow;

        Complexity = newComplexity;
        UpdatedAt = now;

        _domainEvents.Add(new RequestComplexityUpdated(
            requestId: Id,
            previousComplexity: previousComplexity,
            newComplexity: newComplexity,
            actorPersonId: actorPersonId,
            reason: reason?.Trim(),
            occurredAtUtc: now));
    }

    /// <summary>
    /// Reassigns ownership to a new person (Architecture CR-016 TD-003).
    /// </summary>
    public void ReassignOwner(
        Guid newOwnerPersonId,
        Guid actorPersonId,
        RequestStatus? targetStatusForEscalated = null,
        string? notes = null,
        DateTime? utcNow = null)
    {
        AssignOwner(newOwnerPersonId, actorPersonId, notes, utcNow);
    }

    // =========================================================================
    // Sub-Task Checklist Operations (CR-006 Architecture TD-001, TD-003, TD-004, TD-007)
    // =========================================================================

    private void EnsureActiveStateForSubTaskMutation(string operation)
    {
        if (Status == RequestStatus.Completed || Status == RequestStatus.Cancelled)
        {
            throw new InvalidRequestStateTransitionException(
                Id,
                Status,
                operation,
                reason: $"Cannot perform '{operation}' on a closed request.");
        }
    }

    private void RecalculateProgress()
    {
        TotalSubTasksCount = _subTasks.Count;
        CompletedSubTasksCount = _subTasks.Count(t => t.IsCompleted);

        if (TotalSubTasksCount > 0)
        {
            CompletionPercentage = (int)Math.Round((double)CompletedSubTasksCount / TotalSubTasksCount * 100.0);
        }
        else
        {
            CompletionPercentage = Status == RequestStatus.Completed ? 100 : 0;
        }
    }

    /// <summary>
    /// Adds a new operational sub-task checklist item to the Request.
    /// </summary>
    public RequestSubTask AddSubTask(
        string title,
        Guid? assigneePersonId,
        Guid actorPersonId,
        DateTime? utcNow = null,
        Guid? subTaskId = null)
    {
        EnsureActiveStateForSubTaskMutation(nameof(AddSubTask));

        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        var now = utcNow ?? DateTime.UtcNow;
        var sortOrder = _subTasks.Count > 0 ? _subTasks.Max(t => t.SortOrder) + 1 : 0;
        var id = subTaskId ?? Guid.NewGuid();

        var subTask = new RequestSubTask(
            id,
            Id,
            title,
            assigneePersonId,
            sortOrder,
            now);

        _subTasks.Add(subTask);
        UpdatedAt = now;
        RecalculateProgress();

        _domainEvents.Add(new RequestSubTaskAdded(
            Id,
            subTask.Id,
            subTask.Title,
            subTask.AssigneePersonId,
            TotalSubTasksCount,
            CompletionPercentage,
            actorPersonId,
            now));

        return subTask;
    }

    /// <summary>
    /// Marks an existing sub-task checklist item completed on the Request.
    /// </summary>
    public void CompleteSubTask(
        Guid subTaskId,
        Guid completedByPersonId,
        DateTime? utcNow = null)
    {
        EnsureActiveStateForSubTaskMutation(nameof(CompleteSubTask));

        if (subTaskId == Guid.Empty)
            throw new RequestDomainValidationException("SubTaskId cannot be empty.", nameof(subTaskId));
        if (completedByPersonId == Guid.Empty)
            throw new RequestDomainValidationException("CompletedByPersonId cannot be empty.", nameof(completedByPersonId));

        var subTask = _subTasks.FirstOrDefault(t => t.Id == subTaskId)
            ?? throw new KeyNotFoundException($"Sub-task '{subTaskId}' not found on Request '{Id}'.");

        var now = utcNow ?? DateTime.UtcNow;
        subTask.MarkCompleted(completedByPersonId, now);
        UpdatedAt = now;
        RecalculateProgress();

        _domainEvents.Add(new RequestSubTaskCompleted(
            Id,
            subTaskId,
            completedByPersonId,
            CompletedSubTasksCount,
            CompletionPercentage,
            now));
    }

    /// <summary>
    /// Reopens a completed sub-task checklist item back to pending on the Request.
    /// </summary>
    public void ReopenSubTask(
        Guid subTaskId,
        Guid actorPersonId,
        DateTime? utcNow = null)
    {
        EnsureActiveStateForSubTaskMutation(nameof(ReopenSubTask));

        if (subTaskId == Guid.Empty)
            throw new RequestDomainValidationException("SubTaskId cannot be empty.", nameof(subTaskId));
        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        var subTask = _subTasks.FirstOrDefault(t => t.Id == subTaskId)
            ?? throw new KeyNotFoundException($"Sub-task '{subTaskId}' not found on Request '{Id}'.");

        var now = utcNow ?? DateTime.UtcNow;
        subTask.Reopen(now);
        UpdatedAt = now;
        RecalculateProgress();

        _domainEvents.Add(new RequestSubTaskReopened(
            Id,
            subTaskId,
            actorPersonId,
            CompletedSubTasksCount,
            CompletionPercentage,
            now));
    }

    /// <summary>
    /// Removes a sub-task checklist item from the Request.
    /// </summary>
    public void RemoveSubTask(
        Guid subTaskId,
        Guid actorPersonId,
        DateTime? utcNow = null)
    {
        EnsureActiveStateForSubTaskMutation(nameof(RemoveSubTask));

        if (subTaskId == Guid.Empty)
            throw new RequestDomainValidationException("SubTaskId cannot be empty.", nameof(subTaskId));
        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        var subTask = _subTasks.FirstOrDefault(t => t.Id == subTaskId)
            ?? throw new KeyNotFoundException($"Sub-task '{subTaskId}' not found on Request '{Id}'.");

        _subTasks.Remove(subTask);
        var now = utcNow ?? DateTime.UtcNow;
        UpdatedAt = now;
        RecalculateProgress();

        _domainEvents.Add(new RequestSubTaskRemoved(
            Id,
            subTaskId,
            TotalSubTasksCount,
            CompletionPercentage,
            actorPersonId,
            now));
    }

    /// <summary>
    /// Rehydrates a <see cref="Request"/> aggregate root from persistence storage without emitting domain events.
    /// </summary>
    public static Request Rehydrate(
        Guid id,
        string title,
        string description,
        string requestType,
        RequestStatus status,
        string priority,
        Guid? ownerPersonId,
        Guid? customerId,
        Guid? productId,
        Guid? workPackageId,
        string? evaluationNotes,
        DateTime createdAt,
        DateTime? updatedAt,
        RequestResolution? resolution = null,
        IEnumerable<RequestAssignment>? assignments = null,
        int complexity = 1,
        IEnumerable<RequestSubTask>? subTasks = null,
        int totalSubTasksCount = 0,
        int completedSubTasksCount = 0,
        int completionPercentage = 0)
    {
        var request = new Request
        {
            Id = id,
            Title = title,
            Description = description,
            RequestType = requestType,
            Status = status,
            Priority = priority,
            Complexity = complexity,
            OwnerPersonId = ownerPersonId,
            CustomerId = customerId,
            ProductId = productId,
            WorkPackageId = workPackageId,
            EvaluationNotes = evaluationNotes,
            Resolution = resolution,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            TotalSubTasksCount = totalSubTasksCount,
            CompletedSubTasksCount = completedSubTasksCount,
            CompletionPercentage = completionPercentage
        };

        if (assignments is not null)
        {
            request._assignments.AddRange(assignments);
        }

        if (subTasks is not null)
        {
            request._subTasks.AddRange(subTasks);
            if (totalSubTasksCount == 0 && request._subTasks.Count > 0)
            {
                request.RecalculateProgress();
            }
        }

        return request;
    }

    /// <summary>
    /// Legacy overload of <see cref="Rehydrate"/> supporting obsolete escalation and management decision parameters.
    /// </summary>
    [Obsolete("Use the overload without escalationReason and managementDecisionNotes.")]
    public static Request Rehydrate(
        Guid id,
        string title,
        string description,
        string requestType,
        RequestStatus status,
        string priority,
        Guid? ownerPersonId,
        Guid? customerId,
        Guid? productId,
        Guid? workPackageId,
        string? evaluationNotes,
        string? escalationReason,
        string? managementDecisionNotes,
        DateTime createdAt,
        DateTime? updatedAt,
        RequestResolution? resolution = null,
        IEnumerable<RequestAssignment>? assignments = null,
        int complexity = 1,
        IEnumerable<RequestSubTask>? subTasks = null,
        int totalSubTasksCount = 0,
        int completedSubTasksCount = 0,
        int completionPercentage = 0)
    {
        return Rehydrate(
            id,
            title,
            description,
            requestType,
            status,
            priority,
            ownerPersonId,
            customerId,
            productId,
            workPackageId,
            evaluationNotes,
            createdAt,
            updatedAt,
            resolution,
            assignments,
            complexity,
            subTasks,
            totalSubTasksCount,
            completedSubTasksCount,
            completionPercentage);
    }
}
