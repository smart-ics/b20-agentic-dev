namespace ICS.Web.Controllers;

using System.Diagnostics;
using ICS.Modules.Customer;
using ICS.Modules.Customer.Application.DTOs;
using ICS.Modules.Organization;
using ICS.Modules.Organization.Application.DTOs;
using ICS.Modules.Product;
using ICS.Modules.Product.Application.DTOs;
using ICS.Modules.WorkPackage;
using ICS.Modules.WorkPackage.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// REST API Controller managing Work Package operations per Architecture §11, §16, §19.4, §19.5, §19.6, §19.8,
/// and SCR-WP-001 (Slice P5-S22). Delivers Work Package creation, lifecycle management, and scope review.
/// All endpoints are protected by [Authorize].
/// </summary>
[ApiController]
[Route("api/v1/work-packages")]
[Authorize]
public class WorkPackagesController : ControllerBase
{
    private readonly IWorkPackageService _workPackageService;
    private readonly IWorkPackageQueryService _workPackageQueryService;
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly ILogger<WorkPackagesController> _logger;

    public WorkPackagesController(
        IWorkPackageService workPackageService,
        IWorkPackageQueryService workPackageQueryService,
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        ILogger<WorkPackagesController> logger)
    {
        _workPackageService = workPackageService ?? throw new ArgumentNullException(nameof(workPackageService));
        _workPackageQueryService = workPackageQueryService ?? throw new ArgumentNullException(nameof(workPackageQueryService));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Supplies active organization persons for the Work Package owner dropdown selector.
    /// GET /api/v1/work-packages/owners
    /// </summary>
    [HttpGet("owners")]
    [ProducesResponseType(typeof(IReadOnlyList<PersonDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveOwners(CancellationToken cancellationToken)
    {
        var persons = await _organizationQueryService.ListActivePersonsAsync(cancellationToken);
        return Ok(persons);
    }

    /// <summary>
    /// Supplies active customers for the Work Package customer dropdown selector.
    /// GET /api/v1/work-packages/customers
    /// </summary>
    [HttpGet("customers")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveCustomers(CancellationToken cancellationToken)
    {
        var customers = await _customerQueryService.ListActiveCustomersAsync(cancellationToken);
        return Ok(customers);
    }

    /// <summary>
    /// Supplies active products for the Work Package product dropdown selector.
    /// GET /api/v1/work-packages/products
    /// </summary>
    [HttpGet("products")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveProducts(CancellationToken cancellationToken)
    {
        var products = await _productQueryService.ListActiveProductsAsync(cancellationToken);
        return Ok(products);
    }

    /// <summary>
    /// Lists Work Packages with optional filtering (status, owner, customer, product) and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(WorkPackageGridResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListWorkPackages(
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? ownerPersonId = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] string? statuses = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var filter = new WorkPackageGridFilterDto(
            SearchTerm: searchTerm,
            OwnerPersonId: ownerPersonId,
            CustomerId: customerId,
            ProductId: productId,
            Statuses: statuses,
            FromDate: fromDate,
            ToDate: toDate,
            Skip: skip,
            Take: take);

        var result = await _workPackageQueryService.ListWorkPackagesAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets a specific Work Package by identifier.
    /// </summary>
    [HttpGet("{id:guid}", Name = "GetWorkPackageById")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkPackageById(Guid id, CancellationToken cancellationToken)
    {
        var workPackage = await _workPackageQueryService.GetWorkPackageByIdAsync(id, cancellationToken);
        if (workPackage == null)
        {
            return WorkPackageNotFound(id);
        }

        return Ok(workPackage);
    }

    /// <summary>
    /// Creates a new Work Package.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateWorkPackage([FromBody] CreateWorkPackageRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequestProblem("Request payload is required.");
        }

        var workPackage = await _workPackageService.CreateWorkPackageAsync(
            request.Name,
            request.Objective,
            request.OwnerPersonId,
            request.CustomerId,
            request.ProductId,
            request.WorkPackageId,
            cancellationToken);

        _logger.LogInformation("Work Package '{WorkPackageId}' ({Name}) created successfully.", workPackage.Id, workPackage.Name);

        return CreatedAtRoute("GetWorkPackageById", new { id = workPackage.Id }, workPackage);
    }

    /// <summary>
    /// Updates the objective and name of a Work Package.
    /// </summary>
    [HttpPut("{id:guid}/objective")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateObjective(Guid id, [FromBody] UpdateObjectiveRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequestProblem("Request payload is required.");
        }

        var existing = await _workPackageQueryService.GetWorkPackageByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return WorkPackageNotFound(id);
        }

        var updated = await _workPackageService.UpdateObjectiveAsync(id, request.Name, request.Objective, cancellationToken);

        _logger.LogInformation("Work Package '{WorkPackageId}' objective updated successfully.", id);

        return Ok(updated);
    }

    /// <summary>
    /// Assigns an owner to a Work Package.
    /// </summary>
    [HttpPut("{id:guid}/owner")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignOwner(Guid id, [FromBody] AssignOwnerRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.NewOwnerPersonId == Guid.Empty)
        {
            return BadRequestProblem("A valid NewOwnerPersonId is required.");
        }

        var existing = await _workPackageQueryService.GetWorkPackageByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return WorkPackageNotFound(id);
        }

        var updated = await _workPackageService.AssignOwnerAsync(id, request.NewOwnerPersonId, cancellationToken);

        _logger.LogInformation("Work Package '{WorkPackageId}' owner changed to '{OwnerPersonId}'.", id, request.NewOwnerPersonId);

        return Ok(updated);
    }

    /// <summary>
    /// Activates a Work Package (DRAFT → ACTIVE).
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateWorkPackage(Guid id, CancellationToken cancellationToken)
    {
        var existing = await _workPackageQueryService.GetWorkPackageByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return WorkPackageNotFound(id);
        }

        await _workPackageService.ActivateWorkPackageAsync(id, cancellationToken);
        var refreshed = await _workPackageQueryService.GetWorkPackageByIdAsync(id, cancellationToken);

        _logger.LogInformation("Work Package '{WorkPackageId}' activated.", id);

        return Ok(refreshed);
    }

    /// <summary>
    /// Closes a Work Package (ACTIVE/DRAFT → CLOSED).
    /// </summary>
    [HttpPost("{id:guid}/close")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CloseWorkPackage(Guid id, [FromBody] CloseWorkPackageRequest request, CancellationToken cancellationToken)
    {
        var existing = await _workPackageQueryService.GetWorkPackageByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return WorkPackageNotFound(id);
        }

        var closed = await _workPackageService.CloseWorkPackageAsync(id, request.Reason, cancellationToken);

        _logger.LogInformation("Work Package '{WorkPackageId}' closed. Reason: {Reason}", id, request.Reason ?? "(no reason)");

        return Ok(closed);
    }

    /// <summary>
    /// Gets the full scope of a Work Package: package details plus its active and historical request memberships.
    /// </summary>
    [HttpGet("{id:guid}/scope")]
    [ProducesResponseType(typeof(WorkPackageScopeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkPackageScope(Guid id, CancellationToken cancellationToken)
    {
        var scope = await _workPackageQueryService.GetWorkPackageScopeAsync(id, cancellationToken);
        if (scope == null)
        {
            return WorkPackageNotFound(id);
        }

        return Ok(scope);
    }

    /// <summary>
    /// Adds a Request to a Work Package.
    /// </summary>
    [HttpPost("{id:guid}/requests/{requestId:guid}")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddRequestToWorkPackage(Guid id, Guid requestId, CancellationToken cancellationToken)
    {
        var existing = await _workPackageQueryService.GetWorkPackageByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return WorkPackageNotFound(id);
        }

        var result = await _workPackageService.AddRequestToWorkPackageAsync(id, requestId, cancellationToken);

        _logger.LogInformation("Request '{RequestId}' added to Work Package '{WorkPackageId}'.", requestId, id);

        return Ok(result);
    }

    /// <summary>
    /// Removes a Request from a Work Package.
    /// </summary>
    [HttpDelete("{id:guid}/requests/{requestId:guid}")]
    [ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveRequestFromWorkPackage(Guid id, Guid requestId, CancellationToken cancellationToken)
    {
        var existing = await _workPackageQueryService.GetWorkPackageByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return WorkPackageNotFound(id);
        }

        var result = await _workPackageService.RemoveRequestFromWorkPackageAsync(id, requestId, cancellationToken);

        _logger.LogInformation("Request '{RequestId}' removed from Work Package '{WorkPackageId}'.", requestId, id);

        return Ok(result);
    }

    private IActionResult WorkPackageNotFound(Guid id)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            Title = "Work Package Not Found",
            Status = StatusCodes.Status404NotFound,
            Detail = $"Work Package with ID '{id}' was not found.",
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["errorCode"] = "WORK_PACKAGE_NOT_FOUND";
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
/// HTTP request model for creating a Work Package.
/// </summary>
public sealed record CreateWorkPackageRequest(
    string Name,
    string Objective,
    Guid OwnerPersonId,
    Guid? CustomerId = null,
    Guid? ProductId = null,
    Guid? WorkPackageId = null);

/// <summary>
/// HTTP request model for updating Work Package objective.
/// </summary>
public sealed record UpdateObjectiveRequest(
    string Name,
    string Objective);

/// <summary>
/// HTTP request model for assigning Work Package owner.
/// </summary>
public sealed record AssignOwnerRequest(
    Guid NewOwnerPersonId);

/// <summary>
/// HTTP request model for closing a Work Package.
/// </summary>
public sealed record CloseWorkPackageRequest(
    string? Reason = null);
