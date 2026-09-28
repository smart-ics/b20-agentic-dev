using Cakra.Modules.Identity.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cakra.Tests.Integration.Identity;

/// <summary>
/// Integration tests verifying the Identity module registration within the ASP.NET Core host (P2-S09).
/// Confirms that DI resolves all IAM repositories and application services, and that DbUp migration
/// scripts for the identity schema are present and embedded.
/// </summary>
public class IdentityModuleRegistrationTests : IntegrationTestBase
{
    [Fact]
    public void Identity_services_and_repositories_are_resolvable_from_service_provider()
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var authService = services.GetService<IAuthenticationService>();
        authService.Should().NotBeNull("IAuthenticationService must be registered in DI via IdentityModule");

        var accountRepo = services.GetService<IUserAccountRepository>();
        accountRepo.Should().NotBeNull("IUserAccountRepository must be registered in DI via IdentityModule");

        var sessionRepo = services.GetService<IUserSessionRepository>();
        sessionRepo.Should().NotBeNull("IUserSessionRepository must be registered in DI via IdentityModule");

        var passwordHasher = services.GetService<IPasswordHasher<UserAccount>>();
        passwordHasher.Should().NotBeNull("IPasswordHasher<UserAccount> must be registered in DI via IdentityModule");
    }

    [Fact]
    public void Identity_migration_script_is_embedded_in_api_assembly()
    {
        var apiAssembly = typeof(Program).Assembly;
        var resourceNames = apiAssembly.GetManifestResourceNames();

        resourceNames.Should().Contain(
            name => name.EndsWith("0002_identity_tables.sql", StringComparison.OrdinalIgnoreCase),
            "0002_identity_tables.sql must be embedded in Cakra.Api assembly for DbUp discovery");
    }
}
