using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Integration.Pipeline;

/// <summary>
/// Integration tests verifying the application request pipeline (P1-S06):
/// - Liveness and readiness health checks (/health/live, /health/ready) - Architecture §19.9
/// - RFC 7807 ProblemDetails on validation errors and unhandled exceptions - Architecture §19.6
/// - REST API route convention /api/v1/{module}/{resource} - Architecture §19.6
/// - MediatR FluentValidation pipeline execution
/// </summary>
public class ApplicationPipelineTests : IntegrationTestBase
{
    [Fact]
    public async Task HealthCheck_live_returns_200_OK_with_healthy_status()
    {
        var response = await Client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Healthy");
    }

    [Fact]
    public async Task HealthCheck_ready_endpoint_is_mapped_and_evaluates_readiness()
    {
        var response = await Client.GetAsync("/health/ready");

        // The endpoint is registered and responds; without a live SQL Server in the
        // test runner it returns 503 ServiceUnavailable, or 200 OK when connected.
        // It must NOT return 404 NotFound.
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        ((int)response.StatusCode).Should().BeOneOf(200, 503);
    }

    [Fact]
    public async Task Api_base_route_convention_resolves_and_returns_camelCase_json()
    {
        var response = await Client.GetAsync("/api/v1/system/probe");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("status", out var statusProp).Should().BeTrue();
        statusProp.GetString().Should().Be("ok");

        body.TryGetProperty("timestamp", out var timestampProp).Should().BeTrue();
        timestampProp.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task MediatR_and_validation_pipeline_dispatches_valid_request()
    {
        var response = await Client.GetAsync("/api/v1/system/probe/ping?count=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().Should().Be("pong:5");
    }

    [Fact]
    public async Task Validation_failure_returns_RFC7807_problem_details_with_400_and_camelCase_errors()
    {
        // Negative count triggers StubModulePingRequestValidator failure
        var response = await Client.GetAsync("/api/v1/system/probe/ping?count=-1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be(400);
        body.GetProperty("title").GetString().Should().Be("Validation Error");
        body.GetProperty("errorCode").GetString().Should().Be("VALIDATION_FAILED");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();

        // Verify field-level validation errors formatted with camelCase keys
        body.TryGetProperty("errors", out var errorsProp).Should().BeTrue();
        errorsProp.TryGetProperty("value", out var valueErrors).Should().BeTrue();
        valueErrors.EnumerateArray().First().GetString().Should().Contain("Value");
    }

    [Fact]
    public async Task Unhandled_exception_returns_RFC7807_problem_details_with_500()
    {
        var response = await Client.GetAsync("/api/v1/system/probe/error");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be(500);
        body.GetProperty("title").GetString().Should().Be("Internal Server Error");
        body.GetProperty("errorCode").GetString().Should().Be("INTERNAL_SERVER_ERROR");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Spa_route_fallback_is_mapped_without_shadowing_api()
    {
        // Non-API route falls back to SPA without producing a 500 error
        var response = await Client.GetAsync("/non-api-client-route");
        ((int)response.StatusCode).Should().NotBe(500);
    }
}
