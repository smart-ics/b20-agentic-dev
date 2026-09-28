namespace ICS.Web.Controllers;

using ICS.Core.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Diagnostic test controller exposing protected and role-restricted endpoints
/// for RBAC and authentication pipeline verification per Architecture §14, §18, and §19.5.
/// </summary>
[ApiController]
[Route("api/v1/test/security")]
public class SecurityTestController : ControllerBase
{
    private readonly ICurrentContextProvider _currentContext;

    public SecurityTestController(ICurrentContextProvider currentContext)
    {
        _currentContext = currentContext;
    }

    /// <summary>
    /// Protected endpoint requiring an authenticated user.
    /// Returns 401 Unauthorized if unauthenticated.
    /// </summary>
    [HttpGet("protected")]
    [Authorize]
    public IActionResult GetProtected()
    {
        return Ok(new
        {
            message = "Protected resource accessed successfully.",
            userId = _currentContext.CurrentUserId,
            personId = _currentContext.CurrentPersonId,
            roles = _currentContext.CurrentRoles,
            isAuthenticated = _currentContext.IsAuthenticated
        });
    }

    /// <summary>
    /// Management-only endpoint requiring the 'Management' role.
    /// Returns 401 if unauthenticated, 403 if authenticated but lacking 'Management' role.
    /// </summary>
    [HttpGet("management")]
    [Authorize(Roles = "Management")]
    public IActionResult GetManagement()
    {
        return Ok(new
        {
            message = "Management resource accessed successfully.",
            userId = _currentContext.CurrentUserId,
            personId = _currentContext.CurrentPersonId,
            roles = _currentContext.CurrentRoles,
            isAuthenticated = _currentContext.IsAuthenticated
        });
    }
}
