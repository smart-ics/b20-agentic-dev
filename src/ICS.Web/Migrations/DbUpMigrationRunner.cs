namespace ICS.Web.Migrations;

using System.Reflection;
using DbUp;
using DbUp.Engine;
using Microsoft.Extensions.Logging;

/// <summary>
/// Executes idempotent sequential SQL migrations via DbUp against SQL Server.
/// Architecture §17, §19.3, and §19.10.
/// </summary>
public static class DbUpMigrationRunner
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);

    /// <summary>
    /// Ensures database existence and executes pending migrations in strict sequential order.
    /// </summary>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <param name="logger">Optional logger for operational logging.</param>
    /// <returns>The result of the database upgrade operation.</returns>
    public static DatabaseUpgradeResult Run(string connectionString, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
        }

        MigrationLock.Wait();
        try
        {
            logger?.LogInformation("Ensuring target database exists...");
            EnsureDatabase.For.SqlDatabase(connectionString);

            logger?.LogInformation("Discovering and executing sequential idempotent SQL migrations via DbUp...");

            var upgrader = DeployChanges.To
                .SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly(), scriptName => scriptName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                .JournalToSqlTable("dbo", "SchemaVersions")
                .WithTransactionPerScript()
                .LogToConsole()
                .Build();

            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
            {
                logger?.LogError(result.Error, "Database migration failed: {ErrorMessage}", result.Error?.Message);
                throw new InvalidOperationException($"Database migration failed: {result.Error?.Message}", result.Error);
            }

            logger?.LogInformation("Database migrations applied successfully. Applied scripts count: {Count}", result.Scripts.Count());
            return result;
        }
        finally
        {
            MigrationLock.Release();
        }
    }
}
