using System.Diagnostics;
using Cakra.Modules.Customer;
using Cakra.Modules.Customer.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cakra.Api.Controllers;

/// <summary>
/// REST API controller exposing published Customer lookups (<see cref="ICustomerQueryService"/>)
/// and mutation endpoints for operational screens and UI modal dialogs (<c>SCR-CUST-001</c>, <c>SCR-CUST-002</c>)
/// (Architecture §7, §8, §9, §19.5, §19.6).
/// </summary>
[Authorize]
[Route("api/v1/customers")]
public sealed class CustomersController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICustomerQueryService _customerQueryService;

    public CustomersController(
        IMediator mediator,
        ICustomerQueryService customerQueryService)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
    }

    /// <summary>
    /// Retrieves all customers, or only active customers when <paramref name="activeOnly"/> is <c>true</c>
    /// (Architecture §7).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> ListCustomers(
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var customers = activeOnly
            ? await _mediator.Send(new ListActiveCustomersQuery(), cancellationToken)
            : await _customerQueryService.ListAllCustomersAsync(cancellationToken);

        return Ok(customers);
    }

    /// <summary>
    /// Retrieves all active customers ordered by name for UI dropdown selectors
    /// (<c>CustomerQueryService.ListActiveCustomers</c>; Architecture §7, P4-S21).
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> ListActiveCustomers(
        CancellationToken cancellationToken = default)
    {
        var customers = await _mediator.Send(new ListActiveCustomersQuery(), cancellationToken);
        return Ok(customers);
    }

    /// <summary>
    /// Retrieves a customer record by unique identifier (Architecture §7).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CustomerDto>> GetCustomerById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var customer = await _mediator.Send(new GetCustomerByIdQuery(id), cancellationToken)
            ?? throw new KeyNotFoundException($"Customer '{id}' was not found.");

        return Ok(customer);
    }

    /// <summary>
    /// Retrieves all contacts associated with the specified customer (Architecture §7).
    /// </summary>
    [HttpGet("{id:guid}/contacts")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CustomerContactDto>>> GetCustomerContacts(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _ = await _mediator.Send(new GetCustomerByIdQuery(id), cancellationToken)
            ?? throw new KeyNotFoundException($"Customer '{id}' was not found.");

        var contacts = await _mediator.Send(new GetCustomerContactsQuery(id), cancellationToken);
        return Ok(contacts);
    }

    /// <summary>
    /// Creates a new customer master record (<c>SCR-CUST-001</c>; Architecture §7, CR-002).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateCustomer(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return CreateBadRequestProblem("Request body cannot be null.");
        }

        try
        {
            var command = new CreateCustomerCommand(
                request.CustomerCode,
                request.CustomerName,
                request.HasActiveMaintenanceContract);

            var result = await _mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetCustomerById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return CreateConflictProblem(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Updates master data attributes of an existing customer (<c>SCR-CUST-002</c>; Architecture §7, CR-002).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateCustomer(
        Guid id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return CreateBadRequestProblem("Request body cannot be null.");
        }

        try
        {
            var command = new UpdateCustomerCommand(
                id,
                request.CustomerCode,
                request.CustomerName,
                request.HasActiveMaintenanceContract);

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return CreateConflictProblem(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Activates a customer master record (<c>SCR-CUST-002</c>; Architecture §7, CR-002).
    /// </summary>
    [HttpPut("{id:guid}/activate")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ActivateCustomer(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new ActivateCustomerCommand(id);
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Deactivates a customer master record (<c>SCR-CUST-002</c>; Architecture §7, CR-002).
    /// </summary>
    [HttpPut("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeactivateCustomer(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new DeactivateCustomerCommand(id);
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Creates a new contact associated with a customer (<c>SCR-CUST-002</c>; Architecture §7, CR-002).
    /// </summary>
    [HttpPost("{id:guid}/contacts")]
    [ProducesResponseType(typeof(CustomerContactDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateCustomerContact(
        Guid id,
        [FromBody] CreateCustomerContactRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return CreateBadRequestProblem("Request body cannot be null.");
        }

        try
        {
            var command = new CreateCustomerContactCommand(
                id,
                request.Name,
                request.Position,
                request.PhoneNumber,
                request.Email);

            var result = await _mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetCustomerContacts), new { id }, result);
        }
        catch (ArgumentException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Updates an existing contact for a customer (<c>SCR-CUST-002</c>; Architecture §7, CR-002).
    /// </summary>
    [HttpPut("{id:guid}/contacts/{contactId:guid}")]
    [ProducesResponseType(typeof(CustomerContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateCustomerContact(
        Guid id,
        Guid contactId,
        [FromBody] UpdateCustomerContactRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return CreateBadRequestProblem("Request body cannot be null.");
        }

        try
        {
            var command = new UpdateCustomerContactCommand(
                contactId,
                request.Name,
                request.Position,
                request.PhoneNumber,
                request.Email,
                request.Status);

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
    }

    private ObjectResult CreateConflictProblem(string detail)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict",
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8"
        };
        problem.Extensions["errorCode"] = "CUSTOMER_CODE_CONFLICT";
        problem.Extensions["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status409Conflict
        };
    }

    private ObjectResult CreateBadRequestProblem(string detail)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        };
        problem.Extensions["errorCode"] = "BAD_REQUEST";
        problem.Extensions["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }
}

/// <summary>
/// Request payload for creating a customer.
/// </summary>
public sealed record CreateCustomerRequest(
    string CustomerCode,
    string CustomerName,
    bool HasActiveMaintenanceContract = false);

/// <summary>
/// Request payload for updating a customer.
/// </summary>
public sealed record UpdateCustomerRequest(
    string CustomerCode,
    string CustomerName,
    bool HasActiveMaintenanceContract);

/// <summary>
/// Request payload for creating a customer contact.
/// </summary>
public sealed record CreateCustomerContactRequest(
    string Name,
    string? Position = null,
    string? PhoneNumber = null,
    string? Email = null);

/// <summary>
/// Request payload for updating a customer contact.
/// </summary>
public sealed record UpdateCustomerContactRequest(
    string Name,
    string? Position = null,
    string? PhoneNumber = null,
    string? Email = null,
    string? Status = null);
