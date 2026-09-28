namespace ICS.Web.Controllers;

using System.Diagnostics;
using ICS.Modules.Organization;
using ICS.Modules.Organization.Application.DTOs;
using ICS.Modules.Product;
using ICS.Modules.Product.Application;
using ICS.Modules.Product.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// REST API Controller managing Product Catalog per Architecture §10, §16, §19.4, §19.6,
/// and SCR-PRD-001 (Slice P3-S15).
/// Exposes catalog listing, product detail, creation, attribute updates, owner assignment,
/// and lifecycle status transitions (activate/deactivate).
/// All endpoints are protected by [Authorize].
/// </summary>
[ApiController]
[Route("api/v1/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IProductQueryService _productQueryService;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IProductService productService,
        IProductQueryService productQueryService,
        IOrganizationQueryService organizationQueryService,
        ILogger<ProductsController> logger)
    {
        _productService = productService ?? throw new ArgumentNullException(nameof(productService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Lists products. Supports filtering by active status via activeOnly query parameter.
    /// GET /api/v1/products
    /// GET /api/v1/products?activeOnly=true
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProducts([FromQuery] bool? activeOnly, CancellationToken cancellationToken)
    {
        if (activeOnly == true)
        {
            var activeProducts = await _productQueryService.ListActiveProductsAsync(cancellationToken);
            return Ok(activeProducts);
        }

        var allProducts = await _productQueryService.ListAllProductsAsync(cancellationToken);
        return Ok(allProducts);
    }

    /// <summary>
    /// Explicit endpoint for active products only.
    /// GET /api/v1/products/active
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListActiveProducts(CancellationToken cancellationToken)
    {
        var products = await _productQueryService.ListActiveProductsAsync(cancellationToken);
        return Ok(products);
    }

    /// <summary>
    /// Supplies active organization persons for product owner dropdown selectors.
    /// GET /api/v1/products/owners
    /// </summary>
    [HttpGet("owners")]
    [ProducesResponseType(typeof(IReadOnlyList<PersonDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListEligibleOwners(CancellationToken cancellationToken)
    {
        var persons = await _organizationQueryService.ListActivePersonsAsync(cancellationToken);
        return Ok(persons);
    }

    /// <summary>
    /// Gets product detail by unique identifier.
    /// GET /api/v1/products/{id}
    /// </summary>
    [HttpGet("{id:guid}", Name = "GetProductById")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productQueryService.GetProductByIdAsync(id, cancellationToken);
        if (product == null)
        {
            return ProductNotFound(id);
        }

        return Ok(product);
    }

    /// <summary>
    /// Creates a new Product aggregate.
    /// POST /api/v1/products
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequestProblem("Request payload is required.");
        }

        var product = await _productService.CreateProductAsync(
            request.Code,
            request.Name,
            request.Description,
            request.OwnerPersonId,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Product '{Code}' ({ProductId}) created successfully.", product.Code, product.ProductId);

        return CreatedAtRoute("GetProductById", new { id = product.ProductId }, product);
    }

    /// <summary>
    /// Updates product descriptive attributes (Name, Description).
    /// PUT /api/v1/products/{id}
    /// </summary>
    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequestProblem("Request payload is required.");
        }

        var existing = await _productQueryService.GetProductByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return ProductNotFound(id);
        }

        var updated = await _productService.UpdateProductAsync(id, request.Name, request.Description, cancellationToken);

        _logger.LogInformation("Product '{ProductId}' updated successfully.", id);

        return Ok(updated);
    }

    /// <summary>
    /// Assigns or reassigns product owner to a Person from Organization domain.
    /// PUT /api/v1/products/{id}/owner
    /// POST /api/v1/products/{id}/owner
    /// </summary>
    [HttpPut("{id:guid}/owner")]
    [HttpPost("{id:guid}/owner")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignProductOwner(Guid id, [FromBody] AssignProductOwnerRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.ResolvedOwnerPersonId == Guid.Empty)
        {
            return BadRequestProblem("A valid OwnerPersonId is required.");
        }

        var existing = await _productQueryService.GetProductByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return ProductNotFound(id);
        }

        var updated = await _productService.AssignProductOwnerAsync(id, request.ResolvedOwnerPersonId, cancellationToken);

        _logger.LogInformation("Product '{ProductId}' owner changed to '{OwnerPersonId}'.", id, request.ResolvedOwnerPersonId);

        return Ok(updated);
    }

    /// <summary>
    /// Activates a product.
    /// POST /api/v1/products/{id}/activate
    /// PUT /api/v1/products/{id}/activate
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [HttpPut("{id:guid}/activate")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateProduct(Guid id, CancellationToken cancellationToken)
    {
        var existing = await _productQueryService.GetProductByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return ProductNotFound(id);
        }

        await _productService.ActivateProductAsync(id, cancellationToken);
        var refreshed = await _productQueryService.GetProductByIdAsync(id, cancellationToken);

        _logger.LogInformation("Product '{ProductId}' activated.", id);

        return Ok(refreshed);
    }

    /// <summary>
    /// Deactivates a product.
    /// POST /api/v1/products/{id}/deactivate
    /// PUT /api/v1/products/{id}/deactivate
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [HttpPut("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateProduct(Guid id, CancellationToken cancellationToken)
    {
        var existing = await _productQueryService.GetProductByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return ProductNotFound(id);
        }

        await _productService.DeactivateProductAsync(id, cancellationToken);
        var refreshed = await _productQueryService.GetProductByIdAsync(id, cancellationToken);

        _logger.LogInformation("Product '{ProductId}' deactivated.", id);

        return Ok(refreshed);
    }

    private IActionResult ProductNotFound(Guid id)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            Title = "Product Not Found",
            Status = StatusCodes.Status404NotFound,
            Detail = $"Product with ID '{id}' was not found.",
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["errorCode"] = "PRODUCT_NOT_FOUND";
        problem.Extensions["traceId"] = traceId;
        problem.Extensions["timestamp"] = DateTime.UtcNow;

        return NotFound(problem);
    }

    private IActionResult BadRequestProblem(string detail)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "Bad Request",
            Status = StatusCodes.Status400BadRequest,
            Detail = detail,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["errorCode"] = "BAD_REQUEST";
        problem.Extensions["traceId"] = traceId;
        problem.Extensions["timestamp"] = DateTime.UtcNow;

        return BadRequest(problem);
    }
}

/// <summary>
/// HTTP request model for creating a product.
/// </summary>
public sealed record CreateProductRequest(
    string Code,
    string Name,
    string? Description,
    Guid OwnerPersonId);

/// <summary>
/// HTTP request model for updating product attributes.
/// </summary>
public sealed record UpdateProductRequest(
    string Name,
    string? Description);

/// <summary>
/// HTTP request model for assigning product owner.
/// Accepts either OwnerPersonId or NewOwnerPersonId.
/// </summary>
public sealed class AssignProductOwnerRequest
{
    public Guid OwnerPersonId { get; set; }

    public Guid NewOwnerPersonId
    {
        get => OwnerPersonId;
        set => OwnerPersonId = value;
    }

    public Guid ResolvedOwnerPersonId => OwnerPersonId != Guid.Empty ? OwnerPersonId : NewOwnerPersonId;
}
