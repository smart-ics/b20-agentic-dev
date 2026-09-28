using System.IO;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using ICS.Core.Audit;
using ICS.Core.Auth;
using ICS.Core.Validation;
using ICS.Web.Middleware;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ICS.Web.Diagnostics;

/// <summary>
/// Runtime validation suite for slice P1-S06: Application Pipeline Foundation.
/// Verifies centralized exception handling (RFC 7807 ProblemDetails), camelCase serialization,
/// security context middleware, audit logging middleware, and MediatR validation pipeline behavior.
/// </summary>
public static class PipelineValidator
{
    public static void Verify(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("Beginning Application Pipeline Foundation verification (P1-S06)...");

        // 1. Verify ExceptionHandlingMiddleware produces RFC 7807 ValidationProblemDetails on ValidationException
        VerifyValidationExceptionHandling(services, logger);

        // 2. Verify ExceptionHandlingMiddleware produces RFC 7807 ProblemDetails on KeyNotFoundException
        VerifyNotFoundExceptionHandling(services, logger);

        // 3. Verify ExceptionHandlingMiddleware produces RFC 7807 ProblemDetails on unhandled Exception
        VerifyServerExceptionHandling(services, logger);

        // 4. Verify SecurityContextMiddleware populates ICurrentContextAccessor
        VerifySecurityContextMiddleware(services, logger);

        // 5. Verify AuditLoggingMiddleware populates IAuditContextAccessor
        VerifyAuditLoggingMiddleware(services, logger);

        // 6. Verify MediatR validation pipeline behavior end-to-end
        VerifyMediatRValidationPipeline(services, logger);

        logger.LogInformation("Application Pipeline Foundation verification (P1-S06) completed successfully!");
    }

    private static void VerifyValidationExceptionHandling(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("Verifying RFC 7807 ValidationProblemDetails handling...");

        var environment = services.GetRequiredService<IHostEnvironment>();
        var middlewareLogger = services.GetRequiredService<ILogger<ExceptionHandlingMiddleware>>();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ValidationException(new[]
            {
                new ValidationFailure("Message", "Message is required."),
                new ValidationFailure("Message", "Message must be non-empty.")
            }),
            middlewareLogger,
            environment);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/v1/ping";

        middleware.InvokeAsync(context).GetAwaiter().GetResult();

        if (context.Response.StatusCode != StatusCodes.Status400BadRequest)
        {
            throw new InvalidOperationException($"Expected HTTP 400 for ValidationException, got {context.Response.StatusCode}");
        }

        if (!context.Response.ContentType?.Contains("application/problem+json") ?? true)
        {
            throw new InvalidOperationException($"Expected Content-Type 'application/problem+json', got '{context.Response.ContentType}'");
        }

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = reader.ReadToEnd();

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var status = root.GetProperty("status").GetInt32();
        var errorCode = root.GetProperty("errorCode").GetString();
        var errors = root.GetProperty("errors");
        var hasMessageErrors = errors.TryGetProperty("message", out var messageProp);

        if (status != 400 || errorCode != "VALIDATION_ERROR" || !hasMessageErrors)
        {
            throw new InvalidOperationException($"Validation ProblemDetails JSON structure incorrect: {responseBody}");
        }

        logger.LogInformation("RFC 7807 ValidationProblemDetails verified with camelCase errors dictionary.");
    }

    private static void VerifyNotFoundExceptionHandling(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("Verifying RFC 7807 NotFound ProblemDetails handling...");

        var environment = services.GetRequiredService<IHostEnvironment>();
        var middlewareLogger = services.GetRequiredService<ILogger<ExceptionHandlingMiddleware>>();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new KeyNotFoundException("Entity with ID 123 was not found."),
            middlewareLogger,
            environment);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/v1/requests/123";

        middleware.InvokeAsync(context).GetAwaiter().GetResult();

        if (context.Response.StatusCode != StatusCodes.Status404NotFound)
        {
            throw new InvalidOperationException($"Expected HTTP 404 for KeyNotFoundException, got {context.Response.StatusCode}");
        }

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = reader.ReadToEnd();

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var errorCode = root.GetProperty("errorCode").GetString();
        if (errorCode != "RESOURCE_NOT_FOUND")
        {
            throw new InvalidOperationException($"Expected errorCode 'RESOURCE_NOT_FOUND', got '{errorCode}'");
        }

        logger.LogInformation("RFC 7807 NotFound ProblemDetails verified.");
    }

    private static void VerifyServerExceptionHandling(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("Verifying RFC 7807 500 Server Error ProblemDetails handling...");

        var environment = services.GetRequiredService<IHostEnvironment>();
        var middlewareLogger = services.GetRequiredService<ILogger<ExceptionHandlingMiddleware>>();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ApplicationException("Simulated unexpected crash"),
            middlewareLogger,
            environment);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/v1/system/error";

        middleware.InvokeAsync(context).GetAwaiter().GetResult();

        if (context.Response.StatusCode != StatusCodes.Status500InternalServerError)
        {
            throw new InvalidOperationException($"Expected HTTP 500 for unhandled Exception, got {context.Response.StatusCode}");
        }

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = reader.ReadToEnd();

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var errorCode = root.GetProperty("errorCode").GetString();
        if (errorCode != "INTERNAL_SERVER_ERROR")
        {
            throw new InvalidOperationException($"Expected errorCode 'INTERNAL_SERVER_ERROR', got '{errorCode}'");
        }

        logger.LogInformation("RFC 7807 Server Error ProblemDetails verified.");
    }

    private static void VerifySecurityContextMiddleware(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("Verifying SecurityContextMiddleware...");

        using var scope = services.CreateScope();
        var contextAccessor = scope.ServiceProvider.GetRequiredService<ICurrentContextAccessor>();
        var contextProvider = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>();
        var middlewareLogger = scope.ServiceProvider.GetRequiredService<ILogger<SecurityContextMiddleware>>();

        var middleware = new SecurityContextMiddleware(
            _ => Task.CompletedTask,
            middlewareLogger);

        var context = new DefaultHttpContext();
        var testUserId = Guid.NewGuid();
        var testPersonId = Guid.NewGuid();
        context.Request.Headers[SecurityContextMiddleware.UserIdHeader] = testUserId.ToString();
        context.Request.Headers[SecurityContextMiddleware.PersonIdHeader] = testPersonId.ToString();
        context.Request.Headers[SecurityContextMiddleware.RolesHeader] = "SystemAdministrator, TeamLead";

        middleware.InvokeAsync(context, contextAccessor).GetAwaiter().GetResult();

        if (contextProvider.CurrentUserId != testUserId ||
            contextProvider.CurrentPersonId != testPersonId ||
            !contextProvider.IsInRole("SystemAdministrator") ||
            !contextProvider.IsInRole("TeamLead") ||
            !contextProvider.IsAuthenticated)
        {
            throw new InvalidOperationException("SecurityContextMiddleware failed to populate ambient context correctly.");
        }

        logger.LogInformation("SecurityContextMiddleware ambient population verified.");
    }

    private static void VerifyAuditLoggingMiddleware(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("Verifying AuditLoggingMiddleware...");

        using var scope = services.CreateScope();
        var auditAccessor = scope.ServiceProvider.GetRequiredService<IAuditContextAccessor>();
        var auditContext = scope.ServiceProvider.GetRequiredService<IAuditContext>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>();
        var middlewareLogger = scope.ServiceProvider.GetRequiredService<ILogger<AuditLoggingMiddleware>>();

        var middleware = new AuditLoggingMiddleware(
            _ => Task.CompletedTask,
            middlewareLogger);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "192.168.1.100";
        context.Request.Headers["User-Agent"] = "TestClient/1.0";
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/ping";

        middleware.InvokeAsync(context, auditAccessor, currentContext).GetAwaiter().GetResult();

        if (auditContext.IpAddress != "192.168.1.100" ||
            auditContext.UserAgent != "TestClient/1.0")
        {
            throw new InvalidOperationException("AuditLoggingMiddleware failed to populate IAuditContext correctly.");
        }

        logger.LogInformation("AuditLoggingMiddleware client metadata population verified.");
    }

    private static void VerifyMediatRValidationPipeline(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("Verifying MediatR FluentValidation pipeline behavior...");

        using var scope = services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Valid command
        var validCommand = new PingCommand("Ping Test Message");
        var validResult = mediator.Send(validCommand).GetAwaiter().GetResult();
        if (validResult.Echo != "Ping Test Message")
        {
            throw new InvalidOperationException("Valid ping command failed execution.");
        }

        // Invalid command: empty message
        var invalidCommand = new PingCommand("");
        var validationThrown = false;
        try
        {
            mediator.Send(invalidCommand).GetAwaiter().GetResult();
        }
        catch (ValidationException ex)
        {
            validationThrown = true;
            if (!ex.Errors.Any(e => e.PropertyName == "Message"))
            {
                throw new InvalidOperationException("ValidationException did not contain expected 'Message' error.");
            }
        }

        if (!validationThrown)
        {
            throw new InvalidOperationException("MediatR FluentValidation behavior failed to throw ValidationException on invalid request.");
        }

        logger.LogInformation("MediatR FluentValidation pipeline behavior verified.");
    }
}
