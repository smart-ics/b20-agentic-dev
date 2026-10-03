using Cakra.Core;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Identity.Persistence;
using Cakra.Modules.Identity.Registration;
using Cakra.Modules.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Cakra.Modules.Identity;

/// <summary>
/// Module bootstrapper for the Identity &amp; Access module (Architecture §19.2).
/// <para>
/// This slice (P1-S04) establishes the pattern: the host discovers every
/// <see cref="IModule"/> in the deployed <c>Cakra.Modules.*</c> assemblies and
/// invokes <see cref="RegisterServices"/>. Feature vertical slices added by
/// later phases extend this method with their services and repositories, and
/// their MediatR handlers / FluentValidation validators are auto-discovered from
/// this assembly by the host pipeline.
/// </para>
/// </summary>
public sealed class IdentityModule : IModule
{
    /// <inheritdoc />
    public string Name => "Identity";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Technical probe proving the module self-registration pattern end to end.
        services.AddSingleton<StubRegistrationProbe>();

        // Architecture §14, §19.3, §19.5: IAM Repositories and Application Services
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddSingleton<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IUserAccountService, UserAccountService>();
    }
}
