using Cakra.Core;
using Cakra.Modules.WorkPackage.Domain.Events;
using Cakra.Modules.WorkPackage.Domain.Exceptions;

namespace Cakra.Modules.WorkPackage.Domain;

/// <summary>
/// Work Package Aggregate Root (Architecture §11, Domain §5, §6).
/// Defines a temporary container of related operational Requests that share a common objective.
/// Provides operational grouping context without owning or altering Request lifecycles.
/// </summary>
public sealed class WorkPackage : EntityBase
{
    private readonly List<WorkPackageRequest> _requests = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>Descriptive name/title of the Work Package.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>The intended result that gives the Work Package its purpose.</summary>
    public string Objective { get; private set; } = string.Empty;

    /// <summary>Current lifecycle state of the Work Package (DRAFT, ACTIVE, CLOSED).</summary>
    public WorkPackageStatus Status { get; private set; }

    /// <summary>Reference to the Person accountable for maintaining the Work Package.</summary>
    public Guid OwnerPersonId { get; private set; }

    /// <summary>Optional Customer associated with this Work Package.</summary>
    public Guid? CustomerId { get; private set; }

    /// <summary>Optional Product associated with this Work Package.</summary>
    public Guid? ProductId { get; private set; }

    /// <summary>Optional target deadline date normalized to UTC midnight (Architecture CR-023 §4 TD-002).</summary>
    public DateTime? Deadline { get; private set; }

    /// <summary>Reason recorded when the Work Package is closed, or <c>null</c> if not closed.</summary>
    public string? ClosedReason { get; private set; }

    /// <summary>UTC timestamp when the Work Package was closed, or <c>null</c> if not closed.</summary>
    public DateTime? ClosedAt { get; private set; }

    /// <summary>All constituent requests (current and historical traceability per Business Rule 15).</summary>
    public IReadOnlyCollection<WorkPackageRequest> Requests => _requests.AsReadOnly();

    /// <summary>Active constituent requests currently in scope ordered by SortOrder.</summary>
    public IEnumerable<WorkPackageRequest> ActiveRequests => _requests.Where(r => r.IsActive).OrderBy(r => r.SortOrder).ThenBy(r => r.AddedAt);

    /// <summary>Domain events raised during the lifecycle of this aggregate.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Parameterless constructor for Dapper persistence hydration.
    /// </summary>
    private WorkPackage()
    {
    }

    /// <summary>
    /// Factory method to create a new Work Package in DRAFT status (Architecture §11, CR-023 §4 TD-002).
    /// </summary>
    public static WorkPackage Create(
        string name,
        string objective,
        Guid ownerPersonId,
        Guid? customerId = null,
        Guid? productId = null,
        Guid? id = null,
        DateTime? createdAtUtc = null,
        DateTime? deadline = null)
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

        var packageId = id ?? Guid.NewGuid();
        if (packageId == Guid.Empty)
        {
            throw new ArgumentException("WorkPackageId cannot be empty.", nameof(id));
        }

        var timestamp = createdAtUtc ?? DateTime.UtcNow;

        var workPackage = new WorkPackage
        {
            Id = packageId,
            Name = name.Trim(),
            Objective = objective.Trim(),
            OwnerPersonId = ownerPersonId,
            CustomerId = customerId,
            ProductId = productId,
            Deadline = NormalizeDeadline(deadline),
            Status = WorkPackageStatus.Draft,
            CreatedAt = timestamp,
            UpdatedAt = null
        };

        workPackage.AddDomainEvent(new WorkPackageCreated(
            packageId,
            workPackage.Name,
            workPackage.Objective,
            ownerPersonId,
            customerId,
            productId,
            timestamp));

        return workPackage;
    }

    /// <summary>
    /// Transitions the Work Package state from DRAFT to ACTIVE (Architecture §11, Domain §9).
    /// </summary>
    /// <param name="activatedAtUtc">Optional activation timestamp in UTC.</param>
    public void Activate(DateTime? activatedAtUtc = null)
    {
        if (Status == WorkPackageStatus.Active)
        {
            throw new InvalidWorkPackageStateTransitionException(
                Id,
                Status,
                WorkPackageStatus.Active,
                $"Work Package '{Id}' is already in ACTIVE state.");
        }

        if (Status == WorkPackageStatus.Closed)
        {
            throw new InvalidWorkPackageStateTransitionException(
                Id,
                Status,
                WorkPackageStatus.Active,
                $"Work Package '{Id}' is CLOSED and cannot be activated.");
        }

        var previousStatus = Status;
        Status = WorkPackageStatus.Active;
        var timestamp = activatedAtUtc ?? DateTime.UtcNow;
        UpdatedAt = timestamp;

        AddDomainEvent(new WorkPackageActivated(Id, previousStatus, Status, timestamp));
    }

    /// <summary>
    /// Transitions the Work Package state to CLOSED from ACTIVE or DRAFT (Architecture §11, Domain §9).
    /// </summary>
    /// <param name="reason">Mandatory reason describing why the package was closed.</param>
    /// <param name="closedAtUtc">Optional close timestamp in UTC.</param>
    public void Close(string reason, DateTime? closedAtUtc = null)
    {
        if (Status == WorkPackageStatus.Closed)
        {
            throw new InvalidWorkPackageStateTransitionException(
                Id,
                Status,
                WorkPackageStatus.Closed,
                $"Work Package '{Id}' is already CLOSED.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Close reason cannot be null or empty.", nameof(reason));
        }

        var previousStatus = Status;
        Status = WorkPackageStatus.Closed;
        ClosedReason = reason.Trim();
        var timestamp = closedAtUtc ?? DateTime.UtcNow;
        ClosedAt = timestamp;
        UpdatedAt = timestamp;

        AddDomainEvent(new WorkPackageClosed(Id, ClosedReason, previousStatus, Status, timestamp));
    }

    /// <summary>
    /// Updates the title and objective of the Work Package (Architecture §11).
    /// </summary>
    public void UpdateObjective(string name, string objective, DateTime? updatedAtUtc = null)
    {
        if (Status == WorkPackageStatus.Closed)
        {
            throw new WorkPackageDomainException(
                $"Cannot update objective for closed Work Package '{Id}'.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Work package name cannot be null or empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(objective))
        {
            throw new ArgumentException("Work package objective cannot be null or empty.", nameof(objective));
        }

        Name = name.Trim();
        Objective = objective.Trim();
        UpdatedAt = updatedAtUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the target deadline date for the Work Package (Architecture CR-023 §4 TD-002).
    /// Allowed in DRAFT and ACTIVE states; throws WorkPackageDomainException when CLOSED.
    /// </summary>
    public void UpdateDeadline(DateTime? deadline, DateTime? updatedAtUtc = null)
    {
        if (Status == WorkPackageStatus.Closed)
        {
            throw new WorkPackageDomainException(
                $"Cannot update deadline for closed Work Package '{Id}'.");
        }

        var normalizedNewDeadline = NormalizeDeadline(deadline);
        if (normalizedNewDeadline == Deadline)
        {
            return;
        }

        Deadline = normalizedNewDeadline;
        UpdatedAt = updatedAtUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the Customer and Product context associations of the Work Package (Architecture CR-024).
    /// Allowed in DRAFT and ACTIVE states; throws WorkPackageDomainException when CLOSED.
    /// </summary>
    public void UpdateContext(Guid? customerId, Guid? productId, DateTime? updatedAtUtc = null)
    {
        if (Status == WorkPackageStatus.Closed)
        {
            throw new WorkPackageDomainException(
                $"Cannot update context for closed Work Package '{Id}'.");
        }

        var normalizedCustomerId = customerId == Guid.Empty ? null : customerId;
        var normalizedProductId = productId == Guid.Empty ? null : productId;

        if (normalizedCustomerId == CustomerId && normalizedProductId == ProductId)
        {
            return;
        }

        var previousCustomerId = CustomerId;
        var previousProductId = ProductId;

        CustomerId = normalizedCustomerId;
        ProductId = normalizedProductId;
        var timestamp = updatedAtUtc ?? DateTime.UtcNow;
        UpdatedAt = timestamp;

        AddDomainEvent(new WorkPackageContextChanged(
            Id,
            previousCustomerId,
            CustomerId,
            previousProductId,
            ProductId,
            timestamp));
    }

    /// <summary>
    /// Reassigns ownership of the Work Package to a new Person (Architecture §11).
    /// Emits <see cref="WorkPackageOwnerChanged"/> when the owner actually changes.
    /// </summary>
    public void AssignOwner(Guid newOwnerPersonId, DateTime? updatedAtUtc = null)
    {
        if (Status == WorkPackageStatus.Closed)
        {
            throw new WorkPackageDomainException(
                $"Cannot reassign owner of closed Work Package '{Id}'.");
        }

        if (newOwnerPersonId == Guid.Empty)
        {
            throw new ArgumentException("New owner PersonId cannot be empty.", nameof(newOwnerPersonId));
        }

        if (newOwnerPersonId == OwnerPersonId)
        {
            return;
        }

        var previousOwner = OwnerPersonId;
        OwnerPersonId = newOwnerPersonId;
        var timestamp = updatedAtUtc ?? DateTime.UtcNow;
        UpdatedAt = timestamp;

        AddDomainEvent(new WorkPackageOwnerChanged(Id, previousOwner, newOwnerPersonId, timestamp));
    }

    /// <summary>
    /// Adds a Request to the Work Package, enforcing Business Rule 9 within this package.
    /// Allowed in DRAFT and ACTIVE states.
    /// </summary>
    /// <param name="requestId">Unique identifier of the request to associate.</param>
    /// <param name="addedAtUtc">Optional timestamp when the request was added.</param>
    public WorkPackageRequest AddRequest(
        Guid requestId,
        DateTime? addedAtUtc = null)
    {
        return AddRequestInternal(requestId, addedAtUtc, null);
    }

    /// <summary>
    /// Adds a Request to the Work Package with Business Rule 9 validation via checker interface.
    /// Allowed in DRAFT and ACTIVE states.
    /// </summary>
    /// <param name="requestId">Unique identifier of the request to associate.</param>
    /// <param name="activeWorkPackageChecker">Contract to check if request is in another active package.</param>
    /// <param name="addedAtUtc">Optional timestamp when the request was added.</param>
    public WorkPackageRequest AddRequest(
        Guid requestId,
        IActiveWorkPackageChecker activeWorkPackageChecker,
        DateTime? addedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(activeWorkPackageChecker);
        return AddRequestInternal(
            requestId,
            addedAtUtc,
            reqId => activeWorkPackageChecker.IsRequestInActiveWorkPackage(reqId, Id));
    }

    /// <summary>
    /// Adds a Request to the Work Package, enforcing Business Rule 9 via predicate:
    /// A request may belong to at most one active Work Package.
    /// Allowed in DRAFT and ACTIVE states.
    /// </summary>
    /// <param name="requestId">Unique identifier of the request to associate.</param>
    /// <param name="isAlreadyInActiveWorkPackage">Predicate to check whether the request is active in another Work Package.</param>
    /// <param name="addedAtUtc">Optional timestamp when the request was added.</param>
    public WorkPackageRequest AddRequest(
        Guid requestId,
        Func<Guid, bool> isAlreadyInActiveWorkPackage,
        DateTime? addedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(isAlreadyInActiveWorkPackage);
        return AddRequestInternal(requestId, addedAtUtc, isAlreadyInActiveWorkPackage);
    }

    private WorkPackageRequest AddRequestInternal(
        Guid requestId,
        DateTime? addedAtUtc,
        Func<Guid, bool>? isAlreadyInActiveWorkPackage)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("RequestId cannot be empty.", nameof(requestId));
        }

        if (Status == WorkPackageStatus.Closed)
        {
            throw new WorkPackageDomainException(
                $"Cannot add request '{requestId}' to closed Work Package '{Id}'.");
        }

        // Business Rule 9 invariant: request must not already be an active member of THIS package
        if (_requests.Any(r => r.RequestId == requestId && r.IsActive))
        {
            throw new BusinessRuleViolationException(
                9,
                $"Request '{requestId}' is already an active member of Work Package '{Id}'.");
        }

        // Business Rule 9 invariant: request must not be an active member of ANOTHER package
        if (isAlreadyInActiveWorkPackage is not null && isAlreadyInActiveWorkPackage(requestId))
        {
            throw new BusinessRuleViolationException(
                9,
                $"Request '{requestId}' already belongs to another active Work Package.");
        }

        var timestamp = addedAtUtc ?? DateTime.UtcNow;
        var nextSortOrder = _requests.Where(r => r.IsActive).Select(r => (int?)r.SortOrder).Max() ?? -1;
        var membership = new WorkPackageRequest(Guid.NewGuid(), Id, requestId, timestamp, nextSortOrder + 1);
        _requests.Add(membership);
        UpdatedAt = timestamp;

        AddDomainEvent(new RequestAddedToWorkPackage(Id, requestId, timestamp));
        return membership;
    }

    /// <summary>
    /// Reorders the active constituent requests of the Work Package according to the specified sequence of Request IDs.
    /// Allowed only in DRAFT and ACTIVE states (Architecture CR-015 §4 TD-003).
    /// </summary>
    /// <param name="orderedRequestIds">Ordered list of Request IDs representing the new sequence.</param>
    /// <param name="reorderedAtUtc">Optional timestamp when the reordering occurred.</param>
    public void ReorderRequests(IReadOnlyList<Guid> orderedRequestIds, DateTime? reorderedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(orderedRequestIds);

        if (Status == WorkPackageStatus.Closed)
        {
            throw new InvalidWorkPackageStateTransitionException(
                Id,
                Status,
                Status,
                $"Cannot reorder requests in a CLOSED work package '{Id}'.");
        }

        var activeList = _requests.Where(r => r.IsActive).ToList();

        if (orderedRequestIds.Count != activeList.Count)
        {
            throw new WorkPackageDomainException(
                $"Ordered request count ({orderedRequestIds.Count}) does not match active request count ({activeList.Count}) in Work Package '{Id}'.");
        }

        if (orderedRequestIds.Distinct().Count() != orderedRequestIds.Count)
        {
            throw new WorkPackageDomainException("Ordered request IDs must not contain duplicates.");
        }

        var activeByRequestId = activeList.ToDictionary(r => r.RequestId);

        for (var i = 0; i < orderedRequestIds.Count; i++)
        {
            var requestId = orderedRequestIds[i];
            if (!activeByRequestId.TryGetValue(requestId, out var membership))
            {
                throw new WorkPackageDomainException(
                    $"Request '{requestId}' is not an active member of Work Package '{Id}'.");
            }

            membership.SetSortOrder(i, reorderedAtUtc);
        }

        UpdatedAt = reorderedAtUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Removes a Request from the Work Package by deactivating its membership link (Architecture §11).
    /// Allowed in DRAFT and ACTIVE states.
    /// </summary>
    public void RemoveRequest(Guid requestId, DateTime? removedAtUtc = null)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("RequestId cannot be empty.", nameof(requestId));
        }

        if (Status == WorkPackageStatus.Closed)
        {
            throw new WorkPackageDomainException(
                $"Cannot remove request '{requestId}' from closed Work Package '{Id}'.");
        }

        var membership = _requests.FirstOrDefault(r => r.RequestId == requestId && r.IsActive);
        if (membership is null)
        {
            throw new WorkPackageDomainException(
                $"Request '{requestId}' is not an active member of Work Package '{Id}'.");
        }

        var timestamp = removedAtUtc ?? DateTime.UtcNow;
        membership.MarkRemoved(timestamp);
        UpdatedAt = timestamp;

        AddDomainEvent(new RequestRemovedFromWorkPackage(Id, requestId, timestamp));
    }

    /// <summary>
    /// Clears recorded domain events after dispatch.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    /// Records a domain event on the aggregate.
    /// </summary>
    private void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>
    /// Normalizes a non-null deadline date to UTC midnight (00:00:00Z) per Architecture CR-023 §4 TD-002.
    /// </summary>
    private static DateTime? NormalizeDeadline(DateTime? deadline)
    {
        if (!deadline.HasValue) return null;
        var d = deadline.Value;
        return new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>
    /// Rehydrates a WorkPackage aggregate from persistence storage without emitting domain events.
    /// </summary>
    public static WorkPackage Rehydrate(
        Guid id,
        string name,
        string objective,
        WorkPackageStatus status,
        Guid ownerPersonId,
        Guid? customerId,
        Guid? productId,
        string? closedReason,
        DateTime? closedAt,
        DateTime createdAt,
        DateTime? updatedAt,
        IEnumerable<WorkPackageRequest>? requests = null,
        DateTime? deadline = null)
    {
        var package = new WorkPackage
        {
            Id = id,
            Name = name,
            Objective = objective,
            Status = status,
            OwnerPersonId = ownerPersonId,
            CustomerId = customerId,
            ProductId = productId,
            ClosedReason = closedReason,
            ClosedAt = closedAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Deadline = NormalizeDeadline(deadline)
        };

        if (requests is not null)
        {
            package._requests.AddRange(requests);
        }

        return package;
    }
}
