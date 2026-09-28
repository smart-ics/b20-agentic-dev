using Microsoft.AspNetCore.Mvc;

namespace Cakra.Api.Controllers;

/// <summary>
/// Base API controller establishing the REST route convention:
/// <c>/api/v1/{module}/{resource}</c> (Architecture §19.6).
/// All module API controllers inherit from this base class or follow this route convention.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
}
