namespace ICS.Modules.Request.Application.Queries;

using FluentValidation;
using ICS.Modules.Customer;
using ICS.Modules.Organization;
using ICS.Modules.Product;
using ICS.Modules.Request.Application.Commands;
using ICS.Modules.Request.Application.DTOs;
using ICS.Modules.Request.Domain;
using ICS.Modules.Request.Persistence;
using MediatR;

#region GetRequestById

public sealed record GetRequestByIdQuery(Guid RequestId) : IRequest<RequestDto?>;

public sealed class GetRequestByIdQueryValidator : AbstractValidator<GetRequestByIdQuery>
{
    public GetRequestByIdQueryValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("RequestId is required.");
    }
}

internal sealed class GetRequestByIdQueryHandler : IRequestHandler<GetRequestByIdQuery, RequestDto?>
{
    private readonly IRequestRepository _requestRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;

    public GetRequestByIdQueryHandler(
        IRequestRepository requestRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService)
    {
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
    }

    public async Task<RequestDto?> Handle(GetRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var aggregate = await _requestRepository.GetByIdAsync(request.RequestId, cancellationToken);
        if (aggregate == null)
        {
            return null;
        }

        var histories = await _requestRepository.GetStateHistoryAsync(aggregate.Id, cancellationToken);

        return await RequestDtoMapper.ToDtoAsync(
            aggregate,
            _organizationQueryService,
            _customerQueryService,
            _productQueryService,
            histories,
            cancellationToken);
    }
}

#endregion

#region GetRequestStateHistory

public sealed record GetRequestStateHistoryQuery(Guid RequestId) : IRequest<IReadOnlyList<RequestStateHistoryDto>>;

public sealed class GetRequestStateHistoryQueryValidator : AbstractValidator<GetRequestStateHistoryQuery>
{
    public GetRequestStateHistoryQueryValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("RequestId is required.");
    }
}

internal sealed class GetRequestStateHistoryQueryHandler : IRequestHandler<GetRequestStateHistoryQuery, IReadOnlyList<RequestStateHistoryDto>>
{
    private readonly IRequestRepository _requestRepository;
    private readonly IOrganizationQueryService _organizationQueryService;

    public GetRequestStateHistoryQueryHandler(
        IRequestRepository requestRepository,
        IOrganizationQueryService organizationQueryService)
    {
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
    }

    public async Task<IReadOnlyList<RequestStateHistoryDto>> Handle(GetRequestStateHistoryQuery request, CancellationToken cancellationToken)
    {
        var rawHistories = await _requestRepository.GetStateHistoryAsync(request.RequestId, cancellationToken);

        var enrichedList = new List<RequestStateHistoryDto>();
        foreach (var h in rawHistories)
        {
            string? actorName = null;
            if (h.ActorPersonId != Guid.Empty)
            {
                var actor = await _organizationQueryService.GetPersonByIdAsync(h.ActorPersonId, cancellationToken);
                actorName = actor?.Name;
            }

            enrichedList.Add(h with { ActorName = actorName });
        }

        return enrichedList;
    }
}

#endregion

#region ListMyAssignedRequests (UC-COL-004, SCR-REQ-005)

public sealed record ListMyAssignedRequestsQuery(
    Guid PersonId,
    bool IncludeClosed = false) : IRequest<MyAssignedRequestsResponseDto>;

public sealed class ListMyAssignedRequestsQueryValidator : AbstractValidator<ListMyAssignedRequestsQuery>
{
    public ListMyAssignedRequestsQueryValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty().WithMessage("PersonId is required.");
    }
}

internal sealed class ListMyAssignedRequestsQueryHandler : IRequestHandler<ListMyAssignedRequestsQuery, MyAssignedRequestsResponseDto>
{
    private readonly IRequestRepository _requestRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;

    public ListMyAssignedRequestsQueryHandler(
        IRequestRepository requestRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService)
    {
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
    }

    public async Task<MyAssignedRequestsResponseDto> Handle(ListMyAssignedRequestsQuery request, CancellationToken cancellationToken)
    {
        var aggregates = await _requestRepository.ListMyAssignedAsync(request.PersonId, request.IncludeClosed, cancellationToken);

        var summaryItems = new List<RequestSummaryDto>();
        foreach (var req in aggregates)
        {
            string? ownerName = null;
            if (req.OwnerPersonId.HasValue && req.OwnerPersonId.Value != Guid.Empty)
            {
                var owner = await _organizationQueryService.GetPersonByIdAsync(req.OwnerPersonId.Value, cancellationToken);
                ownerName = owner?.Name;
            }

            string? customerName = null;
            if (req.CustomerId.HasValue && req.CustomerId.Value != Guid.Empty)
            {
                var customer = await _customerQueryService.GetCustomerByIdAsync(req.CustomerId.Value, cancellationToken);
                customerName = customer?.CustomerName;
            }

            string? productName = null;
            if (req.ProductId.HasValue && req.ProductId.Value != Guid.Empty)
            {
                var product = await _productQueryService.GetProductByIdAsync(req.ProductId.Value, cancellationToken);
                productName = product?.Name;
            }

            summaryItems.Add(new RequestSummaryDto(
                req.Id,
                req.Title,
                req.Description,
                req.Type,
                req.Priority,
                req.Status,
                req.RequesterPersonId,
                req.RequesterName,
                req.OwnerPersonId,
                ownerName,
                req.CustomerId,
                customerName,
                req.ProductId,
                productName,
                req.WorkPackageId,
                req.IsAwaitingManagementDecision,
                req.EscalatedAt,
                req.CreatedAt,
                req.UpdatedAt,
                req.ClosedAt,
                req.Resolution?.Outcome));
        }

        var activeCount = summaryItems.Count(i => !RequestStatus.IsTerminal(i.Status));
        var awaitingDecisionCount = summaryItems.Count(i => i.IsAwaitingManagementDecision && !RequestStatus.IsTerminal(i.Status));
        var escalatedCount = summaryItems.Count(i => string.Equals(i.Status, RequestStatus.Escalated, StringComparison.OrdinalIgnoreCase));

        return new MyAssignedRequestsResponseDto(
            activeCount,
            awaitingDecisionCount,
            escalatedCount,
            summaryItems);
    }
}

#endregion

#region GetFilteredRequestGrid (UC-COL-002, SCR-REQ-004)

public sealed record GetFilteredRequestGridQuery(RequestGridFilterDto Filter) : IRequest<RequestGridResultDto>;

public sealed class GetFilteredRequestGridQueryValidator : AbstractValidator<GetFilteredRequestGridQuery>
{
    public GetFilteredRequestGridQueryValidator()
    {
        RuleFor(x => x.Filter).NotNull().WithMessage("Filter is required.");
        RuleFor(x => x.Filter.Take).GreaterThan(0).LessThanOrEqualTo(500).WithMessage("Take must be between 1 and 500.");
        RuleFor(x => x.Filter.Skip).GreaterThanOrEqualTo(0).WithMessage("Skip cannot be negative.");
    }
}

internal sealed class GetFilteredRequestGridQueryHandler : IRequestHandler<GetFilteredRequestGridQuery, RequestGridResultDto>
{
    private readonly IRequestRepository _requestRepository;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;

    public GetFilteredRequestGridQueryHandler(
        IRequestRepository requestRepository,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService)
    {
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
    }

    public async Task<RequestGridResultDto> Handle(GetFilteredRequestGridQuery request, CancellationToken cancellationToken)
    {
        var (totalCount, items) = await _requestRepository.GetFilteredGridAsync(request.Filter, cancellationToken);

        var summaryItems = new List<RequestSummaryDto>();
        foreach (var req in items)
        {
            string? ownerName = null;
            if (req.OwnerPersonId.HasValue && req.OwnerPersonId.Value != Guid.Empty)
            {
                var owner = await _organizationQueryService.GetPersonByIdAsync(req.OwnerPersonId.Value, cancellationToken);
                ownerName = owner?.Name;
            }

            string? customerName = null;
            if (req.CustomerId.HasValue && req.CustomerId.Value != Guid.Empty)
            {
                var customer = await _customerQueryService.GetCustomerByIdAsync(req.CustomerId.Value, cancellationToken);
                customerName = customer?.CustomerName;
            }

            string? productName = null;
            if (req.ProductId.HasValue && req.ProductId.Value != Guid.Empty)
            {
                var product = await _productQueryService.GetProductByIdAsync(req.ProductId.Value, cancellationToken);
                productName = product?.Name;
            }

            summaryItems.Add(new RequestSummaryDto(
                req.Id,
                req.Title,
                req.Description,
                req.Type,
                req.Priority,
                req.Status,
                req.RequesterPersonId,
                req.RequesterName,
                req.OwnerPersonId,
                ownerName,
                req.CustomerId,
                customerName,
                req.ProductId,
                productName,
                req.WorkPackageId,
                req.IsAwaitingManagementDecision,
                req.EscalatedAt,
                req.CreatedAt,
                req.UpdatedAt,
                req.ClosedAt,
                req.Resolution?.Outcome));
        }

        return new RequestGridResultDto(totalCount, summaryItems);
    }
}

#endregion
