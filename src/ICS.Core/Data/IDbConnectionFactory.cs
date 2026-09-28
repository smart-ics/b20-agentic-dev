namespace ICS.Core.Data;

using Microsoft.Data.SqlClient;

/// <summary>
/// Factory abstraction for creating and opening SQL Server database connections.
/// Architecture §19.3 & §19.10.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Creates a new, unopened <see cref="SqlConnection"/>.
    /// </summary>
    SqlConnection CreateConnection();

    /// <summary>
    /// Creates and asynchronously opens a new <see cref="SqlConnection"/>.
    /// </summary>
    Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
