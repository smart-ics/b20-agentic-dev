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
    public DatabaseUpgradeResult Run()
    {
        // Creates the database if it does not already exist.
        EnsureDatabase.For.SqlDatabase(_connectionString);

        var upgrader = DeployChanges.To
            .SqlDatabase(_connectionString)
            .WithScriptsEmbeddedInAssembly(
                typeof(DatabaseMigrationRunner).Assembly,
                resourceName => resourceName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (result.Successful)
        {
            foreach (var script in result.Scripts)
            {
                _logger.LogInformation("Applied database migration script {ScriptName}", script.Name);
            }
        }
        else
        {
            _logger.LogError(result.Error, "Database migration failed");
        }

        return result;
    }
}
