using Cakra.Modules.Customer;
using Cakra.Modules.Customer.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cakra.Api.Controllers;

/// <summary>
/// REST API controller exposing published Customer lookups (<see cref="ICustomerQueryService"/>)
/// for operational screens and UI dropdown selectors such as Create Request (<c>SCR-REQ-002</c>),
/// Request Search (<c>SCR-REQ-005</c>), Work Package (<c>SCR-WP-001</c>), and Operational Feed (<c>SCR-FEED-001</c>)
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
}
