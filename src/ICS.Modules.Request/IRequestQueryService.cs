namespace ICS.Modules.Request;

using ICS.Modules.Request.Application.DTOs;

/// <summary>
/// In-process query contract exposed by the Request module for cross-module integration and UI queries.
/// Architecture §7, §8, §15, §16, §20.
/// </summary>
public interface IRequestQueryService
{
    /// <summary>
    /// Gets a Request by identifier including owner, customer, product details, assignments, and resolution.
    /// </summary>
    Task<RequestDto?> GetRequestByIdAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the chronological state change audit history for a given Request (FEAT-COL-003).
    /// </summary>
    Task<IReadOnlyList<RequestStateHistoryDto>> GetRequestStateHistoryAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists active requests currently assigned to the specified Person, with workload counts (UC-COL-004, SCR-REQ-005).
    /// </summary>
    Task<MyAssignedRequestsResponseDto> ListMyAssignedRequestsAsync(Guid personId, bool includeClosed = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches and filters historical Request records with multi-attribute criteria and pagination (UC-COL-002, SCR-REQ-004).
    /// </summary>
    Task<RequestGridResultDto> GetFilteredRequestGridAsync(RequestGridFilterDto filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous convenience overload: gets a Request by identifier.
    /// </summary>
    RequestDto? GetRequestById(Guid requestId);

    /// <summary>
    /// Synchronous convenience overload: gets state change history for a Request.
    /// </summary>
    IReadOnlyList<RequestStateHistoryDto> GetRequestStateHistory(Guid requestId);

    /// <summary>
    /// Synchronous convenience overload: lists assigned requests for a Person.
    /// </summary>
    MyAssignedRequestsResponseDto ListMyAssignedRequests(Guid personId, bool includeClosed = false);

    /// <summary>
    /// Synchronous convenience overload: queries filtered request grid.
    /// </summary>
    RequestGridResultDto GetFilteredRequestGrid(RequestGridFilterDto filter);
}
