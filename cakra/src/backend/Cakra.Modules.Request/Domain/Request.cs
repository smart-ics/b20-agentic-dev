using Cakra.Core;
using Cakra.Modules.Request.Domain.Events;
using Cakra.Modules.Request.Domain.Exceptions;

namespace Cakra.Modules.Request.Domain;

/// <summary>
/// Authoritative Request aggregate root managing the operational demand lifecycle,
/// state transitions, ownership assignments, and resolution (Architecture §7, §8).
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

    /// <summary>Documented justification and needed assistance when escalated.</summary>
    public string? EscalationReason { get; private set; }

    /// <summary>Decision details, docket, or determination recorded during management elevation.</summary>
    public string? ManagementDecisionNotes { get; private set; }

    /// <summary>Recorded resolution outcome when the request is closed (rejected or completed).</summary>
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
    /// Assigns or reassigns the operational owner of the Request (UC-REQ-002, UC-MGT-001).
    /// Transitions CAPTURED -> EVALUATING, ESCALATED -> EVALUATING, or preserves active status.
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

        if (Status == RequestStatus.Rejected || Status == RequestStatus.Completed)
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
                newStatus = RequestStatus.Evaluating;
                break;

            case RequestStatus.Escalated:
                newStatus = RequestStatus.Evaluating;
                break;

            case RequestStatus.Evaluating:
            case RequestStatus.Accepted:
            case RequestStatus.InProgress:
                if (OwnerPersonId.HasValue && OwnerPersonId.Value == ownerPersonId)
                {
                    throw new InvalidOperationException("Request is already assigned to this person.");
                }
                newStatus = Status;
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
                ? "Initial owner assignment -> EVALUATING"
                : prevStatus == RequestStatus.Escalated
                    ? "Escalation reassignment -> EVALUATING"
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
    /// Records triage evaluation notes while in EVALUATING state (UC-REQ-003).
    /// Lifecycle state remains EVALUATING.
    /// </summary>
    public void Evaluate(
        string evaluationNotes,
        Guid actorPersonId,
        DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(evaluationNotes))
            throw new RequestDomainValidationException("Evaluation notes cannot be empty.", nameof(evaluationNotes));
        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        if (Status != RequestStatus.Evaluating)
        {
            throw new InvalidRequestStateTransitionException(
                Id, Status, nameof(Evaluate), reason: "Request must be in EVALUATING state to record an evaluation.");
        }

        var now = utcNow ?? DateTime.UtcNow;
        EvaluationNotes = evaluationNotes.Trim();
        UpdatedAt = now;

        _domainEvents.Add(new RequestEvaluated(
            requestId: Id,
            evaluatedByPersonId: actorPersonId,
            evaluationNotes: EvaluationNotes,
            occurredAtUtc: now));
    }

    /// <summary>
    /// Updates the complexity rating while the request is in an active lifecycle state (Architecture CR-005).
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

        if (Status == RequestStatus.Completed || Status == RequestStatus.Rejected)
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
    /// Accepts operational responsibility for the Request, transitioning EVALUATING -> ACCEPTED (UC-REQ-004).
    /// </summary>
    public void Accept(
        Guid actorPersonId,
        string? notes = null,
        DateTime? utcNow = null)
    {
        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        if (Status != RequestStatus.Evaluating)
        {
            throw new InvalidRequestStateTransitionException(
                Id, Status, nameof(Accept), RequestStatus.Accepted, reason: "Request must be in EVALUATING state to be accepted.");
        }

        var now = utcNow ?? DateTime.UtcNow;
        var prevStatus = Status;

        Status = RequestStatus.Accepted;
        UpdatedAt = now;

        var assignment = RequestAssignment.Create(
            requestId: Id,
            previousOwnerPersonId: OwnerPersonId,
            assignedOwnerPersonId: OwnerPersonId,
            actorPersonId: actorPersonId,
            previousStatus: prevStatus,
            newStatus: RequestStatus.Accepted,
            assignedAtUtc: now,
            notes: notes ?? "Request accepted");

        _assignments.Add(assignment);

        _domainEvents.Add(new RequestAccepted(
            requestId: Id,
            ownerPersonId: OwnerPersonId ?? actorPersonId,
            notes: notes,
            occurredAtUtc: now));
    }

    /// <summary>
    /// Alias for <see cref="Accept(Guid, string?, DateTime?)"/> to support UC-REQ-004 terminology.
    /// </summary>
    public void AcceptResponsibility(
        Guid actorPersonId,
        string? notes = null,
        DateTime? utcNow = null) => Accept(actorPersonId, notes, utcNow);

    /// <summary>
    /// Declines responsibility during evaluation, closing the Request with outcome REJECTED (UC-REQ-005).
    /// Transitions EVALUATING -> REJECTED.
    /// </summary>
    public void Reject(
        string reason,
        Guid actorPersonId,
        DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new RequestDomainValidationException("Rejection reason cannot be empty.", nameof(reason));
        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        if (Status != RequestStatus.Evaluating)
        {
            throw new InvalidRequestStateTransitionException(
                Id, Status, nameof(Reject), RequestStatus.Rejected, reason: "Request must be in EVALUATING state to be rejected.");
        }

        var now = utcNow ?? DateTime.UtcNow;
        var prevStatus = Status;

        Status = RequestStatus.Rejected;
        Resolution = RequestResolution.Create(
            requestId: Id,
            outcome: ResolutionOutcomeNames.Rejected,
            description: reason,
            resolvedBy: actorPersonId,
            resolvedAt: now);
        UpdatedAt = now;

        var assignment = RequestAssignment.Create(
            requestId: Id,
            previousOwnerPersonId: OwnerPersonId,
            assignedOwnerPersonId: OwnerPersonId,
            actorPersonId: actorPersonId,
            previousStatus: prevStatus,
            newStatus: RequestStatus.Rejected,
            assignedAtUtc: now,
            notes: $"Request rejected: {reason.Trim()}");

        _assignments.Add(assignment);

        _domainEvents.Add(new RequestRejected(
            requestId: Id,
            rejectedByPersonId: actorPersonId,
            rejectionReason: reason.Trim(),
            occurredAtUtc: now));
    }

    /// <summary>
    /// Escalates the Request due to authority, technical, or resource barriers (UC-REQ-006).
    /// Valid transitions: EVALUATING -> ESCALATED or IN_PROGRESS -> ESCALATED.
    /// </summary>
    public void Escalate(
        string reason,
        Guid actorPersonId,
        DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new RequestDomainValidationException("Escalation reason cannot be empty.", nameof(reason));
        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        if (Status != RequestStatus.Evaluating && Status != RequestStatus.InProgress)
        {
            throw new InvalidRequestStateTransitionException(
                Id, Status, nameof(Escalate), RequestStatus.Escalated, reason: "Request can only be escalated from EVALUATING or IN_PROGRESS state.");
        }

        var now = utcNow ?? DateTime.UtcNow;
        var prevStatus = Status;

        Status = RequestStatus.Escalated;
        EscalationReason = reason.Trim();
        UpdatedAt = now;

        var assignment = RequestAssignment.Create(
            requestId: Id,
            previousOwnerPersonId: OwnerPersonId,
            assignedOwnerPersonId: OwnerPersonId,
            actorPersonId: actorPersonId,
            previousStatus: prevStatus,
            newStatus: RequestStatus.Escalated,
            assignedAtUtc: now,
            notes: $"Request escalated: {reason.Trim()}");

        _assignments.Add(assignment);

        _domainEvents.Add(new RequestEscalated(
            requestId: Id,
            escalatedByPersonId: actorPersonId,
            escalationReason: reason.Trim(),
            occurredAtUtc: now));
    }

    /// <summary>
    /// Starts active work on the Request, transitioning ACCEPTED -> IN_PROGRESS or ESCALATED -> IN_PROGRESS.
    /// </summary>
    public void StartProgress(
        Guid actorPersonId,
        string? notes = null,
        DateTime? utcNow = null)
    {
        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        if (Status != RequestStatus.Accepted && Status != RequestStatus.Escalated)
        {
            throw new InvalidRequestStateTransitionException(
                Id, Status, nameof(StartProgress), RequestStatus.InProgress, reason: "Request can only transition to IN_PROGRESS from ACCEPTED or ESCALATED state.");
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
            notes: notes ?? (prevStatus == RequestStatus.Escalated ? "Resumed progress from ESCALATED" : "Work started on request"));

        _assignments.Add(assignment);
    }

    /// <summary>
    /// Resumes active work from ESCALATED state, transitioning ESCALATED -> IN_PROGRESS.
    /// </summary>
    public void ResumeProgress(
        Guid actorPersonId,
        string? notes = null,
        DateTime? utcNow = null)
    {
        if (Status != RequestStatus.Escalated)
        {
            throw new InvalidRequestStateTransitionException(
                Id, Status, nameof(ResumeProgress), RequestStatus.InProgress, reason: "Request can only be resumed from ESCALATED state.");
        }

        StartProgress(actorPersonId, notes ?? "Progress resumed from ESCALATED", utcNow);
    }

    /// <summary>
    /// Concludes work on the Request, transitioning IN_PROGRESS -> COMPLETED (UC-REQ-008).
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
            description: resolutionDescription,
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
    /// Elevates an active or escalated request for a management decision (UC-REQ-007).
    /// Does not change lifecycle status; emits <see cref="ManagementDecisionRequested"/>.
    /// </summary>
    public void RequestManagementDecision(
        string decisionDetails,
        Guid actorPersonId,
        DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(decisionDetails))
            throw new RequestDomainValidationException("Decision details cannot be empty.", nameof(decisionDetails));
        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        if (Status == RequestStatus.Rejected || Status == RequestStatus.Completed)
        {
            throw new InvalidRequestStateTransitionException(
                Id, Status, nameof(RequestManagementDecision), reason: "Cannot request management decision on a closed request.");
        }

        var now = utcNow ?? DateTime.UtcNow;
        ManagementDecisionNotes = decisionDetails.Trim();
        UpdatedAt = now;

        _domainEvents.Add(new ManagementDecisionRequested(
            requestId: Id,
            requestedByPersonId: actorPersonId,
            decisionDetails: decisionDetails.Trim(),
            occurredAtUtc: now));
    }

    /// <summary>
    /// Applies a management determination to an ESCALATED request, transitioning to EVALUATING or IN_PROGRESS.
    /// </summary>
    public void ApplyManagementDecision(
        RequestStatus targetStatus,
        string decisionNotes,
        Guid actorPersonId,
        DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(decisionNotes))
            throw new RequestDomainValidationException("Decision notes cannot be empty.", nameof(decisionNotes));
        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        if (Status != RequestStatus.Escalated)
        {
            throw new InvalidRequestStateTransitionException(
                Id, Status, nameof(ApplyManagementDecision), targetStatus, reason: "Management decision can only be applied to a request in ESCALATED state.");
        }

        if (targetStatus != RequestStatus.Evaluating && targetStatus != RequestStatus.InProgress)
        {
            throw new InvalidRequestStateTransitionException(
                Id, Status, nameof(ApplyManagementDecision), targetStatus, reason: "Management decision on ESCALATED request can only transition to EVALUATING or IN_PROGRESS.");
        }

        var now = utcNow ?? DateTime.UtcNow;
        var prevStatus = Status;

        Status = targetStatus;
        ManagementDecisionNotes = decisionNotes.Trim();
        UpdatedAt = now;

        var assignment = RequestAssignment.Create(
            requestId: Id,
            previousOwnerPersonId: OwnerPersonId,
            assignedOwnerPersonId: OwnerPersonId,
            actorPersonId: actorPersonId,
            previousStatus: prevStatus,
            newStatus: targetStatus,
            assignedAtUtc: now,
            notes: $"Management decision applied -> {targetStatus}: {decisionNotes.Trim()}");

        _assignments.Add(assignment);
    }

    /// <summary>
    /// Reassigns ownership to a new person (UC-MGT-001).
    /// From ESCALATED, transitions to EVALUATING (default) or IN_PROGRESS.
    /// From CAPTURED, transitions to EVALUATING.
    /// From EVALUATING, ACCEPTED, or IN_PROGRESS, preserves status.
    /// </summary>
    public void ReassignOwner(
        Guid newOwnerPersonId,
        Guid actorPersonId,
        RequestStatus? targetStatusForEscalated = null,
        string? notes = null,
        DateTime? utcNow = null)
    {
        if (newOwnerPersonId == Guid.Empty)
            throw new RequestDomainValidationException("NewOwnerPersonId cannot be empty.", nameof(newOwnerPersonId));
        if (actorPersonId == Guid.Empty)
            throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        if (Status == RequestStatus.Rejected || Status == RequestStatus.Completed)
        {
            throw new InvalidRequestStateTransitionException(
                Id, Status, nameof(ReassignOwner), reason: "Closed requests cannot be reassigned.");
        }

        if (OwnerPersonId.HasValue && OwnerPersonId.Value == newOwnerPersonId)
        {
            throw new InvalidOperationException("Proposed owner is identical to current owner.");
        }

        var now = utcNow ?? DateTime.UtcNow;
        var prevOwner = OwnerPersonId;
        var prevStatus = Status;

        RequestStatus newStatus;

        if (Status == RequestStatus.Escalated)
        {
            newStatus = targetStatusForEscalated ?? RequestStatus.Evaluating;
            if (newStatus != RequestStatus.Evaluating && newStatus != RequestStatus.InProgress)
            {
                throw new InvalidRequestStateTransitionException(
                    Id, Status, nameof(ReassignOwner), newStatus, reason: "Reassigning an ESCALATED request can only transition to EVALUATING or IN_PROGRESS.");
            }
        }
        else if (Status == RequestStatus.Captured)
        {
            newStatus = RequestStatus.Evaluating;
        }
        else
        {
            newStatus = Status;
        }

        OwnerPersonId = newOwnerPersonId;
        Status = newStatus;
        UpdatedAt = now;

        var assignment = RequestAssignment.Create(
            requestId: Id,
            previousOwnerPersonId: prevOwner,
            assignedOwnerPersonId: newOwnerPersonId,
            actorPersonId: actorPersonId,
            previousStatus: prevStatus,
            newStatus: newStatus,
            assignedAtUtc: now,
            notes: notes ?? $"Owner reassigned to {newOwnerPersonId}");

        _assignments.Add(assignment);

        _domainEvents.Add(new RequestAssigned(
            requestId: Id,
            ownerPersonId: newOwnerPersonId,
            previousOwnerPersonId: prevOwner,
            actorPersonId: actorPersonId,
            previousStatus: prevStatus,
            newStatus: newStatus,
            notes: notes,
            occurredAtUtc: now));
    }

    // =========================================================================
    // Sub-Task Checklist Operations (CR-006 Architecture TD-001, TD-003, TD-004, TD-007)
    // =========================================================================

    private void EnsureActiveStateForSubTaskMutation(string operation)
    {
        if (Status == RequestStatus.Completed || Status == RequestStatus.Rejected)
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
    /// Adds a new operational sub-task checklist item to the Request (Architecture CR-006 TD-001, TD-003, TD-004, TD-007).
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
    /// Marks an existing sub-task checklist item completed on the Request (Architecture CR-006 TD-001, TD-003, TD-004, TD-007).
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
    /// Reopens a completed sub-task checklist item back to pending on the Request (Architecture CR-006 TD-001, TD-003, TD-004, TD-007).
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
    /// Removes a sub-task checklist item from the Request (Architecture CR-006 TD-001, TD-003, TD-004, TD-007).
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
            EscalationReason = escalationReason,
            ManagementDecisionNotes = managementDecisionNotes,
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
}
