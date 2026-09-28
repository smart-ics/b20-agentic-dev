namespace ICS.Modules.WorkPackage;

using ICS.Modules.Customer;
using ICS.Modules.Organization;
using ICS.Modules.Product;
using ICS.Modules.WorkPackage.Application;
using ICS.Modules.WorkPackage.Domain;
using ICS.Modules.WorkPackage.Infrastructure;

/// <summary>
/// Dapper-backed query service for the Work Package module. Reads exclusively from workpackage.* schema
/// and resolves denormalized reference names through the owning modules' published query services.
/// Architecture §11, §15, §16, §19.3, §20.
/// </summary>
public sealed class WorkPackageQueryService : IWorkPackageQueryService
{
    private readonly IWorkPackageRepository _repository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;

    public WorkPackageQueryService(
        IWorkPackageRepository repository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
    }

    public async Task<WorkPackageDto?> GetWorkPackageByIdAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default)
    {
        if (workPackageId == Guid.Empty)
        {
            return null;
        }

        var workPackage = await _repository.GetByIdAsync(workPackageId, cancellationToken);
        return workPackage is null
            ? null
            : await ToDtoAsync(workPackage, cancellationToken);
    }

    public async Task<WorkPackageGridResultDto> ListWorkPackagesAsync(
        WorkPackageGridFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveFilter = filter ?? new WorkPackageGridFilterDto();
        var (totalCount, items) = await _repository.GetFilteredGridAsync(effectiveFilter, cancellationToken);

        var dtos = new List<WorkPackageDto>(items.Count);
        foreach (var workPackage in items)
        {
            dtos.Add(await ToDtoAsync(workPackage, cancellationToken));
        }

        return new WorkPackageGridResultDto(totalCount, dtos);
    }

    public async Task<WorkPackageScopeDto?> GetWorkPackageScopeAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default)
    {
        if (workPackageId == Guid.Empty)
        {
            return null;
        }

        var workPackage = await _repository.GetByIdAsync(workPackageId, cancellationToken);
        if (workPackage is null)
        {
            return null;
        }

        var ownerName = await ResolveOwnerNameAsync(workPackage.OwnerPersonId, cancellationToken);
        var (customerName, productName) = await ResolveCustomerAndProductNamesAsync(
            workPackage.CustomerId,
            workPackage.ProductId,
            cancellationToken);

        var allRequests = workPackage.Requests
            .Select(ToMembershipDto)
            .ToList();

        var activeRequests = workPackage.Requests
            .Where(r => r.IsActive)
            .Select(ToMembershipDto)
            .ToList();

        return new WorkPackageScopeDto(
            workPackage.Id,
            workPackage.Name,
            workPackage.Objective,
            workPackage.Status,
            workPackage.OwnerPersonId,
            ownerName,
            workPackage.CustomerId,
            customerName,
            workPackage.ProductId,
            productName,
            workPackage.CreatedAt,
            workPackage.UpdatedAt,
            workPackage.ClosedAt,
            workPackage.CloseReason,
            activeRequests,
            allRequests);
    }

    public async Task<WorkPackageRequestMembershipDto?> GetRequestWorkPackageAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var conflict = await _repository.GetActiveMembershipForRequestAsync(requestId, cancellationToken);
        return conflict is null ? null : ToMembershipDto(conflict.Value.Membership);
    }

    public async Task<IReadOnlyList<WorkPackageStateHistoryDto>> GetWorkPackageStateHistoryAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default)
    {
        var history = await _repository.GetStateHistoryAsync(workPackageId, cancellationToken);

        var enriched = new List<WorkPackageStateHistoryDto>(history.Count);
        foreach (var entry in history)
        {
            var actorName = await ResolveOwnerNameAsync(entry.ActorPersonId, cancellationToken);
            enriched.Add(entry with { ActorName = actorName });
        }

        return enriched;
    }

    public Task<bool> IsRequestInActiveWorkPackageAsync(
        Guid requestId,
        Guid? excludingWorkPackageId = null,
        CancellationToken cancellationToken = default) =>
        _repository.IsRequestInActiveWorkPackageAsync(requestId, excludingWorkPackageId, cancellationToken);

    private async Task<WorkPackageDto> ToDtoAsync(WorkPackage workPackage, CancellationToken cancellationToken)
    {
        var ownerName = await ResolveOwnerNameAsync(workPackage.OwnerPersonId, cancellationToken);
        var (customerName, productName) = await ResolveCustomerAndProductNamesAsync(
            workPackage.CustomerId,
            workPackage.ProductId,
            cancellationToken);

        var memberships = workPackage.Requests.Select(ToMembershipDto).ToList();

        return new WorkPackageDto(
            workPackage.Id,
            workPackage.Name,
            workPackage.Objective,
            workPackage.Status,
            workPackage.IsDraft,
            workPackage.IsActive,
            workPackage.IsClosed,
            workPackage.OwnerPersonId,
            ownerName,
            workPackage.CustomerId,
            customerName,
            workPackage.ProductId,
            productName,
            workPackage.CreatedAt,
            workPackage.UpdatedAt,
            workPackage.ClosedAt,
            workPackage.CloseReason,
            memberships.Count(m => m.IsActive),
            memberships);
    }

    private async Task<string?> ResolveOwnerNameAsync(Guid ownerPersonId, CancellationToken cancellationToken)
    {
        if (ownerPersonId == Guid.Empty)
        {
            return null;
        }

        var person = await _organizationQueryService.GetPersonByIdAsync(ownerPersonId, cancellationToken);
        return person?.Name;
    }

    private async Task<(string? CustomerName, string? ProductName)> ResolveCustomerAndProductNamesAsync(
        Guid? customerId,
        Guid? productId,
        CancellationToken cancellationToken)
    {
        string? customerName = null;
        if (customerId.HasValue && customerId.Value != Guid.Empty)
        {
            var customer = await _customerQueryService.GetCustomerByIdAsync(customerId.Value, cancellationToken);
            customerName = customer?.CustomerName;
        }

        string? productName = null;
        if (productId.HasValue && productId.Value != Guid.Empty)
        {
            var product = await _productQueryService.GetProductByIdAsync(productId.Value, cancellationToken);
            productName = product?.Name;
        }

        return (customerName, productName);
    }

    private static WorkPackageRequestMembershipDto ToMembershipDto(WorkPackageRequest membership) =>
        new(
            membership.Id,
            membership.WorkPackageId,
            membership.RequestId,
            membership.AddedAt,
            membership.RemovedAt,
            membership.IsActive);
}
