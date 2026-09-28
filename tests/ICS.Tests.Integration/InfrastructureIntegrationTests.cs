namespace ICS.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using ICS.Core.Validation;
using Xunit;

/// <summary>
/// Integration test verifying the test infrastructure: WebApplicationFactory,
/// in-process HTTP dispatch, and Respawn-based test isolation.
/// </summary>
public class InfrastructureIntegrationTests : IntegrationTestBase
{
    public InfrastructureIntegrationTests(IcsWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task LivenessHealthCheck_ReturnsOk()
    {
        var response = await Client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Healthy");
    }

    [Fact]
    public async Task ReadinessHealthCheck_ReturnsOk_ConfirmingDatabaseConnectivity()
    {
        var response = await Client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Healthy");
    }

    [Fact]
    public async Task DatabaseReset_ExecutesSuccessfully()
    {
        var act = () => Factory.ResetDatabaseAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PingEndpoint_DispatchesViaPipelineAndReturnsResult()
    {
        var command = new PingCommand("Integration-Infrastructure-Smoke-Test");
        var response = await Client.PostAsJsonAsync("/api/v1/ping", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PingResult>();
        result.Should().NotBeNull();
        result!.Echo.Should().Be("Integration-Infrastructure-Smoke-Test");
    }

    [Fact]
    public async Task SystemInfoEndpoint_ReturnsOkWithStatus()
    {
        var response = await Client.GetAsync("/api/v1/system/info");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("ICS Operational System");
    }

    [Fact]
    public async Task Spa_RootEndpoint_ServesIndexHtml()
    {
        var response = await Client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("<title>ICS Operational System</title>");
        html.Should().Contain("id=\"app\"");
    }

    [Fact]
    public async Task Spa_ClientRoute_FallsBackToIndexHtml()
    {
        var response = await Client.GetAsync("/login");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("<title>ICS Operational System</title>");
        html.Should().Contain("id=\"app\"");
    }
}
