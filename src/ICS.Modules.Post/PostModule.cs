using ICS.Core.Modules;
using ICS.Modules.Post.Application;
using ICS.Modules.Post.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ICS.Modules.Post;

/// <summary>
/// Module registration bootstrapper for the Post bounded context.
/// Configures Post application services, query services, handlers, and repositories.
/// Per Architecture §7, §8, §12, §16, §17, and §20.
/// </summary>
public sealed class PostModule : IModule
{
    /// <inheritdoc />
    public string Name => "Post";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // 1. Published cross-module interfaces
        services.AddScoped<IPostQueryService, PostQueryService>();
        services.AddScoped<IPostService, PostService>();

        // 2. Module-internal Dapper repository (not exposed across module boundary)
        services.AddScoped<IPostRepository, PostRepository>();

        // 3. MediatR command handlers and FluentValidation validators are discovered
        //    automatically by the MediatR and FluentValidation registrations performed
        //    during host composition (see Program.cs / CoreServiceExtensions).
    }
}