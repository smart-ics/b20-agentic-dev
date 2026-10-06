using Cakra.Api.Infrastructure.Context;
using Cakra.Api.Infrastructure.Migrations;
using Cakra.Core;
using Cakra.Modules.Customer.Services;
using Cakra.Modules.Organization.Commands;
using Cakra.Modules.Post;
using Cakra.Modules.Post.Domain;
using Cakra.Modules.Post.Domain.Events;
using Cakra.Modules.Post.Services;
using Cakra.Modules.Product.Services;
using Cakra.Modules.Request.Domain.Events;
using Cakra.Modules.Request.Services;
using Dapper;
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

namespace Cakra.Tests.Integration.Post;

/// <summary>
/// SQL Server integration tests verifying P6-S30 Feed Query Service — Filtered Queries &amp; Exception Detection:
/// <list type="bullet">
///   <item>
///     <description>
///       <c>FeedQueryService.GetFeed(filter, pagination)</c> (<see cref="IFeedQueryService"/> / <see cref="GetFeedQuery"/>)
///       executes a single-table indexed Dapper parameterized query over <c>post.FeedItems</c> filtering on
///       <c>Visibility = 'VISIBLE'</c>, <c>Status = 'ACTIVE'</c>, <c>CustomerId</c>, <c>ProductId</c>, and
///       <c>IsException</c> with <c>ORDER BY CreatedAt DESC</c> and <c>OFFSET / FETCH NEXT</c> pagination
///       (Architecture §8 <c>UC-FCOL-005</c>, <c>UC-AWR-001..003</c>; §12 Query Semantics; §20 Sub-50ms &amp; Parameterization).
///     </description>
///   </item>
///   <item>
///     <description>
///       <c>FeedProjectionHandler</c> populates <c>IsException = TRUE</c> and <c>ExceptionType</c>
///       (<c>ESCALATION</c>, <c>REJECTION</c>, <c>STALLED</c>) when processing domain events indicating
///       exceptional request states (<c>RequestEscalated</c>, <c>RequestRejected</c>, <c>RequestStalled</c>,
///       and <c>PostCreated</c>).
///     </description>
///   </item>
///   <item>
///     <description>
///       Pagination (<c>PageSize</c>, <c>Offset</c>) returns accurately bounded result sets across page boundaries.
///     </description>
///   </item>
/// </list>
/// Uses isolated database <c>CakraTestDb_FeedQuery</c> + Respawn cleanup.
/// </summary>
public sealed class FeedQueryIntegrationTests : IAsyncLifetime
{
    private const string LocalDbFallback = "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb_FeedQuery;Integrated Security=true;TrustServerCertificate=True;";
    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private bool _sqlServerAvailable;

    public FeedQueryIntegrationTests()
    {
        var baseConnectionString = DatabaseResetHelper.ResolveConnectionString() ?? LocalDbFallback;
        _connectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "CakraTestDb_FeedQuery"
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
    public async Task GetFeed_with_no_filter_Customer_filter_and_Product_filter_returns_visible_active_items_ordered_by_CreatedAt_desc()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var feedQueryService = scope.ServiceProvider.GetRequiredService<IFeedQueryService>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        var author = await mediator.Send(new CreatePersonCommand(
            "Rina",
            "Kartika",
            $"rina.{Guid.NewGuid():N}@cakra.id"));

        currentContext?.Initialize(Guid.NewGuid(), author.Id, new[] { "Implementator", "Programmer" });

        var customerA = await mediator.Send(new CreateCustomerCommand(
            $"CUST-A-{Guid.NewGuid():N}"[..14],
            "RSUD Tarakan Jakarta",
            HasActiveMaintenanceContract: true));

        var customerB = await mediator.Send(new CreateCustomerCommand(
            $"CUST-B-{Guid.NewGuid():N}"[..14],
            "RSUP Dr. Sardjito",
            HasActiveMaintenanceContract: true));

        var productX = await mediator.Send(new CreateProductCommand(
            $"PRD-X-{Guid.NewGuid():N}"[..14],
            "MyHospital Billing",
            "Hospital Billing & INA-CBGs Claim Engine",
            author.Id));

        var productY = await mediator.Send(new CreateProductCommand(
            $"PRD-Y-{Guid.NewGuid():N}"[..14],
            "PenaEl Radiology",
            "PACS & RIS Diagnostic Imaging Suite",
            author.Id));

        // Create 3 visible active posts with distinct CreatedAt timestamps + 1 hidden post + 1 archived post
        var post1 = await mediator.Send(new RecordSystemPostCommand(
            Title: "Deployed INA-CBGs tariff update v5.4 for RSUD Tarakan",
            Content: "Updated inpatient grouping table for BPJS claims.",
            AuthorPersonId: author.Id,
            CustomerId: customerA.Id,
            ProductId: productX.Id));

        await Task.Delay(15);

        var post2 = await mediator.Send(new RecordSystemPostCommand(
            Title: "Configured DICOM Modality Worklist for CT-Scan room 2",
            Content: "Modality AE Title mapped to PenaEl RIS broker.",
            AuthorPersonId: author.Id,
            CustomerId: customerA.Id,
            ProductId: productY.Id));

        await Task.Delay(15);

        var post3 = await mediator.Send(new RecordSystemPostCommand(
            Title: "Optimized discharge billing invoice summary query at RSUP Sardjito",
            Content: "Reduced invoice calculation latency from 950ms to 35ms.",
            AuthorPersonId: author.Id,
            CustomerId: customerB.Id,
            ProductId: productX.Id));

        await Task.Delay(15);

        var hiddenPost = await mediator.Send(new RecordSystemPostCommand(
            Title: "Draft internal note (hidden)",
            Content: "Hidden operational note that must not appear in GetFeed.",
            AuthorPersonId: author.Id,
            CustomerId: customerA.Id,
            ProductId: productX.Id));
        await mediator.Send(new TogglePostVisibilityCommand(hiddenPost.Id, PostVisibilityNames.Hidden, author.Id));

        var archivedPost = await mediator.Send(new RecordSystemPostCommand(
            Title: "Superseded operational notice (archived)",
            Content: "Archived post that must not appear in GetFeed.",
            AuthorPersonId: author.Id,
            CustomerId: customerB.Id,
            ProductId: productY.Id));
        await mediator.Send(new ArchivePostCommand(archivedPost.Id, author.Id));

        // 1. No filter -> returns the 3 VISIBLE + ACTIVE posts ordered by CreatedAt DESC (post3, post2, post1)
        var unfilteredPage = feedQueryService.GetFeed(filter: null, pagination: new FeedPagination(pageSize: 20, offset: 0));
        unfilteredPage.TotalCount.Should().Be(3);
        unfilteredPage.Items.Should().HaveCount(3);
        unfilteredPage.Items.Select(x => x.PostId).Should().ContainInOrder(post3.Id, post2.Id, post1.Id);
        unfilteredPage.Items.Should().OnlyContain(x => x.Visibility == PostVisibilityNames.Visible && x.Status == PostStatusNames.Active);
        unfilteredPage.HasMore.Should().BeFalse();

        // 2. Customer filter (Customer A) -> returns post2, post1 in CreatedAt DESC order
        var customerAPage = await feedQueryService.GetFeedAsync(
            new FeedFilter { CustomerId = customerA.Id },
            new FeedPagination(pageSize: 20, offset: 0));
        customerAPage.TotalCount.Should().Be(2);
        customerAPage.Items.Select(x => x.PostId).Should().ContainInOrder(post2.Id, post1.Id);
        customerAPage.Items.Should().OnlyContain(x => x.CustomerId == customerA.Id && x.CustomerName == "RSUD Tarakan Jakarta");

        // 3. Product filter (Product X) -> returns post3, post1 in CreatedAt DESC order
        var productXPage = await mediator.Send(new GetFeedQuery(ProductId: productX.Id, PageSize: 20, Offset: 0));
        productXPage.TotalCount.Should().Be(2);
        productXPage.Items.Select(x => x.PostId).Should().ContainInOrder(post3.Id, post1.Id);
        productXPage.Items.Should().OnlyContain(x => x.ProductId == productX.Id && x.ProductName == "MyHospital Billing");

        // 4. Combined Customer A + Product Y filter -> returns only post2
        var combinedPage = await feedQueryService.GetFeedAsync(
            new FeedFilter { CustomerId = customerA.Id, ProductId = productY.Id },
            new FeedPagination(pageSize: 20, offset: 0));
        combinedPage.TotalCount.Should().Be(1);
        combinedPage.Items.Should().ContainSingle().Which.PostId.Should().Be(post2.Id);
    }

    [Fact]
    public async Task FeedProjectionHandler_populates_IsException_and_ExceptionType_for_escalation_rejection_and_stalled_requests_and_ExceptionsOnly_filter_passes()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var feedQueryService = scope.ServiceProvider.GetRequiredService<IFeedQueryService>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        var actor = await mediator.Send(new CreatePersonCommand(
            "Bagus",
            "Setiawan",
            $"bagus.{Guid.NewGuid():N}@cakra.id"));

        currentContext?.Initialize(Guid.NewGuid(), actor.Id, new[] { "Implementator", "Programmer", "Management" });

        var customer = await mediator.Send(new CreateCustomerCommand(
            $"CUST-EX-{Guid.NewGuid():N}"[..14],
            "RSUP Sanglah Denpasar",
            HasActiveMaintenanceContract: true));

        var product = await mediator.Send(new CreateProductCommand(
            $"PRD-EX-{Guid.NewGuid():N}"[..14],
            "MyHospital Emergency",
            "Emergency Department Triage & Order Entry",
            actor.Id));

        // 1. Normal operational post (not an exception)
        var normalPost = await mediator.Send(new RecordSystemPostCommand(
            Title: "Routine triage dashboard responsiveness check",
            Content: "All ED workstations responding under 40ms.",
            AuthorPersonId: actor.Id,
            CustomerId: customer.Id,
            ProductId: product.Id));

        await Task.Delay(15);

        // 2. ESCALATION scenario A: Request with an existing operational post is escalated -> existing FeedItem is flagged
        var requestWithPost = await mediator.Send(new RecordRequestCommand(
            Title: "ED STAT lab order bridge timeout during shift handover",
            Description: "STAT orders take >45s to reach LIS during 07:00 handover.",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "BUG",
            Priority: "URGENT",
            ActorPersonId: actor.Id));

        // In CR-001, recording a request automatically creates the operational post in post.Posts and post.FeedItems
        Guid postForEscalatedRequestId;
        await using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            postForEscalatedRequestId = await conn.QuerySingleAsync<Guid>(
                "SELECT [PostId] FROM [post].[FeedItems] WHERE [RequestId] = @RequestId;",
                new { RequestId = requestWithPost.Id });
        }

        await mediator.Send(new AssignRequestOwnerCommand(requestWithPost.Id, actor.Id, ActorPersonId: actor.Id));
        await mediator.Publish(new RequestEscalated(
            requestWithPost.Id,
            actor.Id,
            "Requires database administrator approval to increase connection pool limit on production cluster."));

        var escalatedPostFeedItem = await feedQueryService.GetFeedItemByPostIdAsync(postForEscalatedRequestId);
        escalatedPostFeedItem.Should().NotBeNull();
        escalatedPostFeedItem!.IsException.Should().BeTrue();
        escalatedPostFeedItem.ExceptionType.Should().Be(PostExceptionTypes.Escalation);

        await Task.Delay(15);

        // 3. ESCALATION scenario B: Request WITHOUT any prior post is escalated -> FeedProjectionHandler auto-records system exception post & FeedItem
        var requestWithoutPriorPost = await mediator.Send(new RecordRequestCommand(
            Title: "Blood bank crossmatch printer offline in Trauma Center",
            Description: "Label printer fails to print compatibility tags.",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "BUG",
            Priority: "URGENT",
            ActorPersonId: actor.Id));

        await mediator.Send(new AssignRequestOwnerCommand(requestWithoutPriorPost.Id, actor.Id, ActorPersonId: actor.Id));
        await mediator.Publish(new RequestEscalated(
            requestWithoutPriorPost.Id,
            actor.Id,
            "Hardware replacement authorization needed from hospital IT director."));

        await Task.Delay(15);

        // 4. REJECTION scenario: Request is rejected during evaluation -> FeedProjectionHandler populates IsException = TRUE, ExceptionType = 'REJECTION'
        var rejectedRequest = await mediator.Send(new RecordRequestCommand(
            Title: "Custom payroll export to third-party HR vendor",
            Description: "Out-of-scope HR payroll request.",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "CHANGE_REQUEST",
            Priority: "LOW",
            ActorPersonId: actor.Id));

        await mediator.Send(new AssignRequestOwnerCommand(rejectedRequest.Id, actor.Id, ActorPersonId: actor.Id));
        await mediator.Publish(new RequestRejected(
            rejectedRequest.Id,
            actor.Id,
            "Out of contractual scope: Human Resources payroll integration is excluded from MyHospital Emergency."));

        await Task.Delay(15);

        // 5. STALLED scenario: RequestStalled domain event notification -> FeedProjectionHandler populates IsException = TRUE, ExceptionType = 'STALLED'
        var stalledRequest = await mediator.Send(new RecordRequestCommand(
            Title: "Pending VPN firewall rule confirmation from vendor network team",
            Description: "Awaiting hospital firewall whitelist for HL7 outbound port.",
            CustomerId: customer.Id,
            ProductId: product.Id,
            RequestType: "SUPPORT",
            Priority: "HIGH",
            ActorPersonId: actor.Id));

        await mediator.Publish(new RequestStalled(
            requestId: stalledRequest.Id,
            stalledReason: "No activity for 72 hours while awaiting firewall port opening.",
            actorPersonId: actor.Id,
            stalledHours: 72));

        // Query with IsException = true (ExceptionsOnly) -> returns only the 4 exception items (2 ESCALATION, 1 REJECTION, 1 STALLED) and excludes normalPost
        var exceptionsOnlyPage = await feedQueryService.GetFeedAsync(
            new FeedFilter { IsException = true },
            new FeedPagination(pageSize: 20, offset: 0));

        exceptionsOnlyPage.TotalCount.Should().Be(4);
        exceptionsOnlyPage.Items.Should().HaveCount(4);
        exceptionsOnlyPage.Items.Should().OnlyContain(x => x.IsException);
        exceptionsOnlyPage.Items.Select(x => x.PostId).Should().NotContain(normalPost.Id);
        exceptionsOnlyPage.Items.Select(x => x.ExceptionType).Should().Contain(new[]
        {
            PostExceptionTypes.Escalation,
            PostExceptionTypes.Rejection,
            PostExceptionTypes.Stalled
        });

        // Verify filtering by specific ExceptionType ('ESCALATION', 'REJECTION', 'STALLED')
        var escalationOnlyPage = await feedQueryService.GetFeedAsync(
            new FeedFilter { IsException = true, ExceptionType = PostExceptionTypes.Escalation },
            new FeedPagination(pageSize: 20, offset: 0));
        escalationOnlyPage.TotalCount.Should().Be(2);
        escalationOnlyPage.Items.Select(x => x.RequestId).Should().BeEquivalentTo(new Guid?[]
        {
            requestWithPost.Id,
            requestWithoutPriorPost.Id
        });

        var rejectionOnlyPage = await feedQueryService.GetFeedAsync(
            new FeedFilter { IsException = true, ExceptionType = PostExceptionTypes.Rejection },
            new FeedPagination(pageSize: 20, offset: 0));
        rejectionOnlyPage.TotalCount.Should().Be(1);
        rejectionOnlyPage.Items[0].RequestId.Should().Be(rejectedRequest.Id);
        rejectionOnlyPage.Items[0].ExceptionType.Should().Be(PostExceptionTypes.Rejection);

        var stalledOnlyPage = await feedQueryService.GetFeedAsync(
            new FeedFilter { IsException = true, ExceptionType = PostExceptionTypes.Stalled },
            new FeedPagination(pageSize: 20, offset: 0));
        stalledOnlyPage.TotalCount.Should().Be(1);
        stalledOnlyPage.Items[0].RequestId.Should().Be(stalledRequest.Id);
        stalledOnlyPage.Items[0].ExceptionType.Should().Be(PostExceptionTypes.Stalled);
    }

    [Fact]
    public async Task GetFeed_pagination_boundaries_return_exact_non_overlapping_slices_in_CreatedAt_descending_order()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var feedQueryService = scope.ServiceProvider.GetRequiredService<IFeedQueryService>();

        var author = await mediator.Send(new CreatePersonCommand(
            "Lukman",
            "Hakim",
            $"lukman.{Guid.NewGuid():N}@cakra.id"));

        var createdPostIdsInAscendingOrder = new List<Guid>();
        for (var i = 1; i <= 5; i++)
        {
            var post = await mediator.Send(new RecordSystemPostCommand(
                Title: $"Operational Feed Item #{i}",
                Content: $"Pagination test payload item #{i}.",
                AuthorPersonId: author.Id));

            createdPostIdsInAscendingOrder.Add(post.Id);
            await Task.Delay(15);
        }

        // Expected descending order: #5, #4, #3, #2, #1
        var expectedDescendingPostIds = createdPostIdsInAscendingOrder.AsEnumerable().Reverse().ToList();

        // Page 1: Offset = 0, PageSize = 2 -> items [0, 1] (#5, #4), HasMore = true
        var page1 = await feedQueryService.GetFeedAsync(
            new FeedFilter(),
            new FeedPagination(pageSize: 2, offset: 0));
        page1.TotalCount.Should().Be(5);
        page1.PageSize.Should().Be(2);
        page1.Offset.Should().Be(0);
        page1.Page.Should().Be(1);
        page1.TotalPages.Should().Be(3);
        page1.HasMore.Should().BeTrue();
        page1.Items.Select(x => x.PostId).Should().ContainInOrder(expectedDescendingPostIds[0], expectedDescendingPostIds[1]);

        // Page 2: Offset = 2, PageSize = 2 -> items [2, 3] (#3, #2), HasMore = true
        var page2 = await feedQueryService.GetFeedAsync(
            new FeedFilter(),
            new FeedPagination(pageSize: 2, offset: 2));
        page2.TotalCount.Should().Be(5);
        page2.PageSize.Should().Be(2);
        page2.Offset.Should().Be(2);
        page2.Page.Should().Be(2);
        page2.HasMore.Should().BeTrue();
        page2.Items.Select(x => x.PostId).Should().ContainInOrder(expectedDescendingPostIds[2], expectedDescendingPostIds[3]);

        // Page 3 (final partial page): Offset = 4, PageSize = 2 -> item [4] (#1), HasMore = false
        var page3 = await feedQueryService.GetFeedAsync(
            new FeedFilter(),
            new FeedPagination(pageSize: 2, offset: 4));
        page3.TotalCount.Should().Be(5);
        page3.PageSize.Should().Be(2);
        page3.Offset.Should().Be(4);
        page3.Page.Should().Be(3);
        page3.HasMore.Should().BeFalse();
        page3.Items.Should().ContainSingle().Which.PostId.Should().Be(expectedDescendingPostIds[4]);

        // Beyond range: Offset = 10, PageSize = 2 -> 0 items, TotalCount = 5, HasMore = false
        var beyondPage = await feedQueryService.GetFeedAsync(
            new FeedFilter(),
            new FeedPagination(pageSize: 2, offset: 10));
        beyondPage.TotalCount.Should().Be(5);
        beyondPage.Items.Should().BeEmpty();
        beyondPage.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task GetFeed_with_SearchTerm_matches_across_all_five_dimensions_multi_word_prefix_and_clearing_search_restores_full_feed()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var scope = _factory!.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var feedQueryService = scope.ServiceProvider.GetRequiredService<IFeedQueryService>();
        var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

        // Create Authors and Commenters in Organization
        var authorHendro = await mediator.Send(new CreatePersonCommand(
            "Hendro",
            "Gunawan",
            $"hendro.{Guid.NewGuid():N}@cakra.id"));

        var commenterSiti = await mediator.Send(new CreatePersonCommand(
            "Siti",
            "Rahma",
            $"siti.{Guid.NewGuid():N}@cakra.id"));

        var authorBudi = await mediator.Send(new CreatePersonCommand(
            "Budi",
            "Santoso",
            $"budi.{Guid.NewGuid():N}@cakra.id"));

        currentContext?.Initialize(Guid.NewGuid(), authorHendro.Id, new[] { "Implementator", "Programmer" });

        // Create Customers
        var customerCipto = await mediator.Send(new CreateCustomerCommand(
            $"CUST-CIPTO-{Guid.NewGuid():N}"[..14],
            "RSUD Cipto Mangunkusumo",
            HasActiveMaintenanceContract: true));

        var customerPratama = await mediator.Send(new CreateCustomerCommand(
            $"CUST-PRATAMA-{Guid.NewGuid():N}"[..14],
            "Klinik Pratama Sehat",
            HasActiveMaintenanceContract: true));

        // Create Products
        var productNeuroScan = await mediator.Send(new CreateProductCommand(
            $"PRD-NEURO-{Guid.NewGuid():N}"[..14],
            "NeuroScan Diagnostic RIS",
            "Neurology imaging and diagnostic suite",
            authorHendro.Id));

        var productApotek = await mediator.Send(new CreateProductCommand(
            $"PRD-APOTEK-{Guid.NewGuid():N}"[..14],
            "ApotekDirect Inventory",
            "Pharmacy pharmaceutical stock management",
            authorBudi.Id));

        // Post 1: Authored by Hendro, Product: NeuroScan, Customer: Cipto, with two distinct comments
        var post1 = await mediator.Send(new RecordSystemPostCommand(
            Title: "Configured DICOM PACS bridge workstation",
            Content: "Connected workstation to modality gateway broker.",
            AuthorPersonId: authorHendro.Id,
            CustomerId: customerCipto.Id,
            ProductId: productNeuroScan.Id));

        // Add Comment 1 by Siti Rahma
        currentContext?.Initialize(Guid.NewGuid(), commenterSiti.Id, new[] { "Implementator" });
        await mediator.Send(new PostCommentCommand(
            PostId: post1.Id,
            Content: "Investigated network latency across subnets.",
            AuthorPersonId: commenterSiti.Id));

        // Add Comment 2 by Hendro Gunawan
        currentContext?.Initialize(Guid.NewGuid(), authorHendro.Id, new[] { "Implementator" });
        await mediator.Send(new PostCommentCommand(
            PostId: post1.Id,
            Content: "Applied firmware hotfix patch to resolve timeout.",
            AuthorPersonId: authorHendro.Id));

        await Task.Delay(15);

        // Post 2: Created via Request (RequestRecordedPostHandler)
        var request2 = await mediator.Send(new RecordRequestCommand(
            Title: "Emergency Triage Queue Overload",
            Description: "Automated bed allocation halted due to HL7 synchronization failure.",
            CustomerId: customerCipto.Id,
            ProductId: productNeuroScan.Id,
            RequestType: "BUG",
            Priority: "URGENT",
            ActorPersonId: authorHendro.Id));

        await Task.Delay(15);

        // Post 3: Unrelated post by Budi, Product: ApotekDirect, Customer: Pratama
        currentContext?.Initialize(Guid.NewGuid(), authorBudi.Id, new[] { "Programmer" });
        var post3 = await mediator.Send(new RecordSystemPostCommand(
            Title: "Monthly inventory balance reconciliation",
            Content: "All pharmacy pharmaceutical stocks verified without discrepancy.",
            AuthorPersonId: authorBudi.Id,
            CustomerId: customerPratama.Id,
            ProductId: productApotek.Id));

        // =========================================================================
        // 1. Dimension: Product Name ("NeuroScan", "ApotekDirect")
        // =========================================================================
        var productSearch = await feedQueryService.GetFeedAsync(new FeedFilter { SearchTerm = "NeuroScan" });
        productSearch.Items.Should().HaveCount(2);
        productSearch.Items.Select(x => x.PostId).Should().NotContain(post3.Id);

        var productApotekSearch = await feedQueryService.GetFeedAsync(new FeedFilter { SearchTerm = "ApotekDirect" });
        productApotekSearch.Items.Should().ContainSingle().Which.PostId.Should().Be(post3.Id);

        // =========================================================================
        // 2. Dimension: Customer Name ("Cipto", "Pratama")
        // =========================================================================
        var customerCiptoSearch = await feedQueryService.GetFeedAsync(new FeedFilter { SearchTerm = "Cipto" });
        customerCiptoSearch.Items.Should().HaveCount(2);
        customerCiptoSearch.Items.Select(x => x.PostId).Should().NotContain(post3.Id);

        var customerPratamaSearch = await feedQueryService.GetFeedAsync(new FeedFilter { SearchTerm = "Pratama" });
        customerPratamaSearch.Items.Should().ContainSingle().Which.PostId.Should().Be(post3.Id);

        // =========================================================================
        // 3. Dimension: Author Person Name ("Hendro", "Santoso")
        // =========================================================================
        var authorHendroSearch = await feedQueryService.GetFeedAsync(new FeedFilter { SearchTerm = "Hendro" });
        authorHendroSearch.Items.Should().HaveCount(2);

        var authorBudiSearch = await feedQueryService.GetFeedAsync(new FeedFilter { SearchTerm = "Santoso" });
        authorBudiSearch.Items.Should().ContainSingle().Which.PostId.Should().Be(post3.Id);

        // =========================================================================
        // 4. Dimension: Commenter Name ("Rahma" from commenter Siti Rahma)
        // =========================================================================
        var commenterSearch = await feedQueryService.GetFeedAsync(new FeedFilter { SearchTerm = "Rahma" });
        commenterSearch.Items.Should().ContainSingle().Which.PostId.Should().Be(post1.Id);

        // =========================================================================
        // 5. Dimension: Request Text (Title "Triage", Description "allocation")
        // =========================================================================
        var requestTitleSearch = await feedQueryService.GetFeedAsync(new FeedFilter { SearchTerm = "Triage" });
        requestTitleSearch.Items.Should().ContainSingle().Which.RequestId.Should().Be(request2.Id);

        var requestDescSearch = await feedQueryService.GetFeedAsync(new FeedFilter { SearchTerm = "allocation" });
        requestDescSearch.Items.Should().ContainSingle().Which.RequestId.Should().Be(request2.Id);

        // =========================================================================
        // 6. Dimension: Comment Text across multiple comments ("latency", "firmware")
        // =========================================================================
        var comment1Search = await feedQueryService.GetFeedAsync(new FeedFilter { SearchTerm = "latency" });
        comment1Search.Items.Should().ContainSingle().Which.PostId.Should().Be(post1.Id);

        var comment2Search = await feedQueryService.GetFeedAsync(new FeedFilter { SearchTerm = "firmware" });
        comment2Search.Items.Should().ContainSingle().Which.PostId.Should().Be(post1.Id);

        // =========================================================================
        // 7. Multi-word Prefix-AND logic ("Hend Neuro", "Rahma subnets", non-matching)
        // =========================================================================
        var multiWordPrefixSearch = await feedQueryService.GetFeedAsync(new FeedFilter { SearchTerm = "Hend Neuro" });
        multiWordPrefixSearch.Items.Should().HaveCount(2);

        var multiWordCommentSearch = await feedQueryService.GetFeedAsync(new FeedFilter { SearchTerm = "Rahma subnets" });
        multiWordCommentSearch.Items.Should().ContainSingle().Which.PostId.Should().Be(post1.Id);

        var nonMatchingMultiWord = await feedQueryService.GetFeedAsync(new FeedFilter { SearchTerm = "Triage NonExistentTokenXYZ" });
        nonMatchingMultiWord.Items.Should().BeEmpty();
        nonMatchingMultiWord.TotalCount.Should().Be(0);

        // =========================================================================
        // 8. Clearing search restores full feed list
        // =========================================================================
        var clearSearchNull = await feedQueryService.GetFeedAsync(new FeedFilter { SearchTerm = null });
        clearSearchNull.TotalCount.Should().Be(3);
        clearSearchNull.Items.Should().HaveCount(3);

        var clearSearchEmpty = await feedQueryService.GetFeedAsync(new FeedFilter { SearchTerm = "" });
        clearSearchEmpty.TotalCount.Should().Be(3);
        clearSearchEmpty.Items.Should().HaveCount(3);

        var clearSearchWhitespace = await feedQueryService.GetFeedAsync(new FeedFilter { SearchTerm = "   " });
        clearSearchWhitespace.TotalCount.Should().Be(3);
        clearSearchWhitespace.Items.Should().HaveCount(3);
    }
}
