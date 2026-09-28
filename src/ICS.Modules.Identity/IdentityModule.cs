using ICS.Core.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ICS.Modules.Identity;

/// <summary>
/// Module registration bootstrapper for the Identity bounded context.
/// Configures IAM services, user sessions, authentication handlers, and validators.
/// Per Architecture §14, §18, and §19.2.
/// </summary>
public sealed class IdentityModule : IModule
{
    /// <inheritdoc />
    public string Name => "Identity";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<Microsoft.AspNetCore.Identity.IPasswordHasher<Domain.UserAccount>, Microsoft.AspNetCore.Identity.PasswordHasher<Domain.UserAccount>>();
        services.AddScoped<Persistence.IUserAccountRepository, Persistence.UserAccountRepository>();
        services.AddScoped<Persistence.IUserSessionRepository, Persistence.UserSessionRepository>();
        services.AddScoped<Application.IAuthenticationService, Application.AuthenticationService>();
        services.AddScoped<Application.IAuthorizationService, Application.AuthorizationService>();
        services.AddScoped<IIdentityDataSeeder, IdentityDataSeeder>();
    }
}
