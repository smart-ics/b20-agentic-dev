namespace ICS.Core.Data;

using Microsoft.Data.SqlClient;

/// <summary>
/// Concrete implementation of <see cref="IDbConnectionFactory"/> producing <see cref="SqlConnection"/> instances.
/// Architecture §19.3 & §19.10.
/// </summary>
public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of <see cref="SqlConnectionFactory"/> with the specified connection string.
    /// </summary>
    /// <param name="connectionString">The SQL Server connection string.</param>
    public SqlConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Database connection string cannot be null or empty.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    /// <inheritdoc />
    public SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    /// <inheritdoc />
    public async Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
