namespace ICS.Modules.Request.Persistence;

using ICS.Modules.Request.Application.DTOs;
using ICS.Modules.Request.Domain;

/// <summary>
/// Internal repository contract for Request aggregate persistence using explicit parameterized Dapper SQL.
/// Architecture §10, §16, §17, §19.3, §20.
/// </summary>
internal interface IRequestRepository
{
    Task<Request?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken = default);

    Task AddAsync(Request request, CancellationToken cancellationToken = default);

    Task UpdateAsync(Request request, CancellationToken cancellationToken = default);

    Task AddStateHistoryAsync(
        Guid requestId,
        string? fromStatus,
        string toStatus,
        Guid actorPersonId,
        string? reason,
        DateTime changedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RequestStateHistoryDto>> GetStateHistoryAsync(Guid requestId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Request>> ListMyAssignedAsync(Guid personId, bool includeClosed = false, CancellationToken cancellationToken = default);

    Task<(int TotalCount, IReadOnlyList<Request> Items)> GetFilteredGridAsync(RequestGridFilterDto filter, CancellationToken cancellationToken = default);
}
