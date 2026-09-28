using ICS.Core.Modules;
using ICS.Modules.Request.Application;
using ICS.Modules.Request.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ICS.Modules.Request;

/// <summary>
/// Module registration bootstrapper for the Request bounded context.
/// Configures Request lifecycle services, queries, handlers, and repositories.
/// Per Architecture §7, §8, §16, §17, and §20.
/// </summary>
public sealed class RequestModule : IModule
{
    /// <inheritdoc />
    public string Name => "Request";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // 1. Published cross-module interfaces
        services.AddScoped<IRequestQueryService, RequestQueryService>();
        services.AddScoped<IRequestService, RequestService>();

        // 2. Module-internal Dapper repository (not exposed across boundary)
        services.AddScoped<IRequestRepository, RequestRepository>();
    }
}
