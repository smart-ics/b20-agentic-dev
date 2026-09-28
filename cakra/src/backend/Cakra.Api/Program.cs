using Cakra.Api.Infrastructure.HealthChecks;
using Cakra.Api.Infrastructure.Migrations;
using Cakra.Api.Infrastructure.Persistence;
using Cakra.Core.Infrastructure.Persistence;

using Cakra.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Architecture §19.10: configuration via appsettings or the
// ConnectionStrings__DefaultConnection environment variable.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        app.Logger.LogError("ConnectionStrings:DefaultConnection is not configured; cannot run migrations.");
        return 1;
    }

    var migrationResult = app.Services.GetRequiredService<DatabaseMigrationRunner>().Run();
    return migrationResult.Successful ? 0 : 1;
}

// Architecture §19.3, §19.10: apply idempotent migrations at startup before serving traffic.
if (!string.IsNullOrWhiteSpace(connectionString))
{
    var migrationResult = app.Services.GetRequiredService<DatabaseMigrationRunner>().Run();
    if (!migrationResult.Successful)
    {
        throw new InvalidOperationException(
            "Database migration failed during application startup.", migrationResult.Error);
    }
}
else
{
    app.Logger.LogWarning(
        "ConnectionStrings:DefaultConnection is not configured; skipping database migrations.");
}

// ---------------------------------------------------------------------------
// SPA static asset hosting (P1-S08 — Architecture §19.10).
// Serves the Vite-built Vue 3 SPA (src/frontend/Cakra.Web/dist → wwwroot/)
// with an index.html fallback for client-side Vue Router routes.
// NOTE: P1-S06 owns the canonical middleware pipeline order and will position
// static-file serving at its documented step 3. This call is isolated in the
// SpaStaticFilesExtensions helper for that integration.
// ---------------------------------------------------------------------------
app.UseCakraSpa();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Architecture §19.9: readiness endpoint validating SQL Server connectivity.
app.MapHealthChecks("/health/ready");

app.Run();

return 0;

// Exposed so integration tests can host the API in-process via WebApplicationFactory<Program>
// (Architecture §19.8). P1-S06 owns the real application pipeline.
public partial class Program { }
