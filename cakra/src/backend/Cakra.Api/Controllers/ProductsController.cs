using System.Diagnostics;
using Cakra.Modules.Organization;
using Cakra.Modules.Product;
using Cakra.Modules.Product.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Cakra.Api.Controllers;

/// <summary>
/// REST API controller for Product Catalog management (<c>SCR-PRD-001</c>, <c>UC-PRD-001</c>,
/// <c>UC-PRD-002</c>, <c>FEAT-PRD-001</c>; Architecture §9, §10, §19.5, §19.6).
/// Exposes <c>/api/v1/products</c> endpoints protected by <see cref="AuthorizeAttribute"/>,
/// dispatching commands to <see cref="ProductService"/> via MediatR and queries to
/// <see cref="IProductQueryService"/> / <see cref="IOrganizationQueryService"/>.
/// </summary>
[Authorize]
[Route("api/v1/products")]
public sealed class ProductsController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IMediator mediator,
        IOrganizationQueryService organizationQueryService,
        ILogger<ProductsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Lists all products in the catalog (<c>ListAllProducts</c>), or only active products
    /// when <paramref name="activeOnly"/> is <c>true</c> (Architecture §10).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductCatalogItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ProductCatalogItemResponse>>> ListAllProducts(
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var products = activeOnly
            ? await _mediator.Send(new ListActiveProductsQuery(), cancellationToken)
            : await _mediator.Send(new ListAllProductsQuery(), cancellationToken);

        var responses = await EnrichProductsWithOwnerNamesAsync(products, cancellationToken);
        return Ok(responses);
    }

    /// <summary>
    /// Lists all active products in the catalog (<c>ListActiveProducts</c>; Architecture §10).
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductCatalogItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ProductCatalogItemResponse>>> ListActiveProducts(
        CancellationToken cancellationToken = default)
    {
        var products = await _mediator.Send(new ListActiveProductsQuery(), cancellationToken);
        var responses = await EnrichProductsWithOwnerNamesAsync(products, cancellationToken);
        return Ok(responses);
    }

    /// <summary>
    /// Lists active organizational persons for the Product Owner selector dropdown
    /// via <see cref="IOrganizationQueryService.ListActivePersonsAsync"/> (Architecture §10).
    /// </summary>
    [HttpGet("owners")]
    [ProducesResponseType(typeof(IReadOnlyList<PersonDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PersonDto>>> ListActiveOwners(
        CancellationToken cancellationToken = default)
    {
        var persons = await _organizationQueryService.ListActivePersonsAsync(cancellationToken);
        return Ok(persons);
    }

    /// <summary>
    /// Retrieves a single product's authoritative record by unique identifier (<c>GetProductById</c>; Architecture §10).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProductCatalogItemResponse>> GetProductById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await _mediator.Send(new GetProductByIdQuery(id), cancellationToken)
            ?? throw new KeyNotFoundException($"Product '{id}' was not found.");

        var response = await EnrichProductWithOwnerNameAsync(product, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Creates a new product in the catalog (<c>CreateProduct</c>; Architecture §10).
    /// Validates unique code and active owner in Organization.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductCatalogItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductRequest? request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateProductCommand(
            request?.ResolvedCode ?? string.Empty,
            request?.ResolvedName ?? string.Empty,
            request?.Description,
            request?.OwnerPersonId ?? Guid.Empty);

        try
        {
            var created = await _mediator.Send(command, cancellationToken);
            var response = await EnrichProductWithOwnerNameAsync(created, cancellationToken);

            _logger.LogInformation(
                "Created product '{ProductCode}' ({ProductId}) with owner '{OwnerPersonId}'.",
                created.Code,
                created.Id,
                created.OwnerPersonId);

            return CreatedAtAction(nameof(GetProductById), new { id = created.Id }, response);
        }
        catch (InvalidOperationException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Updates descriptive attributes of an existing product (<c>UpdateProduct</c>; Architecture §10).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProductCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductRequest? request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateProductCommand(
            id,
            request?.ResolvedName ?? string.Empty,
            request?.Description);

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);

            if (request?.OwnerPersonId is { } newOwnerId &&
                newOwnerId != Guid.Empty &&
                newOwnerId != updated.OwnerPersonId)
            {
                updated = await _mediator.Send(
                    new AssignProductOwnerCommand(id, newOwnerId),
                    cancellationToken);
            }

            var response = await EnrichProductWithOwnerNameAsync(updated, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Assigns a new product owner to an existing product (<c>AssignProductOwner</c>; Architecture §10).
    /// Validates the owner via <see cref="IOrganizationQueryService"/>.
    /// </summary>
    [HttpPut("{id:guid}/owner")]
    [HttpPatch("{id:guid}/owner")]
    [HttpPost("{id:guid}/owner")]
    [ProducesResponseType(typeof(ProductCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AssignProductOwner(
        Guid id,
        [FromBody] AssignProductOwnerRequest? request,
        CancellationToken cancellationToken = default)
    {
        var command = new AssignProductOwnerCommand(
            id,
            request?.ResolvedOwnerPersonId ?? Guid.Empty);

        try
        {
            var updated = await _mediator.Send(command, cancellationToken);
            var response = await EnrichProductWithOwnerNameAsync(updated, cancellationToken);

            _logger.LogInformation(
                "Assigned new owner '{OwnerPersonId}' to product '{ProductId}'.",
                updated.OwnerPersonId,
                updated.Id);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Transitions a product's lifecycle status to <c>ACTIVE</c> (<c>ActivateProduct</c>; Architecture §10).
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [HttpPut("{id:guid}/activate")]
    [ProducesResponseType(typeof(ProductCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProductCatalogItemResponse>> ActivateProduct(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var updated = await _mediator.Send(new ActivateProductCommand(id), cancellationToken);
        var response = await EnrichProductWithOwnerNameAsync(updated, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Transitions a product's lifecycle status to <c>INACTIVE</c> (<c>DeactivateProduct</c>; Architecture §10).
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [HttpPut("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(ProductCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProductCatalogItemResponse>> DeactivateProduct(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var updated = await _mediator.Send(new DeactivateProductCommand(id), cancellationToken);
        var response = await EnrichProductWithOwnerNameAsync(updated, cancellationToken);
        return Ok(response);
    }

    private async Task<IReadOnlyList<ProductCatalogItemResponse>> EnrichProductsWithOwnerNamesAsync(
        IReadOnlyList<ProductDto> products,
        CancellationToken cancellationToken)
    {
        if (products.Count == 0)
        {
            return Array.Empty<ProductCatalogItemResponse>();
        }

        var activePersons = await _organizationQueryService.ListActivePersonsAsync(cancellationToken);
        var ownerLookup = activePersons.ToDictionary(p => p.Id, p => p.FullName);

        var results = new List<ProductCatalogItemResponse>(products.Count);
        foreach (var product in products)
        {
            if (!ownerLookup.TryGetValue(product.OwnerPersonId, out var ownerName))
            {
                var person = await _organizationQueryService.GetPersonByIdAsync(product.OwnerPersonId, cancellationToken);
                ownerName = person?.FullName;
                if (!string.IsNullOrWhiteSpace(ownerName))
                {
                    ownerLookup[product.OwnerPersonId] = ownerName;
                }
            }

            results.Add(ProductCatalogItemResponse.FromDto(product, ownerName));
        }

        return results;
    }

    private async Task<ProductCatalogItemResponse> EnrichProductWithOwnerNameAsync(
        ProductDto product,
        CancellationToken cancellationToken)
    {
        var person = await _organizationQueryService.GetPersonByIdAsync(product.OwnerPersonId, cancellationToken);
        var ownerName = person?.FullName;

        if (string.IsNullOrWhiteSpace(ownerName))
        {
            var activePersons = await _organizationQueryService.ListActivePersonsAsync(cancellationToken);
            ownerName = activePersons.FirstOrDefault(p => p.Id == product.OwnerPersonId)?.FullName;
        }

        return ProductCatalogItemResponse.FromDto(product, ownerName);
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

    /// <summary>
    /// Request payload for <c>POST /api/v1/products</c>.
    /// </summary>
    public sealed class CreateProductRequest
    {
        public string? Code { get; set; }
        public string? ProductCode { get; set; }
        public string? Name { get; set; }
        public string? ProductName { get; set; }
        public string? Description { get; set; }
        public Guid OwnerPersonId { get; set; }

        public string ResolvedCode =>
            !string.IsNullOrWhiteSpace(Code) ? Code : (ProductCode ?? string.Empty);

        public string ResolvedName =>
            !string.IsNullOrWhiteSpace(Name) ? Name : (ProductName ?? string.Empty);
    }

    /// <summary>
    /// Request payload for <c>PUT /api/v1/products/{id}</c>.
    /// </summary>
    public sealed class UpdateProductRequest
    {
        public string? Name { get; set; }
        public string? ProductName { get; set; }
        public string? Description { get; set; }
        public Guid? OwnerPersonId { get; set; }

        public string ResolvedName =>
            !string.IsNullOrWhiteSpace(Name) ? Name : (ProductName ?? string.Empty);
    }

    /// <summary>
    /// Request payload for <c>PUT /api/v1/products/{id}/owner</c>.
    /// Accepts either <c>newOwnerPersonId</c> or <c>ownerPersonId</c>.
    /// </summary>
    public sealed class AssignProductOwnerRequest
    {
        public Guid? NewOwnerPersonId { get; set; }
        public Guid? OwnerPersonId { get; set; }

        public Guid ResolvedOwnerPersonId =>
            NewOwnerPersonId is { } newId && newId != Guid.Empty
                ? newId
                : (OwnerPersonId ?? Guid.Empty);
    }

    /// <summary>
    /// Response view model for Product Catalog endpoints (<c>SCR-PRD-001</c>).
    /// </summary>
    public sealed record ProductCatalogItemResponse
    {
        public Guid Id { get; init; }
        public Guid ProductId => Id;
        public string Code { get; init; } = string.Empty;
        public string ProductCode => Code;
        public string Name { get; init; } = string.Empty;
        public string ProductName => Name;
        public string? Description { get; init; }
        public Guid OwnerPersonId { get; init; }
        public string? OwnerName { get; init; }
        public string Status { get; init; } = "ACTIVE";
        public bool IsActive => string.Equals(Status, "ACTIVE", StringComparison.OrdinalIgnoreCase);
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }

        public static ProductCatalogItemResponse FromDto(ProductDto dto, string? ownerName = null)
        {
            ArgumentNullException.ThrowIfNull(dto);

            return new ProductCatalogItemResponse
            {
                Id = dto.Id,
                Code = dto.Code,
                Name = dto.Name,
                Description = dto.Description,
                OwnerPersonId = dto.OwnerPersonId,
                OwnerName = ownerName,
                Status = dto.Status,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt
            };
        }
    }
}
