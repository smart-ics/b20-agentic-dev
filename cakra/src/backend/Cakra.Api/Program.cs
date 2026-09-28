using System.Diagnostics;
using System.Text.Json;
using Cakra.Api.Extensions;
using Cakra.Api.Infrastructure.Authentication;
using Cakra.Api.Infrastructure.HealthChecks;
using Cakra.Api.Infrastructure.Logging;
using Cakra.Api.Infrastructure.Migrations;
using Cakra.Api.Infrastructure.Persistence;
using Cakra.Api.Middleware;
using Cakra.Core;
using Cakra.Core.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Architecture §19.9: Serilog structured JSON logging enriched with
// TraceId, SpanId, UserId, PersonId, and SourceContext.
builder.ConfigureCakraSerilog();

// Architecture §19.10: configuration via appsettings or the
// ConnectionStrings__DefaultConnection environment variable.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
}

// Architecture §19.6: REST API with System.Text.Json using camelCase naming policy.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Architecture §19.5: Authentication & Authorization services (P2-S10 concrete)
builder.Services.AddCakraAuthentication();
builder.Services.AddAuthorization();

// Architecture §19.2: DI container + module bootstrappers (P1-S04). The host
// discovers every IModule in the deployed Cakra.Modules.* assemblies, registers
// the shared core services, and wires the MediatR + FluentValidation pipeline.
var moduleAssemblies = ModuleRegistrationExtensions.DiscoverModuleAssemblies();
builder.Services.AddCakraCore(moduleAssemblies);
builder.Services.AddCakraModules(moduleAssemblies);

// Architecture §19.3: database connection factory + DbUp migration runner.
builder.Services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(connectionString ?? string.Empty));
builder.Services.AddSingleton(sp => new DatabaseMigrationRunner(
    connectionString ?? string.Empty,
    sp.GetRequiredService<ILogger<DatabaseMigrationRunner>>()));

// Architecture §19.9: database connectivity health check.
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "db", "ready" });

var app = builder.Build();

// Architecture §19.10: standalone CLI migration switch
// (`dotnet Cakra.Api.dll --migrate`). Runs migrations and exits without serving traffic.
if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    var migrateConnectionString = !string.IsNullOrWhiteSpace(connectionString)
        ? connectionString
        : "Server=(localdb)\\MSSQLLocalDB;Database=CakraDb;Integrated Security=True;TrustServerCertificate=True;";

    var migrationResult = DatabaseMigrationRunner.Run(migrateConnectionString, app.Logger);
    return migrationResult.Successful ? 0 : 1;
}

// Architecture §19.3, §19.10: apply idempotent migrations at startup before serving traffic
// when Database:RunMigrationsOnStartup is true (default true in Production) or in Production environment.
var runMigrationsOnStartup = builder.Configuration.GetValue<bool?>("Database:RunMigrationsOnStartup")
    ?? (app.Environment.IsProduction() || true);
if (runMigrationsOnStartup && !string.IsNullOrWhiteSpace(connectionString))
{
    var migrationResult = DatabaseMigrationRunner.Run(connectionString, app.Logger);
    if (!migrationResult.Successful)
    {
        throw new InvalidOperationException(
            "Database migration failed during application startup.", migrationResult.Error);
    }
}
else if (string.IsNullOrWhiteSpace(connectionString))
{
    app.Logger.LogWarning(
        "ConnectionStrings:DefaultConnection is not configured; skipping database migrations.");
}

// ===========================================================================
// HTTP Request Pipeline (Architecture §19.6, §19.9, §19.10; P1-S06, P7-S39)
// The middleware pipeline is registered in the exact canonical order:
//   1. UseSerilogRequestLogging() — request/response structured logging
//   2. Global exception handling middleware — produces RFC 7807 ProblemDetails JSON
//   3. UseDefaultFiles() & UseStaticFiles() — serves SPA assets from wwwroot/
//   4. UseRouting()
//   5. UseAuthentication() — concrete cookie handler wired in P2-S10
//   6. UseAuthorization()
//   7. MapControllers() & MapFallbackToFile("index.html") — endpoint mapping
// ===========================================================================

// 1. UseSerilogRequestLogging() — request/response logging
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            diagnosticContext.Set("TraceId", activity.TraceId.ToHexString());
            diagnosticContext.Set("SpanId", activity.SpanId.ToHexString());
        }
        else
        {
            diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
        }

        var contextProvider = httpContext.RequestServices.GetService<ICurrentContextProvider>();
        if (contextProvider != null)
        {
            if (contextProvider.CurrentUserId.HasValue)
            {
                diagnosticContext.Set("UserId", contextProvider.CurrentUserId.Value);
            }
            if (contextProvider.CurrentPersonId.HasValue)
            {
                diagnosticContext.Set("PersonId", contextProvider.CurrentPersonId.Value);
            }
        }
    };
});

// 2. Global exception handling middleware — produces RFC 7807 ProblemDetails JSON
app.UseGlobalExceptionHandler();

// 3. UseDefaultFiles() & UseStaticFiles() — serves SPA assets from wwwroot/
app.UseDefaultFiles();
app.UseStaticFiles();

// Configure OpenAPI in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 4. UseRouting()
app.UseRouting();

// 5. UseAuthentication() — concrete cookie handler wired in P2-S10
app.UseAuthentication();
app.UseSecurityContext();

// 6. UseAuthorization()
app.UseAuthorization();
app.UseAuditLogging();

// 7. MapControllers() — endpoint mapping
app.MapControllers();

// Health check endpoints (Architecture §19.9):
//   /health/live  - Liveness probe confirming process is responsive
//   /health/ready - Readiness probe validating SQL Server connectivity
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// SPA history fallback (Architecture §19.10): non-/api and non-/health routes
// fall back to wwwroot/index.html so the Vue 3 SPA is served at / and deep links.
app.MapFallbackToFile("index.html");

app.Run();

return 0;

// Exposed so integration tests can host the API in-process via WebApplicationFactory<Program>
// (Architecture §19.8).
public partial class Program { }
