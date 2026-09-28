using Microsoft.Data.SqlClient;
using Respawn;
using Respawn.Graph;

namespace Cakra.Tests.Integration;

/// <summary>
/// Respawn-backed helper that returns the isolated integration-test SQL Server database to a
/// clean state between tests (Architecture §19.8). The test database connection string is
/// resolved from an environment variable so that it remains separate from the application
/// connection string. The Respawner itself is created lazily on first reset.
/// </summary>
public sealed class DatabaseResetHelper
{
    /// <summary>
    /// Environment variable that holds the integration-test database connection string.
    /// </summary>
    public const string ConnectionStringEnvironmentVariable = "CAKRA_TEST_DB_CONNECTION";

    /// <summary>
    /// Fallback environment variable using the standard ASP.NET Core double-underscore
    /// configuration format.
    /// </summary>
    public const string ConnectionStringFallbackEnvironmentVariable = "ConnectionStrings__TestConnection";

    private static readonly Table[] TablesToIgnore =
    {
        // The DbUp migration journal is owned by the host and must survive test resets.
        new("dbo", "__SchemaVersions"),
    };

    private readonly string? _connectionString;
    private readonly Lazy<Task<Respawner>> _respawner;

    public DatabaseResetHelper()
    {
        _connectionString = ResolveConnectionString();
        _respawner = new Lazy<Task<Respawner>>(CreateRespawnerAsync);
    }

    /// <summary>True when a test database connection string has been configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

    /// <summary>The resolved test database connection string, or <c>null</c> when unconfigured.</summary>
    public string? ConnectionString => _connectionString;

    /// <summary>
    /// Resolves the integration-test database connection string from the environment.
    /// Returns <c>null</c> when no test database has been configured.
    /// </summary>
    public static string? ResolveConnectionString()
    {
        var value = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            value = Environment.GetEnvironmentVariable(ConnectionStringFallbackEnvironmentVariable);
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Deletes all data from the test database while preserving ignored infrastructure tables.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no test database connection string has been configured.
    /// </exception>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        if (_connectionString is null)
        {
            throw new InvalidOperationException(
                "No integration-test database connection string is configured. Set the " +
                $"'{ConnectionStringEnvironmentVariable}' environment variable (or " +
                $"'{ConnectionStringFallbackEnvironmentVariable}') to an isolated SQL Server database.");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var respawner = await _respawner.Value.ConfigureAwait(false);
        await respawner.ResetAsync(connection).ConfigureAwait(false);
    }

    private async Task<Respawner> CreateRespawnerAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        return await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToIgnore = TablesToIgnore,
        }).ConfigureAwait(false);
    }
}
