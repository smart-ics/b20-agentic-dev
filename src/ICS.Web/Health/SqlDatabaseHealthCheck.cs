namespace ICS.Web.Health;

using Dapper;
using ICS.Core.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Health check that verifies SQL Server connectivity via IDbConnectionFactory.
/// Architecture §19.9 & §19.10.
/// </summary>
public class SqlDatabaseHealthCheck : IHealthCheck
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<SqlDatabaseHealthCheck> _logger;

    public SqlDatabaseHealthCheck(IDbConnectionFactory connectionFactory, ILogger<SqlDatabaseHealthCheck> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var result = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1;", cancellationToken: cancellationToken));

            if (result == 1)
            {
                return HealthCheckResult.Healthy("SQL Server database connectivity confirmed.");
            }

            return HealthCheckResult.Unhealthy("SQL Server ping query returned unexpected response.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SQL Server health check probe failed.");
            return HealthCheckResult.Unhealthy("SQL Server database connectivity failed.", ex);
        }
    }
}
