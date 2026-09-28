using Cakra.Core;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Cakra.Modules.Post.Persistence;
using Cakra.Modules.Post.Services;
using Cakra.Modules.Product;
using Cakra.Modules.Request;
using Cakra.Modules.WorkPackage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cakra.Modules.Post;

/// <summary>
/// Module bootstrapper for the Post module (Architecture §6, §7, §8, §12, §19.2).
/// Discovered and invoked by the host application at startup.
/// </summary>
public sealed class PostModule : IModule
{
    /// <inheritdoc />
    public string Name => "Post";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Internal repositories (Architecture §19.3, §21 - Scoped per request, not exposed outside module)
        services.AddScoped<IPostRepository, PostRepository>();

        // Application Command Service (Architecture §7, §8, §12, §15, §18)
        services.AddScoped<PostService>(sp => new PostService(
            sp.GetRequiredService<IPostRepository>(),
            sp.GetRequiredService<IOrganizationQueryService>(),
            sp.GetRequiredService<ICustomerQueryService>(),
            sp.GetRequiredService<IProductQueryService>(),
            sp.GetRequiredService<IRequestQueryService>(),
            sp.GetRequiredService<IWorkPackageQueryService>(),
            sp.GetService<IDomainEventDispatcher>(),
            sp.GetService<ICurrentContextProvider>(),
            sp.GetService<IAuditContext>(),
            sp.GetService<ISystemClock>(),
            sp.GetService<ILogger<PostService>>()));
        services.AddScoped<IPostService>(sp => sp.GetRequiredService<PostService>());

        // Published Cross-Module Query Service (Architecture §7, §8, §15, §20, §21)
        services.AddScoped<PostQueryService>(sp => new PostQueryService(
            sp.GetRequiredService<IDbConnectionFactory>(),
            sp.GetService<IOrganizationQueryService>(),
            sp.GetService<ICustomerQueryService>(),
            sp.GetService<IProductQueryService>(),
            sp.GetService<IRequestQueryService>(),
            sp.GetService<IWorkPackageQueryService>()));
        services.AddScoped<IPostQueryService>(sp => sp.GetRequiredService<PostQueryService>());

        // Feed Projection Handler (Architecture §7, §12, §15, §19.2 - MediatR INotificationHandler<T> auto-discovered by AddCakraCore)
        services.AddScoped<FeedProjectionHandler>(sp => new FeedProjectionHandler(
            sp.GetRequiredService<IDbConnectionFactory>(),
            sp.GetService<IOrganizationQueryService>(),
            sp.GetService<ICustomerQueryService>(),
            sp.GetService<IProductQueryService>(),
            sp.GetService<IRequestQueryService>(),
            sp.GetService<IWorkPackageQueryService>(),
            sp.GetService<ISystemClock>(),
            sp.GetService<ILogger<FeedProjectionHandler>>()));

        // Feed Query Service (Architecture §7, §8, §12, §20 - single-table indexed Dapper queries over post.FeedItems)
        services.AddScoped<FeedQueryService>(sp => new FeedQueryService(
            sp.GetRequiredService<IDbConnectionFactory>(),
            sp.GetService<ILogger<FeedQueryService>>()));
        services.AddScoped<IFeedQueryService>(sp => sp.GetRequiredService<FeedQueryService>());

        // Feed Projection Rebuilder & Background Channel Worker (Architecture §12 Rebuild Strategy, §19.7 System.Threading.Channels)
        services.AddSingleton<FeedProjectionRebuilder>(sp => new FeedProjectionRebuilder(
            sp.GetRequiredService<IDbConnectionFactory>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetService<ISystemClock>(),
            sp.GetService<ILogger<FeedProjectionRebuilder>>()));
        services.AddSingleton<IFeedProjectionRebuilder>(sp => sp.GetRequiredService<FeedProjectionRebuilder>());
        services.AddHostedService<FeedProjectionRebuilder>(sp => sp.GetRequiredService<FeedProjectionRebuilder>());
    }
}
