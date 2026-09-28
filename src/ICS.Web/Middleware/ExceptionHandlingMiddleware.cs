using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ICS.Web.Middleware;

/// <summary>
/// Centralized exception handling middleware producing standard RFC 7807 Problem Details (ProblemDetails)
/// JSON responses with consistent error codes and camelCase serialization per Architecture §19.6.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
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
            _logger.LogWarning(
                "Response has already started, unable to write RFC 7807 ProblemDetails for exception: {Message}",
                exception.Message);
            return;
        }

        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        var spanId = Activity.Current?.SpanId.ToString() ?? string.Empty;
        var timestamp = DateTime.UtcNow;

        var (statusCode, errorCode, problemDetails) = exception switch
        {
            ValidationException validationEx => CreateValidationProblemDetails(context, validationEx),
            BadHttpRequestException badReqEx => CreateProblemDetails(
                context,
                badReqEx.StatusCode,
                "BAD_REQUEST",
                "Bad Request",
                badReqEx.Message,
                "https://tools.ietf.org/html/rfc7231#section-6.5.1"),
            KeyNotFoundException notFoundEx => CreateProblemDetails(
                context,
                StatusCodes.Status404NotFound,
                "RESOURCE_NOT_FOUND",
                "Resource Not Found",
                notFoundEx.Message,
                "https://tools.ietf.org/html/rfc7231#section-6.5.4"),
            UnauthorizedAccessException unauthorizedEx => CreateProblemDetails(
                context,
                StatusCodes.Status401Unauthorized,
                "UNAUTHORIZED",
                "Unauthorized",
                unauthorizedEx.Message,
                "https://tools.ietf.org/html/rfc7235#section-3.1"),
            InvalidOperationException invalidOpEx => CreateProblemDetails(
                context,
                StatusCodes.Status400BadRequest,
                "INVALID_OPERATION",
                "Invalid Operation",
                invalidOpEx.Message,
                "https://tools.ietf.org/html/rfc7231#section-6.5.1"),
            ArgumentException argEx => CreateProblemDetails(
                context,
                StatusCodes.Status400BadRequest,
                "INVALID_ARGUMENT",
                "Invalid Argument",
                argEx.Message,
                "https://tools.ietf.org/html/rfc7231#section-6.5.1"),
            _ => CreateServerErrorProblemDetails(context, exception)
        };

        // Attach common RFC 7807 extension attributes
        problemDetails.Extensions["errorCode"] = errorCode;
        problemDetails.Extensions["traceId"] = traceId;
        problemDetails.Extensions["spanId"] = spanId;
        problemDetails.Extensions["timestamp"] = timestamp;

        if (statusCode >= 500)
        {
            _logger.LogError(
                exception,
                "Unhandled server exception processing request {Method} {Path}. ErrorCode: {ErrorCode}, TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                errorCode,
                traceId);
        }
        else
        {
            _logger.LogWarning(
                "Request failed with client error {StatusCode} ({ErrorCode}) on {Method} {Path}: {Message}. TraceId: {TraceId}",
                statusCode,
                errorCode,
                context.Request.Method,
                context.Request.Path,
                problemDetails.Detail,
                traceId);
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json; charset=utf-8";

        await JsonSerializer.SerializeAsync(context.Response.Body, problemDetails, problemDetails.GetType(), JsonOptions);
    }

    private static (int StatusCode, string ErrorCode, ProblemDetails Details) CreateValidationProblemDetails(
        HttpContext context,
        ValidationException validationEx)
    {
        var errors = validationEx.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).Distinct().ToArray()
            );

        var details = new ValidationProblemDetails(errors)
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "Validation Error",
            Status = StatusCodes.Status400BadRequest,
            Detail = "One or more validation errors occurred.",
            Instance = context.Request.Path
        };

        return (StatusCodes.Status400BadRequest, "VALIDATION_ERROR", details);
    }

    private static (int StatusCode, string ErrorCode, ProblemDetails Details) CreateProblemDetails(
        HttpContext context,
        int statusCode,
        string errorCode,
        string title,
        string detail,
        string type)
    {
        var details = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = context.Request.Path
        };

        return (statusCode, errorCode, details);
    }

    private (int StatusCode, string ErrorCode, ProblemDetails Details) CreateServerErrorProblemDetails(
        HttpContext context,
        Exception exception)
    {
        var isDevelopment = _environment.IsDevelopment();
        var details = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Title = "Internal Server Error",
            Status = StatusCodes.Status500InternalServerError,
            Detail = isDevelopment
                ? exception.Message
                : "An unexpected error occurred while processing your request. Please reference the trace ID when contacting support.",
            Instance = context.Request.Path
        };

        if (isDevelopment)
        {
            details.Extensions["exceptionDetails"] = exception.ToString();
        }

        return (StatusCodes.Status500InternalServerError, "INTERNAL_SERVER_ERROR", details);
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "general";
        }

        if (value.Length == 1)
        {
            return value.ToLowerInvariant();
        }

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }
}
