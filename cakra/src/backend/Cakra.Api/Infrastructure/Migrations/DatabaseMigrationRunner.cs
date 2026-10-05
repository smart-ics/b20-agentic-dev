using DbUp;
using DbUp.Engine;
using Microsoft.Extensions.Logging;

namespace Cakra.Api.Infrastructure.Migrations;

/// <summary>
/// DbUp migration runner for CAKRA. Discovers embedded raw SQL migration scripts
/// under <c>Migrations/Scripts</c> and executes them in strict dependency order
/// (ascending script name) against SQL Server. DbUp's journal table
/// (<c>dbo.SchemaVersions</c>) records applied scripts, so reruns are idempotent
/// and only new scripts are applied — Architecture §19.3, §19.10.
/// </summary>
public sealed class DatabaseMigrationRunner
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseMigrationRunner> _logger;

    public DatabaseMigrationRunner(string connectionString, ILogger<DatabaseMigrationRunner> logger)
    {
        _connectionString = connectionString ?? string.Empty;
        _logger = logger;
    }

    /// <summary>
    /// Ensures the target database exists, then applies all pending embedded
    /// migration scripts in order.
    /// </summary>
    /// <returns>The DbUp upgrade result; callers should check <c>Successful</c>.</returns>
    public DatabaseUpgradeResult Run() => Run(_connectionString, _logger);

    /// <summary>
    /// Static entrypoint for executing DbUp SQL Server migrations against the supplied
    /// <paramref name="connectionString"/> (Architecture §19.3, §19.10).
    /// </summary>
    public static DatabaseUpgradeResult Run(string connectionString, ILogger? logger = null)
    {
        // Creates the database if it does not already exist.
        EnsureDatabase.For.SqlDatabase(connectionString);

        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                typeof(DatabaseMigrationRunner).Assembly,
                resourceName => resourceName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .WithoutTransaction()
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (result.Successful)
        {
            foreach (var script in result.Scripts)
            {
                logger?.LogInformation("Applied database migration script {ScriptName}", script.Name);
            }
        }
        else
        {
            logger?.LogError(result.Error, "Database migration failed");
        }

        return result;
    }
}
