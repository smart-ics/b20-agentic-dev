[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace ICS.Tests.Integration;

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Abstract base class for integration tests per Architecture §19.8.
/// Sets up WebApplicationFactory, in-process HttpClient, and automated Respawn database reset before each test.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<IcsWebApplicationFactory>, IAsyncLifetime
{
    protected readonly IcsWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(IcsWebApplicationFactory factory)
    {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        Client = factory.CreateClient();
    }

    /// <summary>
    /// Gets the root service provider from the web application host.
    /// </summary>
    protected IServiceProvider Services => Factory.Services;

    /// <summary>
    /// Creates an isolated dependency injection service scope.
    /// </summary>
    protected IServiceScope CreateScope() => Factory.Services.CreateScope();

    /// <summary>
    /// Executes an asynchronous delegate within a newly created dependency injection scope.
    /// </summary>
    protected async Task ExecuteInScopeAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = CreateScope();
        await action(scope.ServiceProvider);
    }

    /// <summary>
    /// Executes an asynchronous delegate returning a result within a newly created dependency injection scope.
    /// </summary>
    protected async Task<TResult> ExecuteInScopeAsync<TResult>(Func<IServiceProvider, Task<TResult>> action)
    {
        using var scope = CreateScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>
    /// Resets the database before each test execution to guarantee test isolation.
    /// </summary>
    public virtual async Task InitializeAsync()
    {
        TestDomainEventCollector.Clear();
        await Factory.ResetDatabaseAsync();
    }

    public virtual Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
