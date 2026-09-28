using MediatR;

namespace Cakra.Modules.Request.Services;

/// <summary>
/// MediatR query to retrieve a Request by its unique identifier, including resolution and assignment history
/// (Architecture §7, §8).
/// </summary>
public sealed record GetRequestByIdQuery(Guid RequestId) : IRequest<RequestDto?>;

/// <summary>
/// MediatR query to retrieve the chronological state transition and ownership audit history for a Request
/// (Architecture §7, §8 — UC-COL-002, UC-COL-003).
/// </summary>
public sealed record GetRequestStateHistoryQuery(Guid RequestId) : IRequest<IReadOnlyList<RequestAssignmentDto>>;

/// <summary>
/// MediatR query to retrieve all requests assigned to the current authenticated person
/// (from <c>ICurrentContextProvider.CurrentPersonId</c>) or an explicit <paramref name="PersonId"/>
/// (Architecture §7, §8 — UC-COL-004).
/// </summary>
public sealed record ListMyAssignedRequestsQuery(Guid? PersonId = null) : IRequest<IReadOnlyList<RequestDto>>;

/// <summary>
/// MediatR query to retrieve a filtered and paginated request grid
/// (Architecture §7, §8 — UC-COL-002..004).
/// </summary>
public sealed record GetFilteredRequestGridQuery(
    string? Status = null,
    Guid? AssigneePersonId = null,
    Guid? CustomerId = null,
    Guid? ProductId = null,
    int Page = 1,
    int PageSize = 20,
    int? Offset = null,
    Guid? WorkPackageId = null,
    string? SearchTerm = null) : IRequest<PagedRequestGridResult>
{
    public RequestGridFilter ToFilter() => new()
    {
        Status = Status,
        AssigneePersonId = AssigneePersonId,
        CustomerId = CustomerId,
        ProductId = ProductId,
        WorkPackageId = WorkPackageId,
        SearchTerm = SearchTerm,
        Page = Page,
        PageSize = PageSize,
        Offset = Offset
    };
}
