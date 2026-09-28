using System.Data;
using Cakra.Core.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace Cakra.Api.Infrastructure.Persistence;

/// <summary>
/// SQL Server implementation of <see cref="IDbConnectionFactory"/> backed by
/// <see cref="SqlConnection"/> (<c>Microsoft.Data.SqlClient</c>).
/// The connection string is supplied by the composition root from
/// <c>ConnectionStrings:DefaultConnection</c> (or the
/// <c>ConnectionStrings__DefaultConnection</c> environment variable) — Architecture §19.3, §19.10.
/// </summary>
public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString ?? string.Empty;
    }

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}
