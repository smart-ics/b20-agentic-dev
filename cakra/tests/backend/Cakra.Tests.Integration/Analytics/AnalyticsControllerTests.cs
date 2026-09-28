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
using Cakra.Modules.Identity.Services;
using Cakra.Modules.Organization.Commands;
using Cakra.Modules.Product.Services;
using Cakra.Modules.Request.Services;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Respawn;
using Respawn.Graph;
using Xunit;

namespace Cakra.Tests.Integration.Analytics;

/// <summary>
/// Integration tests verifying Slice P7-S36: Management Analytics API Controller
/// (<c>/api/v1/analytics/*</c> in <c>Cakra.Api</c>; Architecture §7, §8, §9, §13, §14, §18, §19.5, §19.6, §19.8):
/// - <c>GET /api/v1/analytics/customer-portfolio?customerId={id}</c> -> <c>ManagementAnalyticsService.GetCustomerRequestPortfolio</c>
/// - <c>GET /api/v1/analytics/programmer-performance?personId={id}&amp;startMonth={ym}&amp;endMonth={ym}</c> -> Dapper query over <c>MonthlyCustomerPerformanceSnapshots</c> and <c>DailyWorkloadSnapshots</c>
/// - <c>GET /api/v1/analytics/programmer-workload?personId={id}</c> -> <c>ManagementAnalyticsService.GetProgrammerActiveWorkload</c>
/// - RBAC enforcement (<c>[Authorize(Roles = "Management")]</c>):
///   - Unauthenticated user -> HTTP 401 Unauthorized ProblemDetails
///   - Non-Management user (e.g. Programmer) -> HTTP 403 Forbidden ProblemDetails
///   - Authorized Management user -> HTTP 200 OK
/// </summary>
public class AnalyticsControllerTests : IAsyncLifetime
{
    private const string LocalDbFallback =
        "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb_AnalyticsController;Integrated Security=true;TrustServerCertificate=True;";

    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private bool _sqlServerAvailable;

    public AnalyticsControllerTests()
    {
        var baseConnection = DatabaseResetHelper.ResolveConnectionString() ?? LocalDbFallback;
        _connectionString = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = "CakraTestDb_AnalyticsController"
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
        migrationResult.Successful.Should().BeTrue("DbUp migrations must succeed for CakraTestDb_AnalyticsController");

        await using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
        }

        _factory = new CakraWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
                builder.UseSetting("ConnectionStrings:TestConnection", _connectionString);
            });

        await ResetTablesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_sqlServerAvailable)
        {
            await ResetTablesAsync();
        }

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private async Task ResetTablesAsync()
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = ["analytics", "request", "product", "customer", "organization", "identity"],
            TablesToIgnore = [new Table("dbo", "__SchemaVersions"), new Table("dbo", "SchemaVersions")]
        });

        await respawner.ResetAsync(conn);
    }

    [Fact]
    public async Task Unauthenticated_user_requests_to_analytics_endpoints_return_401_Unauthorized()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var sampleId = Guid.NewGuid();

        var portfolioResp = await client.GetAsync($"/api/v1/analytics/customer-portfolio?customerId={sampleId}");
        portfolioResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        portfolioResp.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var portfolioProblem = await portfolioResp.Content.ReadFromJsonAsync<JsonElement>();
        portfolioProblem.GetProperty("errorCode").GetString().Should().Be("UNAUTHORIZED");

        var perfResp = await client.GetAsync($"/api/v1/analytics/programmer-performance?personId={sampleId}&startMonth=2026-09&endMonth=2026-09");
        perfResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        perfResp.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var perfProblem = await perfResp.Content.ReadFromJsonAsync<JsonElement>();
        perfProblem.GetProperty("errorCode").GetString().Should().Be("UNAUTHORIZED");

        var workloadResp = await client.GetAsync($"/api/v1/analytics/programmer-workload?personId={sampleId}");
        workloadResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        workloadResp.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var workloadProblem = await workloadResp.Content.ReadFromJsonAsync<JsonElement>();
        workloadProblem.GetProperty("errorCode").GetString().Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Non_Management_user_requests_to_analytics_endpoints_return_403_Forbidden()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        Guid programmerPersonId;
        string programmerSessionToken;

        using (var scope = _factory!.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var userAccountRepo = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

            var programmer = await mediator.Send(new CreatePersonCommand(
                "Budi",
                "Santoso",
                $"budi.{Guid.NewGuid():N}@cakra.id"));
            programmerPersonId = programmer.Id;

            var programmerRole = await mediator.Send(new CreateRoleCommand("Programmer", "Operational Programmer Role"));
            await mediator.Send(new AssignRoleToPersonCommand(programmerPersonId, programmerRole.Id));

            var user = new UserAccount
            {
                Id = Guid.NewGuid(),
                PersonId = programmerPersonId,
                Username = $"budi.{Guid.NewGuid():N}"[..20],
                Email = programmer.Email,
                Status = UserAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = authService.HashPassword(user, "Password123!");
            await userAccountRepo.AddAsync(user);

            var loginResult = await authService.LoginAsync(user.Username, "Password123!");
            loginResult.Succeeded.Should().BeTrue();
            programmerSessionToken = loginResult.SessionToken!;
        }

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}={programmerSessionToken}");

        var sampleCustomerId = Guid.NewGuid();

        var portfolioResp = await client.GetAsync($"/api/v1/analytics/customer-portfolio?customerId={sampleCustomerId}");
        portfolioResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        portfolioResp.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var portfolioProblem = await portfolioResp.Content.ReadFromJsonAsync<JsonElement>();
        portfolioProblem.GetProperty("status").GetInt32().Should().Be(403);
        portfolioProblem.GetProperty("errorCode").GetString().Should().Be("FORBIDDEN");

        var perfResp = await client.GetAsync($"/api/v1/analytics/programmer-performance?personId={programmerPersonId}&startMonth=2026-09&endMonth=2026-09");
        perfResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        perfResp.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var perfProblem = await perfResp.Content.ReadFromJsonAsync<JsonElement>();
        perfProblem.GetProperty("status").GetInt32().Should().Be(403);
        perfProblem.GetProperty("errorCode").GetString().Should().Be("FORBIDDEN");

        var workloadResp = await client.GetAsync($"/api/v1/analytics/programmer-workload?personId={programmerPersonId}");
        workloadResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        workloadResp.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var workloadProblem = await workloadResp.Content.ReadFromJsonAsync<JsonElement>();
        workloadProblem.GetProperty("status").GetInt32().Should().Be(403);
        workloadProblem.GetProperty("errorCode").GetString().Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Authorized_Management_user_can_access_customer_portfolio_programmer_performance_and_programmer_workload_endpoints_with_200_OK()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        Guid managerPersonId;
        Guid programmerPersonId;
        Guid customerId;
        Guid activeRequestId;
        Guid blockerRequestId;
        Guid completedRequestId;
        string managementSessionToken;
        var todayUtc = DateTime.UtcNow.Date;
        var currentYearMonth = todayUtc.ToString("yyyy-MM");

        using (var scope = _factory!.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var userAccountRepo = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
            var snapshotJob = scope.ServiceProvider.GetRequiredService<AnalyticsSnapshotJob>();
            var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

            // 1. Seed Management Person + Role + UserAccount + Session
            var manager = await mediator.Send(new CreatePersonCommand(
                "Hendra",
                "Kusuma",
                $"hendra.mgt.{Guid.NewGuid():N}@cakra.id"));
            managerPersonId = manager.Id;

            var managementRole = await mediator.Send(new CreateRoleCommand("Management", "Executive & Operational Management"));
            await mediator.Send(new AssignRoleToPersonCommand(managerPersonId, managementRole.Id));

            var managerUser = new UserAccount
            {
                Id = Guid.NewGuid(),
                PersonId = managerPersonId,
                Username = $"hendra.{Guid.NewGuid():N}"[..20],
                Email = manager.Email,
                Status = UserAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            managerUser.PasswordHash = authService.HashPassword(managerUser, "Password123!");
            await userAccountRepo.AddAsync(managerUser);

            var loginResult = await authService.LoginAsync(managerUser.Username, "Password123!");
            loginResult.Succeeded.Should().BeTrue();
            managementSessionToken = loginResult.SessionToken!;

            // 2. Seed Programmer, Customer, Product, and Operational Requests
            var programmer = await mediator.Send(new CreatePersonCommand(
                "Rina",
                "Wijaya",
                $"rina.prog.{Guid.NewGuid():N}@cakra.id"));
            programmerPersonId = programmer.Id;

            var customer = await mediator.Send(new CreateCustomerCommand(
                "CUST-SARDJITO",
                "RSUP Dr. Sardjito",
                HasActiveMaintenanceContract: true));
            customerId = customer.Id;

            var product = await mediator.Send(new CreateProductCommand(
                "MYHOSPITAL",
                "MyHospital EMR",
                "Electronic Medical Record",
                programmerPersonId));

            currentContext?.Initialize(managerUser.Id, programmerPersonId, new[] { "Programmer", "Management" });

            // Active request (IN_PROGRESS)
            var reqActive = await mediator.Send(new RecordRequestCommand(
                Title: "Inpatient Pharmacy Dispensing Timeout",
                Description: "Optimize batch stock deduction query",
                CustomerId: customerId,
                ProductId: product.Id,
                RequestType: "Bug",
                Priority: "HIGH"));
            await mediator.Send(new AssignRequestOwnerCommand(reqActive.Id, programmerPersonId));
            await mediator.Send(new AcceptRequestResponsibilityCommand(reqActive.Id));
            activeRequestId = reqActive.Id;

            // Open blocker (ESCALATED)
            var reqBlocker = await mediator.Send(new RecordRequestCommand(
                Title: "BPJS Bridging TLS Handshake Failure",
                Description: "Requires hospital firewall whitelist update",
                CustomerId: customerId,
                ProductId: product.Id,
                RequestType: "Support",
                Priority: "URGENT"));
            await mediator.Send(new AssignRequestOwnerCommand(reqBlocker.Id, programmerPersonId));
            await mediator.Send(new EscalateRequestCommand(reqBlocker.Id, "Waiting on hospital network team"));
            blockerRequestId = reqBlocker.Id;

            // Completed request (COMPLETED)
            var reqCompleted = await mediator.Send(new RecordRequestCommand(
                Title: "Lab Result PDF Header Logo Fix",
                Description: "Fix aspect ratio on pathology report header",
                CustomerId: customerId,
                ProductId: product.Id,
                RequestType: "Bug",
                Priority: "NORMAL"));
            await mediator.Send(new AssignRequestOwnerCommand(reqCompleted.Id, programmerPersonId));
            await mediator.Send(new AcceptRequestResponsibilityCommand(reqCompleted.Id));
            await mediator.Send(new ReviewRequestCompletionCommand(reqCompleted.Id, "Updated report template v2.1"));
            completedRequestId = reqCompleted.Id;

            // 3. Capture daily workload snapshot and monthly customer performance snapshot
            await snapshotJob.TriggerDailySnapshotAsync(todayUtc);
            await snapshotJob.TriggerMonthlySnapshotAsync(currentYearMonth);
        }

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}={managementSessionToken}");

        // Endpoint 1: GET /api/v1/analytics/customer-portfolio?customerId={id}
        var portfolioResp = await client.GetAsync($"/api/v1/analytics/customer-portfolio?customerId={customerId}");
        portfolioResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var portfolio = await portfolioResp.Content.ReadFromJsonAsync<JsonElement>();
        portfolio.GetProperty("customerId").GetGuid().Should().Be(customerId);
        portfolio.GetProperty("customerCode").GetString().Should().Be("CUST-SARDJITO");
        portfolio.GetProperty("customerName").GetString().Should().Be("RSUP Dr. Sardjito");
        portfolio.GetProperty("hasActiveMaintenanceContract").GetBoolean().Should().BeTrue();
        portfolio.GetProperty("contractStatus").GetString().Should().Be("ACTIVE");
        portfolio.GetProperty("activeRequestsCount").GetInt32().Should().Be(2);
        portfolio.GetProperty("openBlockersCount").GetInt32().Should().Be(1);
        portfolio.GetProperty("recentCompletionsCount").GetInt32().Should().Be(1);
        portfolio.GetProperty("activeRequests").GetArrayLength().Should().Be(2);
        portfolio.GetProperty("openBlockers").GetArrayLength().Should().Be(1);
        portfolio.GetProperty("openBlockers")[0].GetProperty("requestId").GetGuid().Should().Be(blockerRequestId);
        portfolio.GetProperty("recentCompletions").GetArrayLength().Should().Be(1);
        portfolio.GetProperty("recentCompletions")[0].GetProperty("requestId").GetGuid().Should().Be(completedRequestId);

        // Endpoint 2: GET /api/v1/analytics/programmer-performance?personId={id}&startMonth={ym}&endMonth={ym}
        var perfResp = await client.GetAsync(
            $"/api/v1/analytics/programmer-performance?personId={programmerPersonId}&startMonth={currentYearMonth}&endMonth={currentYearMonth}");
        perfResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var perf = await perfResp.Content.ReadFromJsonAsync<JsonElement>();
        perf.GetProperty("personId").GetGuid().Should().Be(programmerPersonId);
        perf.GetProperty("personName").GetString().Should().Be("Rina Wijaya");
        perf.GetProperty("startMonth").GetString().Should().Be(currentYearMonth);
        perf.GetProperty("endMonth").GetString().Should().Be(currentYearMonth);
        perf.GetProperty("totalCompletedRequests").GetInt32().Should().Be(1);
        perf.GetProperty("monthlySeries").GetArrayLength().Should().Be(1);
        perf.GetProperty("dailyWorkloadSnapshots").GetArrayLength().Should().Be(1);
        perf.GetProperty("monthlyCustomerSnapshots").GetArrayLength().Should().Be(1);
        perf.GetProperty("monthlyCustomerSnapshots")[0].GetProperty("customerId").GetGuid().Should().Be(customerId);
        perf.GetProperty("monthlyCustomerSnapshots")[0].GetProperty("resolvedRequestsCount").GetInt32().Should().Be(1);

        // Endpoint 3: GET /api/v1/analytics/programmer-workload (unfiltered + filtered by personId)
        var allWorkloadResp = await client.GetAsync("/api/v1/analytics/programmer-workload");
        allWorkloadResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var allWorkloads = await allWorkloadResp.Content.ReadFromJsonAsync<JsonElement>();
        allWorkloads.GetArrayLength().Should().Be(2, "both Hendra and Rina are active persons in Organization");

        var filteredWorkloadResp = await client.GetAsync($"/api/v1/analytics/programmer-workload?personId={programmerPersonId}");
        filteredWorkloadResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var filteredWorkloads = await filteredWorkloadResp.Content.ReadFromJsonAsync<JsonElement>();
        filteredWorkloads.GetArrayLength().Should().Be(1);

        var rinaWorkload = filteredWorkloads[0];
        rinaWorkload.GetProperty("personId").GetGuid().Should().Be(programmerPersonId);
        rinaWorkload.GetProperty("personName").GetString().Should().Be("Rina Wijaya");
        rinaWorkload.GetProperty("inProgressCount").GetInt32().Should().Be(1);
        rinaWorkload.GetProperty("escalatedCount").GetInt32().Should().Be(1);
        rinaWorkload.GetProperty("totalActiveCount").GetInt32().Should().Be(2);
        rinaWorkload.GetProperty("activeRequests").GetArrayLength().Should().Be(2);
        var activeIds = rinaWorkload.GetProperty("activeRequests").EnumerateArray()
            .Select(r => r.GetProperty("requestId").GetGuid())
            .ToList();
        activeIds.Should().Contain(new[] { activeRequestId, blockerRequestId });
        activeIds.Should().NotContain(completedRequestId);

        // Verify validation error (400 Bad Request ProblemDetails) when customerId is omitted
        var missingCustomerResp = await client.GetAsync("/api/v1/analytics/customer-portfolio");
        missingCustomerResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        missingCustomerResp.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }
}
