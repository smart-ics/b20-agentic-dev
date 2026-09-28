using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Cakra.Tests.Integration;

/// <summary>
/// In-process <see cref="WebApplicationFactory{TEntryPoint}"/> for the <c>Cakra.Api</c> host
/// (Architecture §19.8). The factory is intentionally resilient to the current state of
/// <c>Program</c>: the isolated test database connection string is only redirected when it
/// is configured through the environment, so the host can still boot without one.
/// </summary>
public class CakraWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        var testConnectionString = DatabaseResetHelper.ResolveConnectionString();
        if (!string.IsNullOrWhiteSpace(testConnectionString))
        {
            // Point the application under test at the isolated test database instead of the
            // application connection string.
            builder.UseSetting("ConnectionStrings:DefaultConnection", testConnectionString);
            builder.UseSetting("ConnectionStrings:TestConnection", testConnectionString);
        }
    }
}
