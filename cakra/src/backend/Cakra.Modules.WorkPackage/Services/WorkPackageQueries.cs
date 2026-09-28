using MediatR;

namespace Cakra.Modules.WorkPackage.Services;

/// <summary>
/// MediatR query to retrieve a Work Package by its unique identifier (Architecture §11).
/// </summary>
public sealed record GetWorkPackageByIdQuery(Guid WorkPackageId) : IRequest<WorkPackageDto?>;

/// <summary>
/// MediatR query to retrieve a filtered list of Work Packages (Architecture §11 — UC-WP-003, SCR-WP-001).
/// </summary>
public sealed record ListWorkPackagesQuery(
    string? Status = null,
    Guid? OwnerPersonId = null,
    Guid? CustomerId = null,
    Guid? ProductId = null) : IRequest<IReadOnlyList<WorkPackageDto>>;

/// <summary>
/// MediatR query to retrieve the current and historical scope of linked Requests for a Work Package,
/// enriched with Request details from <c>IRequestQueryService</c> (Architecture §11 — UC-WP-003).
/// </summary>
public sealed record GetWorkPackageScopeQuery(Guid WorkPackageId) : IRequest<IReadOnlyList<WorkPackageScopeItemDto>>;

/// <summary>
/// MediatR query to resolve the active Work Package containing the specified Request (Architecture §11).
/// </summary>
public sealed record GetRequestWorkPackageQuery(Guid RequestId) : IRequest<WorkPackageDto?>;
