using System.Data;

namespace Cakra.Core.Infrastructure.Persistence;

/// <summary>
/// Creates ADO.NET database connections for the CAKRA persistence layer.
/// All data access uses Dapper with explicit parameterized SQL over the
/// connections produced by this factory — see Architecture §19.3.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Creates a new, closed database connection. The caller owns the
    /// connection and is responsible for opening and disposing it.
    /// </summary>
    IDbConnection CreateConnection();
}
