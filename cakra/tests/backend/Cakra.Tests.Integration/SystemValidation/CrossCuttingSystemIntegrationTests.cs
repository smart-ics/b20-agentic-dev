using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cakra.Api.Infrastructure.Authentication;
using Cakra.Api.Infrastructure.Context;
using Cakra.Api.Infrastructure.Migrations;
using Cakra.Core;
using Cakra.Modules.Analytics;
using Cakra.Modules.Customer.Services;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Organization.Commands;
using Cakra.Modules.Post.Domain;
using Cakra.Modules.Post.Services;
using Cakra.Modules.Product.Services;
using Cakra.Modules.Request.Domain;
using Dapper;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Respawn;
using Respawn.Graph;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;
using Xunit;

namespace Cakra.Tests.Integration.SystemValidation;

/// <summary>
/// System-wide cross-cutting and end-to-end integration validation test suite for Slice P7-S38
/// ("Cross-Cutting Finalization &amp; System Integration Validation"):
/// <list type="number">
///   <item><description><c>POST /api/v1/requests</c> with valid payload returns HTTP 201 and emits Serilog structured JSON log output with <c>PersonId</c>, <c>TraceId</c>, <c>Timestamp</c>, and <c>SourceContext</c> (Architecture §18, §19.9).</description></item>
///   <item><description><c>POST /api/v1/requests</c> with missing required fields returns HTTP 400 RFC 7807 <c>ProblemDetails</c> (<c>application/problem+json</c>) with <c>errors</c> dictionary containing field validation messages (Architecture §19.6).</description></item>
///   <item><description><c>POST /api/v1/posts</c> with valid <c>RequestId</c> reference populates <c>post.FeedItems</c> with matching <c>CustomerId</c>, <c>ProductId</c>, and <c>PostId</c> (Architecture §12, §18).</description></item>
///   <item><description><c>POST /api/v1/requests/{id}/escalate</c> updates <c>post.FeedItems</c> with <c>IsException = TRUE</c> and <c>ExceptionType = 'ESCALATION'</c> (Architecture §12).</description></item>
///   <item><description><c>GET /api/v1/analytics/programmer-workload</c> enforces RBAC: HTTP 403 for non-Management role, HTTP 200 for Management role (Architecture §14).</description></item>
///   <item><description>Full cookie session lifecycle: <c>POST /api/v1/auth/login</c> (<c>Set-Cookie</c> with <c>HttpOnly</c>, <c>SameSite=Strict</c>) -&gt; <c>GET /api/v1/requests</c> (200) -&gt; <c>POST /api/v1/auth/logout</c> -&gt; <c>GET /api/v1/requests</c> with same cookie (401) (Architecture §19.5).</description></item>
///   <item><description><c>GET /health/live</c> -&gt; HTTP 200 and <c>GET /health/ready</c> -&gt; HTTP 200 with SQL Server connectivity (Architecture §19.9).</description></item>
///   <item><description><c>FeedProjectionRebuilder.RebuildAll()</c> regenerates <c>post.FeedItems</c> so row count equals <c>post.Posts</c> row count where <c>Status = 'ACTIVE'</c> (Architecture §19.7).</description></item>
///   <item><description>All smoke tests pass for API endpoints backing <c>SCR-AUTH-001</c>, <c>SCR-FEED-001</c>, <c>SCR-POST-001</c>, <c>SCR-REQ-001..005</c>, <c>SCR-WP-001</c>, <c>SCR-PRD-001</c>, and <c>SCR-MGT-001..003</c>.</description></item>
/// </list>
/// Uses isolated database <c>CakraTestDb_SystemValidation</c> + Respawn cleanup.
/// </summary>
public sealed class CrossCuttingSystemIntegrationTests : IAsyncLifetime
{
    private const string LocalDbFallback =
        "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb_SystemValidation;Integrated Security=true;TrustServerCertificate=True;";

    private readonly string _connectionString;
    private readonly InMemoryJsonLogSink _logSink = new();
    private WebApplicationFactory<Program>? _factory;
    private Respawner? _respawner;
    private bool _sqlServerAvailable;

    public CrossCuttingSystemIntegrationTests()
    {
        var baseConnectionString = DatabaseResetHelper.ResolveConnectionString() ?? LocalDbFallback;
        _connectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "CakraTestDb_SystemValidation"
        }.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var masterConnectionString = new SqlConnectionStringBuilder(_connectionString)
            {
                InitialCatalog = "master"
            }.ConnectionString;

            await using var probeConn = new SqlConnection(masterConnectionString);
            await probeConn.OpenAsync();
            _sqlServerAvailable = true;
        }
        catch
        {
            _sqlServerAvailable = false;
            return;
        }

        var runner = new DatabaseMigrationRunner(_connectionString, NullLogger<DatabaseMigrationRunner>.Instance);
        var migrationResult = runner.Run();
        migrationResult.Successful.Should().BeTrue("DbUp migrations must execute cleanly for CakraTestDb_SystemValidation");

        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                SchemasToInclude =
                [
                    "analytics",
                    "post",
                    "workpackage",
                    "request",
                    "product",
                    "customer",
                    "organization",
                    "identity"
                ],
                TablesToIgnore =
                [
                    new Table("dbo", "__SchemaVersions"),
                    new Table("dbo", "SchemaVersions")
                ]
            });
            await _respawner.ResetAsync(connection);
        }

        _factory = new CakraWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
            builder.UseSetting("ConnectionStrings:TestConnection", _connectionString);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ILogEventSink>(_logSink);
            });
        });
    }

    public async Task DisposeAsync()
    {
        if (_sqlServerAvailable && _respawner is not null)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await _respawner.ResetAsync(connection);
        }

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    /// <summary>
    /// Criterion 1: <c>POST /api/v1/requests</c> with valid payload returns HTTP 201;
    /// assert Serilog structured log output contains JSON entry with fields
    /// <c>PersonId</c>, <c>TraceId</c>, <c>Timestamp</c>, and <c>SourceContext</c> (Architecture §18, §19.9).
    /// </summary>
    [Fact]
    public async Task Post_requests_with_valid_payload_returns_201_and_emits_Serilog_structured_JSON_log_with_PersonId_TraceId_Timestamp_and_SourceContext()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        var ctx = await SeedSystemActorsAndMasterDataAsync();
        using var client = ctx.ProgrammerClient;

        _logSink.Clear();

        var response = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "HL7 Lab Order Synchronization Delay on Morning Ward Rounds",
            description = "Inpatient ward order batch experiences 15-second delay during peak morning rounds.",
            customerId = ctx.CustomerId,
            productId = ctx.ProductId,
            requestType = "Bug",
            priority = "HIGH"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("id").GetGuid().Should().NotBeEmpty();
        created.GetProperty("status").GetString().Should().Be(RequestStatusNames.Captured);

        var jsonEntries = _logSink.GetJsonEntries();
        jsonEntries.Should().NotBeEmpty("Serilog must emit structured JSON log entries during POST /api/v1/requests");

        var matchingEntries = jsonEntries
            .Select(raw => JsonDocument.Parse(raw).RootElement)
            .Where(root =>
            {
                var hasTimestamp =
                    (root.TryGetProperty("Timestamp", out var ts) && !string.IsNullOrWhiteSpace(ts.GetString())) ||
                    (root.TryGetProperty("@t", out var compactTs) && !string.IsNullOrWhiteSpace(compactTs.GetString()));

                var personIdValue = TryExtractPropertyString(root, "PersonId");
                var traceIdValue = TryExtractPropertyString(root, "TraceId");
                var sourceContextValue = TryExtractPropertyString(root, "SourceContext");

                return hasTimestamp
                    && string.Equals(personIdValue, ctx.ProgrammerPersonId.ToString(), StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(traceIdValue)
                    && !string.IsNullOrWhiteSpace(sourceContextValue);
            })
            .ToList();

        matchingEntries.Should().NotBeEmpty(
            "at least one Serilog JSON log entry for POST /api/v1/requests must contain Timestamp, PersonId='{0}', TraceId, and SourceContext",
            ctx.ProgrammerPersonId);
    }

    /// <summary>
    /// Criterion 2: <c>POST /api/v1/requests</c> with missing required fields returns HTTP 400;
    /// response body deserializes as RFC 7807 <c>ProblemDetails</c> with <c>Content-Type: application/problem+json</c>
    /// and <c>errors</c> dictionary containing field validation messages (Architecture §19.6).
    /// </summary>
    [Fact]
    public async Task Post_requests_with_missing_required_fields_returns_400_RFC7807_ProblemDetails_with_application_problem_json_and_errors_dictionary()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        var ctx = await SeedSystemActorsAndMasterDataAsync();
        using var client = ctx.ProgrammerClient;

        var response = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "",
            description = ""
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var rawBody = await response.Content.ReadAsStringAsync();
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var problemDetails = JsonSerializer.Deserialize<ValidationProblemDetails>(rawBody, jsonOptions);
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Title.Should().Be("Validation Error");
        problemDetails.Errors.Should().ContainKey("title");
        problemDetails.Errors["title"].Should().Contain(msg => msg.Contains("title", StringComparison.OrdinalIgnoreCase));
        problemDetails.Errors.Should().ContainKey("description");
        problemDetails.Errors["description"].Should().Contain(msg => msg.Contains("description", StringComparison.OrdinalIgnoreCase));

        var rawProblem = JsonDocument.Parse(rawBody).RootElement;
        rawProblem.GetProperty("errorCode").GetString().Should().Be("VALIDATION_FAILED");
        rawProblem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Criterion 3: Record a request via <c>POST /api/v1/requests</c> -&gt; query <c>post.FeedItems</c>
    /// table -&gt; assert row automatically created with correct <c>CustomerId</c>, <c>ProductId</c>, and <c>RequestId</c> (Architecture CR-001).
    /// Also asserts direct <c>POST /api/v1/posts</c> is decommissioned (404/405).
    /// </summary>
    [Fact]
    public async Task Post_requests_automatically_inserts_FeedItems_row_and_POST_posts_returns_not_found()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        var ctx = await SeedSystemActorsAndMasterDataAsync();
        using var client = ctx.ProgrammerClient;

        // Record a Request linked to CustomerId and ProductId
        var createReqResponse = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "INA-CBGs Claim Grouper Tariff Table Update",
            description = "Update inpatient procedure mapping for Q4 tariff adjustment.",
            customerId = ctx.CustomerId,
            productId = ctx.ProductId,
            requestType = "Support",
            priority = "HIGH"
        });
        createReqResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var requestId = (await createReqResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Direct POST /api/v1/posts is decommissioned -> 404/405
        var directPostResp = await client.PostAsJsonAsync("/api/v1/posts", new
        {
            title = "Direct post should fail",
            content = "Decommissioned endpoint",
            requestId
        });
        directPostResp.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);

        // Query post.FeedItems directly in SQL Server: recording request automatically inserted row!
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var feedItem = await conn.QuerySingleOrDefaultAsync<(Guid PostId, Guid? CustomerId, Guid? ProductId, Guid? RequestId)>(
            """
            SELECT [PostId], [CustomerId], [ProductId], [RequestId]
            FROM [post].[FeedItems]
            WHERE [RequestId] = @RequestId;
            """,
            new { RequestId = requestId });

        feedItem.Should().NotBe(default);
        feedItem.PostId.Should().NotBeEmpty();
        feedItem.CustomerId.Should().Be(ctx.CustomerId);
        feedItem.ProductId.Should().Be(ctx.ProductId);
        feedItem.RequestId.Should().Be(requestId);
    }

    /// <summary>
    /// Criterion 4: Pause a request via <c>POST /api/v1/requests/{id}/pause</c> -&gt; query <c>post.FeedItems</c> -&gt;
    /// assert <c>IsException = TRUE</c> and <c>ExceptionType = 'ESCALATION'</c> (Architecture §12; CR-016 TD-002).
    /// </summary>
    [Fact]
    public async Task Pause_request_via_Post_requests_id_pause_sets_FeedItems_IsException_true_and_ExceptionType_ESCALATION()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        var ctx = await SeedSystemActorsAndMasterDataAsync();
        using var client = ctx.ProgrammerClient;

        // 1. Create Request
        var createReqResponse = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Emergency Department Triage Screen Freeze on Barcode Scan",
            description = "Scanner input buffer overflow when scanning 2D wristband barcodes.",
            customerId = ctx.CustomerId,
            productId = ctx.ProductId,
            requestType = "Bug",
            priority = "URGENT"
        });
        createReqResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var requestId = (await createReqResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // 2. Assign Request Owner -> ASSIGNED
        var assignResponse = await client.PostAsJsonAsync($"/api/v1/requests/{requestId}/assign", new
        {
            ownerPersonId = ctx.ProgrammerPersonId,
            notes = "Assigned for emergency triage investigation"
        });
        assignResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Start Work -> IN_PROGRESS
        var startResponse = await client.PostAsJsonAsync($"/api/v1/requests/{requestId}/start", new
        {
            notes = "Starting triage"
        });
        startResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Pause Request via POST /api/v1/requests/{id}/pause
        var pauseResponse = await client.PostAsJsonAsync($"/api/v1/requests/{requestId}/pause", new
        {
            note = "Requires vendor SDK firmware patch approval from hospital IT director."
        });
        pauseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Query post.FeedItems and assert IsException = TRUE and ExceptionType = 'ESCALATION'
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var feedRow = await conn.QuerySingleOrDefaultAsync<(Guid PostId, Guid? RequestId, bool IsException, string? ExceptionType)>(
            """
            SELECT TOP (1) [PostId], [RequestId], [IsException], [ExceptionType]
            FROM [post].[FeedItems]
            WHERE [RequestId] = @RequestId
            ORDER BY [UpdatedAt] DESC;
            """,
            new { RequestId = requestId });

        feedRow.Should().NotBe(default);
        feedRow.RequestId.Should().Be(requestId);
        feedRow.IsException.Should().BeTrue();
        feedRow.ExceptionType.Should().Be(PostExceptionTypes.Escalation);
    }

    /// <summary>
    /// Criterion 5: <c>GET /api/v1/analytics/programmer-workload</c> with non-Management role -&gt; HTTP 403;
    /// with Management role -&gt; HTTP 200 (Architecture §14 RBAC).
    /// </summary>
    [Fact]
    public async Task Get_analytics_programmer_workload_returns_403_for_non_Management_role_and_200_for_Management_role()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        var ctx = await SeedSystemActorsAndMasterDataAsync();
        using var programmerClient = ctx.ProgrammerClient;
        using var managementClient = ctx.ManagementClient;

        // Non-Management role (Programmer) -> HTTP 403 Forbidden
        var forbiddenResponse = await programmerClient.GetAsync("/api/v1/analytics/programmer-workload");
        forbiddenResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        forbiddenResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var forbiddenProblem = await forbiddenResponse.Content.ReadFromJsonAsync<JsonElement>();
        forbiddenProblem.GetProperty("status").GetInt32().Should().Be(403);
        forbiddenProblem.GetProperty("errorCode").GetString().Should().Be("FORBIDDEN");

        // Management role -> HTTP 200 OK
        var okResponse = await managementClient.GetAsync("/api/v1/analytics/programmer-workload");
        okResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var workloads = await okResponse.Content.ReadFromJsonAsync<JsonElement>();
        workloads.ValueKind.Should().Be(JsonValueKind.Array);
        workloads.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    /// <summary>
    /// Criterion 6: Full cookie session lifecycle:
    /// <c>POST /api/v1/auth/login</c> -&gt; assert <c>Set-Cookie</c> header with <c>HttpOnly</c>, <c>SameSite=Strict</c> -&gt;
    /// <c>GET /api/v1/requests</c> with cookie -&gt; HTTP 200 -&gt;
    /// <c>POST /api/v1/auth/logout</c> -&gt;
    /// <c>GET /api/v1/requests</c> with same cookie -&gt; HTTP 401 (Architecture §19.5).
    /// </summary>
    [Fact]
    public async Task Full_cookie_session_lifecycle_login_sets_HttpOnly_SameSite_Strict_cookie_authorizes_requests_and_logout_invalidates_cookie()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        var ctx = await SeedSystemActorsAndMasterDataAsync();
        ctx.ProgrammerClient.Dispose();
        ctx.ManagementClient.Dispose();

        using var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        // 1. POST /api/v1/auth/login -> assert Set-Cookie header with HttpOnly and SameSite=Strict
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = ctx.ProgrammerUsername,
            password = ctx.Password
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        loginResponse.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders).Should().BeTrue();
        var sessionCookieHeader = setCookieHeaders!
            .FirstOrDefault(h => h.StartsWith($"{CakraAuthenticationDefaults.CookieName}=", StringComparison.Ordinal));
        sessionCookieHeader.Should().NotBeNull("login must issue the Cakra.Session cookie");
        sessionCookieHeader!.ToLowerInvariant().Should().Contain("httponly");
        sessionCookieHeader.ToLowerInvariant().Should().Contain("samesite=strict");

        var sessionCookieValue = ExtractSessionCookieValue(sessionCookieHeader);
        sessionCookieValue.Should().NotBeNullOrWhiteSpace();
        var cookieHeaderValue = $"{CakraAuthenticationDefaults.CookieName}={sessionCookieValue}";

        // 2. GET /api/v1/requests with cookie -> HTTP 200 OK
        using (var authedGetRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/requests"))
        {
            authedGetRequest.Headers.Add("Cookie", cookieHeaderValue);
            var authedGetResponse = await client.SendAsync(authedGetRequest);
            authedGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 3. POST /api/v1/auth/logout with cookie -> HTTP 200 OK
        using (var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout"))
        {
            logoutRequest.Headers.Add("Cookie", cookieHeaderValue);
            var logoutResponse = await client.SendAsync(logoutRequest);
            logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 4. GET /api/v1/requests with same cookie after logout -> HTTP 401 Unauthorized
        using (var postLogoutGetRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/requests"))
        {
            postLogoutGetRequest.Headers.Add("Cookie", cookieHeaderValue);
            var postLogoutGetResponse = await client.SendAsync(postLogoutGetRequest);
            postLogoutGetResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }

    /// <summary>
    /// Criterion 7: <c>GET /health/live</c> -&gt; HTTP 200; <c>GET /health/ready</c> -&gt; HTTP 200
    /// (with SQL Server connectivity) (Architecture §19.9).
    /// </summary>
    [Fact]
    public async Task Health_live_and_health_ready_return_200_OK_with_SQL_Server_connectivity()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var liveResponse = await client.GetAsync("/health/live");
        liveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await liveResponse.Content.ReadAsStringAsync()).Should().Be("Healthy");

        var readyResponse = await client.GetAsync("/health/ready");
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await readyResponse.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    /// <summary>
    /// Criterion 8: Invoke <c>FeedProjectionRebuilder.RebuildAll()</c> -&gt; assert <c>post.FeedItems</c>
    /// row count equals <c>post.Posts</c> row count (where <c>Status = 'ACTIVE'</c>) (Architecture §19.7).
    /// </summary>
    [Fact]
    public async Task FeedProjectionRebuilder_RebuildAll_makes_FeedItems_row_count_equal_active_Posts_row_count()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        var ctx = await SeedSystemActorsAndMasterDataAsync();
        using var client = ctx.ProgrammerClient;
        ctx.ManagementClient.Dispose();

        // Create a request and two active posts
        var reqResp = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Radiology PACS Worklist Modality Filter Fix",
            description = "Ensure CT and MR modality filters query DICOM worklist accurately.",
            customerId = ctx.CustomerId,
            productId = ctx.ProductId,
            requestType = "Bug",
            priority = "NORMAL"
        });
        reqResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var requestId = (await reqResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Post 1 was automatically recorded when Request was created above.
        // Post 2 is recorded via RecordSystemPostCommand.
        using (var scope = _factory!.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new RecordSystemPostCommand(
                Title: "Weekly MyHospital Core release notes published",
                Content: "Includes billing rounding adjustments and pharmacy batch stock checks.",
                CustomerId: ctx.CustomerId,
                ProductId: ctx.ProductId));
        }

        // Truncate post.FeedItems to simulate projection rebuild from scratch
        await using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync("TRUNCATE TABLE [post].[FeedItems];");
        }

        // Invoke FeedProjectionRebuilder.RebuildAll()
        using (var scope = _factory!.Services.CreateScope())
        {
            var rebuilder = scope.ServiceProvider.GetRequiredService<IFeedProjectionRebuilder>();
            var rebuiltCount = rebuilder.RebuildAll();
            rebuiltCount.Should().BeGreaterThanOrEqualTo(2);
        }

        await using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();

            var activePostsCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM [post].[Posts] WHERE [Status] = N'ACTIVE';");

            var feedItemsCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM [post].[FeedItems] WHERE [Status] = N'ACTIVE';");

            activePostsCount.Should().Be(2);
            feedItemsCount.Should().Be(activePostsCount);
        }
    }

    /// <summary>
    /// Criterion 9: All smoke tests pass for API endpoints backing:
    /// <c>SCR-AUTH-001</c>, <c>SCR-FEED-001</c>, <c>SCR-POST-001</c>, <c>SCR-REQ-001..005</c>,
    /// <c>SCR-WP-001</c>, <c>SCR-PRD-001</c>, and <c>SCR-MGT-001..003</c>.
    /// </summary>
    [Fact]
    public async Task All_screen_backing_API_endpoints_pass_end_to_end_smoke_tests()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        var ctx = await SeedSystemActorsAndMasterDataAsync();
        ctx.ProgrammerClient.Dispose();
        ctx.ManagementClient.Dispose();

        using var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        // =====================================================================
        // 1. SCR-AUTH-001: POST /api/v1/auth/login & GET /api/v1/auth/me
        // =====================================================================
        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = ctx.ManagementUsername,
            password = ctx.Password
        });
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        loginResp.Headers.TryGetValues("Set-Cookie", out var setCookies).Should().BeTrue();
        var sessionCookieHeader = setCookies!.First(h => h.StartsWith($"{CakraAuthenticationDefaults.CookieName}=", StringComparison.Ordinal));
        var sessionToken = ExtractSessionCookieValue(sessionCookieHeader);
        client.DefaultRequestHeaders.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}={sessionToken}");

        var meResp = await client.GetAsync("/api/v1/auth/me");
        meResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var meJson = await meResp.Content.ReadFromJsonAsync<JsonElement>();
        meJson.GetProperty("personId").GetGuid().Should().Be(ctx.ManagementPersonId);

        // =====================================================================
        // 2. SCR-PRD-001: GET /api/v1/products, GET /api/v1/products/active, POST /api/v1/products
        // =====================================================================
        var createProductResp = await client.PostAsJsonAsync("/api/v1/products", new
        {
            code = "PENAEL-EMR",
            name = "PenaEl Electronic Medical Record",
            description = "Outpatient and Inpatient Clinical Documentation",
            ownerPersonId = ctx.ProgrammerPersonId
        });
        createProductResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var newProductId = (await createProductResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var listProductsResp = await client.GetAsync("/api/v1/products");
        listProductsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await listProductsResp.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().BeGreaterThanOrEqualTo(2);

        var activeProductsResp = await client.GetAsync("/api/v1/products/active");
        activeProductsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await activeProductsResp.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().BeGreaterThanOrEqualTo(2);

        var getProductResp = await client.GetAsync($"/api/v1/products/{newProductId}");
        getProductResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // =====================================================================
        // 3. SCR-REQ-001..005: POST /api/v1/requests, GET /api/v1/requests,
        //    GET /api/v1/requests/{id}, GET /api/v1/requests/{id}/history, GET /api/v1/requests/my
        // =====================================================================
        var createReqResp = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            title = "Outpatient e-Prescription Drug Interaction Alert",
            description = "Add clinical decision support alert for duplicate anticoagulant prescriptions.",
            customerId = ctx.CustomerId,
            productId = newProductId,
            requestType = "Feature",
            priority = "HIGH"
        });
        createReqResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var requestId = (await createReqResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var assignReqResp = await client.PostAsJsonAsync($"/api/v1/requests/{requestId}/assign", new
        {
            ownerPersonId = ctx.ManagementPersonId,
            notes = "Assigned for smoke validation"
        });
        assignReqResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var listReqResp = await client.GetAsync("/api/v1/requests?page=1&pageSize=10");
        listReqResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await listReqResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);

        var getReqResp = await client.GetAsync($"/api/v1/requests/{requestId}");
        getReqResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var historyReqResp = await client.GetAsync($"/api/v1/requests/{requestId}/history");
        historyReqResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await historyReqResp.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().BeGreaterThanOrEqualTo(2);

        var myReqResp = await client.GetAsync("/api/v1/requests/my");
        myReqResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await myReqResp.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        // =====================================================================
        // 4. SCR-WP-001: POST /api/v1/work-packages, GET /api/v1/work-packages,
        //    GET /api/v1/work-packages/{id}, GET /api/v1/work-packages/{id}/scope
        // =====================================================================
        var createWpResp = await client.PostAsJsonAsync("/api/v1/work-packages", new
        {
            name = "Q4 Clinical Safety Enhancements",
            objective = "Deliver e-Prescription drug interaction and allergy check enhancements.",
            ownerPersonId = ctx.ProgrammerPersonId,
            customerId = ctx.CustomerId,
            productId = newProductId
        });
        createWpResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var workPackageId = (await createWpResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var addWpScopeResp = await client.PostAsJsonAsync($"/api/v1/work-packages/{workPackageId}/requests", new
        {
            requestId
        });
        addWpScopeResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var listWpResp = await client.GetAsync("/api/v1/work-packages");
        listWpResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await listWpResp.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        var getWpResp = await client.GetAsync($"/api/v1/work-packages/{workPackageId}");
        getWpResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var getWpScopeResp = await client.GetAsync($"/api/v1/work-packages/{workPackageId}/scope");
        getWpScopeResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await getWpScopeResp.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(1);

        // =====================================================================
        // 5. SCR-FEED-001: Assert POST /api/v1/posts decommissioned, GET /api/v1/feed,
        //    GET /api/v1/posts/{id}, POST/GET comments, POST/GET reactions
        // =====================================================================
        var directPostResp = await client.PostAsJsonAsync("/api/v1/posts", new
        {
            title = "Clinical safety rule engine prototype ready for review",
            content = "Completed anticoagulant interaction rule table verification.",
            requestId
        });
        directPostResp.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);

        // Post was automatically created when Request was recorded earlier
        Guid postId;
        await using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            postId = await conn.QuerySingleAsync<Guid>(
                "SELECT [PostId] FROM [post].[FeedItems] WHERE [RequestId] = @RequestId;",
                new { RequestId = requestId });
        }

        var feedResp = await client.GetAsync("/api/v1/feed?pageSize=10&offset=0");
        feedResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await feedResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);

        var getPostResp = await client.GetAsync($"/api/v1/posts/{postId}");
        getPostResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var postCommentResp = await client.PostAsJsonAsync($"/api/v1/posts/{postId}/comments", new
        {
            content = "Reviewed with hospital chief pharmacist; approved for staging."
        });
        postCommentResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var getCommentsResp = await client.GetAsync($"/api/v1/posts/{postId}/comments");
        getCommentsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await getCommentsResp.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(1);

        var postReactionResp = await client.PostAsJsonAsync($"/api/v1/posts/{postId}/reactions", new
        {
            reactionType = PostReactionTypes.Seen
        });
        postReactionResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var getReactionsResp = await client.GetAsync($"/api/v1/posts/{postId}/reactions");
        getReactionsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await getReactionsResp.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(1);

        // =====================================================================
        // 6. SCR-MGT-001..003: customer-portfolio, programmer-performance, programmer-workload
        // =====================================================================
        var todayUtc = DateTime.UtcNow.Date;
        var currentYearMonth = todayUtc.ToString("yyyy-MM");
        using (var scope = _factory.Services.CreateScope())
        {
            var snapshotJob = scope.ServiceProvider.GetRequiredService<AnalyticsSnapshotJob>();
            await snapshotJob.TriggerDailySnapshotAsync(todayUtc);
            await snapshotJob.TriggerMonthlySnapshotAsync(currentYearMonth);
        }

        var portfolioResp = await client.GetAsync($"/api/v1/analytics/customer-portfolio?customerId={ctx.CustomerId}");
        portfolioResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var performanceResp = await client.GetAsync(
            $"/api/v1/analytics/programmer-performance?personId={ctx.ProgrammerPersonId}&startMonth={currentYearMonth}&endMonth={currentYearMonth}");
        performanceResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var workloadResp = await client.GetAsync($"/api/v1/analytics/programmer-workload?personId={ctx.ManagementPersonId}");
        workloadResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // =====================================================================
        // 7. SCR-AUTH-001: POST /api/v1/auth/logout
        // =====================================================================
        var logoutResp = await client.PostAsync("/api/v1/auth/logout", null);
        logoutResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static string ExtractSessionCookieValue(string setCookieHeader)
    {
        var prefix = $"{CakraAuthenticationDefaults.CookieName}=";
        var semicolonIndex = setCookieHeader.IndexOf(';');
        return semicolonIndex > prefix.Length
            ? setCookieHeader.Substring(prefix.Length, semicolonIndex - prefix.Length)
            : setCookieHeader.Substring(prefix.Length);
    }

    private static string? TryExtractPropertyString(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var directProp))
        {
            return directProp.ValueKind == JsonValueKind.String ? directProp.GetString() : directProp.ToString();
        }

        if (root.TryGetProperty("Properties", out var props) &&
            props.ValueKind == JsonValueKind.Object &&
            props.TryGetProperty(propertyName, out var nestedProp))
        {
            return nestedProp.ValueKind == JsonValueKind.String ? nestedProp.GetString() : nestedProp.ToString();
        }

        return null;
    }

    private async Task<SeededSystemContext> SeedSystemActorsAndMasterDataAsync()
    {
        const string password = "CakraSystemPassword#2026";
        Guid programmerPersonId;
        Guid managementPersonId;
        Guid customerId;
        Guid productId;
        string programmerUsername;
        string managementUsername;
        string programmerToken;
        string managementToken;

        using (var scope = _factory!.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var userAccountRepo = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
            var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

            var programmer = await mediator.Send(new CreatePersonCommand(
                "Budi",
                "Santoso",
                $"budi.{Guid.NewGuid():N}@cakra.id"));
            programmerPersonId = programmer.Id;

            var manager = await mediator.Send(new CreatePersonCommand(
                "Hendra",
                "Kusuma",
                $"hendra.{Guid.NewGuid():N}@cakra.id"));
            managementPersonId = manager.Id;

            var programmerRole = await mediator.Send(new CreateRoleCommand("Programmer", "Operational Programmer"));
            var managementRole = await mediator.Send(new CreateRoleCommand("Management", "Operational Management"));

            await mediator.Send(new AssignRoleToPersonCommand(programmerPersonId, programmerRole.Id));
            await mediator.Send(new AssignRoleToPersonCommand(managementPersonId, managementRole.Id));

            var customer = await mediator.Send(new CreateCustomerCommand(
                $"CUST-{Guid.NewGuid():N}"[..14],
                "RSUP Dr. Sardjito",
                HasActiveMaintenanceContract: true));
            customerId = customer.Id;

            var product = await mediator.Send(new CreateProductCommand(
                $"PRD-{Guid.NewGuid():N}"[..14],
                "MyHospital Core",
                "Hospital Information System",
                programmerPersonId));
            productId = product.Id;

            programmerUsername = $"budi.{Guid.NewGuid():N}"[..20];
            var programmerUser = new UserAccount
            {
                Id = Guid.NewGuid(),
                PersonId = programmerPersonId,
                Username = programmerUsername,
                Email = programmer.Email,
                Status = UserAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            programmerUser.PasswordHash = authService.HashPassword(programmerUser, password);
            await userAccountRepo.AddAsync(programmerUser);

            managementUsername = $"hendra.{Guid.NewGuid():N}"[..20];
            var managementUser = new UserAccount
            {
                Id = Guid.NewGuid(),
                PersonId = managementPersonId,
                Username = managementUsername,
                Email = manager.Email,
                Status = UserAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            managementUser.PasswordHash = authService.HashPassword(managementUser, password);
            await userAccountRepo.AddAsync(managementUser);

            var progLogin = await authService.LoginAsync(programmerUsername, password);
            progLogin.Succeeded.Should().BeTrue();
            programmerToken = progLogin.SessionToken!;

            var mgtLogin = await authService.LoginAsync(managementUsername, password);
            mgtLogin.Succeeded.Should().BeTrue();
            managementToken = mgtLogin.SessionToken!;

            currentContext?.Initialize(programmerUser.Id, programmerPersonId, ["Programmer"]);
        }

        var programmerClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        programmerClient.DefaultRequestHeaders.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}={programmerToken}");

        var managementClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        managementClient.DefaultRequestHeaders.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}={managementToken}");

        return new SeededSystemContext(
            programmerClient,
            managementClient,
            programmerPersonId,
            managementPersonId,
            programmerUsername,
            managementUsername,
            password,
            customerId,
            productId);
    }

    private sealed record SeededSystemContext(
        HttpClient ProgrammerClient,
        HttpClient ManagementClient,
        Guid ProgrammerPersonId,
        Guid ManagementPersonId,
        string ProgrammerUsername,
        string ManagementUsername,
        string Password,
        Guid CustomerId,
        Guid ProductId);

    private sealed class InMemoryJsonLogSink : ILogEventSink
    {
        private readonly ConcurrentQueue<string> _jsonLines = new();
        private readonly JsonFormatter _formatter = new();

        public void Emit(LogEvent logEvent)
        {
            using var writer = new StringWriter();
            _formatter.Format(logEvent, writer);
            var line = writer.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(line))
            {
                _jsonLines.Enqueue(line);
            }
        }

        public void Clear()
        {
            while (_jsonLines.TryDequeue(out _))
            {
            }
        }

        public IReadOnlyList<string> GetJsonEntries() => _jsonLines.ToArray();
    }
}
