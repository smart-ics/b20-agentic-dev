using Cakra.Core.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cakra.Api.Infrastructure.HealthChecks;

/// <summary>
/// Readiness health check that confirms SQL Server connectivity by opening a
/// connection from the <see cref="IDbConnectionFactory"/> and executing a
/// trivial command. Registered against the <c>/health/ready</c> endpoint —
/// Architecture §19.9.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DatabaseHealthCheck(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            command.ExecuteScalar();

            return Task.FromResult(HealthCheckResult.Healthy("SQL Server connection confirmed."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy("SQL Server connection failed.", ex));
        }
    }
}
