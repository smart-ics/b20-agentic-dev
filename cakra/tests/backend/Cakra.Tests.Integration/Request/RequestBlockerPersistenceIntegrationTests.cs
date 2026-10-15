using Cakra.Api.Infrastructure.Migrations;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Persistence;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Respawn;
using Respawn.Graph;
using Xunit;

namespace Cakra.Tests.Integration.Request;

/// <summary>
/// Integration tests verifying P2-S03:
/// Dapper Repository Mapping & Transactional Persistence for Blockers:
/// - In GetByIdAsync, execute parameterized query selecting from [request].[RequestBlockers] for @RequestId ordered by CreatedAtUtc ASC.
/// - Pass hydrated blockers to Request.Rehydrate.
/// - In SaveAsync / UpdateAsync, persist changes to [request].[Requests] (ActiveBlockersCount),
///   insert new blocker rows, and update cleared blocker rows (IsCleared, ClearedAtUtc, ClearedByPersonId, ClearanceReason, UpdatedAt)
///   within the existing transactional connection scope.
/// - Concurrency, cascade delete, and nullable person IDs handled gracefully.
/// </summary>
[Collection("OrganizationDatabase")]
public class RequestBlockerPersistenceIntegrationTests : IAsyncLifetime
{
    private const string LocalDbFallback = "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb;Integrated Security=true;TrustServerCertificate=True;";
    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private bool _sqlServerAvailable;

    public RequestBlockerPersistenceIntegrationTests()
    {
        _connectionString = DatabaseResetHelper.ResolveConnectionString() ?? LocalDbFallback;
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
        migrationResult.Successful.Should().BeTrue("DbUp migrations including 0021_request_blockers.sql must execute without errors");

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
            SchemasToInclude = ["request", "product", "customer", "organization"],
            TablesToIgnore = [new Table("dbo", "__SchemaVersions"), new Table("dbo", "SchemaVersions")]
        });

        await respawner.ResetAsync(conn);
    }

    [Fact]
    public async Task AddAsync_WithInitialBlocker_PersistsRequestAndBlockers_AndHydratesViaGetByIdAsync()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRequestRepository>();

        var requestId = Guid.NewGuid();
        var actorPersonId = Guid.NewGuid();
        var ownerPersonId = Guid.NewGuid();
        var blockerId = Guid.NewGuid();
        var blockerTime = DateTime.UtcNow;

        var request = Cakra.Modules.Request.Domain.Request.Record(
            id: requestId,
            title: "Request with initial blocker",
            description: "Testing AddAsync blocker persistence",
            requestType: "DEFECT",
            actorPersonId: actorPersonId,
            initialOwnerPersonId: ownerPersonId);

        request.AddBlocker(blockerId, "Waiting on API contract finalization", actorPersonId, blockerTime);

        // Act
        await repository.AddAsync(request);
        var loaded = await repository.GetByIdAsync(requestId);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(requestId);
        loaded.Status.Should().Be(RequestStatus.Paused);
        loaded.ActiveBlockersCount.Should().Be(1);
        loaded.Blockers.Should().HaveCount(1);

        var loadedBlocker = loaded.Blockers.Single();
        loadedBlocker.Id.Should().Be(blockerId);
        loadedBlocker.RequestId.Should().Be(requestId);
        loadedBlocker.Reason.Should().Be("Waiting on API contract finalization");
        loadedBlocker.CreatedByPersonId.Should().Be(actorPersonId);
        loadedBlocker.CreatedAtUtc.Should().BeCloseTo(blockerTime, TimeSpan.FromMilliseconds(50));
        loadedBlocker.IsCleared.Should().BeFalse();
        loadedBlocker.ClearedAtUtc.Should().BeNull();
        loadedBlocker.ClearedByPersonId.Should().BeNull();
        loadedBlocker.ClearanceReason.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_TransactionalPersistence_InsertsNewBlockers_AndUpdatesClearedBlockers()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRequestRepository>();

        var requestId = Guid.NewGuid();
        var actorPersonId = Guid.NewGuid();
        var ownerPersonId = Guid.NewGuid();

        var request = Cakra.Modules.Request.Domain.Request.Record(
            id: requestId,
            title: "Request for incremental blocker transitions",
            description: "Testing SaveAsync blocker updates",
            requestType: "IMPROVEMENT",
            actorPersonId: actorPersonId,
            initialOwnerPersonId: ownerPersonId);

        await repository.AddAsync(request);

        // 1. Add first blocker and save
        var blocker1Id = Guid.NewGuid();
        var blocker1Time = DateTime.UtcNow;
        request.AddBlocker(blocker1Id, "External vendor dependency blocked", actorPersonId, blocker1Time);
        await repository.SaveAsync(request);

        var checkpoint1 = await repository.GetByIdAsync(requestId);
        checkpoint1.Should().NotBeNull();
        checkpoint1!.ActiveBlockersCount.Should().Be(1);
        checkpoint1.Blockers.Should().HaveCount(1);
        checkpoint1.Blockers.Single().IsCleared.Should().BeFalse();

        // 2. Add second blocker, and clear the first blocker
        var blocker2Id = Guid.NewGuid();
        var blocker2Time = DateTime.UtcNow.AddMinutes(5);
        request.AddBlocker(blocker2Id, "Database schema migration blocked", ownerPersonId, blocker2Time);

        var clearTime = DateTime.UtcNow.AddMinutes(10);
        var clearedByPersonId = Guid.NewGuid();
        request.ClearBlocker(blocker1Id, clearedByPersonId, "Vendor provided hotfix package", clearTime);

        await repository.SaveAsync(request);

        // 3. Reload and verify round-trip persistence
        var checkpoint2 = await repository.GetByIdAsync(requestId);
        checkpoint2.Should().NotBeNull();
        checkpoint2!.ActiveBlockersCount.Should().Be(1);
        checkpoint2.Blockers.Should().HaveCount(2);

        var loadedB1 = checkpoint2.Blockers.Single(b => b.Id == blocker1Id);
        loadedB1.IsCleared.Should().BeTrue();
        loadedB1.ClearedByPersonId.Should().Be(clearedByPersonId);
        loadedB1.ClearanceReason.Should().Be("Vendor provided hotfix package");
        loadedB1.ClearedAtUtc.Should().BeCloseTo(clearTime, TimeSpan.FromMilliseconds(50));
        loadedB1.Reason.Should().Be("External vendor dependency blocked");

        var loadedB2 = checkpoint2.Blockers.Single(b => b.Id == blocker2Id);
        loadedB2.IsCleared.Should().BeFalse();
        loadedB2.CreatedByPersonId.Should().Be(ownerPersonId);
        loadedB2.Reason.Should().Be("Database schema migration blocked");
        loadedB2.ClearedByPersonId.Should().BeNull();
        loadedB2.ClearanceReason.Should().BeNull();

        // 4. Clear second blocker with null clearance reason (graceful nullable handling)
        var clearTime2 = DateTime.UtcNow.AddMinutes(15);
        request.ClearBlocker(blocker2Id, ownerPersonId, null, clearTime2);
        await repository.SaveAsync(request);

        var checkpoint3 = await repository.GetByIdAsync(requestId);
        checkpoint3.Should().NotBeNull();
        checkpoint3!.ActiveBlockersCount.Should().Be(0);
        checkpoint3.Blockers.Should().HaveCount(2);
        checkpoint3.Blockers.All(b => b.IsCleared).Should().BeTrue();

        var loadedB2Cleared = checkpoint3.Blockers.Single(b => b.Id == blocker2Id);
        loadedB2Cleared.IsCleared.Should().BeTrue();
        loadedB2Cleared.ClearanceReason.Should().BeNull();
        loadedB2Cleared.ClearedByPersonId.Should().Be(ownerPersonId);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsBlockersOrderedByCreatedAtUtcAscending()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRequestRepository>();

        var requestId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var request = Cakra.Modules.Request.Domain.Request.Record(
            id: requestId,
            title: "Blocker Ordering Test",
            description: "Verifying CreatedAtUtc ASC sorting",
            requestType: "SUPPORT",
            actorPersonId: actorId,
            initialOwnerPersonId: actorId);

        var baseTime = DateTime.UtcNow;
        var b1Id = Guid.NewGuid();
        var b2Id = Guid.NewGuid();
        var b3Id = Guid.NewGuid();

        request.AddBlocker(b1Id, "Blocker 1 (earliest)", actorId, baseTime.AddHours(-2));
        request.AddBlocker(b2Id, "Blocker 2 (middle)", actorId, baseTime.AddHours(-1));
        request.AddBlocker(b3Id, "Blocker 3 (latest)", actorId, baseTime);

        await repository.AddAsync(request);

        var loaded = await repository.GetByIdAsync(requestId);
        loaded.Should().NotBeNull();
        loaded!.Blockers.Should().HaveCount(3);

        var blockersList = loaded.Blockers.ToList();
        blockersList[0].Id.Should().Be(b1Id);
        blockersList[1].Id.Should().Be(b2Id);
        blockersList[2].Id.Should().Be(b3Id);
    }

    [Fact]
    public async Task DeleteAsync_CascadesAndRemovesBlockersFromDatabase()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRequestRepository>();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

        var requestId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var request = Cakra.Modules.Request.Domain.Request.Record(
            id: requestId,
            title: "Delete Cascade Test",
            description: "Verifying FK ON DELETE CASCADE",
            requestType: "GENERAL",
            actorPersonId: actorId,
            initialOwnerPersonId: actorId);

        request.AddBlocker(Guid.NewGuid(), "Impending blocker", actorId, DateTime.UtcNow);
        await repository.AddAsync(request);

        // Verify row exists in DB
        using (var connection = connectionFactory.CreateConnection())
        {
            var count = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM [request].[RequestBlockers] WHERE [RequestId] = @RequestId;",
                new { RequestId = requestId });
            count.Should().Be(1);
        }

        // Delete request
        await repository.DeleteAsync(requestId);

        // Verify request is gone
        var loaded = await repository.GetByIdAsync(requestId);
        loaded.Should().BeNull();

        // Verify blocker was cascade-deleted by foreign key
        using (var connection = connectionFactory.CreateConnection())
        {
            var blockerCount = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM [request].[RequestBlockers] WHERE [RequestId] = @RequestId;",
                new { RequestId = requestId });
            blockerCount.Should().Be(0);
        }
    }
}
