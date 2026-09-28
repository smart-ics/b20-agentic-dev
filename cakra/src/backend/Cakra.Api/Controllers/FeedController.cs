using System.Diagnostics;
using Cakra.Modules.Post;
using Cakra.Modules.Post.Domain;
using Cakra.Modules.Post.Domain.Exceptions;
using Cakra.Modules.Post.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Cakra.Api.Controllers;

/// <summary>
/// REST API controller for the Operational Feed module (<c>SCR-FEED-001</c>,
/// <c>UC-FCOL-004</c>, <c>UC-FCOL-005</c>, <c>UC-AWR-001..003</c>, <c>FEAT-FCOL-005</c>, <c>FEAT-AWR-001</c>;
/// Architecture §6, §7, §8, §9, §12, §19.5, §19.6, §20).
/// Exposes <c>/api/v1/feed/*</c> endpoints protected by <see cref="AuthorizeAttribute"/>,
/// wiring <see cref="FeedQueryService"/> (<see cref="IFeedQueryService"/>) to HTTP endpoints.
/// </summary>
[Authorize]
[Route("api/v1/feed")]
public sealed class FeedController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFeedQueryService _feedQueryService;
    private readonly ILogger<FeedController> _logger;

    public FeedController(
        IMediator mediator,
        IFeedQueryService feedQueryService,
        ILogger<FeedController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _feedQueryService = feedQueryService ?? throw new ArgumentNullException(nameof(feedQueryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves a filtered and paginated operational feed stream from the <c>post.FeedItems</c>
    /// materialized read model (<c>FeedQueryService.GetFeed</c>; Architecture §7, §8, §9, §12, §20 —
    /// <c>SCR-FEED-001</c>, <c>UC-FCOL-005</c>, <c>UC-AWR-001..003</c>).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(FeedPageResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFeed(
        [FromQuery] Guid? customerId = null,
        [FromQuery] Guid? customer = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] Guid? product = null,
        [FromQuery] bool? isException = null,
        [FromQuery] bool? exceptionsOnly = null,
        [FromQuery] bool? exceptionOnly = null,
        [FromQuery] string? exceptionType = null,
        [FromQuery] int pageSize = FeedPagination.DefaultPageSize,
        [FromQuery] int? offset = null,
        [FromQuery] int? page = null,
        [FromQuery] Guid? requestId = null,
        [FromQuery] Guid? request = null,
        [FromQuery] Guid? workPackageId = null,
        [FromQuery] Guid? workPackage = null,
        [FromQuery] Guid? authorPersonId = null,
        [FromQuery] Guid? authorId = null,
        [FromQuery] Guid? author = null,
        [FromQuery] string? referenceType = null,
        [FromQuery] Guid? referenceId = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? search = null,
        [FromQuery] string? q = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedCustomerId = FirstNonEmptyGuid(customerId, customer);
        var resolvedProductId = FirstNonEmptyGuid(productId, product);
        var resolvedRequestId = FirstNonEmptyGuid(requestId, request);
        var resolvedWorkPackageId = FirstNonEmptyGuid(workPackageId, workPackage);
        var resolvedAuthorPersonId = FirstNonEmptyGuid(authorPersonId, authorId, author);
        var resolvedSearchTerm = FirstNonWhiteSpace(searchTerm, search, q);
        var resolvedExceptionType = FirstNonWhiteSpace(exceptionType);

        if (resolvedExceptionType is not null &&
            !PostExceptionTypes.All.Contains(resolvedExceptionType, StringComparer.OrdinalIgnoreCase))
        {
            return CreateBadRequestProblem(
                $"ExceptionType must be one of: {string.Join(", ", PostExceptionTypes.All)}.");
        }

        bool? resolvedIsException = null;
        if (isException == true || exceptionsOnly == true || exceptionOnly == true || resolvedExceptionType is not null)
        {
            resolvedIsException = true;
        }
        else if (isException == false || exceptionsOnly == false || exceptionOnly == false)
        {
            resolvedIsException = false;
        }

        var effectivePageSize = pageSize <= 0 ? FeedPagination.DefaultPageSize : Math.Min(pageSize, FeedPagination.MaxPageSize);
        var effectivePage = page.HasValue && page.Value >= 1 ? page.Value : (int?)null;
        var effectiveOffset = offset.HasValue
            ? Math.Max(0, offset.Value)
            : (effectivePage.HasValue ? (effectivePage.Value - 1) * effectivePageSize : 0);

        var filter = new FeedFilter
        {
            CustomerId = resolvedCustomerId,
            ProductId = resolvedProductId,
            IsException = resolvedIsException,
            ExceptionType = resolvedExceptionType,
            RequestId = resolvedRequestId,
            WorkPackageId = resolvedWorkPackageId,
            AuthorPersonId = resolvedAuthorPersonId,
            ReferenceType = FirstNonWhiteSpace(referenceType),
            ReferenceId = FirstNonEmptyGuid(referenceId),
            SearchTerm = resolvedSearchTerm,
            Page = effectivePage ?? ((effectiveOffset / effectivePageSize) + 1),
            PageSize = effectivePageSize,
            Offset = effectiveOffset
        };

        var pagination = new FeedPagination(
            pageSize: effectivePageSize,
            offset: effectiveOffset,
            page: effectivePage);

        var query = new GetFeedQuery(
            CustomerId: resolvedCustomerId,
            ProductId: resolvedProductId,
            IsException: resolvedIsException,
            PageSize: effectivePageSize,
            Offset: effectiveOffset,
            Page: effectivePage,
            ExceptionType: resolvedExceptionType,
            RequestId: resolvedRequestId,
            WorkPackageId: resolvedWorkPackageId,
            AuthorPersonId: resolvedAuthorPersonId,
            SearchTerm: resolvedSearchTerm,
            Filter: filter,
            Pagination: pagination);

        try
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or PostDomainException or ArgumentException)
        {
            _logger.LogWarning(ex, "Invalid operational feed query parameters.");
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Retrieves a single projected <see cref="FeedItemDto"/> by its <c>FeedItemId</c> or associated <c>PostId</c>.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FeedItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FeedItemDto>> GetFeedItemById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await _mediator.Send(new GetFeedItemByIdQuery(id), cancellationToken)
            ?? await _feedQueryService.GetFeedItemByPostIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"FeedItem '{id}' was not found.");

        return Ok(item);
    }

    /// <summary>
    /// Retrieves a single projected <see cref="FeedItemDto"/> by its associated <c>PostId</c>.
    /// </summary>
    [HttpGet("by-post/{postId:guid}")]
    [ProducesResponseType(typeof(FeedItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FeedItemDto>> GetFeedItemByPostId(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var item = await _mediator.Send(new GetFeedItemByPostIdQuery(postId), cancellationToken)
            ?? throw new KeyNotFoundException($"FeedItem for Post '{postId}' was not found.");

        return Ok(item);
    }

    private ObjectResult CreateBadRequestProblem(string detail)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        };
        problem.Extensions["errorCode"] = "BAD_REQUEST";
        problem.Extensions["traceId"] = traceId;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static Guid? FirstNonEmptyGuid(params Guid?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.HasValue && candidate.Value != Guid.Empty)
            {
                return candidate.Value;
            }
        }

        return null;
    }

    private static string? FirstNonWhiteSpace(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate.Trim();
            }
        }

        return null;
    }
}
