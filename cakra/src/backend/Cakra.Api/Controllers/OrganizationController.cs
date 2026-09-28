using Cakra.Modules.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cakra.Api.Controllers;

/// <summary>
/// REST API controller exposing published organizational lookups (<see cref="IOrganizationQueryService"/>)
/// for UI dropdown selectors such as Product Owner, Request Assignee, and Work Package Owner
/// (Architecture §7, §10, §19.5, §19.6).
/// </summary>
[Authorize]
[Route("api/v1/organization")]
public sealed class OrganizationController : ApiControllerBase
{
    private readonly IOrganizationQueryService _organizationQueryService;

    public OrganizationController(IOrganizationQueryService organizationQueryService)
    {
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
}
