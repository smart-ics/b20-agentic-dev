using System.Diagnostics;
using Cakra.Modules.Organization;
using Cakra.Modules.Organization.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cakra.Api.Controllers;

/// <summary>
/// REST API controller exposing published organizational lookups (<see cref="IOrganizationQueryService"/>)
/// for UI dropdown selectors such as Product Owner, Request Assignee, and Work Package Owner,
/// and mutation endpoints for Person management screens (<c>SCR-ORG-001</c>, <c>SCR-ORG-002</c>)
/// (Architecture §7, §10, §19.5, §19.6).
/// </summary>
[Authorize]
[Route("api/v1/organization")]
public sealed class OrganizationController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly IOrganizationQueryService _organizationQueryService;

    public OrganizationController(
        IMediator mediator,
        IOrganizationQueryService organizationQueryService)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _organizationQueryService = organizationQueryService
            ?? throw new ArgumentNullException(nameof(organizationQueryService));
    }

    /// <summary>
    /// Retrieves all active organizational persons ordered by name for UI dropdown selectors
    /// (Architecture §7, §10).
    /// </summary>
    [HttpGet("persons/active")]
    [ProducesResponseType(typeof(IReadOnlyList<PersonDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PersonDto>>> ListActivePersons(
        CancellationToken cancellationToken = default)
    {
        var persons = await _organizationQueryService.ListActivePersonsAsync(cancellationToken);
        return Ok(persons);
    }

    /// <summary>
    /// Retrieves active organizational persons (convenience alias for <c>/api/v1/organization/persons/active</c>).
    /// </summary>
    [HttpGet("persons")]
    [ProducesResponseType(typeof(IReadOnlyList<PersonDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PersonDto>>> ListPersons(
        CancellationToken cancellationToken = default)
    {
        var persons = await _organizationQueryService.ListActivePersonsAsync(cancellationToken);
        return Ok(persons);
    }

    /// <summary>
    /// Retrieves all organizational persons (both active and inactive) ordered by last name and first name
    /// for Person Management screen (<c>SCR-ORG-001</c>; Architecture §7).
    /// </summary>
    [HttpGet("persons/all")]
    [ProducesResponseType(typeof(IReadOnlyList<PersonDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PersonDto>>> ListAllPersons(
        CancellationToken cancellationToken = default)
    {
        var persons = await _organizationQueryService.ListAllPersonsAsync(cancellationToken);
        return Ok(persons);
    }

    /// <summary>
    /// Retrieves an organizational person by unique identifier (Architecture §7).
    /// </summary>
    [HttpGet("persons/{id:guid}")]
    [ProducesResponseType(typeof(PersonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PersonDto>> GetPersonById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var person = await _organizationQueryService.GetPersonByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Person '{id}' was not found.");

        return Ok(person);
    }

    /// <summary>
    /// Creates a new organizational person (<c>SCR-ORG-001</c>, <c>SCR-ORG-002</c>; Architecture §7).
    /// </summary>
    [HttpPost("persons")]
    [Authorize(Roles = "Administrator,Admin")]
    [ProducesResponseType(typeof(PersonDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePerson(
        [FromBody] CreatePersonRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return CreateBadRequestProblem("Request body cannot be null.");
        }

        try
        {
            var command = new CreatePersonCommand(
                request.FirstName,
                request.LastName,
                request.Email);

            var result = await _mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetPersonById), new { id = result.Id }, result);
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
    /// Updates identity attributes of an existing organizational person (<c>SCR-ORG-001</c>, <c>SCR-ORG-002</c>; Architecture §7).
    /// </summary>
    [HttpPut("persons/{id:guid}")]
    [Authorize(Roles = "Administrator,Admin")]
    [ProducesResponseType(typeof(PersonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdatePerson(
        Guid id,
        [FromBody] UpdatePersonRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return CreateBadRequestProblem("Request body cannot be null.");
        }

        try
        {
            var command = new UpdatePersonCommand(
                id,
                request.FirstName,
                request.LastName,
                request.Email);

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
        catch (KeyNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
        }
    }

    /// <summary>
    /// Activates an organizational person (<c>SCR-ORG-001</c>, <c>SCR-ORG-002</c>; Architecture §7).
    /// </summary>
    [HttpPut("persons/{id:guid}/activate")]
    [Authorize(Roles = "Administrator,Admin")]
    [ProducesResponseType(typeof(PersonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ActivatePerson(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new ActivatePersonCommand(id);
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
        }
    }

    /// <summary>
    /// Deactivates an organizational person (<c>SCR-ORG-001</c>, <c>SCR-ORG-002</c>; Architecture §7).
    /// </summary>
    [HttpPut("persons/{id:guid}/deactivate")]
    [Authorize(Roles = "Administrator,Admin")]
    [ProducesResponseType(typeof(PersonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeactivatePerson(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new DeactivatePersonCommand(id);
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return CreateBadRequestProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
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
        problem.Extensions["errorCode"] = "PERSON_CONFLICT";
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

    private ObjectResult CreateNotFoundProblem(string detail)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not Found",
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4"
        };
        problem.Extensions["errorCode"] = "NOT_FOUND";
        problem.Extensions["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status404NotFound
        };
    }
}

/// <summary>
/// Request payload for creating a person.
/// </summary>
public sealed record CreatePersonRequest(
    string FirstName,
    string LastName,
    string Email);

/// <summary>
/// Request payload for updating a person.
/// </summary>
public sealed record UpdatePersonRequest(
    string FirstName,
    string LastName,
    string Email);