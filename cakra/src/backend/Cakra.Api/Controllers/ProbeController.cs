using Cakra.Core;
using Cakra.Modules.Identity.Registration;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cakra.Api.Controllers;

/// <summary>
/// Architectural probe controller establishing the REST route convention:
/// <c>/api/v1/{module}/{resource}</c> (Architecture §19.6).
/// Exposes verification endpoints for the MediatR FluentValidation pipeline,
/// the global exception handler, the application pipeline, and security/RBAC enforcement (Architecture §18, §19.5).
/// </summary>
[Route("api/v1/system/probe")]
public sealed class ProbeController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly ISystemClock _clock;

    public ProbeController(IMediator mediator, ISystemClock clock)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Simple health probe demonstrating base route convention.</summary>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            timestamp = _clock.UtcNow
        });
    }

    /// <summary>
    /// Dispatches <see cref="StubModulePingRequest"/> through the MediatR + FluentValidation pipeline.
    /// A negative count triggers FluentValidation and throws <see cref="FluentValidation.ValidationException"/>,
    /// proving the global exception handler maps validation failures to 400 ProblemDetails.
    /// </summary>
    [HttpGet("ping")]
    public async Task<IActionResult> Ping([FromQuery] int count = 1, CancellationToken cancellationToken = default)
    {
        var response = await _mediator.Send(new StubModulePingRequest(count), cancellationToken);
        return Ok(new { message = response });
    }

    /// <summary>
    /// Throws an unhandled exception to verify the global exception handler returns
    /// standard RFC 7807 500 ProblemDetails.
    /// </summary>
    [HttpGet("error")]
    public IActionResult ThrowError()
    {
        throw new InvalidOperationException("Probe error triggered for testing.");
    }

    /// <summary>
    /// Protected probe endpoint requiring authentication (Architecture §18, §19.5).
    /// Returns the resolved ambient security context from <see cref="ICurrentContextProvider"/>.
    /// </summary>
    [Authorize]
    [HttpGet("secure")]
    public IActionResult SecureProbe([FromServices] ICurrentContextProvider contextProvider)
    {
        return Ok(new
        {
            userId = contextProvider.CurrentUserId,
            personId = contextProvider.CurrentPersonId,
            roles = contextProvider.CurrentRoles
        });
    }

    /// <summary>
    /// Role-restricted probe endpoint requiring the "Management" role (Architecture §18, §19.5).
    /// </summary>
    [Authorize(Roles = "Management")]
    [HttpGet("management")]
    public IActionResult ManagementProbe([FromServices] ICurrentContextProvider contextProvider)
    {
        return Ok(new
        {
            status = "management-authorized",
            userId = contextProvider.CurrentUserId,
            personId = contextProvider.CurrentPersonId,
            roles = contextProvider.CurrentRoles
        });
    }
}
