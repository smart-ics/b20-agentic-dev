using Cakra.Api.Extensions;
using Cakra.Api.Infrastructure.Context;
using Cakra.Core;
using Cakra.Modules.Identity.Registration;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cakra.Tests.Integration.Modules;

/// <summary>
/// P1-S04 DI verification: builds the same service registrations the host uses
/// (<see cref="CoreServicesExtensions.AddCakraCore"/> /
/// <see cref="ModuleRegistrationExtensions.AddCakraModules"/>) and asserts every
/// core interface, MediatR, the validation behavior, and the module
/// self-registration resolve without unresolved dependencies.
/// </summary>
public class ModuleRegistrationTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        var moduleAssemblies = ModuleRegistrationExtensions.DiscoverModuleAssemblies();
        services.AddCakraCore(moduleAssemblies);
        services.AddCakraModules(moduleAssemblies);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    [Fact]
    public void Container_builds_without_unresolved_dependencies()
    {
        using var provider = BuildProvider();

        provider.Should().NotBeNull();
    }

    [Fact]
    public void Core_interfaces_are_resolvable_after_startup()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISystemClock>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IAuditContext>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>().Should().NotBeNull();
    }

    [Fact]
    public void MediatR_and_the_validation_pipeline_are_registered()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IMediator>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ISender>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IPublisher>().Should().NotBeNull();

        scope.ServiceProvider.GetServices<IPipelineBehavior<StubModulePingRequest, string>>()
            .Should().Contain(behavior => behavior is ValidationBehaviour<StubModulePingRequest, string>);
    }

    [Fact]
    public void Module_services_are_self_registered_by_the_module()
    {
        using var provider = BuildProvider();

        provider.GetRequiredService<StubRegistrationProbe>().ModuleName.Should().Be("Identity");
    }

    [Fact]
    public void Module_validator_is_auto_discovered()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetServices<IValidator<StubModulePingRequest>>()
            .Should().ContainSingle()
            .Which.Should().BeOfType<StubModulePingRequestValidator>();
    }

    [Fact]
    public async Task Module_handler_is_auto_discovered_and_dispatched()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var response = await scope.ServiceProvider
            .GetRequiredService<IMediator>()
            .Send(new StubModulePingRequest(5));

        response.Should().Be("pong:5");
    }

    [Fact]
    public async Task Validation_behavior_rejects_invalid_request_before_the_handler()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var act = async () => await scope.ServiceProvider
            .GetRequiredService<IMediator>()
            .Send(new StubModulePingRequest(-1));

        await act.Should().ThrowAsync<ValidationException>();
    }
}

/// <summary>
/// P1-S04 startup smoke check: boots the real <c>Cakra.Api</c> host in-process
/// with the connection string forced empty so migrations are skipped, proving
/// the host starts and resolves its core dependency graph with no unresolved
/// dependencies at startup.
/// </summary>
public class HostStartupDiSmokeTests
{
    [Fact]
    public void Host_starts_and_resolves_core_services()
    {
        using var factory = new DiSmokeWebApplicationFactory();
        using var scope = factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISystemClock>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IAuditContext>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IMediator>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<StubRegistrationProbe>().Should().NotBeNull();
    }

    private sealed class DiSmokeWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                // Overrides any ambient connection string so the host skips DbUp
                // migrations and the smoke test stays independent of SQL Server.
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = string.Empty,
                });
            });
        }
    }
}
