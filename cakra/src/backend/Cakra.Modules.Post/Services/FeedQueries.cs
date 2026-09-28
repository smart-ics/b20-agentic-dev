using Cakra.Modules.Post.Domain;
using FluentValidation;
using MediatR;

namespace Cakra.Modules.Post.Services;

/// <summary>
/// MediatR query to retrieve a filtered and paginated operational feed from <c>post.FeedItems</c>
/// (Architecture §7, §8, §9, §12, §20 — <c>UC-FCOL-005</c>, <c>UC-AWR-001..003</c>, <c>SCR-FEED-001</c>).
/// </summary>
public sealed record GetFeedQuery(
    Guid? CustomerId = null,
    Guid? ProductId = null,
    bool? IsException = null,
    int PageSize = FeedPagination.DefaultPageSize,
    int Offset = 0,
    int? Page = null,
    string? ExceptionType = null,
    Guid? RequestId = null,
    Guid? WorkPackageId = null,
    Guid? AuthorPersonId = null,
    string? SearchTerm = null,
    FeedFilter? Filter = null,
    FeedPagination? Pagination = null) : IRequest<FeedPageResultDto>;

/// <summary>
/// FluentValidation validator for <see cref="GetFeedQuery"/> (Architecture §19.2).
/// </summary>
public sealed class GetFeedQueryValidator : AbstractValidator<GetFeedQuery>
{
    public GetFeedQueryValidator()
    {
        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage("PageSize must be greater than 0.")
            .LessThanOrEqualTo(FeedPagination.MaxPageSize)
            .WithMessage($"PageSize cannot exceed {FeedPagination.MaxPageSize}.");

        RuleFor(x => x.Offset)
            .GreaterThanOrEqualTo(0).WithMessage("Offset cannot be negative.");

        RuleFor(x => x.Page)
            .Must(p => !p.HasValue || p.Value >= 1)
            .WithMessage("Page must be greater than or equal to 1 when specified.");

        RuleFor(x => x.ExceptionType)
            .Must(type => string.IsNullOrWhiteSpace(type) || PostExceptionTypes.All.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage($"ExceptionType must be one of: {string.Join(", ", PostExceptionTypes.All)}.");
    }
}

/// <summary>
/// MediatR query to retrieve a single projected <see cref="FeedItemDto"/> by its associated <c>PostId</c>.
/// </summary>
public sealed record GetFeedItemByPostIdQuery(Guid PostId) : IRequest<FeedItemDto?>;

/// <summary>
/// MediatR query to retrieve a single projected <see cref="FeedItemDto"/> by its <c>FeedItemId</c>.
/// </summary>
public sealed record GetFeedItemByIdQuery(Guid FeedItemId) : IRequest<FeedItemDto?>;
