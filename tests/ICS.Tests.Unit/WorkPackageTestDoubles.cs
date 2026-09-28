namespace ICS.Tests.Unit;

using ICS.Core.Domain;
using ICS.Core.Time;
using ICS.Modules.Customer;
using ICS.Modules.Customer.Application.DTOs;
using ICS.Modules.Organization;
using ICS.Modules.Organization.Application.DTOs;
using ICS.Modules.Product;
using ICS.Modules.Product.Application.DTOs;
using ICS.Modules.Request;
using ICS.Modules.Request.Application.DTOs;
using ICS.Modules.WorkPackage;
using ICS.Modules.WorkPackage.Application;
using ICS.Modules.WorkPackage.Domain;
using ICS.Modules.WorkPackage.Infrastructure;

/// <summary>
/// Deterministic test doubles for the Work Package application slice. The in-memory repository
/// enforces the same active-membership invariants as the Dapper implementation so Business Rule 9
/// behaviour can be verified without a live database.
/// Architecture §19.8.
/// </summary>
internal sealed class FakeWorkPackageRepository : IWorkPackageRepository
{
    private readonly Dictionary<Guid, WorkPackage> _workPackages = new();
    private readonly List<WorkPackage> _allWorkPackages = new();

    public List<WorkPackageStateHistoryRecord> StateHistory { get; } = new();

    public WorkPackage Seed(WorkPackage workPackage)
    {
        var hydrated = Hydrate(workPackage);
        _workPackages[workPackage.Id] = hydrated;
        _allWorkPackages.Add(hydrated);
        return hydrated;
    }

    /// <summary>
    /// Mirrors the production hydration path: a fresh aggregate is rebuilt through the public
    /// hydration constructor, which restores state without raising domain events. Real Dapper
    /// reads return a new instance per query, so the fake must not leak the pending events that
    /// <c>WorkPackage.Create</c> left on a freshly created aggregate.
    /// </summary>
    private static WorkPackage Hydrate(WorkPackage source) =>
        new(
            source.Id,
            source.Name,
            source.Objective,
            source.Status,
            source.OwnerPersonId,
            source.CustomerId,
            source.ProductId,
            source.CreatedAt,
            source.UpdatedAt,
            source.ClosedAt,
            source.CloseReason,
            source.Requests.Select(m => new WorkPackageRequest(
                m.Id, m.WorkPackageId, m.RequestId, m.AddedAt, m.RemovedAt, m.IsActive)));

    public Task<WorkPackage?> GetByIdAsync(Guid workPackageId, CancellationToken cancellationToken = default)
    {
        _workPackages.TryGetValue(workPackageId, out var workPackage);
        return Task.FromResult(workPackage is null ? null : Hydrate(workPackage));
    }

    public Task AddAsync(WorkPackage workPackage, CancellationToken cancellationToken = default)
    {
        var hydrated = Hydrate(workPackage);
        _workPackages[workPackage.Id] = hydrated;
        _allWorkPackages.Add(hydrated);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(WorkPackage workPackage, CancellationToken cancellationToken = default)
    {
        _workPackages[workPackage.Id] = Hydrate(workPackage);
        return Task.CompletedTask;
    }

    public Task<bool> IsRequestInActiveWorkPackageAsync(
        Guid requestId,
        Guid? excludingWorkPackageId = null,
        CancellationToken cancellationToken = default)
    {
        var result = _workPackages.Values
            .Where(wp => !excludingWorkPackageId.HasValue || wp.Id != excludingWorkPackageId.Value)
            .Any(wp => wp.IsActive && wp.HasActiveRequest(requestId));

        return Task.FromResult(result);
    }

    public Task<int> AddRequestMembershipAsync(
        WorkPackage workPackage,
        WorkPackageRequest membership,
        CancellationToken cancellationToken = default)
    {
        // Mirrors the production guard exactly: the SQL only refuses a second ACTIVE link for the
        // same (WorkPackageId, RequestId) pair. Cross-package uniqueness is the handler's decision,
        // so this guard must not second-guess it.
        if (_workPackages.TryGetValue(workPackage.Id, out var stored) &&
            stored.HasActiveRequest(membership.RequestId))
        {
            return Task.FromResult(0);
        }

        _workPackages[workPackage.Id] = Hydrate(workPackage);
        return Task.FromResult(1);
    }

    public Task<int> DeactivateRequestMembershipAsync(
        Guid workPackageId,
        Guid requestId,
        DateTime removedAt,
        CancellationToken cancellationToken = default)
    {
        if (!_workPackages.TryGetValue(workPackageId, out var stored))
        {
            return Task.FromResult(0);
        }

        // Mirrors the SQL UPDATE ... WHERE IsActive = 1: no active row means no rows affected.
        if (!stored.HasActiveRequest(requestId))
        {
            return Task.FromResult(0);
        }

        // The SQL statement targets the row by (WorkPackageId, RequestId, IsActive = 1), so the
        // deactivation is applied to the stored row itself rather than to the caller's working copy.
        stored.RemoveRequest(requestId, removedAt);
        stored.ClearDomainEvents();
        _workPackages[workPackageId] = Hydrate(stored);
        return Task.FromResult(1);
    }

    public Task AddStateHistoryAsync(
        Guid workPackageId,
        string? fromStatus,
        string toStatus,
        Guid actorPersonId,
        string? reason,
        DateTime changedAt,
        CancellationToken cancellationToken = default)
    {
        StateHistory.Add(new WorkPackageStateHistoryRecord(
            workPackageId,
            fromStatus,
            toStatus,
            actorPersonId,
            reason,
            changedAt));

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WorkPackage>> ListAsync(
        WorkPackageGridFilterDto filter,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkPackage>>(_allWorkPackages.ToList());

    public Task<(int TotalCount, IReadOnlyList<WorkPackage> Items)> GetFilteredGridAsync(
        WorkPackageGridFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var items = (IReadOnlyList<WorkPackage>)_allWorkPackages.ToList();
        return Task.FromResult((items.Count, items));
    }

    public Task<IReadOnlyList<WorkPackageRequest>> GetMembershipsAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkPackageRequest>>(
            _workPackages.TryGetValue(workPackageId, out var wp)
                ? wp.Requests.ToList()
                : new List<WorkPackageRequest>());

    public Task<IReadOnlyList<WorkPackageStateHistoryDto>> GetStateHistoryAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkPackageStateHistoryDto>>(
            StateHistory
                .Where(h => h.WorkPackageId == workPackageId)
                .Select(h => new WorkPackageStateHistoryDto(
                    Guid.NewGuid(),
                    h.WorkPackageId,
                    h.FromStatus,
                    h.ToStatus,
                    h.ActorPersonId,
                    null,
                    h.Reason,
                    h.ChangedAt))
                .ToList());

    public Task<(WorkPackageRequest Membership, string HolderStatus)?> GetActiveMembershipForRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        foreach (var workPackage in _workPackages.Values)
        {
            var membership = workPackage.ActiveRequests.FirstOrDefault(r => r.RequestId == requestId);
            if (membership is not null)
            {
                return Task.FromResult<(WorkPackageRequest, string)?>((membership, workPackage.Status));
            }
        }

        return Task.FromResult<(WorkPackageRequest, string)?>(null);
    }

    internal sealed record WorkPackageStateHistoryRecord(
        Guid WorkPackageId,
        string? FromStatus,
        string ToStatus,
        Guid ActorPersonId,
        string? Reason,
        DateTime ChangedAt);
}

/// <summary>
/// In-memory WorkPackageQueryService double that projects through the same DTO shape as the
/// production service, including Business Rule 9 lookups.
/// </summary>
internal sealed class FakeWorkPackageQueryService : IWorkPackageQueryService
{
    private readonly FakeWorkPackageRepository _repository;

    public FakeWorkPackageQueryService(FakeWorkPackageRepository repository)
    {
        _repository = repository;
    }

    public Task<WorkPackageDto?> GetWorkPackageByIdAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ToDto(_repository.GetByIdAsync(workPackageId, cancellationToken).Result));

    public Task<WorkPackageGridResultDto> ListWorkPackagesAsync(
        WorkPackageGridFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var (total, items) = _repository.GetFilteredGridAsync(filter ?? new WorkPackageGridFilterDto(), cancellationToken)
            .Result;

        var dtos = items.Select(ToDto).Where(d => d is not null).Select(d => d!).ToList();
        return Task.FromResult(new WorkPackageGridResultDto(total, dtos));
    }

    public Task<WorkPackageScopeDto?> GetWorkPackageScopeAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default)
    {
        var workPackage = _repository.GetByIdAsync(workPackageId, cancellationToken).Result;
        if (workPackage is null)
        {
            return Task.FromResult<WorkPackageScopeDto?>(null);
        }

        var all = workPackage.Requests
            .Select(m => new WorkPackageRequestMembershipDto(m.Id, m.WorkPackageId, m.RequestId, m.AddedAt, m.RemovedAt, m.IsActive))
            .ToList();

        var active = all.Where(m => m.IsActive).ToList();

        return Task.FromResult<WorkPackageScopeDto?>(new WorkPackageScopeDto(
            workPackage.Id,
            workPackage.Name,
            workPackage.Objective,
            workPackage.Status,
            workPackage.OwnerPersonId,
            null,
            workPackage.CustomerId,
            null,
            workPackage.ProductId,
            null,
            workPackage.CreatedAt,
            workPackage.UpdatedAt,
            workPackage.ClosedAt,
            workPackage.CloseReason,
            active,
            all));
    }

    public Task<WorkPackageRequestMembershipDto?> GetRequestWorkPackageAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var conflict = _repository.GetActiveMembershipForRequestAsync(requestId, cancellationToken).Result;
        return Task.FromResult(conflict is null
            ? null
            : new WorkPackageRequestMembershipDto(
                conflict.Value.Membership.Id,
                conflict.Value.Membership.WorkPackageId,
                conflict.Value.Membership.RequestId,
                conflict.Value.Membership.AddedAt,
                conflict.Value.Membership.RemovedAt,
                conflict.Value.Membership.IsActive));
    }

    public Task<IReadOnlyList<WorkPackageStateHistoryDto>> GetWorkPackageStateHistoryAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default) =>
        _repository.GetStateHistoryAsync(workPackageId, cancellationToken);

    public Task<bool> IsRequestInActiveWorkPackageAsync(
        Guid requestId,
        Guid? excludingWorkPackageId = null,
        CancellationToken cancellationToken = default) =>
        _repository.IsRequestInActiveWorkPackageAsync(requestId, excludingWorkPackageId, cancellationToken);

    private static WorkPackageDto? ToDto(WorkPackage? workPackage)
    {
        if (workPackage is null)
        {
            return null;
        }

        var memberships = workPackage.Requests
            .Select(m => new WorkPackageRequestMembershipDto(m.Id, m.WorkPackageId, m.RequestId, m.AddedAt, m.RemovedAt, m.IsActive))
            .ToList();

        return new WorkPackageDto(
            workPackage.Id,
            workPackage.Name,
            workPackage.Objective,
            workPackage.Status,
            workPackage.IsDraft,
            workPackage.IsActive,
            workPackage.IsClosed,
            workPackage.OwnerPersonId,
            null,
            workPackage.CustomerId,
            null,
            workPackage.ProductId,
            null,
            workPackage.CreatedAt,
            workPackage.UpdatedAt,
            workPackage.ClosedAt,
            workPackage.CloseReason,
            memberships.Count(m => m.IsActive),
            memberships);
    }
}

/// <summary>
/// Fixed clock for deterministic timestamp assertions.
/// </summary>
internal sealed class FixedClock : ISystemClock
{
    public FixedClock(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; }
    public DateTimeOffset OffsetUtcNow => new(UtcNow, TimeSpan.Zero);
}

/// <summary>
/// Captures every dispatched domain event for assertions.
/// </summary>
internal sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
{
    public List<IDomainEvent> Published { get; } = new();

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        Published.Add(domainEvent);
        return Task.CompletedTask;
    }

    public Task PublishAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        Published.AddRange(domainEvents);
        return Task.CompletedTask;
    }

    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default) =>
        PublishAsync(domainEvent, cancellationToken);

    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default) =>
        PublishAsync(domainEvents, cancellationToken);
}

/// <summary>
/// Organization query service double with configurable person existence and status.
/// Architecture §15 cross-module validation contract.
/// </summary>
internal sealed class StubOrganizationQueryService : IOrganizationQueryService
{
    private readonly Dictionary<Guid, PersonDto> _persons = new();

    public StubOrganizationQueryService AddActivePerson(Guid personId, string name = "Active Person") =>
        Add(personId, name, "ACTIVE");

    public StubOrganizationQueryService AddInactivePerson(Guid personId, string name = "Inactive Person") =>
        Add(personId, name, "INACTIVE");

    private StubOrganizationQueryService Add(Guid personId, string name, string status)
    {
        _persons[personId] = new PersonDto(
            personId,
            name,
            $"{personId.ToString()[..8]}@ics.local",
            status,
            DateTime.UtcNow,
            null);
        return this;
    }

    public Task<PersonDto?> GetPersonByIdAsync(Guid personId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_persons.TryGetValue(personId, out var person) ? person : null);

    public Task<IReadOnlyList<PersonDto>> ListActivePersonsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PersonDto>>(
            _persons.Values.Where(p => p.Status == "ACTIVE").ToList());

    public Task<IReadOnlyList<TeamMemberDto>> GetTeamRosterAsync(Guid teamId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TeamMemberDto>>(Array.Empty<TeamMemberDto>());

    public Task<IReadOnlyList<string>> GetPersonRolesAsync(Guid personId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task<IReadOnlyList<string>> GetPersonResponsibilitiesAsync(Guid personId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_persons.TryGetValue(personId, out var person) && person.Status == "ACTIVE");

    public Task<IReadOnlyList<string>> GetRolesByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default) =>
        GetPersonRolesAsync(personId, cancellationToken);

    public void RegisterBootstrapRoles(Guid personId, IEnumerable<string> roles)
    {
    }
}

/// <summary>
/// Customer query service double with configurable customer existence and status.
/// Architecture §15 cross-module validation contract.
/// </summary>
internal sealed class StubCustomerQueryService : ICustomerQueryService
{
    private readonly Dictionary<Guid, CustomerDto> _customers = new();

    public StubCustomerQueryService AddActiveCustomer(Guid customerId, string name = "Active Customer") =>
        Add(customerId, name, "ACTIVE");

    public StubCustomerQueryService AddInactiveCustomer(Guid customerId, string name = "Inactive Customer") =>
        Add(customerId, name, "INACTIVE");

    private StubCustomerQueryService Add(Guid customerId, string name, string status)
    {
        _customers[customerId] = new CustomerDto(
            customerId,
            $"CUST-{customerId.ToString()[..4]}",
            name,
            status,
            false,
            DateTime.UtcNow,
            null);
        return this;
    }

    public Task<CustomerDto?> GetCustomerByIdAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_customers.TryGetValue(customerId, out var customer) ? customer : null);

    public Task<IReadOnlyList<CustomerDto>> ListActiveCustomersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CustomerDto>>(
            _customers.Values.Where(c => c.Status == "ACTIVE").ToList());

    public Task<IReadOnlyList<CustomerContactDto>> GetCustomerContactsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CustomerContactDto>>(Array.Empty<CustomerContactDto>());

    public Task<CustomerWithContractStatusDto?> GetCustomerWithContractStatusAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<CustomerWithContractStatusDto?>(null);

    public CustomerDto? GetCustomerById(Guid customerId) => _customers.GetValueOrDefault(customerId);

    public IReadOnlyList<CustomerDto> ListActiveCustomers() =>
        _customers.Values.Where(c => c.Status == "ACTIVE").ToList();

    public IReadOnlyList<CustomerContactDto> GetCustomerContacts(Guid customerId) => Array.Empty<CustomerContactDto>();

    public CustomerWithContractStatusDto? GetCustomerWithContractStatus(Guid customerId) => null;
}

/// <summary>
/// Product query service double with configurable product existence and status.
/// Architecture §15 cross-module validation contract.
/// </summary>
internal sealed class StubProductQueryService : IProductQueryService
{
    private readonly Dictionary<Guid, ProductDto> _products = new();

    public StubProductQueryService AddActiveProduct(Guid productId, string name = "Active Product") =>
        Add(productId, name, "ACTIVE");

    public StubProductQueryService AddInactiveProduct(Guid productId, string name = "Inactive Product") =>
        Add(productId, name, "INACTIVE");

    private StubProductQueryService Add(Guid productId, string name, string status)
    {
        _products[productId] = new ProductDto(
            productId,
            $"PROD-{productId.ToString()[..4]}",
            name,
            null,
            Guid.NewGuid(),
            status,
            DateTime.UtcNow,
            null);
        return this;
    }

    public Task<ProductDto?> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_products.TryGetValue(productId, out var product) ? product : null);

    public Task<ProductDto?> GetProductByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(_products.Values.FirstOrDefault(p => p.Code == code));

    public Task<IReadOnlyList<ProductDto>> ListActiveProductsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProductDto>>(
            _products.Values.Where(p => p.Status == "ACTIVE").ToList());

    public Task<IReadOnlyList<ProductDto>> ListAllProductsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProductDto>>(_products.Values.ToList());

    public ProductDto? GetProductById(Guid productId) => _products.GetValueOrDefault(productId);

    public ProductDto? GetProductByCode(string code) => _products.Values.FirstOrDefault(p => p.Code == code);

    public IReadOnlyList<ProductDto> ListActiveProducts() =>
        _products.Values.Where(p => p.Status == "ACTIVE").ToList();

    public IReadOnlyList<ProductDto> ListAllProducts() => _products.Values.ToList();
}

/// <summary>
/// Request query service double controlling Request existence for cross-module validation.
/// Architecture §15 cross-module validation contract.
/// </summary>
internal sealed class StubRequestQueryService : IRequestQueryService
{
    private readonly HashSet<Guid> _existingRequests = new();

    public StubRequestQueryService AddRequest(Guid requestId)
    {
        _existingRequests.Add(requestId);
        return this;
    }

    public Task<RequestDto?> GetRequestByIdAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_existingRequests.Contains(requestId) ? BuildDto(requestId) : null);

    public Task<IReadOnlyList<RequestStateHistoryDto>> GetRequestStateHistoryAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RequestStateHistoryDto>>(Array.Empty<RequestStateHistoryDto>());

    public Task<MyAssignedRequestsResponseDto> ListMyAssignedRequestsAsync(Guid personId, bool includeClosed = false, CancellationToken cancellationToken = default) =>
        Task.FromResult(EmptyAssigned());

    public Task<RequestGridResultDto> GetFilteredRequestGridAsync(RequestGridFilterDto filter, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RequestGridResultDto(0, Array.Empty<RequestSummaryDto>()));

    public RequestDto? GetRequestById(Guid requestId) =>
        _existingRequests.Contains(requestId) ? BuildDto(requestId) : null;

    public IReadOnlyList<RequestStateHistoryDto> GetRequestStateHistory(Guid requestId) =>
        Array.Empty<RequestStateHistoryDto>();

    public MyAssignedRequestsResponseDto ListMyAssignedRequests(Guid personId, bool includeClosed = false) =>
        EmptyAssigned();

    public RequestGridResultDto GetFilteredRequestGrid(RequestGridFilterDto filter) =>
        new(0, Array.Empty<RequestSummaryDto>());

    private static MyAssignedRequestsResponseDto EmptyAssigned() =>
        new(0, 0, 0, Array.Empty<RequestSummaryDto>());

    private static RequestDto BuildDto(Guid requestId)
    {
        var now = DateTime.UtcNow;

        return new RequestDto(
            RequestId: requestId,
            Title: "Integration Request",
            Description: "Request created for Work Package membership tests",
            Type: "INCIDENT",
            Priority: "MEDIUM",
            Status: "NEW",
            IsTerminal: false,
            IsActive: true,
            RequesterPersonId: null,
            RequesterContactId: null,
            RequesterName: null,
            OwnerPersonId: null,
            OwnerName: null,
            CustomerId: null,
            CustomerName: null,
            CustomerCode: null,
            ProductId: null,
            ProductName: null,
            ProductCode: null,
            WorkPackageId: null,
            IsAwaitingManagementDecision: false,
            ManagementDecisionQuestion: null,
            ManagementDecisionOptions: null,
            ManagementDecisionImpact: null,
            ManagementDecisionRequestedAt: null,
            EscalationReason: null,
            EscalatedByPersonId: null,
            EscalatedByName: null,
            EscalatedAt: null,
            CreatedAt: now,
            UpdatedAt: null,
            ClosedAt: null);
    }
}
