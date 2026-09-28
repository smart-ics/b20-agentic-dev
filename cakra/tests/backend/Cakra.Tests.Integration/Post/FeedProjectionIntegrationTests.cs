using Cakra.Api.Infrastructure.Context;
using Cakra.Api.Infrastructure.Migrations;
using Cakra.Core;
using Cakra.Modules.Customer.Services;
using Cakra.Modules.Organization.Commands;
using Cakra.Modules.Post;
using Cakra.Modules.Post.Domain;
using Cakra.Modules.Post.Services;
using Cakra.Modules.Product.Services;
using Cakra.Modules.Request.Services;
using Cakra.Modules.WorkPackage.Services;
using Dapper;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Respawn;
using Respawn.Graph;
using Xunit;

namespace Cakra.Tests.Integration.Post;

/// <summary>
/// SQL Server integration tests verifying P6-S29 Feed Projection — <c>FeedItems</c> Table &amp; <c>FeedProjectionHandler</c>:
/// <list type="bullet">
///   <item>
///     <description>
///       DbUp migration script <c>0009_feed_items_table.sql</c> creates <c>post.FeedItems</c> with all
///       Architecture §12 columns, intra-schema FK <c>FK_FeedItems_Posts</c>, unique constraint
///       <c>UQ_FeedItems_PostId</c>, zero cross-schema foreign keys, and query indexes.
///     </description>
///   </item>
///   <item>
///     <description>
///       <c>FeedProjectionHandler</c> handles all six Post domain events (<c>PostCreated</c>,
///       <c>CommentAdded</c>, <c>ReactionAdded</c>, <c>ReactionRemoved</c>, <c>PostVisibilityChanged</c>,
///       <c>PostArchived</c>) synchronously in-process using Dapper parameterized SQL.
///     </description>
///   </item>
///   <item>
///     <description>
///       <c>FeedProjectionRebuilder.RebuildAll()</c> and <c>EnqueueRebuildAsync()</c> (via
///       <see cref="System.Threading.Channels"/> <see cref="IHostedService"/>) truncate and completely
///       regenerate <c>post.FeedItems</c> from authoritative <c>post.Posts</c>, <c>post.PostReferences</c>,
///       <c>post.Comments</c>, and <c>post.Reactions</c>.
///     </description>
///   </item>
/// </list>
/// Uses isolated database <c>CakraTestDb_FeedProjection</c> + Respawn cleanup.
/// </summary>
public sealed class FeedProjectionIntegrationTests : IAsyncLifetime
{
    private const string LocalDbFallback = "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb_FeedProjection;Integrated Security=true;TrustServerCertificate=True;";
    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private bool _sqlServerAvailable;

    public FeedProjectionIntegrationTests()
    {
        var baseConnectionString = DatabaseResetHelper.ResolveConnectionString() ?? LocalDbFallback;
        _connectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "CakraTestDb_FeedProjection"
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
        migrationResult.Successful.Should().BeTrue("DbUp migrations must execute without errors");

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
            SchemasToInclude = ["post", "workpackage", "request", "product", "customer", "organization"],
            TablesToIgnore = [new Table("dbo", "__SchemaVersions"), new Table("dbo", "SchemaVersions")]
        });

        await respawner.ResetAsync(conn);
    }

    [Fact]
    public async Task Migration_0009_creates_FeedItems_table_with_all_columns_indexes_intra_schema_fk_and_zero_cross_schema_fks()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string columnsSql = """
            SELECT c.name
            FROM sys.columns c
            INNER JOIN sys.tables t ON t.object_id = c.object_id
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = N'post' AND t.name = N'FeedItems'
            ORDER BY c.column_id;
            """;

        var columns = (await conn.QueryAsync<string>(columnsSql)).ToList();
        columns.Should().Contain(new[]
        {
            "FeedItemId",
            "PostId",
            "AuthorPersonId",
            "AuthorName",
            "PostType",
            "Source",
            "Title",
            "ContentExcerpt",
            "Summary",
            "Status",
            "Visibility",
            "IsException",
            "ExceptionType",
            "ReferenceType",
            "ReferenceId",
            "ReferenceDisplay",
            "CustomerId",
            "CustomerName",
            "ProductId",
            "ProductName",
            "RequestId",
            "WorkPackageId",
            "CommentCount",
            "LatestCommentExcerpt",
            "ReactionCountsJson",
            "CreatedAt",
            "UpdatedAt",
            "LastActivityAt"
        });

        const string foreignKeysSql = """
            SELECT
                fk.name AS ForeignKeyName,
                SCHEMA_NAME(tp.schema_id) AS ParentSchema,
                tp.name AS ParentTable,
                SCHEMA_NAME(tr.schema_id) AS ReferencedSchema,
                tr.name AS ReferencedTable
            FROM sys.foreign_keys fk
            INNER JOIN sys.tables tp ON tp.object_id = fk.parent_object_id
            INNER JOIN sys.tables tr ON tr.object_id = fk.referenced_object_id
            WHERE SCHEMA_NAME(tp.schema_id) = N'post'
              AND tp.name = N'FeedItems';
            """;

        var fks = (await conn.QueryAsync<ForeignKeyInfo>(foreignKeysSql)).ToList();
        fks.Should().ContainSingle();
        fks[0].ForeignKeyName.Should().Be("FK_FeedItems_Posts");
        fks[0].ParentSchema.Should().Be("post");
        fks[0].ReferencedSchema.Should().Be("post");
        fks[0].ReferencedTable.Should().Be("Posts");

        const string indexesSql = """
            SELECT i.name
            FROM sys.indexes i
            INNER JOIN sys.tables t ON t.object_id = i.object_id
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = N'post' AND t.name = N'FeedItems' AND i.name IS NOT NULL;
            """;

        var indexes = (await conn.QueryAsync<string>(indexesSql)).ToList();
        indexes.Should().Contain(new[]
        {
            "PK_FeedItems",
            "UQ_FeedItems_PostId",
            "IX_FeedItems_Visibility_Status_CreatedAt",
            "IX_FeedItems_CustomerId_CreatedAt",
            "IX_FeedItems_ProductId_CreatedAt",
            "IX_FeedItems_IsException_CreatedAt",
            "IX_FeedItems_CreatedAt"
        });

        using var scope = _factory!.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IFeedProjectionRebuilder>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<FeedProjectionRebuilder>().Should().NotBeNull();
        scope.ServiceProvider.GetServices<IHostedService>()
            .Should().Contain(s => s is FeedProjectionRebuilder);
    }

    [Fact]
    public async Task Post_creation_comments_reactions_visibility_and_archive_synchronously_update_FeedItems()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        // 1. Seed Organization Persons, Customer, Product, Request, and WorkPackage
        var author = await mediator.Send(new CreatePersonCommand(
            "Dian",
            "Purnama",
            $"dian.{Guid.NewGuid():N}@cakra.id"));

        var reviewer = await mediator.Send(new CreatePersonCommand(
            "Hendra",
            "Wibowo",
            $"hendra.{Guid.NewGuid():N}@cakra.id"));

        var customer = await mediator.Send(new CreateCustomerCommand(
            $"CUST-{Guid.NewGuid():N}"[..14],
            "RSUD Pasar Minggu",
            HasActiveMaintenanceContract: true));

        var product = await mediator.Send(new CreateProductCommand(
            $"PRD-{Guid.NewGuid():N}"[..14],
            "MyHospitalEMR",
            "Electronic Medical Record Inpatient Module",
            author.Id));

        currentContext?.Initialize(Guid.NewGuid(), author.Id, new[] { "Implementator", "Programmer" });

        var request = await mediator.Send(new RecordRequestCommand(
            Title: "SOAP note autosave timeout on slow ward Wi-Fi",
            Description: "Doctors lose draft SOAP notes when ward Wi-Fi latency exceeds 3000ms.",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "BUG",
            Priority: "HIGH",
            ActorPersonId: author.Id));

        var workPackage = await mediator.Send(new CreateWorkPackageCommand(
            Name: "EMR Ward Resilience Q3",
            Objective: "Improve offline buffering and autosave retry logic for inpatient wards.",
            OwnerPersonId: author.Id,
            CustomerId: customer.Id,
            ProductId: product.Id));

        // 2. Create Operational Post -> verify FeedItem is synchronously inserted
        var post = await mediator.Send(new CreateOperationalPostCommand(
            Title: "Increased SOAP autosave timeout and added local sessionStorage draft buffer",
            Content: "Autosave timeout increased from 3s to 15s with exponential backoff and local draft recovery.",
            AuthorPersonId: author.Id,
            RequestId: request.Id,
            WorkPackageId: workPackage.Id));

        var feedItemAfterCreate = await GetFeedItemByPostIdAsync(post.Id);
        feedItemAfterCreate.Should().NotBeNull("PostCreated event must synchronously insert a FeedItems row");
        feedItemAfterCreate!.FeedItemId.Should().NotBeEmpty();
        feedItemAfterCreate.PostId.Should().Be(post.Id);
        feedItemAfterCreate.Title.Should().Be(post.Title);
        feedItemAfterCreate.ContentExcerpt.Should().Be(post.Content);
        feedItemAfterCreate.Summary.Should().Be(post.Content);
        feedItemAfterCreate.AuthorPersonId.Should().Be(author.Id);
        feedItemAfterCreate.AuthorName.Should().Be("Dian Purnama");
        feedItemAfterCreate.PostType.Should().Be(PostSourceNames.HumanAuthored);
        feedItemAfterCreate.Source.Should().Be(PostSourceNames.HumanAuthored);
        feedItemAfterCreate.CustomerId.Should().Be(customer.Id);
        feedItemAfterCreate.CustomerName.Should().Be("RSUD Pasar Minggu");
        feedItemAfterCreate.ProductId.Should().Be(product.Id);
        feedItemAfterCreate.ProductName.Should().Be("MyHospitalEMR");
        feedItemAfterCreate.RequestId.Should().Be(request.Id);
        feedItemAfterCreate.WorkPackageId.Should().Be(workPackage.Id);
        feedItemAfterCreate.ReferenceType.Should().Be(PostReferenceTypes.Request);
        feedItemAfterCreate.ReferenceId.Should().Be(request.Id);
        feedItemAfterCreate.ReferenceDisplay.Should().Be(request.Title);
        feedItemAfterCreate.CommentCount.Should().Be(0);
        feedItemAfterCreate.LatestCommentExcerpt.Should().BeNull();
        feedItemAfterCreate.ReactionCountsJson.Should().Be("{}");
        feedItemAfterCreate.ReactionCounts.Should().BeEmpty();
        feedItemAfterCreate.IsException.Should().BeFalse();
        feedItemAfterCreate.ExceptionType.Should().BeNull();
        feedItemAfterCreate.Visibility.Should().Be(PostVisibilityNames.Visible);
        feedItemAfterCreate.Status.Should().Be(PostStatusNames.Active);

        var initialActivityAt = feedItemAfterCreate.LastActivityAt;

        // 3. Add Comments -> verify CommentCount increments, LatestCommentExcerpt and LastActivityAt update
        await mediator.Send(new PostCommentCommand(
            PostId: post.Id,
            Content: "Verified on Ward 4B tablet; draft recovery works seamlessly.",
            AuthorPersonId: reviewer.Id));

        var feedItemAfterComment1 = await GetFeedItemByPostIdAsync(post.Id);
        feedItemAfterComment1.Should().NotBeNull();
        feedItemAfterComment1!.CommentCount.Should().Be(1);
        feedItemAfterComment1.LatestCommentExcerpt.Should().Be("Verified on Ward 4B tablet; draft recovery works seamlessly.");
        feedItemAfterComment1.LastActivityAt.Should().BeAfter(initialActivityAt);

        var afterComment1ActivityAt = feedItemAfterComment1.LastActivityAt;

        await mediator.Send(new PostCommentCommand(
            PostId: post.Id,
            Content: "Rolling out hotfix build 2.4.9 to all inpatient nurse stations tonight.",
            AuthorPersonId: author.Id));

        var feedItemAfterComment2 = await GetFeedItemByPostIdAsync(post.Id);
        feedItemAfterComment2.Should().NotBeNull();
        feedItemAfterComment2!.CommentCount.Should().Be(2);
        feedItemAfterComment2.LatestCommentExcerpt.Should().Be("Rolling out hotfix build 2.4.9 to all inpatient nurse stations tonight.");
        feedItemAfterComment2.LastActivityAt.Should().BeAfter(afterComment1ActivityAt);

        // 4. Add & Remove Reactions -> verify ReactionCountsJson updates atomically
        await mediator.Send(new AddReactionCommand(
            PostId: post.Id,
            ReactionType: PostReactionTypes.Seen,
            PersonId: reviewer.Id));

        await mediator.Send(new AddReactionCommand(
            PostId: post.Id,
            ReactionType: PostReactionTypes.Seen,
            PersonId: author.Id));

        await mediator.Send(new AddReactionCommand(
            PostId: post.Id,
            ReactionType: PostReactionTypes.Experienced,
            PersonId: reviewer.Id));

        var feedItemAfterReactions = await GetFeedItemByPostIdAsync(post.Id);
        feedItemAfterReactions.Should().NotBeNull();
        feedItemAfterReactions!.ReactionCounts.Should().HaveCount(2);
        feedItemAfterReactions.ReactionCounts[PostReactionTypes.Seen].Should().Be(2);
        feedItemAfterReactions.ReactionCounts[PostReactionTypes.Experienced].Should().Be(1);
        feedItemAfterReactions.ReactionCount.Should().Be(3);

        await mediator.Send(new RemoveReactionCommand(
            PostId: post.Id,
            ReactionType: PostReactionTypes.Experienced,
            PersonId: reviewer.Id));

        var feedItemAfterReactionRemoved = await GetFeedItemByPostIdAsync(post.Id);
        feedItemAfterReactionRemoved.Should().NotBeNull();
        feedItemAfterReactionRemoved!.ReactionCounts.Should().ContainSingle();
        feedItemAfterReactionRemoved.ReactionCounts[PostReactionTypes.Seen].Should().Be(2);
        feedItemAfterReactionRemoved.ReactionCounts.ContainsKey(PostReactionTypes.Experienced).Should().BeFalse();
        feedItemAfterReactionRemoved.ReactionCountsJson.Should().Be("{\"SEEN\":2}");

        // 5. Toggle Visibility -> verify Visibility updates on FeedItems
        await mediator.Send(new TogglePostVisibilityCommand(post.Id, PostVisibilityNames.Hidden, author.Id));

        var feedItemHidden = await GetFeedItemByPostIdAsync(post.Id);
        feedItemHidden.Should().NotBeNull();
        feedItemHidden!.Visibility.Should().Be(PostVisibilityNames.Hidden);
        feedItemHidden.Status.Should().Be(PostStatusNames.Active);

        await mediator.Send(new TogglePostVisibilityCommand(post.Id, PostVisibilityNames.Visible, author.Id));

        var feedItemVisible = await GetFeedItemByPostIdAsync(post.Id);
        feedItemVisible.Should().NotBeNull();
        feedItemVisible!.Visibility.Should().Be(PostVisibilityNames.Visible);

        // 6. Archive Post -> verify Status = 'ARCHIVED' on FeedItems while retaining counts
        await mediator.Send(new ArchivePostCommand(post.Id, author.Id));

        var feedItemArchived = await GetFeedItemByPostIdAsync(post.Id);
        feedItemArchived.Should().NotBeNull();
        feedItemArchived!.Status.Should().Be(PostStatusNames.Archived);
        feedItemArchived.CommentCount.Should().Be(2);
        feedItemArchived.ReactionCountsJson.Should().Be("{\"SEEN\":2}");
    }

    [Fact]
    public async Task FeedProjectionRebuilder_RebuildAll_and_EnqueueRebuildAsync_regenerate_FeedItems_matching_Posts()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var rebuilder = scope.ServiceProvider.GetRequiredService<IFeedProjectionRebuilder>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        var author = await mediator.Send(new CreatePersonCommand(
            "Arif",
            "Budiman",
            $"arif.{Guid.NewGuid():N}@cakra.id"));

        var customer = await mediator.Send(new CreateCustomerCommand(
            $"CUST-{Guid.NewGuid():N}"[..14],
            "RSUP Fatmawati",
            HasActiveMaintenanceContract: true));

        var product = await mediator.Send(new CreateProductCommand(
            $"PRD-{Guid.NewGuid():N}"[..14],
            "MyHospital Laboratory",
            "LIS Analyzer Integration Module",
            author.Id));

        currentContext?.Initialize(Guid.NewGuid(), author.Id, new[] { "Implementator", "Programmer" });

        var request = await mediator.Send(new RecordRequestCommand(
            Title: "HL7 ASTM serial port parser framing error on Sysmex XN-1000",
            Description: "Checksums fail when analyzer sends multi-frame WBC histograms.",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "BUG",
            Priority: "HIGH",
            ActorPersonId: author.Id));

        // Create Post 1 (human-authored, with comment + reactions)
        var post1 = await mediator.Send(new CreateOperationalPostCommand(
            Title: "Patched ASTM E1381 ETB/ETX frame accumulator for Sysmex XN-1000",
            Content: "Updated frame accumulator to strip intermediate STX/ETB checksum bytes before assembling OBX segments.",
            AuthorPersonId: author.Id,
            RequestId: request.Id));

        await mediator.Send(new PostCommentCommand(
            PostId: post1.Id,
            Content: "Validated against 120 CBC samples in Lab Hematology.",
            AuthorPersonId: author.Id));

        await mediator.Send(new AddReactionCommand(
            PostId: post1.Id,
            ReactionType: PostReactionTypes.HaveIdea,
            PersonId: author.Id));

        // Create Post 2 (system-generated exception post, then archived)
        var post2 = await mediator.Send(new RecordSystemPostCommand(
            Title: "Escalation Alert: HL7 ASTM serial port parser framing error",
            Content: "Escalated due to morning lab queue backlog.",
            AuthorPersonId: author.Id,
            SourceEventType: "RequestEscalated",
            RequestId: request.Id,
            IsException: true,
            ExceptionType: PostExceptionTypes.Escalation));

        await mediator.Send(new ArchivePostCommand(post2.Id, author.Id));

        // Wipe post.FeedItems directly in SQL Server to simulate projection loss
        await using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync("TRUNCATE TABLE [post].[FeedItems];");
            var countAfterTruncate = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM [post].[FeedItems];");
            countAfterTruncate.Should().Be(0);
        }

        // 1. Rebuild synchronously via RebuildAll()
        var rebuiltCount = rebuilder.RebuildAll();
        rebuiltCount.Should().Be(2);

        var rebuiltItem1 = await GetFeedItemByPostIdAsync(post1.Id);
        rebuiltItem1.Should().NotBeNull();
        rebuiltItem1!.Title.Should().Be(post1.Title);
        rebuiltItem1.AuthorPersonId.Should().Be(author.Id);
        rebuiltItem1.AuthorName.Should().Be("Arif Budiman");
        rebuiltItem1.CustomerId.Should().Be(customer.Id);
        rebuiltItem1.CustomerName.Should().Be("RSUP Fatmawati");
        rebuiltItem1.ProductId.Should().Be(product.Id);
        rebuiltItem1.ProductName.Should().Be("MyHospital Laboratory");
        rebuiltItem1.RequestId.Should().Be(request.Id);
        rebuiltItem1.CommentCount.Should().Be(1);
        rebuiltItem1.LatestCommentExcerpt.Should().Be("Validated against 120 CBC samples in Lab Hematology.");
        rebuiltItem1.ReactionCountsJson.Should().Be("{\"HAVE_IDEA\":1}");
        rebuiltItem1.Status.Should().Be(PostStatusNames.Active);
        rebuiltItem1.Visibility.Should().Be(PostVisibilityNames.Visible);

        var rebuiltItem2 = await GetFeedItemByPostIdAsync(post2.Id);
        rebuiltItem2.Should().NotBeNull();
        rebuiltItem2!.PostType.Should().Be(PostSourceNames.SystemGenerated);
        rebuiltItem2.IsException.Should().BeTrue();
        rebuiltItem2.ExceptionType.Should().Be(PostExceptionTypes.Escalation);
        rebuiltItem2.Status.Should().Be(PostStatusNames.Archived);

        // 2. Also verify background Channel-based rebuild via EnqueueRebuildAsync
        await using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync("TRUNCATE TABLE [post].[FeedItems];");
        }

        var completionTask = await rebuilder.EnqueueRebuildAsync();
        var channelRebuiltCount = await completionTask.WaitAsync(TimeSpan.FromSeconds(10));
        channelRebuiltCount.Should().Be(2);

        var channelRebuiltItem1 = await GetFeedItemByPostIdAsync(post1.Id);
        channelRebuiltItem1.Should().NotBeNull();
        channelRebuiltItem1!.CommentCount.Should().Be(1);
        channelRebuiltItem1.ReactionCountsJson.Should().Be("{\"HAVE_IDEA\":1}");
    }

    private async Task<FeedItemDto?> GetFeedItemByPostIdAsync(Guid postId)
    {
        const string sql = """
            SELECT
                [FeedItemId],
                [PostId],
                [AuthorPersonId],
                [AuthorName],
                [PostType],
                [Source],
                [Title],
                [ContentExcerpt],
                [Summary],
                [Status],
                [Visibility],
                [IsException],
                [ExceptionType],
                [ReferenceType],
                [ReferenceId],
                [ReferenceDisplay],
                [CustomerId],
                [CustomerName],
                [ProductId],
                [ProductName],
                [RequestId],
                [WorkPackageId],
                [CommentCount],
                [LatestCommentExcerpt],
                [ReactionCountsJson],
                [CreatedAt],
                [UpdatedAt],
                [LastActivityAt]
            FROM [post].[FeedItems]
            WHERE [PostId] = @PostId;
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<FeedItemDto>(sql, new { PostId = postId });
    }

    private sealed class ForeignKeyInfo
    {
        public string ForeignKeyName { get; set; } = string.Empty;
        public string ParentSchema { get; set; } = string.Empty;
        public string ParentTable { get; set; } = string.Empty;
        public string ReferencedSchema { get; set; } = string.Empty;
        public string ReferencedTable { get; set; } = string.Empty;
    }
}
