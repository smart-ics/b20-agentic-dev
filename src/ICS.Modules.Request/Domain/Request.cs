namespace ICS.Modules.Request.Domain;

using ICS.Core.Domain;
using ICS.Modules.Request.Domain.Events;
using ICS.Modules.Request.Domain.Exceptions;

/// <summary>
/// Authoritative domain aggregate representing a customer or operational Request.
/// State machine: CAPTURED → EVALUATING → ACCEPTED / REJECTED → IN_PROGRESS → ESCALATED → COMPLETED.
/// Architecture §6, §7, §8, §13, §16, §17; request-domain.md.
/// </summary>
public class Request : Entity
{
    public const string PriorityLow = "LOW";
    public const string PriorityMedium = "MEDIUM";
    public const string PriorityHigh = "HIGH";
    public const string PriorityCritical = "CRITICAL";

    public const string TypeBug = "BUG";
    public const string TypeFeature = "FEATURE";
    public const string TypeSupport = "SUPPORT";
    public const string TypeChangeRequest = "CHANGE_REQUEST";

    private readonly List<RequestAssignment> _assignments = new();

    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Type { get; private set; } = TypeSupport;
    public string Priority { get; private set; } = PriorityMedium;
    public string Status { get; private set; } = RequestStatus.Captured;

    public Guid? RequesterPersonId { get; private set; }
    public Guid? RequesterContactId { get; private set; }
    public string? RequesterName { get; private set; }

    public Guid? OwnerPersonId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public Guid? ProductId { get; private set; }
    public Guid? WorkPackageId { get; private set; }

    public bool IsAwaitingManagementDecision { get; private set; }
    public string? ManagementDecisionQuestion { get; private set; }
    public string? ManagementDecisionOptions { get; private set; }
    public string? ManagementDecisionImpact { get; private set; }
    public DateTime? ManagementDecisionRequestedAt { get; private set; }

    public string? EscalationReason { get; private set; }
    public Guid? EscalatedByPersonId { get; private set; }
    public DateTime? EscalatedAt { get; private set; }

    public DateTime? ClosedAt { get; private set; }
    public RequestResolution? Resolution { get; private set; }

    public IReadOnlyCollection<RequestAssignment> Assignments => _assignments.AsReadOnly();

    public bool IsTerminal => RequestStatus.IsTerminal(Status);
    public bool IsActive => RequestStatus.IsActive(Status);

    /// <summary>
    /// Parameterless constructor for Dapper persistence mapping.
    /// </summary>
    protected Request() { }

    /// <summary>
    /// Full constructor for hydrating the entity from persistence without raising domain events.
    /// </summary>
    public Request(
        Guid id,
        string title,
        string description,
        string type,
        string priority,
        string status,
        Guid? requesterPersonId,
        Guid? requesterContactId,
        string? requesterName,
        Guid? ownerPersonId,
        Guid? customerId,
        Guid? productId,
        Guid? workPackageId,
        bool isAwaitingManagementDecision,
        string? managementDecisionQuestion,
        string? managementDecisionOptions,
        string? managementDecisionImpact,
        DateTime? managementDecisionRequestedAt,
        string? escalationReason,
        Guid? escalatedByPersonId,
        DateTime? escalatedAt,
        DateTime createdAt,
        DateTime? updatedAt,
        DateTime? closedAt,
        RequestResolution? resolution = null,
        IEnumerable<RequestAssignment>? assignments = null)
        : base(id)
    {
        Id = id;
        Title = title;
        Description = description;
        Type = type;
        Priority = priority;
        Status = status;
        RequesterPersonId = requesterPersonId;
        RequesterContactId = requesterContactId;
        RequesterName = requesterName;
        OwnerPersonId = ownerPersonId;
        CustomerId = customerId;
        ProductId = productId;
        WorkPackageId = workPackageId;
        IsAwaitingManagementDecision = isAwaitingManagementDecision;
        ManagementDecisionQuestion = managementDecisionQuestion;
        ManagementDecisionOptions = managementDecisionOptions;
        ManagementDecisionImpact = managementDecisionImpact;
        ManagementDecisionRequestedAt = managementDecisionRequestedAt;
        EscalationReason = escalationReason;
        EscalatedByPersonId = escalatedByPersonId;
        EscalatedAt = escalatedAt;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        ClosedAt = closedAt;
        Resolution = resolution;

        if (assignments != null)
        {
            _assignments.AddRange(assignments);
        }
    }

    /// <summary>
    /// Factory method to record and formalize a new operational customer Request (UC-REQ-001).
    /// Initial lifecycle status is <see cref="RequestStatus.Captured"/>.
    /// </summary>
    public static Request Record(
        Guid id,
        string title,
        string description,
        string type,
        string priority,
        Guid? requesterPersonId,
        Guid? requesterContactId,
        string? requesterName,
        Guid? customerId,
        Guid? productId,
        Guid? workPackageId,
        DateTime recordedAt,
        Guid? initialOwnerPersonId = null,
        Guid? assignedByPersonId = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("RequestId cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty.", nameof(description));
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Type cannot be empty.", nameof(type));

        var validPriority = string.IsNullOrWhiteSpace(priority) ? PriorityMedium : priority.Trim().ToUpperInvariant();

        var request = new Request
        {
            Id = id,
            Title = title.Trim(),
            Description = description.Trim(),
            Type = type.Trim().ToUpperInvariant(),
            Priority = validPriority,
            Status = RequestStatus.Captured,
            RequesterPersonId = requesterPersonId,
            RequesterContactId = requesterContactId,
            RequesterName = requesterName?.Trim(),
            CustomerId = customerId,
            ProductId = productId,
            WorkPackageId = workPackageId,
            CreatedAt = recordedAt
        };

        request.AddDomainEvent(new RequestRecorded(
            request.Id,
            request.Title,
            request.Type,
            request.RequesterPersonId,
            request.RequesterContactId,
            request.RequesterName,
            request.CustomerId,
            request.ProductId,
            request.WorkPackageId));

        if (initialOwnerPersonId.HasValue && initialOwnerPersonId.Value != Guid.Empty)
        {
            var actorId = assignedByPersonId ?? requesterPersonId ?? initialOwnerPersonId.Value;
            request.AssignOwner(initialOwnerPersonId.Value, actorId, recordedAt);
        }

        return request;
    }

    /// <summary>
    /// Assigns or reassigns an operational owner to the Request (UC-REQ-002, UC-MGT-001).
    /// Preserves full assignment history in <see cref="Assignments"/>.
    /// </summary>
    public void AssignOwner(
        Guid newOwnerPersonId,
        Guid assignedByPersonId,
        DateTime assignedAt,
        string? note = null)
    {
        if (IsTerminal)
            throw new RequestDomainException($"Cannot assign owner to a closed/terminal Request in status '{Status}'.");
        if (newOwnerPersonId == Guid.Empty)
            throw new ArgumentException("New owner person ID cannot be empty.", nameof(newOwnerPersonId));
        if (assignedByPersonId == Guid.Empty)
            throw new ArgumentException("Assigned by person ID cannot be empty.", nameof(assignedByPersonId));

        var previousOwner = OwnerPersonId;

        // Deactivate previous active assignment record
        foreach (var assignment in _assignments.Where(a => a.IsActive))
        {
            assignment.Deactivate(assignedAt);
        }

        var newAssignment = new RequestAssignment(
            Guid.NewGuid(),
            Id,
            newOwnerPersonId,
            assignedByPersonId,
            assignedAt,
            note,
            isActive: true);

        _assignments.Add(newAssignment);
        OwnerPersonId = newOwnerPersonId;
        UpdatedAt = assignedAt;

        AddDomainEvent(new RequestAssigned(
            Id,
            previousOwner,
            newOwnerPersonId,
            assignedByPersonId,
            note?.Trim()));
    }

    /// <summary>
    /// Evaluates the assigned Request during triage, transitioning state from CAPTURED to EVALUATING (UC-REQ-003).
    /// </summary>
    public void Evaluate(
        Guid evaluatedByPersonId,
        DateTime evaluatedAt,
        string? notes = null)
    {
        RequestStateMachine.EnsureValidTransition(Id, Status, RequestStatus.Evaluating);

        if (evaluatedByPersonId == Guid.Empty)
            throw new ArgumentException("EvaluatedByPersonId cannot be empty.", nameof(evaluatedByPersonId));

        Status = RequestStatus.Evaluating;
        UpdatedAt = evaluatedAt;

        AddDomainEvent(new RequestEvaluated(
            Id,
            evaluatedByPersonId,
            notes?.Trim()));
    }

    /// <summary>
    /// Accepts operational responsibility for the Request, transitioning state from EVALUATING to ACCEPTED (UC-REQ-004).
    /// </summary>
    public void Accept(
        Guid acceptedByPersonId,
        DateTime acceptedAt,
        string? notes = null)
    {
        RequestStateMachine.EnsureValidTransition(Id, Status, RequestStatus.Accepted);

        if (acceptedByPersonId == Guid.Empty)
            throw new ArgumentException("AcceptedByPersonId cannot be empty.", nameof(acceptedByPersonId));

        Status = RequestStatus.Accepted;
        UpdatedAt = acceptedAt;

        AddDomainEvent(new RequestAccepted(
            Id,
            acceptedByPersonId,
            notes?.Trim()));
    }

    /// <summary>
    /// Rejects the Request during evaluation or triage, transitioning state to REJECTED with documented justification (UC-REQ-005).
    /// </summary>
    public void Reject(
        Guid rejectedByPersonId,
        string reason,
        DateTime rejectedAt)
    {
        RequestStateMachine.EnsureValidTransition(Id, Status, RequestStatus.Rejected);

        if (rejectedByPersonId == Guid.Empty)
            throw new ArgumentException("RejectedByPersonId cannot be empty.", nameof(rejectedByPersonId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason cannot be empty or whitespace.", nameof(reason));

        Status = RequestStatus.Rejected;
        UpdatedAt = rejectedAt;
        ClosedAt = rejectedAt;

        Resolution = RequestResolution.CreateRejected(
            Guid.NewGuid(),
            Id,
            reason.Trim(),
            rejectedByPersonId,
            rejectedAt);

        AddDomainEvent(new RequestRejected(
            Id,
            rejectedByPersonId,
            reason.Trim()));
    }

    /// <summary>
    /// Starts active implementation/progress on the Request, transitioning state from ACCEPTED to IN_PROGRESS.
    /// </summary>
    public void StartProgress(
        Guid actorPersonId,
        DateTime startedAt,
        string? notes = null)
    {
        RequestStateMachine.EnsureValidTransition(Id, Status, RequestStatus.InProgress);

        if (actorPersonId == Guid.Empty)
            throw new ArgumentException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        Status = RequestStatus.InProgress;
        UpdatedAt = startedAt;
    }

    /// <summary>
    /// Escalates the Request when it exceeds the owner's authority or capability, transitioning state to ESCALATED (UC-REQ-006).
    /// </summary>
    public void Escalate(
        Guid escalatedByPersonId,
        string reason,
        string? requiredAssistance,
        DateTime escalatedAt)
    {
        RequestStateMachine.EnsureValidTransition(Id, Status, RequestStatus.Escalated);

        if (escalatedByPersonId == Guid.Empty)
            throw new ArgumentException("EscalatedByPersonId cannot be empty.", nameof(escalatedByPersonId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Escalation reason cannot be empty or whitespace.", nameof(reason));

        Status = RequestStatus.Escalated;
        EscalationReason = reason.Trim();
        EscalatedByPersonId = escalatedByPersonId;
        EscalatedAt = escalatedAt;
        UpdatedAt = escalatedAt;

        AddDomainEvent(new RequestEscalated(
            Id,
            escalatedByPersonId,
            reason.Trim(),
            requiredAssistance?.Trim()));
    }

    /// <summary>
    /// Resolves an active escalation, returning the Request to IN_PROGRESS status.
    /// </summary>
    public void ResolveEscalation(
        Guid actorPersonId,
        DateTime resolvedAt,
        string? notes = null)
    {
        RequestStateMachine.EnsureValidTransition(Id, Status, RequestStatus.InProgress);

        if (actorPersonId == Guid.Empty)
            throw new ArgumentException("ActorPersonId cannot be empty.", nameof(actorPersonId));

        Status = RequestStatus.InProgress;
        UpdatedAt = resolvedAt;
    }

    /// <summary>
    /// Elevates an active Request to Management for a policy, risk, or resource decision (UC-REQ-007).
    /// Records the decision question without disrupting the underlying lifecycle state.
    /// </summary>
    public void RequestManagementDecision(
        Guid requestedByPersonId,
        string question,
        string? options,
        string? impact,
        DateTime requestedAt)
    {
        if (IsTerminal)
            throw new RequestDomainException($"Cannot request management decision for a closed Request in status '{Status}'.");
        if (requestedByPersonId == Guid.Empty)
            throw new ArgumentException("RequestedByPersonId cannot be empty.", nameof(requestedByPersonId));
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Management decision question cannot be empty.", nameof(question));

        IsAwaitingManagementDecision = true;
        ManagementDecisionQuestion = question.Trim();
        ManagementDecisionOptions = options?.Trim();
        ManagementDecisionImpact = impact?.Trim();
        ManagementDecisionRequestedAt = requestedAt;
        UpdatedAt = requestedAt;

        AddDomainEvent(new ManagementDecisionRequested(
            Id,
            requestedByPersonId,
            question.Trim(),
            options?.Trim(),
            impact?.Trim()));
    }

    /// <summary>
    /// Completes the Request when work has been reviewed and accepted as resolved, transitioning state to COMPLETED (UC-REQ-008).
    /// </summary>
    public void Complete(
        Guid completedByPersonId,
        string summary,
        DateTime completedAt,
        string outcome = RequestResolution.OutcomeResolved)
    {
        RequestStateMachine.EnsureValidTransition(Id, Status, RequestStatus.Completed);

        if (completedByPersonId == Guid.Empty)
            throw new ArgumentException("CompletedByPersonId cannot be empty.", nameof(completedByPersonId));
        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("Resolution summary cannot be empty.", nameof(summary));

        Status = RequestStatus.Completed;
        UpdatedAt = completedAt;
        ClosedAt = completedAt;
        IsAwaitingManagementDecision = false;

        Resolution = RequestResolution.CreateResolved(
            Guid.NewGuid(),
            Id,
            summary.Trim(),
            completedByPersonId,
            completedAt);

        AddDomainEvent(new RequestCompleted(
            Id,
            completedByPersonId,
            summary.Trim(),
            outcome));
    }

    /// <summary>
    /// Returns the Request for rework during completion review, ensuring the Request remains in IN_PROGRESS status (UC-REQ-008).
    /// </summary>
    public void RequestRework(
        Guid reviewerPersonId,
        string reworkFeedback,
        DateTime reviewedAt)
    {
        if (IsTerminal)
            throw new RequestDomainException($"Cannot request rework for a closed Request in status '{Status}'.");
        if (reviewerPersonId == Guid.Empty)
            throw new ArgumentException("ReviewerPersonId cannot be empty.", nameof(reviewerPersonId));
        if (string.IsNullOrWhiteSpace(reworkFeedback))
            throw new ArgumentException("Rework feedback cannot be empty.", nameof(reworkFeedback));

        Status = RequestStatus.InProgress;
        UpdatedAt = reviewedAt;
    }

    /// <summary>
    /// Updates core descriptive attributes of the Request.
    /// </summary>
    public void UpdateDetails(
        string title,
        string description,
        string type,
        DateTime updatedAt)
    {
        if (IsTerminal)
            throw new RequestDomainException($"Cannot update details of a closed Request in status '{Status}'.");
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty.", nameof(description));
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Type cannot be empty.", nameof(type));

        Title = title.Trim();
        Description = description.Trim();
        Type = type.Trim().ToUpperInvariant();
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Updates the priority of the Request.
    /// </summary>
    public void ChangePriority(string priority, DateTime updatedAt)
    {
        if (IsTerminal)
            throw new RequestDomainException($"Cannot change priority of a closed Request in status '{Status}'.");
        if (string.IsNullOrWhiteSpace(priority))
            throw new ArgumentException("Priority cannot be empty.", nameof(priority));

        Priority = priority.Trim().ToUpperInvariant();
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Associates the Request with an operational Work Package container.
    /// </summary>
    public void AssignWorkPackage(Guid? workPackageId, DateTime updatedAt)
    {
        if (IsTerminal)
            throw new RequestDomainException($"Cannot change Work Package for a closed Request in status '{Status}'.");

        WorkPackageId = workPackageId;
        UpdatedAt = updatedAt;
    }
}
