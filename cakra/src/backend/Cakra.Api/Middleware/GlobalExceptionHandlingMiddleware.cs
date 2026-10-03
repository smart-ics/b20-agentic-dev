using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Cakra.Modules.Request.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cakra.Api.Middleware;

/// <summary>
/// Centralized exception handling middleware producing standard RFC 7807
/// Problem Details (<see cref="ProblemDetails"/>) JSON responses with consistent
/// error codes (Architecture §19.6).
/// Maps FluentValidation exceptions to 400 Bad Request with field-level errors,
/// known exceptions to corresponding HTTP status codes, and unhandled exceptions
/// to 500 Internal Server Error.
/// </summary>
public sealed class GlobalExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Response has already started; cannot write RFC 7807 ProblemDetails response.");
            return;
        }

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        ProblemDetails problemDetails;
        int statusCode;

        switch (exception)
        {
            case ValidationException validationException:
                statusCode = StatusCodes.Status400BadRequest;
                _logger.LogWarning(
                    validationException,
                    "Validation failed for request {Path}: {Count} errors",
                    context.Request.Path,
                    validationException.Errors.Count());

                var errors = validationException.Errors
                    .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()
                    );

                var validationProblem = new ValidationProblemDetails(errors)
                {
                    Status = statusCode,
                    Title = "Validation Error",
                    Detail = "One or more validation errors occurred.",
                    Instance = context.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
                };
                validationProblem.Extensions["errorCode"] = "VALIDATION_FAILED";
                validationProblem.Extensions["traceId"] = traceId;
                problemDetails = validationProblem;
                break;

            case KeyNotFoundException notFoundException:
                statusCode = StatusCodes.Status404NotFound;
                _logger.LogWarning(notFoundException, "Resource not found for request {Path}", context.Request.Path);
                problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = "Resource Not Found",
                    Detail = notFoundException.Message,
                    Instance = context.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4"
                };
                problemDetails.Extensions["errorCode"] = "RESOURCE_NOT_FOUND";
                problemDetails.Extensions["traceId"] = traceId;
                break;

            case UnauthorizedAccessException unauthorizedException:
                statusCode = StatusCodes.Status401Unauthorized;
                _logger.LogWarning(unauthorizedException, "Unauthorized access for request {Path}", context.Request.Path);
                problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = "Unauthorized",
                    Detail = unauthorizedException.Message,
                    Instance = context.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
                };
                problemDetails.Extensions["errorCode"] = "UNAUTHORIZED";
                problemDetails.Extensions["traceId"] = traceId;
                break;

            case RequestDomainException domainException:
                statusCode = StatusCodes.Status400BadRequest;
                _logger.LogWarning(domainException, "Request domain rule violation for request {Path}", context.Request.Path);
                problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = "Bad Request",
                    Detail = domainException.Message,
                    Instance = context.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
                };
                problemDetails.Extensions["errorCode"] = "BAD_REQUEST";
                problemDetails.Extensions["traceId"] = traceId;
                break;

            case InvalidRequestStateTransitionException stateTransitionException:
                statusCode = StatusCodes.Status400BadRequest;
                _logger.LogWarning(stateTransitionException, "Invalid request state transition for request {Path}", context.Request.Path);
                problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = "Bad Request",
                    Detail = stateTransitionException.Message,
                    Instance = context.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
                };
                problemDetails.Extensions["errorCode"] = "BAD_REQUEST";
                problemDetails.Extensions["traceId"] = traceId;
                break;

            case ArgumentException argumentException:
                statusCode = StatusCodes.Status400BadRequest;
                _logger.LogWarning(argumentException, "Invalid argument for request {Path}", context.Request.Path);
                problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = "Bad Request",
                    Detail = argumentException.Message,
                    Instance = context.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
                };
                problemDetails.Extensions["errorCode"] = "BAD_REQUEST";
                problemDetails.Extensions["traceId"] = traceId;
                break;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                _logger.LogError(
                    exception,
                    "Unhandled exception occurred while processing request {Path}: {Message}",
                    context.Request.Path,
                    exception.Message);

                problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = "Internal Server Error",
                    Detail = _environment.IsDevelopment()
                        ? exception.Message
                        : "An unexpected error occurred while processing your request.",
                    Instance = context.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
                };
                problemDetails.Extensions["errorCode"] = "INTERNAL_SERVER_ERROR";
                problemDetails.Extensions["traceId"] = traceId;
                break;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            problemDetails,
            problemDetails.GetType(),
            s_jsonOptions);
    }
}
