namespace ICS.Modules.Request;

using ICS.Modules.Request.Application.DTOs;
using ICS.Modules.Request.Application.Queries;
using MediatR;

/// <summary>
/// Concrete implementation of <see cref="IRequestQueryService"/> published across module boundaries.
/// Architecture §7, §8, §15, §16, §19.2, §19.3, §20.
/// </summary>
public class RequestQueryService : IRequestQueryService
{
    private readonly ISender _sender;

    public RequestQueryService(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public Task<RequestDto?> GetRequestByIdAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new GetRequestByIdQuery(requestId), cancellationToken);
    }

    public Task<IReadOnlyList<RequestStateHistoryDto>> GetRequestStateHistoryAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new GetRequestStateHistoryQuery(requestId), cancellationToken);
    }

    public Task<MyAssignedRequestsResponseDto> ListMyAssignedRequestsAsync(Guid personId, bool includeClosed = false, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new ListMyAssignedRequestsQuery(personId, includeClosed), cancellationToken);
    }

    public Task<RequestGridResultDto> GetFilteredRequestGridAsync(RequestGridFilterDto filter, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new GetFilteredRequestGridQuery(filter), cancellationToken);
    }

    public RequestDto? GetRequestById(Guid requestId)
    {
        return GetRequestByIdAsync(requestId).GetAwaiter().GetResult();
    }

    public IReadOnlyList<RequestStateHistoryDto> GetRequestStateHistory(Guid requestId)
    {
        return GetRequestStateHistoryAsync(requestId).GetAwaiter().GetResult();
    }

    public MyAssignedRequestsResponseDto ListMyAssignedRequests(Guid personId, bool includeClosed = false)
    {
        return ListMyAssignedRequestsAsync(personId, includeClosed).GetAwaiter().GetResult();
    }

    public RequestGridResultDto GetFilteredRequestGrid(RequestGridFilterDto filter)
    {
        return GetFilteredRequestGridAsync(filter).GetAwaiter().GetResult();
    }
}
