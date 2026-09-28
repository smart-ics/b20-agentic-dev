namespace ICS.Modules.WorkPackage.Domain;

using ICS.Core.Domain;
using ICS.Modules.WorkPackage.Domain.Events;
using ICS.Modules.WorkPackage.Domain.Exceptions;

/// <summary>
/// Authoritative domain aggregate representing a Work Package: a temporary container of related
/// operational Requests sharing a common objective.
/// Lifecycle: DRAFT → ACTIVE → CLOSED (or DRAFT → CLOSED).
/// Architecture §11; work-package-domain.md §5, §6, §8, §9.
/// </summary>
public class WorkPackage : Entity
{
    private readonly List<WorkPackageRequest> _requests = new();

    public string Name { get; private set; } = string.Empty;
    public string Objective { get; private set; } = string.Empty;
    public string Status { get; private set; } = WorkPackageStatus.Draft;
    public Guid OwnerPersonId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public Guid? ProductId { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public string? CloseReason { get; private set; }

    /// <summary>
    /// Complete historical list of request memberships associated with this Work Package.
    /// Preserves traceability per Business Rule 15.
    /// </summary>
    public IReadOnlyCollection<WorkPackageRequest> Requests => _requests.AsReadOnly();

    /// <summary>
    /// Current active request memberships in this Work Package.
    /// </summary>
    public IEnumerable<WorkPackageRequest> ActiveRequests => _requests.Where(r => r.IsActive);

    public bool IsClosed => WorkPackageStatus.IsTerminal(Status);
    public bool IsActive => WorkPackageStatus.IsActive(Status);
    public bool IsDraft => WorkPackageStatus.IsDraft(Status);

    /// <summary>
    /// Checks if a request is currently an active member of this Work Package.
    /// </summary>
    public bool HasActiveRequest(Guid requestId) =>
        _requests.Any(r => r.RequestId == requestId && r.IsActive);

    /// <summary>
    /// Parameterless constructor for Dapper persistence mapping.
    /// </summary>
    protected WorkPackage() { }

    /// <summary>
    /// Full constructor for hydrating the entity from persistence without raising domain events.
    /// </summary>
    public WorkPackage(
        Guid id,
        string name,
        string objective,
        string status,
        Guid ownerPersonId,
        Guid? customerId,
        Guid? productId,
        DateTime createdAt,
        DateTime? updatedAt = null,
        DateTime? closedAt = null,
        string? closeReason = null,
        IEnumerable<WorkPackageRequest>? requests = null)
        : base(id)
    {
        Id = id;
        Name = name;
        Objective = objective;
        Status = status;
        OwnerPersonId = ownerPersonId;
        CustomerId = customerId;
        ProductId = productId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        ClosedAt = closedAt;
        CloseReason = closeReason;

        if (requests != null)
        {
            _requests.AddRange(requests);
        }
    }

    /// <summary>
    /// Factory method to create a new Work Package in DRAFT status.
    /// Emits <see cref="WorkPackageCreated"/>.
    /// </summary>
    public static WorkPackage Create(
        Guid id,
        string name,
        string objective,
        Guid ownerPersonId,
        Guid? customerId = null,
        Guid? productId = null,
        DateTime? createdAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("WorkPackageId cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(objective))
            throw new ArgumentException("Objective cannot be empty.", nameof(objective));
        if (ownerPersonId == Guid.Empty)
            throw new ArgumentException("OwnerPersonId cannot be empty.", nameof(ownerPersonId));

        var effectiveCreatedAt = createdAt ?? DateTime.UtcNow;

        var workPackage = new WorkPackage
        {
            Id = id,
            Name = name.Trim(),
            Objective = objective.Trim(),
            Status = WorkPackageStatus.Draft,
            OwnerPersonId = ownerPersonId,
            CustomerId = customerId,
            ProductId = productId,
            CreatedAt = effectiveCreatedAt
        };

        workPackage.AddDomainEvent(new WorkPackageCreated(
            workPackage.Id,
            workPackage.Name,
            workPackage.Objective,
            workPackage.OwnerPersonId,
            workPackage.CustomerId,
            workPackage.ProductId));

        return workPackage;
    }

    /// <summary>
    /// Updates the title and objective of the Work Package.
    /// </summary>
    public void UpdateObjective(string name, string objective, DateTime? updatedAt = null)
    {
        if (IsClosed)
            throw new WorkPackageDomainException("Cannot update objective of a closed Work Package.");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(objective))
            throw new ArgumentException("Objective cannot be empty.", nameof(objective));

        Name = name.Trim();
        Objective = objective.Trim();
        UpdatedAt = updatedAt ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Reassigns ownership of the Work Package to another Person.
    /// Emits <see cref="WorkPackageOwnerChanged"/>.
    /// </summary>
    public void AssignOwner(Guid newOwnerPersonId, DateTime? changedAt = null)
    {
        if (IsClosed)
            throw new WorkPackageDomainException("Cannot reassign owner of a closed Work Package.");
        if (newOwnerPersonId == Guid.Empty)
            throw new ArgumentException("New owner person ID cannot be empty.", nameof(newOwnerPersonId));

        if (OwnerPersonId == newOwnerPersonId)
            return;

        var previousOwner = OwnerPersonId;
        OwnerPersonId = newOwnerPersonId;
        var effectiveChangedAt = changedAt ?? DateTime.UtcNow;
        UpdatedAt = effectiveChangedAt;

        AddDomainEvent(new WorkPackageOwnerChanged(Id, previousOwner, newOwnerPersonId, effectiveChangedAt));
    }

    /// <summary>
    /// Associates an operational Request with this Work Package.
    /// Enforces Business Rule 9: a request may belong to at most one active Work Package.
    /// Emits <see cref="RequestAddedToWorkPackage"/>.
    /// </summary>
    public WorkPackageRequest AddRequest(
        Guid requestId,
        DateTime? addedAt = null,
        Func<Guid, bool>? isRequestInAnotherActivePackage = null)
    {
        if (IsClosed)
            throw new WorkPackageDomainException("Cannot add requests to a closed Work Package.");
        if (requestId == Guid.Empty)
            throw new ArgumentException("RequestId cannot be empty.", nameof(requestId));

        // Enforce Business Rule 9 within this aggregate
        if (HasActiveRequest(requestId))
        {
            throw new BusinessRule9ViolationException(
                requestId,
                Id,
                $"Business Rule 9 violation: Request '{requestId}' is already an active member of Work Package '{Id}'.");
        }

        // Enforce Business Rule 9 across active Work Packages if checker delegate provided
        if (isRequestInAnotherActivePackage != null && isRequestInAnotherActivePackage(requestId))
        {
            throw new BusinessRule9ViolationException(
                requestId,
                Id,
                $"Business Rule 9 violation: Request '{requestId}' already belongs to an active Work Package.");
        }

        var effectiveAddedAt = addedAt ?? DateTime.UtcNow;
        var membership = new WorkPackageRequest(Guid.NewGuid(), Id, requestId, effectiveAddedAt);
        _requests.Add(membership);

        UpdatedAt = effectiveAddedAt;
        AddDomainEvent(new RequestAddedToWorkPackage(Id, requestId, effectiveAddedAt));

        return membership;
    }

    /// <summary>
    /// Associates an operational Request with this Work Package, checking Business Rule 9 via <see cref="IWorkPackageMembershipChecker"/>.
    /// </summary>
    public WorkPackageRequest AddRequest(
        Guid requestId,
        DateTime addedAt,
        IWorkPackageMembershipChecker membershipChecker)
    {
        ArgumentNullException.ThrowIfNull(membershipChecker);
        return AddRequest(requestId, addedAt, rId => membershipChecker.IsRequestInActiveWorkPackage(rId, Id));
    }

    /// <summary>
    /// Removes an operational Request from this Work Package by deactivating the membership link.
    /// Emits <see cref="RequestRemovedFromWorkPackage"/>.
    /// </summary>
    public void RemoveRequest(Guid requestId, DateTime? removedAt = null)
    {
        if (IsClosed)
            throw new WorkPackageDomainException("Cannot remove requests from a closed Work Package.");
        if (requestId == Guid.Empty)
            throw new ArgumentException("RequestId cannot be empty.", nameof(requestId));

        var membership = _requests.FirstOrDefault(r => r.RequestId == requestId && r.IsActive);
        if (membership == null)
        {
            throw new WorkPackageDomainException($"Request '{requestId}' is not an active member of Work Package '{Id}'.");
        }

        var effectiveRemovedAt = removedAt ?? DateTime.UtcNow;
        membership.Deactivate(effectiveRemovedAt);

        UpdatedAt = effectiveRemovedAt;
        AddDomainEvent(new RequestRemovedFromWorkPackage(Id, requestId, effectiveRemovedAt));
    }

    /// <summary>
    /// Activates the Work Package, transitioning state from DRAFT to ACTIVE.
    /// Emits <see cref="WorkPackageActivated"/>.
    /// </summary>
    public void Activate(DateTime? activatedAt = null)
    {
        WorkPackageStateMachine.EnsureValidTransition(Id, Status, WorkPackageStatus.Active);

        var effectiveActivatedAt = activatedAt ?? DateTime.UtcNow;
        Status = WorkPackageStatus.Active;
        UpdatedAt = effectiveActivatedAt;

        AddDomainEvent(new WorkPackageActivated(Id, effectiveActivatedAt));
    }

    /// <summary>
    /// Closes the Work Package, transitioning state from DRAFT or ACTIVE to CLOSED.
    /// Emits <see cref="WorkPackageClosed"/>.
    /// </summary>
    public void Close(string? reason = null, DateTime? closedAt = null)
    {
        WorkPackageStateMachine.EnsureValidTransition(Id, Status, WorkPackageStatus.Closed);

        var effectiveClosedAt = closedAt ?? DateTime.UtcNow;
        Status = WorkPackageStatus.Closed;
        CloseReason = reason?.Trim();
        ClosedAt = effectiveClosedAt;
        UpdatedAt = effectiveClosedAt;

        AddDomainEvent(new WorkPackageClosed(Id, CloseReason, effectiveClosedAt));
    }
}
