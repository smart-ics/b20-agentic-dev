namespace ICS.Tests.Integration;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ICS.Core.Data;
using Microsoft.Data.SqlClient;
using Respawn;
using Respawn.Graph;

/// <summary>
/// Manages Respawn-based database reset utility for test isolation per Architecture §19.8.
/// Resets all module schemas between test executions while preserving migration journal history.
/// </summary>
public sealed class DatabaseResetHelper
{
    private readonly string _connectionString;
    private Respawner? _respawner;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _initialized;

    public DatabaseResetHelper(string connectionString)
    {
        _connectionString = string.IsNullOrWhiteSpace(connectionString)
            ? throw new ArgumentNullException(nameof(connectionString))
            : connectionString;
    }

    /// <summary>
    /// Gets the connection string used for database resets.
    /// </summary>
    public string ConnectionString => _connectionString;

    /// <summary>
    /// Initializes the Respawner instance against the target test database.
    /// Safely handles the state where no business tables have been created yet.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized && _respawner != null)
        {
            return;
        }

        await _semaphore.WaitAsync();
        try
        {
            if (_initialized && _respawner != null)
            {
                return;
            }

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            try
            {
                _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
                {
                    DbAdapter = DbAdapter.SqlServer,
                    SchemasToInclude = DatabaseSchemas.All.ToArray(),
                    TablesToIgnore =
                    [
                        new Table("SchemaVersions")
                    ]
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("No tables found", StringComparison.OrdinalIgnoreCase))
            {
                // When module schemas exist but no business tables have been created yet,
                // Respawn throws InvalidOperationException. We treat this as an empty state.
                _respawner = null;
            }

            _initialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Resets all data in the configured bounded context schemas.
    /// </summary>
    public async Task ResetAsync()
    {
        if (!_initialized || _respawner == null)
        {
            await InitializeAsync();
        }

        // If no tables exist yet, nothing to reset
        if (_respawner == null)
        {
            return;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }
}
