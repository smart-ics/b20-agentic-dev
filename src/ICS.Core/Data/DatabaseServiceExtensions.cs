namespace ICS.Core.Data;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extension methods for registering database persistence infrastructure.
/// </summary>
public static class DatabaseServiceExtensions
{
    public const string DefaultConnectionStringKey = "DefaultConnection";

    /// <summary>
    /// Registers database connection factory using configuration from ConnectionStrings:DefaultConnection
    /// or ConnectionStrings__DefaultConnection environment variable per Architecture §19.10.
    /// </summary>
    public static IServiceCollection AddDatabaseInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var connectionString = config.GetConnectionString(DefaultConnectionStringKey)
                ?? config["ConnectionStrings:DefaultConnection"]
                ?? config["ConnectionStrings__DefaultConnection"]
                ?? "Server=localhost;Database=ICS;Trusted_Connection=True;TrustServerCertificate=True;";

            return new SqlConnectionFactory(connectionString);
        });

        return services;
    }

    /// <summary>
    /// Registers database connection factory with an explicit connection string.
    /// </summary>
    public static IServiceCollection AddDatabaseInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(connectionString));
        return services;
    }
}
