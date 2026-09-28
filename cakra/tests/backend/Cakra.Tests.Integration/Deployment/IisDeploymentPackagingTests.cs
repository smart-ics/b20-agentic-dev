using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using Cakra.Api.Infrastructure.Migrations;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cakra.Tests.Integration.Deployment;

/// <summary>
/// Integration verification tests for Slice P7-S39 ("IIS Production Deployment Packaging &amp; Configuration",
/// Architecture §19.10):
/// <list type="number">
///   <item><description><c>deploy/publish.ps1</c>, <c>deploy/setup-iis.ps1</c>, <c>src/backend/Cakra.Api/appsettings.Production.json</c>, <c>src/backend/Cakra.Api/Cakra.Api.csproj</c>, and <c>src/backend/Cakra.Api/web.config</c> exist and configure IIS In-Process hosting.</description></item>
///   <item><description><c>./publish/Cakra.Api.dll</c>, <c>./publish/web.config</c> (<c>hostingModel="inprocess"</c>, <c>modules="AspNetCoreModuleV2"</c>), <c>./publish/appsettings.Production.json</c>, and <c>./publish/wwwroot/index.html</c> exist and are valid.</description></item>
///   <item><description><c>GET /</c> and SPA client routes serve the compiled Vue 3 SPA <c>index.html</c> (<c>text/html</c>) and <c>GET /health/ready</c> returns HTTP 200 OK when SQL Server is available.</description></item>
///   <item><description><c>dotnet ./publish/Cakra.Api.dll --migrate</c> executes DbUp SQL Server migrations cleanly with exit code 0.</description></item>
///   <item><description>Zero Docker or non-IIS deployment files (<c>Dockerfile</c>, <c>docker-compose*</c>, <c>.dockerignore</c>, systemd, nginx) exist in the repository.</description></item>
/// </list>
/// Uses isolated database <c>CakraTestDb_Deployment</c>.
/// </summary>
public sealed class IisDeploymentPackagingTests : IAsyncLifetime
{
    private const string LocalDbFallback =
        "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb_Deployment;Integrated Security=true;TrustServerCertificate=True;";

    private readonly string _connectionString;
    private readonly string _repoRoot;
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public IisDeploymentPackagingTests()
    {
        var configured = DatabaseResetHelper.ResolveConnectionString();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var builder = new SqlConnectionStringBuilder(configured)
            {
                InitialCatalog = "CakraTestDb_Deployment"
            };
            _connectionString = builder.ConnectionString;
        }
        else
        {
            _connectionString = LocalDbFallback;
        }

        _repoRoot = ResolveRepositoryRoot();
    }

    public Task InitializeAsync()
    {
        var migrationResult = DatabaseMigrationRunner.Run(_connectionString, NullLogger<DatabaseMigrationRunner>.Instance);
        migrationResult.Successful.Should().BeTrue(
            $"DbUp migrations must succeed for CakraTestDb_Deployment, but failed with: {migrationResult.Error}");

        var webRootPath = Path.Combine(_repoRoot, "src", "backend", "Cakra.Api", "wwwroot");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseWebRoot(webRootPath);
                builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
                builder.UseSetting("Database:RunMigrationsOnStartup", "true");
            });

        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public void Deployment_scripts_and_project_files_configure_IIS_InProcess_hosting_and_production_settings()
    {
        var publishScriptPath = Path.Combine(_repoRoot, "deploy", "publish.ps1");
        var setupIisScriptPath = Path.Combine(_repoRoot, "deploy", "setup-iis.ps1");
        var csprojPath = Path.Combine(_repoRoot, "src", "backend", "Cakra.Api", "Cakra.Api.csproj");
        var sourceWebConfigPath = Path.Combine(_repoRoot, "src", "backend", "Cakra.Api", "web.config");
        var prodAppSettingsPath = Path.Combine(_repoRoot, "src", "backend", "Cakra.Api", "appsettings.Production.json");

        File.Exists(publishScriptPath).Should().BeTrue("deploy/publish.ps1 must exist");
        File.Exists(setupIisScriptPath).Should().BeTrue("deploy/setup-iis.ps1 must exist");
        File.Exists(csprojPath).Should().BeTrue("src/backend/Cakra.Api/Cakra.Api.csproj must exist");
        File.Exists(sourceWebConfigPath).Should().BeTrue("src/backend/Cakra.Api/web.config must exist");
        File.Exists(prodAppSettingsPath).Should().BeTrue("src/backend/Cakra.Api/appsettings.Production.json must exist");

        var publishScript = File.ReadAllText(publishScriptPath);
        publishScript.Should().Contain("npm run build");
        publishScript.Should().Contain("src/frontend/Cakra.Web");
        publishScript.Should().Contain("src/backend/Cakra.Api/wwwroot");
        publishScript.Should().Contain("dotnet publish src/backend/Cakra.Api/Cakra.Api.csproj");
        publishScript.Should().Contain("./publish");
        publishScript.Should().Contain("hostingModel=\"inprocess\"");

        var setupIisScript = File.ReadAllText(setupIisScriptPath);
        setupIisScript.Should().Contain("CakraAppPool");
        setupIisScript.Should().Contain("managedRuntimeVersion");
        setupIisScript.Should().Contain("enable32BitAppOnWin64");
        setupIisScript.Should().Contain("AlwaysRunning");
        setupIisScript.Should().Contain("443");
        setupIisScript.Should().Contain("https");

        var csprojContent = File.ReadAllText(csprojPath);
        csprojContent.Should().Contain("<AspNetCoreHostingModel>InProcess</AspNetCoreHostingModel>");

        using var prodSettingsDoc = JsonDocument.Parse(File.ReadAllText(prodAppSettingsPath));
        prodSettingsDoc.RootElement.TryGetProperty("ConnectionStrings", out var connStrings).Should().BeTrue();
        connStrings.TryGetProperty("DefaultConnection", out _).Should().BeTrue();
        prodSettingsDoc.RootElement.TryGetProperty("Database", out var dbSection).Should().BeTrue();
        dbSection.GetProperty("RunMigrationsOnStartup").GetBoolean().Should().BeTrue();
        prodSettingsDoc.RootElement.TryGetProperty("Security", out var secSection).Should().BeTrue();
        secSection.GetProperty("RequireHttpsCookies").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Published_release_directory_contains_binaries_inprocess_web_config_and_compiled_SPA_assets()
    {
        var publishDir = Path.Combine(_repoRoot, "publish");
        var publishedDll = Path.Combine(publishDir, "Cakra.Api.dll");
        var publishedWebConfig = Path.Combine(publishDir, "web.config");
        var publishedProdSettings = Path.Combine(publishDir, "appsettings.Production.json");
        var publishedIndexHtml = Path.Combine(publishDir, "wwwroot", "index.html");
        var sourceWwwrootIndexHtml = Path.Combine(_repoRoot, "src", "backend", "Cakra.Api", "wwwroot", "index.html");

        File.Exists(publishedDll).Should().BeTrue("./publish/Cakra.Api.dll must exist");
        File.Exists(publishedWebConfig).Should().BeTrue("./publish/web.config must exist");
        File.Exists(publishedProdSettings).Should().BeTrue("./publish/appsettings.Production.json must exist");
        File.Exists(publishedIndexHtml).Should().BeTrue("./publish/wwwroot/index.html must exist");
        File.Exists(sourceWwwrootIndexHtml).Should().BeTrue("src/backend/Cakra.Api/wwwroot/index.html must exist");

        var webConfigXml = XDocument.Load(publishedWebConfig);
        var handlerAdd = webConfigXml.Descendants("handlers").Elements("add")
            .FirstOrDefault(e => string.Equals(e.Attribute("name")?.Value, "aspNetCore", StringComparison.OrdinalIgnoreCase));
        handlerAdd.Should().NotBeNull("web.config must register the aspNetCore handler");
        handlerAdd!.Attribute("modules")?.Value.Should().Be("AspNetCoreModuleV2");

        var aspNetCoreElement = webConfigXml.Descendants("aspNetCore").FirstOrDefault();
        aspNetCoreElement.Should().NotBeNull("web.config must include the <aspNetCore> element");
        aspNetCoreElement!.Attribute("hostingModel")?.Value.Should().Be("inprocess");
        aspNetCoreElement.Attribute("processPath")?.Value.Should().Be("dotnet");
        aspNetCoreElement.Attribute("arguments")?.Value.Should().Be(".\\Cakra.Api.dll");
        aspNetCoreElement.Attribute("stdoutLogEnabled")?.Value.Should().Be("false");

        var envVar = aspNetCoreElement.Descendants("environmentVariable")
            .FirstOrDefault(e => string.Equals(e.Attribute("name")?.Value, "ASPNETCORE_ENVIRONMENT", StringComparison.OrdinalIgnoreCase));
        envVar.Should().NotBeNull();
        envVar!.Attribute("value")?.Value.Should().Be("Production");

        var indexHtmlContent = File.ReadAllText(publishedIndexHtml);
        indexHtmlContent.Should().Contain("<div id=\"app\"></div>");
        indexHtmlContent.Should().Contain("/assets/");
    }

    [Fact]
    public async Task Get_root_serves_Vue3_SPA_index_html_and_health_ready_returns_200_OK()
    {
        var rootResponse = await _client!.GetAsync("/");
        rootResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        rootResponse.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        var rootHtml = await rootResponse.Content.ReadAsStringAsync();
        rootHtml.Should().Contain("<div id=\"app\"></div>");

        var spaDeepLinkResponse = await _client.GetAsync("/feed");
        spaDeepLinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        spaDeepLinkResponse.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        var deepLinkHtml = await spaDeepLinkResponse.Content.ReadAsStringAsync();
        deepLinkHtml.Should().Contain("<div id=\"app\"></div>");

        var liveResponse = await _client.GetAsync("/health/live");
        liveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await liveResponse.Content.ReadAsStringAsync()).Should().Be("Healthy");

        var readyResponse = await _client.GetAsync("/health/ready");
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await readyResponse.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task Published_executable_CLI_migrate_switch_applies_migrations_and_exits_zero()
    {
        var publishedDll = Path.Combine(_repoRoot, "publish", "Cakra.Api.dll");
        File.Exists(publishedDll).Should().BeTrue();

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{publishedDll}\" --migrate",
            WorkingDirectory = _repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.Environment["ConnectionStrings__DefaultConnection"] = _connectionString;

        using var process = Process.Start(psi);
        process.Should().NotBeNull();
        await process!.WaitForExitAsync();

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        process.ExitCode.Should().Be(0, $"dotnet Cakra.Api.dll --migrate failed. Stdout: {stdout} Stderr: {stderr}");

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var appliedScriptsCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;");
        appliedScriptsCount.Should().BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void Repository_contains_zero_Docker_or_non_IIS_deployment_files()
    {
        var excludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "node_modules",
            ".git",
            "bin",
            "obj"
        };

        var prohibitedFiles = new List<string>();
        ScanForProhibitedDeploymentFiles(_repoRoot, excludedDirectories, prohibitedFiles);

        prohibitedFiles.Should().BeEmpty(
            "Architecture §19.10 mandates IIS on Windows Server and prohibits Docker/non-IIS deployment artifacts");
    }

    private static void ScanForProhibitedDeploymentFiles(
        string directory,
        HashSet<string> excludedDirectories,
        List<string> results)
    {
        foreach (var file in Directory.GetFiles(directory))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.StartsWith("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("docker-compose", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, ".dockerignore", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".service", StringComparison.OrdinalIgnoreCase) ||
                (fileName.StartsWith("nginx", StringComparison.OrdinalIgnoreCase) && fileName.EndsWith(".conf", StringComparison.OrdinalIgnoreCase)))
            {
                results.Add(file);
            }
        }

        foreach (var subDir in Directory.GetDirectories(directory))
        {
            var dirName = Path.GetFileName(subDir);
            if (excludedDirectories.Contains(dirName))
            {
                continue;
            }

            ScanForProhibitedDeploymentFiles(subDir, excludedDirectories, results);
        }
    }

    private static string ResolveRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Cakra.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate Cakra.sln repository root from test base directory.");
    }
}
