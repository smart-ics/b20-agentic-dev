using Microsoft.AspNetCore.Mvc.Testing;

namespace Cakra.Tests.Integration;

/// <summary>
/// Base class for integration tests. Provides an in-process HTTP client backed by
/// <see cref="WebApplicationFactory{TEntryPoint}"/> for the <c>Cakra.Api</c> host and a
/// Respawn-based <see cref="DatabaseResetHelper"/> for clean test state (Architecture §19.8).
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    private bool _disposed;

    protected IntegrationTestBase()
    {
        Factory = new CakraWebApplicationFactory();
        Client = Factory.CreateClient();
        Database = new DatabaseResetHelper();
    }

    /// <summary>Factory hosting <c>Cakra.Api</c> in-process.</summary>
    protected WebApplicationFactory<Program> Factory { get; }

    /// <summary>HTTP client connected to the in-process host.</summary>
    protected HttpClient Client { get; }

    /// <summary>Respawn-based reset helper for the isolated test database.</summary>
    protected DatabaseResetHelper Database { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Client.Dispose();
            Factory.Dispose();
        }

        _disposed = true;
    }
}
