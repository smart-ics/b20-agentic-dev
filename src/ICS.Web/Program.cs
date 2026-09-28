using System.Text.Json;
using ICS.Core;
using ICS.Core.Audit;
using ICS.Core.Auth;
using ICS.Core.Data;
using ICS.Core.Domain;
using ICS.Core.Domain.Verification;
using ICS.Core.Modules;
using ICS.Core.Validation;
using ICS.Web.Auth;
using ICS.Web.Diagnostics;
using ICS.Web.Health;
using ICS.Web.Logging;
using ICS.Web.Middleware;
using ICS.Web.Migrations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

// 1. Initialize Serilog early bootstrap logger to capture host startup errors
Log.Logger = SerilogConfiguration.CreateBootstrapLogger();

try
{
    Log.Information("Starting ICS Operational System host bootstrap...");

    var builder = WebApplication.CreateBuilder(args);

    // 2. Configure Serilog with structured JSON logging and ambient enrichers (Architecture §19.9)
    builder.ConfigureSerilog();

    // 3. Configure System.Text.Json camelCase naming policy across HTTP JSON & Controllers (Architecture §19.6)
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        });

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    });

    // 4. Define all bounded context module assemblies for auto-discovery
    var moduleAssemblies = new[]
    {
        typeof(CoreServiceExtensions).Assembly,
        typeof(ICS.Modules.Identity.IdentityModule).Assembly,
        typeof(ICS.Modules.Organization.OrganizationModule).Assembly,
        typeof(ICS.Modules.Customer.CustomerModule).Assembly,
        typeof(ICS.Modules.Product.ProductModule).Assembly,
        typeof(ICS.Modules.WorkPackage.WorkPackageModule).Assembly,
        typeof(ICS.Modules.Request.RequestModule).Assembly,
        typeof(ICS.Modules.Post.PostModule).Assembly,
        typeof(ICS.Modules.Analytics.AnalyticsModule).Assembly,
    };

    // 5. Core Services: ISystemClock, ICurrentContextProvider, IAuditContext, MediatR with ValidationBehavior
    builder.Services.AddCoreServices(moduleAssemblies);

    // 6. Database Infrastructure: IDbConnectionFactory (Singleton)
    builder.Services.AddDatabaseInfrastructure(builder.Configuration);

    // 7. Modular Monolith Auto-Discovery: Invokes IModule.RegisterServices on all module bootstrappers
    builder.Services.AddModules(builder.Configuration, moduleAssemblies);

    // 8. Authentication & Authorization: Cookie Authentication (ICS_SESSION) and RBAC (Architecture §14, §19.5)
    builder.Services.AddIcsAuthentication(builder.Configuration);

    // 9. Health Checks: Liveness & Readiness with SQL Server connectivity check (Architecture §19.9)
    builder.Services.AddHealthChecks()
        .AddCheck<SqlDatabaseHealthCheck>("sqlserver", tags: ["ready", "db"]);

    var isTestingEnvironment = builder.Environment.IsEnvironment("Testing")
        || string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Testing", StringComparison.OrdinalIgnoreCase);

    var connectionString = (isTestingEnvironment
        ? (Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
            ?? builder.Configuration.GetConnectionString("TestConnection")
            ?? builder.Configuration["ConnectionStrings:TestConnection"]
            ?? builder.Configuration.GetConnectionString(DatabaseServiceExtensions.DefaultConnectionStringKey)
            ?? builder.Configuration["ConnectionStrings:DefaultConnection"]
            ?? "Server=localhost;Database=ICS_Test;Trusted_Connection=True;TrustServerCertificate=True;")
        : (builder.Configuration.GetConnectionString(DatabaseServiceExtensions.DefaultConnectionStringKey)
            ?? builder.Configuration["ConnectionStrings:DefaultConnection"]
            ?? builder.Configuration["ConnectionStrings__DefaultConnection"]
            ?? "Server=localhost;Database=ICS;Trusted_Connection=True;TrustServerCertificate=True;"));

    // CLI Migration switch support: 'dotnet ICS.Web.dll --migrate'
    if (args.Contains("--migrate"))
    {
        Console.WriteLine("Executing standalone database migrations (--migrate)...");
        var migrationResult = DbUpMigrationRunner.Run(connectionString);
        if (!migrationResult.Successful)
        {
            Console.Error.WriteLine($"Database migration failed: {migrationResult.Error?.Message}");
            Environment.Exit(1);
        }

        Console.WriteLine("Database migrations completed successfully.");
        return;
    }

    var app = builder.Build();

    // CLI DI verification switch support: 'dotnet ICS.Web.dll --verify-di'
    if (args.Contains("--verify-di"))
    {
        Console.WriteLine("Executing Dependency Injection Verification (--verify-di)...");
        DependencyInjectionValidator.VerifyContainer(app.Services, app.Logger);
        Console.WriteLine("Dependency Injection Verification succeeded.");
        return;
    }

    // CLI Event Bus verification switch support: 'dotnet ICS.Web.dll --verify-events'
    if (args.Contains("--verify-events"))
    {
        Console.WriteLine("Executing Domain Event Bus Verification (--verify-events)...");
        DomainEventDispatcherValidator.Verify(app.Services, app.Logger);
        Console.WriteLine("Domain Event Bus Verification succeeded.");
        return;
    }

    // CLI Pipeline verification switch support: 'dotnet ICS.Web.dll --verify-pipeline'
    if (args.Contains("--verify-pipeline"))
    {
        Console.WriteLine("Executing Application Pipeline Verification (--verify-pipeline)...");
        PipelineValidator.Verify(app.Services, app.Logger);
        Console.WriteLine("Application Pipeline Verification succeeded.");
        return;
    }

    // Startup Diagnostic Verifications
    DependencyInjectionValidator.VerifyContainer(app.Services, app.Logger);
    DomainEventDispatcherValidator.Verify(app.Services, app.Logger);
    PipelineValidator.Verify(app.Services, app.Logger);

    // Run DbUp migrations at application startup before serving HTTP traffic (Architecture §19.10)
    var isAppTestingEnvironment = app.Environment.IsEnvironment("Testing")
        || string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Testing", StringComparison.OrdinalIgnoreCase);

    var effectiveConnectionString = (isAppTestingEnvironment
        ? (Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
            ?? app.Configuration.GetConnectionString("TestConnection")
            ?? app.Configuration["ConnectionStrings:TestConnection"]
            ?? app.Configuration.GetConnectionString(DatabaseServiceExtensions.DefaultConnectionStringKey)
            ?? app.Configuration["ConnectionStrings:DefaultConnection"]
            ?? "Server=localhost;Database=ICS_Test;Trusted_Connection=True;TrustServerCertificate=True;")
        : (app.Configuration.GetConnectionString(DatabaseServiceExtensions.DefaultConnectionStringKey)
            ?? app.Configuration["ConnectionStrings:DefaultConnection"]
            ?? app.Configuration["ConnectionStrings__DefaultConnection"]
            ?? connectionString));

    app.Logger.LogInformation("Applying database migrations at startup to target database...");
    DbUpMigrationRunner.Run(effectiveConnectionString, app.Logger);

    // -----------------------------------------------------------------------------------------
    // Documented HTTP Request Pipeline Middleware Registration Order (Architecture §5, §18, §19.6, §19.9)
    // 1. Serilog Request Logging: HTTP request metrics and diagnostic context enrichment
    // 2. Exception Handling: Centralized exception handling returning RFC 7807 ProblemDetails
    // 3. Logging Context: Ambient TraceId, SpanId, UserId, and PersonId pushed into Serilog LogContext
    // 4. Security Context (Placeholder): Extracts ambient user/person into ICurrentContextAccessor
    // 5. Audit Logging Hook: Enriches IAuditContextAccessor with client IP, user agent, and mutation logs
    // 6. Routing & Endpoints: Dispatches controllers, minimal APIs, and health checks
    // -----------------------------------------------------------------------------------------
    app.UseIcsApplicationPipeline();

    // ASP.NET Core Health Checks (Architecture §19.9)
    // /health/live: Application process liveness probe
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    });

    // /health/ready: Application readiness probe validating SQL Server connectivity
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

    // System Module: /api/v1/system/info - Host Status & System Information
    app.MapGet("/api/v1/system/info", () => Results.Ok(new
    {
        status = "healthy",
        system = "ICS Operational System",
        version = "1.0.0"
    }));

    // REST API Base Route Convention: /api/v1/{module}/{resource} (Architecture §19.6)

    // System Module: /api/v1/system/modules
    app.MapGet("/api/v1/system/modules", (IServiceProvider sp) =>
    {
        var modules = sp.GetRegisteredModules().Select(m => m.Name);
        return Results.Ok(new { modules });
    });

    // System Module: /api/v1/system/context - Returns ambient security and audit context
    app.MapGet("/api/v1/system/context", (ICurrentContextProvider currentContext, IAuditContext auditContext) =>
    {
        return Results.Ok(new
        {
            security = new
            {
                userId = currentContext.CurrentUserId,
                personId = currentContext.CurrentPersonId,
                roles = currentContext.CurrentRoles,
                isAuthenticated = currentContext.IsAuthenticated
            },
            audit = new
            {
                userId = auditContext.UserId,
                personId = auditContext.PersonId,
                timestamp = auditContext.Timestamp,
                ipAddress = auditContext.IpAddress,
                userAgent = auditContext.UserAgent
            }
        });
    });

    // System Module: /api/v1/system/protected - Protected endpoint requiring authentication (Architecture §14, §19.5)
    app.MapGet("/api/v1/system/protected", (ICurrentContextProvider currentContext) => Results.Ok(new
    {
        message = "Protected minimal API accessed successfully.",
        userId = currentContext.CurrentUserId,
        personId = currentContext.CurrentPersonId,
        roles = currentContext.CurrentRoles,
        isAuthenticated = currentContext.IsAuthenticated
    })).RequireAuthorization();

    // System Module: /api/v1/system/management - Protected endpoint requiring 'Management' role (Architecture §14, §18)
    app.MapGet("/api/v1/system/management", (ICurrentContextProvider currentContext) => Results.Ok(new
    {
        message = "Management minimal API accessed successfully.",
        userId = currentContext.CurrentUserId,
        personId = currentContext.CurrentPersonId,
        roles = currentContext.CurrentRoles,
        isAuthenticated = currentContext.IsAuthenticated
    })).RequireAuthorization(new AuthorizeAttribute { Roles = "Management" });

    // System Module: /api/v1/system/ping & /api/v1/ping (PingCommand with FluentValidation via MediatR)
    app.MapPost("/api/v1/system/ping", async (PingCommand command, IMediator mediator) =>
    {
        var result = await mediator.Send(command);
        return Results.Ok(result);
    });

    app.MapPost("/api/v1/ping", async (PingCommand command, IMediator mediator) =>
    {
        var result = await mediator.Send(command);
        return Results.Ok(result);
    });

    // System Module: /api/v1/system/events/test - In-process domain event dispatching
    app.MapPost("/api/v1/system/events/test", async (IDomainEventDispatcher dispatcher, IEventExecutionJournal journal) =>
    {
        var testEvent = new SampleMultiHandledDomainEvent("HTTP-Triggered-Domain-Event");
        await dispatcher.PublishAsync(testEvent);
        var handlersInvoked = journal.GetInvocations(testEvent.EventId);
        return Results.Ok(new
        {
            eventId = testEvent.EventId,
            occurredAt = testEvent.OccurredAt,
            handlersInvoked
        });
    });

    // System Module: /api/v1/system/errors/{errorType} - Endpoint to verify RFC 7807 ProblemDetails responses
    app.MapGet("/api/v1/system/errors/{errorType}", (string errorType) =>
    {
        switch (errorType.ToLowerInvariant())
        {
            case "validation":
                throw new FluentValidation.ValidationException(new[]
                {
                    new FluentValidation.Results.ValidationFailure("TestProperty", "TestProperty is invalid.")
                });
            case "notfound":
                throw new KeyNotFoundException("The requested test resource was not found.");
            case "unauthorized":
                throw new UnauthorizedAccessException("Access denied to the test resource.");
            case "invalidoperation":
                throw new InvalidOperationException("Operation cannot be completed in current state.");
            default:
                throw new Exception("Simulated unhandled internal server error.");
        }
    });

    app.MapControllers();

    // Fallback route for Single Page Application client-side navigation (Architecture §19.4, §19.10)
    app.MapFallbackToFile("index.html");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "ICS Operational System host terminated unexpectedly during startup.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Required for WebApplicationFactory integration testing (Architecture §19.8)
public partial class Program { }
