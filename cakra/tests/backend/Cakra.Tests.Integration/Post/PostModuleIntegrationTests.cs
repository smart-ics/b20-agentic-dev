using Cakra.Api.Infrastructure.Context;
using Cakra.Api.Infrastructure.Migrations;
using Cakra.Core;
using Cakra.Modules.Customer.Services;
using Cakra.Modules.Organization.Commands;
using Cakra.Modules.Post;
using Cakra.Modules.Post.Domain;
using Cakra.Modules.Post.Domain.Events;
using Cakra.Modules.Post.Domain.Exceptions;
using Cakra.Modules.Post.Services;
using Cakra.Modules.Product.Services;
using Cakra.Modules.Request.Services;
using Cakra.Modules.WorkPackage.Services;
using Dapper;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Respawn;
using Respawn.Graph;
using Xunit;

namespace Cakra.Tests.Integration.Post;

/// <summary>
/// Integration and domain tests verifying P6-S28 Post Module — Domain, Application Services &amp; Persistence:
/// - DbUp migration script <c>0008_post_tables.sql</c> creates <c>post.Posts</c>, <c>post.Comments</c>,
///   <c>post.Reactions</c>, and <c>post.PostReferences</c> with intra-schema FKs only and zero cross-schema FKs.
/// - <c>PostService</c> MediatR commands (<c>CreateOperationalPost</c>, <c>RecordSystemPost</c>,
///   <c>PostComment</c>, <c>AddReaction</c>, <c>RemoveReaction</c>, <c>TogglePostVisibility</c>,
///   <c>ArchivePost</c>) and <c>PostQueryService</c> queries (<c>GetPostThreadDetails</c>,
///   <c>GetFullComments</c>, <c>GetReactionList</c>) using Dapper parameterized SQL.
/// - Cross-module reference validation via <c>IOrganizationQueryService</c>, <c>ICustomerQueryService</c>,
///   <c>IProductQueryService</c>, <c>IRequestQueryService</c>, and <c>IWorkPackageQueryService</c>.
/// - Permanent Data Retention (Architecture §20, §21): soft-remove on <c>post.Reactions</c>
///   (<c>IsActive = 0</c>, <c>RemovedAt IS NOT NULL</c>) and soft-archive on <c>post.Posts</c>
///   with zero physical <c>DELETE</c> operations.
/// - Domain events (<c>PostCreated</c>, <c>CommentAdded</c>, <c>ReactionAdded</c>, <c>ReactionRemoved</c>,
///   <c>PostVisibilityChanged</c>, <c>PostArchived</c>) emitted with rich metadata for P6-S29.
/// - Isolated SQL Server test database (<c>CakraTestDb_Post</c>) + Respawn cleanup.
/// </summary>
public sealed class PostModuleIntegrationTests : IAsyncLifetime
{
    private const string LocalDbFallback = "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb_Post;Integrated Security=true;TrustServerCertificate=True;";
    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private bool _sqlServerAvailable;

    public PostModuleIntegrationTests()
    {
        var baseConnectionString = DatabaseResetHelper.ResolveConnectionString() ?? LocalDbFallback;
        _connectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "CakraTestDb_Post"
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
    public async Task Migration_0008_creates_post_schema_tables_with_intra_schema_fks_only()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string tablesSql = """
            SELECT t.name
            FROM sys.tables t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = N'post'
            ORDER BY t.name;
            """;

        var tables = (await conn.QueryAsync<string>(tablesSql)).ToList();
        tables.Should().Contain(new[] { "Comments", "PostReferences", "Posts", "Reactions" });

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
              AND tp.name IN (N'Posts', N'Comments', N'Reactions', N'PostReferences')
            ORDER BY fk.name;
            """;

        var fks = (await conn.QueryAsync<ForeignKeyInfo>(foreignKeysSql)).ToList();
        fks.Should().HaveCount(3);
        fks.Should().OnlyContain(fk => fk.ParentSchema == "post" && fk.ReferencedSchema == "post" && fk.ReferencedTable == "Posts");
        fks.Select(fk => fk.ForeignKeyName).Should().BeEquivalentTo(new[]
        {
            "FK_Comments_Posts",
            "FK_PostReferences_Posts",
            "FK_Reactions_Posts"
        });
    }

    [Fact]
    public async Task Post_authoring_comments_reactions_soft_remove_visibility_and_archive_flows_succeed_against_SqlServer()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var postQueryService = scope.ServiceProvider.GetRequiredService<IPostQueryService>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        // 1. Seed Organization Persons, Customer, Product, Request, and WorkPackage
        var author = await mediator.Send(new CreatePersonCommand(
            "Nadia",
            "Kusuma",
            $"nadia.{Guid.NewGuid():N}@cakra.id"));

        var collaborator = await mediator.Send(new CreatePersonCommand(
            "Bima",
            "Santoso",
            $"bima.{Guid.NewGuid():N}@cakra.id"));

        var customer = await mediator.Send(new CreateCustomerCommand(
            $"CUST-{Guid.NewGuid():N}"[..14],
            "RSUP Dr. Kariadi",
            HasActiveMaintenanceContract: true));

        var product = await mediator.Send(new CreateProductCommand(
            $"PRD-{Guid.NewGuid():N}"[..14],
            "MyHospital Pharmacy",
            "Pharmacy Dispensing & Label Printing Module",
            author.Id));

        currentContext?.Initialize(Guid.NewGuid(), author.Id, new[] { "Implementator", "Programmer" });

        var recordedRequest = await mediator.Send(new RecordRequestCommand(
            Title: "Pharmacy label printer cut-off on thermal roll",
            Description: "Barcode cuts off on 50x30mm thermal labels after patch 2.4.",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "BUG",
            Priority: "HIGH",
            ActorPersonId: author.Id));

        var workPackage = await mediator.Send(new CreateWorkPackageCommand(
            Name: "Pharmacy Printing Stabilization",
            Objective: "Resolve thermal label margin and driver issues across RSUP Dr. Kariadi.",
            OwnerPersonId: author.Id,
            CustomerId: customer.Id,
            ProductId: product.Id));

        // 2. Record system post referencing Request and WorkPackage
        var createdPost = await mediator.Send(new RecordSystemPostCommand(
            Title: "Root cause identified for 50x30mm thermal label cut-off",
            Content: "ZPL driver DPI setting defaulted to 300 DPI instead of 203 DPI on Windows print server.",
            AuthorPersonId: author.Id,
            RequestId: recordedRequest.Id,
            WorkPackageId: workPackage.Id));

        createdPost.Should().NotBeNull();
        createdPost.Id.Should().NotBeEmpty();
        createdPost.Source.Should().Be(PostSourceNames.SystemGenerated);
        createdPost.PostType.Should().Be(PostSourceNames.SystemGenerated);
        createdPost.Status.Should().Be(PostStatusNames.Active);
        createdPost.Visibility.Should().Be(PostVisibilityNames.Visible);
        createdPost.AuthorPersonId.Should().Be(author.Id);
        createdPost.AuthorName.Should().Be("Nadia Kusuma");
        createdPost.CustomerId.Should().Be(customer.Id);
        createdPost.CustomerName.Should().Be("RSUP Dr. Kariadi");
        createdPost.ProductId.Should().Be(product.Id);
        createdPost.ProductName.Should().Be("MyHospital Pharmacy");
        createdPost.RequestId.Should().Be(recordedRequest.Id);
        createdPost.WorkPackageId.Should().Be(workPackage.Id);
        createdPost.ReferenceType.Should().Be(PostReferenceTypes.Request);
        createdPost.ReferenceId.Should().Be(recordedRequest.Id);
        createdPost.References.Should().HaveCount(4);

        // 3. Record System-Generated Post for an escalation exception
        var systemPost = await mediator.Send(new RecordSystemPostCommand(
            Title: "Request Escalated: Pharmacy label printer cut-off",
            Content: "System notification: Request escalated due to inpatient pharmacy queue impact.",
            AuthorPersonId: author.Id,
            SourceEventType: "RequestEscalated",
            RequestId: recordedRequest.Id,
            IsException: true,
            ExceptionType: PostExceptionTypes.Escalation));

        systemPost.Source.Should().Be(PostSourceNames.SystemGenerated);
        systemPost.IsException.Should().BeTrue();
        systemPost.ExceptionType.Should().Be(PostExceptionTypes.Escalation);
        systemPost.CustomerId.Should().Be(customer.Id);
        systemPost.ProductId.Should().Be(product.Id);

        // 4. Post comments on the operational post
        var comment1 = await mediator.Send(new PostCommentCommand(
            PostId: createdPost.Id,
            Content: "We also observed this at Outpatient Pharmacy counter 3.",
            AuthorPersonId: collaborator.Id));

        var comment2 = await mediator.Send(new PostCommentCommand(
            PostId: createdPost.Id,
            Content: "Applying 203 DPI template config resolved the cutoff immediately.",
            AuthorPersonId: author.Id));

        comment1.Id.Should().NotBeEmpty();
        comment1.AuthorPersonId.Should().Be(collaborator.Id);
        comment1.AuthorName.Should().Be("Bima Santoso");
        comment2.AuthorName.Should().Be("Nadia Kusuma");

        var fullComments = await postQueryService.GetFullCommentsAsync(createdPost.Id);
        fullComments.Should().HaveCount(2);
        fullComments[0].Id.Should().Be(comment1.Id);
        fullComments[1].Id.Should().Be(comment2.Id);

        // 5. Add structured operational reactions (including idempotent duplicate call)
        var reactionSeen1 = await mediator.Send(new AddReactionCommand(
            PostId: createdPost.Id,
            ReactionType: PostReactionTypes.Seen,
            PersonId: collaborator.Id));

        // Idempotent repeat of same (PostId, PersonId, ReactionType)
        var reactionSeenDuplicate = await mediator.Send(new AddReactionCommand(
            PostId: createdPost.Id,
            ReactionType: PostReactionTypes.Seen,
            PersonId: collaborator.Id));

        reactionSeenDuplicate.Id.Should().Be(reactionSeen1.Id);

        var reactionExperienced = await mediator.Send(new AddReactionCommand(
            PostId: createdPost.Id,
            ReactionType: PostReactionTypes.Experienced,
            PersonId: collaborator.Id));

        var reactionSeenAuthor = await mediator.Send(new AddReactionCommand(
            PostId: createdPost.Id,
            ReactionType: PostReactionTypes.Seen,
            PersonId: author.Id));

        reactionExperienced.ReactionType.Should().Be(PostReactionTypes.Experienced);
        reactionSeenAuthor.ReactionType.Should().Be(PostReactionTypes.Seen);

        var activeReactions = await postQueryService.GetReactionListAsync(createdPost.Id);
        activeReactions.Should().HaveCount(3);

        var threadBeforeRemove = await postQueryService.GetPostThreadDetailsAsync(createdPost.Id);
        threadBeforeRemove.Should().NotBeNull();
        threadBeforeRemove!.CommentCount.Should().Be(2);
        threadBeforeRemove.ReactionCount.Should().Be(3);
        threadBeforeRemove.ReactionCounts[PostReactionTypes.Seen].Should().Be(2);
        threadBeforeRemove.ReactionCounts[PostReactionTypes.Experienced].Should().Be(1);

        // 6. Soft-remove reaction (Permanent Retention: row remains in post.Reactions with IsActive = 0 and RemovedAt != null)
        var afterRemoveDto = await mediator.Send(new RemoveReactionCommand(
            PostId: createdPost.Id,
            ReactionType: PostReactionTypes.Experienced,
            PersonId: collaborator.Id));

        afterRemoveDto.ReactionCount.Should().Be(2);
        afterRemoveDto.ReactionCounts.ContainsKey(PostReactionTypes.Experienced).Should().BeFalse();
        afterRemoveDto.ReactionCounts[PostReactionTypes.Seen].Should().Be(2);

        var activeReactionsAfterRemove = await postQueryService.GetReactionListAsync(createdPost.Id);
        activeReactionsAfterRemove.Should().HaveCount(2);

        // Verify in SQL Server that the reaction row was NOT physically deleted
        await using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var dbReaction = await conn.QuerySingleAsync<(bool IsActive, DateTime? RemovedAt)>(
                "SELECT [IsActive], [RemovedAt] FROM [post].[Reactions] WHERE [Id] = @Id;",
                new { reactionExperienced.Id });

            dbReaction.IsActive.Should().BeFalse("Soft-removed reaction must have IsActive = 0");
            dbReaction.RemovedAt.Should().NotBeNull("Soft-removed reaction must record RemovedAt timestamp");
        }

        // Re-adding the soft-removed reaction reactivates the existing record
        var reactivated = await mediator.Send(new AddReactionCommand(
            PostId: createdPost.Id,
            ReactionType: PostReactionTypes.Experienced,
            PersonId: collaborator.Id));

        reactivated.Id.Should().Be(reactionExperienced.Id);
        reactivated.IsActive.Should().BeTrue();
        reactivated.RemovedAt.Should().BeNull();

        // 7. Toggle Post Visibility (VISIBLE -> HIDDEN -> VISIBLE)
        var hiddenPost = await mediator.Send(new TogglePostVisibilityCommand(createdPost.Id));
        hiddenPost.Visibility.Should().Be(PostVisibilityNames.Hidden);
        hiddenPost.Status.Should().Be(PostStatusNames.Active);

        // Commenting on a HIDDEN post is rejected
        var commentOnHiddenAct = () => mediator.Send(new PostCommentCommand(
            PostId: createdPost.Id,
            Content: "Should fail while hidden",
            AuthorPersonId: author.Id));
        await commentOnHiddenAct.Should().ThrowAsync<InvalidPostStateException>();

        var visibleAgainPost = await mediator.Send(new TogglePostVisibilityCommand(createdPost.Id));
        visibleAgainPost.Visibility.Should().Be(PostVisibilityNames.Visible);

        // 8. Archive Post (ACTIVE -> ARCHIVED) while preserving all comments, reactions, and references
        var archivedPost = await mediator.Send(new ArchivePostCommand(createdPost.Id, author.Id));
        archivedPost.Status.Should().Be(PostStatusNames.Archived);
        archivedPost.ArchivedAt.Should().NotBeNull();
        archivedPost.ArchivedByPersonId.Should().Be(author.Id);

        var archivedThread = await postQueryService.GetPostThreadDetailsAsync(createdPost.Id);
        archivedThread.Should().NotBeNull();
        archivedThread!.Status.Should().Be(PostStatusNames.Archived);
        archivedThread.Comments.Should().HaveCount(2, "Archiving a post must preserve all discussion comments");
        archivedThread.Reactions.Should().HaveCount(3, "Archiving a post must preserve all reactions");
        archivedThread.References.Should().HaveCount(4, "Archiving a post must preserve all references");

        // Archiving an already archived post or commenting on an archived post is rejected
        var archiveAgainAct = () => mediator.Send(new ArchivePostCommand(createdPost.Id, author.Id));
        await archiveAgainAct.Should().ThrowAsync<InvalidPostStateException>();

        var commentOnArchivedAct = () => mediator.Send(new PostCommentCommand(
            PostId: createdPost.Id,
            Content: "Should fail when archived",
            AuthorPersonId: author.Id));
        await commentOnArchivedAct.Should().ThrowAsync<InvalidPostStateException>();
    }

    [Fact]
    public async Task Cross_module_reference_validation_and_reaction_validation_reject_invalid_inputs()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var activeAuthor = await mediator.Send(new CreatePersonCommand(
            "Sinta",
            "Dewi",
            $"sinta.{Guid.NewGuid():N}@cakra.id"));

        var inactiveAuthor = await mediator.Send(new CreatePersonCommand(
            "Doni",
            "Prakoso",
            $"doni.{Guid.NewGuid():N}@cakra.id"));
        await mediator.Send(new DeactivatePersonCommand(inactiveAuthor.Id));

        // 1. Non-existent CustomerId -> KeyNotFoundException
        var missingCustomerAct = () => mediator.Send(new RecordSystemPostCommand(
            Title: "Post with unknown customer",
            Content: "Content",
            AuthorPersonId: activeAuthor.Id,
            CustomerId: Guid.NewGuid()));
        await missingCustomerAct.Should().ThrowAsync<KeyNotFoundException>();

        // 2. Non-existent ProductId -> KeyNotFoundException
        var missingProductAct = () => mediator.Send(new RecordSystemPostCommand(
            Title: "Post with unknown product",
            Content: "Content",
            AuthorPersonId: activeAuthor.Id,
            ProductId: Guid.NewGuid()));
        await missingProductAct.Should().ThrowAsync<KeyNotFoundException>();

        // 3. Non-existent RequestId -> KeyNotFoundException
        var missingRequestAct = () => mediator.Send(new RecordSystemPostCommand(
            Title: "Post with unknown request",
            Content: "Content",
            AuthorPersonId: activeAuthor.Id,
            RequestId: Guid.NewGuid()));
        await missingRequestAct.Should().ThrowAsync<KeyNotFoundException>();

        // 4. Non-existent WorkPackageId -> KeyNotFoundException
        var missingWorkPackageAct = () => mediator.Send(new RecordSystemPostCommand(
            Title: "Post with unknown work package",
            Content: "Content",
            AuthorPersonId: activeAuthor.Id,
            WorkPackageId: Guid.NewGuid()));
        await missingWorkPackageAct.Should().ThrowAsync<KeyNotFoundException>();

        // 5. Create valid system post and verify unsupported social-media reaction ("LIKE") is rejected by FluentValidation
        var validPost = await mediator.Send(new RecordSystemPostCommand(
            Title: "Valid operational update",
            Content: "Deployment completed cleanly.",
            AuthorPersonId: activeAuthor.Id));

        var invalidReactionAct = () => mediator.Send(new AddReactionCommand(
            PostId: validPost.Id,
            ReactionType: "LIKE",
            PersonId: activeAuthor.Id));
        await invalidReactionAct.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public void Post_domain_aggregate_emits_all_six_domain_events_with_feed_projection_metadata()
    {
        var authorId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var workPackageId = Guid.NewGuid();
        var now = new DateTime(2026, 9, 28, 7, 30, 0, DateTimeKind.Utc);

        var post = Cakra.Modules.Post.Domain.Post.RecordSystemPost(
            title: "Database index tuning for inpatient billing",
            content: "Added composite index on BillingTransactions(CustomerId, TransactionDate).",
            authorPersonId: authorId,
            customerId: customerId,
            productId: productId,
            requestId: requestId,
            workPackageId: workPackageId,
            isException: false,
            exceptionType: null,
            authorName: "Rendra Wijaya",
            customerName: "RSUDSleman",
            productName: "MyHospital Billing",
            primaryReferenceDisplay: "REQ-001: Slow billing query",
            createdAtUtc: now);

        post.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PostCreated>()
            .Which.Should().Match<PostCreated>(e =>
                e.PostId == post.Id &&
                e.AuthorPersonId == authorId &&
                e.AuthorName == "Rendra Wijaya" &&
                e.PostType == PostSourceNames.SystemGenerated &&
                e.CustomerId == customerId &&
                e.CustomerName == "RSUDSleman" &&
                e.ProductId == productId &&
                e.ProductName == "MyHospital Billing" &&
                e.RequestId == requestId &&
                e.WorkPackageId == workPackageId &&
                e.ReferenceType == PostReferenceTypes.Request &&
                e.ReferenceId == requestId &&
                e.ReferenceDisplay == "REQ-001: Slow billing query");

        post.ClearDomainEvents();

        var comment = post.AddComment(authorId, "Query time dropped from 1800ms to 12ms.", "Rendra Wijaya", now.AddMinutes(5));
        post.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CommentAdded>()
            .Which.Should().Match<CommentAdded>(e =>
                e.PostId == post.Id &&
                e.CommentId == comment.Id &&
                e.CommentCount == 1 &&
                e.LatestCommentExcerpt == "Query time dropped from 1800ms to 12ms.");

        post.ClearDomainEvents();

        var (reaction, added) = post.AddReaction(authorId, PostReactionTypes.HaveIdea, null, now.AddMinutes(6));
        added.Should().BeTrue();
        post.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ReactionAdded>()
            .Which.Should().Match<ReactionAdded>(e =>
                e.PostId == post.Id &&
                e.ReactionId == reaction.Id &&
                e.ReactionType == PostReactionTypes.HaveIdea &&
                e.ReactionCounts[PostReactionTypes.HaveIdea] == 1);

        post.ClearDomainEvents();

        var removed = post.RemoveReaction(authorId, PostReactionTypes.HaveIdea, null, now.AddMinutes(7));
        removed.Should().NotBeNull();
        post.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ReactionRemoved>()
            .Which.Should().Match<ReactionRemoved>(e =>
                e.PostId == post.Id &&
                e.ReactionId == reaction.Id &&
                e.ReactionType == PostReactionTypes.HaveIdea &&
                !e.ReactionCounts.ContainsKey(PostReactionTypes.HaveIdea));

        post.ClearDomainEvents();

        post.ToggleVisibility(PostVisibility.Hidden, authorId, now.AddMinutes(8));
        post.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PostVisibilityChanged>()
            .Which.Should().Match<PostVisibilityChanged>(e =>
                e.PostId == post.Id &&
                e.PreviousVisibility == PostVisibilityNames.Visible &&
                e.NewVisibility == PostVisibilityNames.Hidden);

        post.ClearDomainEvents();

        post.Archive(authorId, now.AddMinutes(9));
        post.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PostArchived>()
            .Which.Should().Match<PostArchived>(e =>
                e.PostId == post.Id &&
                e.PreviousStatus == PostStatusNames.Active &&
                e.NewStatus == PostStatusNames.Archived);
    }

    [Fact]
    public async Task Historical_human_authored_posts_remain_readable_and_support_comments_and_reactions()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var postQueryService = scope.ServiceProvider.GetRequiredService<IPostQueryService>();

        var author = await mediator.Send(new CreatePersonCommand(
            "Historical",
            "Author",
            $"hist.{Guid.NewGuid():N}@cakra.id"));

        var historicalPostId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Directly insert a HUMAN_AUTHORED post row representing legacy data created prior to CR-001
        await using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync("""
                INSERT INTO [post].[Posts] (
                    [Id], [Title], [Content], [Source], [AuthorPersonId], [Visibility], [Status], [IsException], [CreatedAt]
                ) VALUES (
                    @PostId, @Title, @Content, @Source, @AuthorPersonId, 'VISIBLE', 'ACTIVE', 0, @CreatedAt
                );
                INSERT INTO [post].[FeedItems] (
                    [FeedItemId], [PostId], [Title], [ContentExcerpt], [Summary], [AuthorPersonId], [AuthorName],
                    [PostType], [Source], [Visibility], [Status], [IsException], [CreatedAt], [CommentCount], [ReactionCountsJson], [UpdatedAt], [LastActivityAt]
                ) VALUES (
                    NEWID(), @PostId, @Title, @Content, @Content, @AuthorPersonId, 'Historical Author',
                    @Source, @Source, 'VISIBLE', 'ACTIVE', 0, @CreatedAt, 0, '{}', @CreatedAt, @CreatedAt
                );
                """,
                new
                {
                    PostId = historicalPostId,
                    Title = "Legacy Human Authored Post",
                    Content = "Historical operational note recorded before CR-001.",
                    Source = PostSourceNames.HumanAuthored,
                    AuthorPersonId = author.Id,
                    CreatedAt = now.AddDays(-30)
                });
        }

        // 1. Verify read returns HUMAN_AUTHORED source
        var post = await postQueryService.GetPostThreadDetailsAsync(historicalPostId);
        post.Should().NotBeNull();
        post!.Source.Should().Be(PostSourceNames.HumanAuthored);
        post.Title.Should().Be("Legacy Human Authored Post");

        // 2. Add comment to historical post
        var comment = await mediator.Send(new PostCommentCommand(
            PostId: historicalPostId,
            Content: "Adding comment to historical human-authored post",
            AuthorPersonId: author.Id));
        comment.Should().NotBeNull();
        comment.Content.Should().Be("Adding comment to historical human-authored post");

        // 3. Add reaction to historical post
        var reaction = await mediator.Send(new AddReactionCommand(
            PostId: historicalPostId,
            ReactionType: PostReactionTypes.Seen,
            PersonId: author.Id));
        reaction.Should().NotBeNull();
        reaction.ReactionType.Should().Be(PostReactionTypes.Seen);
        reaction.IsActive.Should().BeTrue();
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
