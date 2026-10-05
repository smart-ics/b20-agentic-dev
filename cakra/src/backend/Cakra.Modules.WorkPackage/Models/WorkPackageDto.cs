using Cakra.Modules.Request;
using Cakra.Modules.WorkPackage.Domain;

namespace Cakra.Modules.WorkPackage;

/// <summary>
/// Read-only data transfer object representing a <see cref="Domain.WorkPackage"/> aggregate root
/// (Architecture §7, §11, §15).
/// </summary>
public record WorkPackageDto
{
    private int? _activeRequestCount;
    private int? _totalRequestCount;

    /// <summary>Unique identifier of the Work Package.</summary>
    public Guid Id { get; init; }

    /// <summary>Unique identifier of the Work Package (synonym for <see cref="Id"/>).</summary>
    public Guid WorkPackageId
    {
        get => Id;
        init => Id = value;
    }

    /// <summary>Descriptive name/title of the Work Package.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The intended result that gives the Work Package its purpose.</summary>
    public string Objective { get; init; } = string.Empty;

    /// <summary>Current lifecycle state string (<c>DRAFT</c>, <c>ACTIVE</c>, <c>CLOSED</c>).</summary>
    public string Status { get; init; } = WorkPackageStatusNames.Draft;

    /// <summary>Identifier of the organizational <c>Person</c> accountable for the Work Package.</summary>
    public Guid OwnerPersonId { get; init; }

    /// <summary>Resolved display name of the Work Package owner, when enriched via <c>IOrganizationQueryService</c>.</summary>
    public string? OwnerName { get; init; }

    /// <summary>Optional <c>CustomerId</c> associated with this Work Package.</summary>
    public Guid? CustomerId { get; init; }

    /// <summary>Resolved customer name, when enriched via <c>ICustomerQueryService</c>.</summary>
    public string? CustomerName { get; init; }

    /// <summary>Resolved customer code, when enriched via <c>ICustomerQueryService</c>.</summary>
    public string? CustomerCode { get; init; }

    /// <summary>Optional <c>ProductId</c> associated with this Work Package.</summary>
    public Guid? ProductId { get; init; }

    /// <summary>Resolved product name, when enriched via <c>IProductQueryService</c>.</summary>
    public string? ProductName { get; init; }

    /// <summary>Resolved product code, when enriched via <c>IProductQueryService</c>.</summary>
    public string? ProductCode { get; init; }

    /// <summary>Reason recorded when the Work Package is closed, or <c>null</c> if not closed.</summary>
    public string? ClosedReason { get; init; }

    /// <summary>UTC timestamp when the Work Package was closed, or <c>null</c> if not closed.</summary>
    public DateTime? ClosedAt { get; init; }

    /// <summary>UTC timestamp when the Work Package was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>UTC timestamp when the Work Package was last updated.</summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>All constituent request memberships (current and historical).</summary>
    public IReadOnlyList<WorkPackageScopeItemDto> Requests { get; init; } = Array.Empty<WorkPackageScopeItemDto>();

    /// <summary>Active constituent request memberships currently in scope.</summary>
    public IReadOnlyList<WorkPackageScopeItemDto> ActiveRequests =>
        Requests.Where(r => r.IsActive).OrderBy(r => r.SortOrder).ThenBy(r => r.AddedAt).ToList();

    /// <summary>Count of active requests in this Work Package.</summary>
    public int ActiveRequestCount
    {
        get => _activeRequestCount ?? Requests.Count(r => r.IsActive);
        init => _activeRequestCount = value;
    }

    /// <summary>Total count of current and historical request memberships in this Work Package.</summary>
    public int TotalRequestCount
    {
        get => _totalRequestCount ?? Requests.Count;
        init => _totalRequestCount = value;
    }

    internal static WorkPackageDto FromDomain(Domain.WorkPackage workPackage)
    {
        ArgumentNullException.ThrowIfNull(workPackage);

        return new WorkPackageDto
        {
            Id = workPackage.Id,
            Name = workPackage.Name,
            Objective = workPackage.Objective,
            Status = workPackage.Status.ToName(),
            OwnerPersonId = workPackage.OwnerPersonId,
            CustomerId = workPackage.CustomerId,
            ProductId = workPackage.ProductId,
            ClosedReason = workPackage.ClosedReason,
            ClosedAt = workPackage.ClosedAt,
            CreatedAt = workPackage.CreatedAt,
            UpdatedAt = workPackage.UpdatedAt,
            Requests = workPackage.Requests.OrderBy(r => r.SortOrder).ThenBy(r => r.AddedAt).Select(WorkPackageScopeItemDto.FromDomain).ToList()
        };
    }
}

/// <summary>
/// Read-only data transfer object representing a <see cref="WorkPackageRequest"/> membership link,
/// optionally enriched with authoritative Request details from <see cref="IRequestQueryService"/>
/// (Architecture §7, §11, §15).
/// </summary>
public record WorkPackageRequestDto
{
    /// <summary>Unique identifier of the membership record.</summary>
    public Guid Id { get; init; }

    /// <summary>Synonym for <see cref="Id"/>.</summary>
    public Guid MembershipId
    {
        get => Id;
        init => Id = value;
    }

    /// <summary>Identifier of the enclosing Work Package.</summary>
    public Guid WorkPackageId { get; init; }

    /// <summary>Identifier of the constituent Request.</summary>
    public Guid RequestId { get; init; }

    /// <summary>Explicit sequence index of the Request within the Work Package scope.</summary>
    public int SortOrder { get; init; }

    /// <summary>UTC timestamp when the Request was added to the Work Package.</summary>
    public DateTime AddedAt { get; init; }

    /// <summary>UTC timestamp when the Request was removed from the Work Package, or <c>null</c> if active.</summary>
    public DateTime? RemovedAt { get; init; }

    /// <summary>Indicates whether this membership link is currently active (<c>RemovedAt == null</c>).</summary>
    public bool IsActive => RemovedAt is null;

    /// <summary>UTC timestamp when the membership row was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>UTC timestamp when the membership row was last updated.</summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>Enriched Request title from <see cref="IRequestQueryService"/>.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Convenience alias for <see cref="Title"/>.</summary>
    public string RequestTitle
    {
        get => Title;
        init => Title = value;
    }

    /// <summary>Enriched Request description from <see cref="IRequestQueryService"/>.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Enriched Request type from <see cref="IRequestQueryService"/>.</summary>
    public string RequestType { get; init; } = string.Empty;

    /// <summary>Enriched authoritative Request lifecycle status from <see cref="IRequestQueryService"/>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Convenience alias for <see cref="Status"/>.</summary>
    public string RequestStatus
    {
        get => Status;
        init => Status = value;
    }

    /// <summary>Enriched Request priority from <see cref="IRequestQueryService"/>.</summary>
    public string Priority { get; init; } = string.Empty;

    /// <summary>Enriched Request Owner <c>PersonId</c> from <see cref="IRequestQueryService"/>.</summary>
    public Guid? OwnerPersonId { get; init; }

    /// <summary>Convenience alias for <see cref="OwnerPersonId"/>.</summary>
    public Guid? RequestOwnerPersonId
    {
        get => OwnerPersonId;
        init => OwnerPersonId = value;
    }

    /// <summary>Enriched Request Owner display name from <see cref="IRequestQueryService"/>.</summary>
    public string? OwnerName { get; init; }

    /// <summary>Convenience alias for <see cref="OwnerName"/>.</summary>
    public string? RequestOwnerName
    {
        get => OwnerName;
        init => OwnerName = value;
    }

    /// <summary>Enriched Request <c>CustomerId</c> from <see cref="IRequestQueryService"/>.</summary>
    public Guid? CustomerId { get; init; }

    /// <summary>Enriched Request customer name from <see cref="IRequestQueryService"/>.</summary>
    public string? CustomerName { get; init; }

    /// <summary>Enriched Request <c>ProductId</c> from <see cref="IRequestQueryService"/>.</summary>
    public Guid? ProductId { get; init; }

    /// <summary>Enriched Request product name from <see cref="IRequestQueryService"/>.</summary>
    public string? ProductName { get; init; }

    /// <summary>Full enriched <see cref="RequestDto"/> record from <see cref="IRequestQueryService"/>, when available.</summary>
    public RequestDto? Request { get; init; }
}

/// <summary>
/// Enriched scope item returned by <see cref="IWorkPackageQueryService.GetWorkPackageScopeAsync(Guid, CancellationToken)"/>
/// representing a linked Request within a Work Package along with its authoritative Request details (Architecture §11).
/// </summary>
public sealed record WorkPackageScopeItemDto : WorkPackageRequestDto
{
    internal static WorkPackageScopeItemDto FromDomain(WorkPackageRequest membership)
    {
        ArgumentNullException.ThrowIfNull(membership);

        return new WorkPackageScopeItemDto
        {
            Id = membership.Id,
            WorkPackageId = membership.WorkPackageId,
            RequestId = membership.RequestId,
            SortOrder = membership.SortOrder,
            AddedAt = membership.AddedAt,
            RemovedAt = membership.RemovedAt,
            CreatedAt = membership.CreatedAt,
            UpdatedAt = membership.UpdatedAt
        };
    }

    internal WorkPackageScopeItemDto EnrichWith(RequestDto? request)
    {
        if (request is null)
        {
            return this;
        }

        return this with
        {
            Title = request.Title,
            Description = request.Description,
            RequestType = request.RequestType,
            Status = request.Status,
            Priority = request.Priority,
            OwnerPersonId = request.OwnerPersonId,
            OwnerName = request.OwnerName,
            CustomerId = request.CustomerId,
            CustomerName = request.CustomerName,
            ProductId = request.ProductId,
            ProductName = request.ProductName,
            Request = request
        };
    }
}
