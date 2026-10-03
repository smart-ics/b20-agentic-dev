using System.Diagnostics;
using System.Text.Json;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Identity.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Cakra.Api.Controllers;

/// <summary>
/// REST API controller exposing user management operations (<see cref="IUserAccountService"/>)
/// restricted strictly to users possessing the Administrator or Admin role (Architecture §14; CR-007).
/// </summary>
[Authorize(Roles = "Administrator,Admin")]
[Route("api/v1/users")]
public sealed class UsersController : ApiControllerBase
{
    private readonly IUserAccountService _userAccountService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserAccountService userAccountService,
        ILogger<UsersController> logger)
    {
        _userAccountService = userAccountService ?? throw new ArgumentNullException(nameof(userAccountService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all user accounts enriched with person display names (Architecture §14; CR-007).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserAccountSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<UserAccountSummaryDto>>> GetAllUsers(
        CancellationToken cancellationToken = default)
    {
        var users = await _userAccountService.GetAllUsersAsync(cancellationToken);
        return Ok(users);
    }

    /// <summary>
    /// Retrieves a single user account by its identifier (Architecture §14; CR-007).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserAccountDto>> GetUserById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await _userAccountService.GetUserByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return CreateNotFoundProblem($"User account '{id}' was not found.");
        }

        return Ok(user);
    }

    /// <summary>
    /// Creates a new user account (Architecture §14; CR-007).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserAccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return CreateBadRequestProblem("Request body cannot be null.");
        }

        try
        {
            var command = new CreateUserAccountCommand(
                request.PersonId,
                request.Username ?? string.Empty,
                request.Email ?? string.Empty,
                request.Password ?? string.Empty,
                request.Status ?? UserAccountStatus.Active);

            var result = await _userAccountService.CreateUserAsync(command, cancellationToken);
            return CreatedAtAction(nameof(GetUserById), new { id = result.UserId }, result);
        }
        catch (ValidationException ex)
        {
            return CreateValidationProblem(ex);
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
    /// Updates an existing user account's attributes, status, and/or password (Architecture §14; CR-007).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateUser(
        Guid id,
        [FromBody] UpdateUserAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return CreateBadRequestProblem("Request body cannot be null.");
        }

        try
        {
            var command = new UpdateUserAccountCommand(
                request.Email,
                request.Status,
                request.NewPassword,
                id);

            var result = await _userAccountService.UpdateUserAsync(id, command, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
        }
        catch (ValidationException ex)
        {
            return CreateValidationProblem(ex);
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

    private ObjectResult CreateNotFoundProblem(string detail)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not Found",
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4"
        };
        problem.Extensions["errorCode"] = "RESOURCE_NOT_FOUND";
        problem.Extensions["traceId"] = traceId;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status404NotFound,
            ContentTypes = { "application/problem+json" }
        };
    }

    private ObjectResult CreateConflictProblem(string detail)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict",
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8"
        };
        problem.Extensions["errorCode"] = "CONFLICT";
        problem.Extensions["traceId"] = traceId;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status409Conflict,
            ContentTypes = { "application/problem+json" }
        };
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

    private ObjectResult CreateValidationProblem(ValidationException ex)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var errors = ex.Errors
            .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
            );

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Error",
            Detail = "One or more validation errors occurred.",
            Instance = HttpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        };
        problem.Extensions["errorCode"] = "VALIDATION_FAILED";
        problem.Extensions["traceId"] = traceId;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    }
}

/// <summary>
/// Request payload for creating a user account (Architecture §14; CR-007).
/// </summary>
public sealed record CreateUserAccountRequest(
    Guid PersonId,
    string? Username,
    string? Email,
    string? Password,
    string? Status = null);

/// <summary>
/// Request payload for updating a user account (Architecture §14; CR-007).
/// </summary>
public sealed record UpdateUserAccountRequest(
    string? Email = null,
    string? Status = null,
    string? NewPassword = null);
