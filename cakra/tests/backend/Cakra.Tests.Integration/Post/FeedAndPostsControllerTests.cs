using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cakra.Api.Infrastructure.Authentication;
using Cakra.Api.Infrastructure.Context;
using Cakra.Api.Infrastructure.Migrations;
using Cakra.Core;
using Cakra.Modules.Customer.Services;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Organization.Commands;
using Cakra.Modules.Post.Domain;
using Cakra.Modules.Product.Services;
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
/// SQL Server integration tests verifying P6-S31 (<c>FeedController</c> and <c>PostsController</c>):
/// <list type="bullet">
///   <item><description>All <c>/api/v1/feed</c> and <c>/api/v1/posts</c> endpoints enforce <c>[Authorize]</c> (401 when unauthenticated).</description></item>
///   <item><description><c>POST /api/v1/posts</c> (<c>PostService.CreateOperationalPost</c> -&gt; 201 Created), including <c>RequestId</c>-only payload automatically inheriting <c>CustomerId</c> and <c>ProductId</c> in <c>post.FeedItems</c> and accepting both <c>content</c> and <c>body</c>.</description></item>
///   <item><description><c>GET /api/v1/posts/{id}</c> (<c>PostQueryService.GetPostThreadDetails</c> -&gt; 200 OK / 404 NotFound).</description></item>
///   <item><description><c>POST /api/v1/posts/{id}/comments</c> (<c>PostService.PostComment</c> -&gt; 201 Created) and <c>GET /api/v1/posts/{id}/comments</c> (<c>PostQueryService.GetFullComments</c> -&gt; 200 OK).</description></item>
///   <item><description><c>POST /api/v1/posts/{id}/reactions</c> (<c>PostService.AddReaction</c> -&gt; 200 OK), <c>GET /api/v1/posts/{id}/reactions</c> (<c>PostQueryService.GetReactionList</c> -&gt; 200 OK), and <c>DELETE /api/v1/posts/{id}/reactions/{reactionType}</c> (<c>PostService.RemoveReaction</c> -&gt; 200 OK).</description></item>
///   <item><description><c>GET /api/v1/feed</c> (<c>FeedQueryService.GetFeed</c> -&gt; 200 OK) with <c>CustomerId</c>, <c>ProductId</c>, <c>IsException</c>, <c>PageSize</c>, and <c>Offset</c> query parameters.</description></item>
///   <item><description>RFC 7807 <c>ProblemDetails</c> responses for 400 validation/domain errors and 404 not found.</description></item>
/// </list>
/// Uses isolated database <c>CakraTestDb_FeedController</c> + Respawn cleanup.
/// </summary>
public sealed class FeedAndPostsControllerTests : IAsyncLifetime
{
    private const string LocalDbFallback =
        "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb_FeedController;Integrated Security=true;TrustServerCertificate=True;";

    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private Respawner? _respawner;
    private bool _sqlServerAvailable;

    public FeedAndPostsControllerTests()
    {
        var baseConnectionString = DatabaseResetHelper.ResolveConnectionString() ?? LocalDbFallback;
        _connectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "CakraTestDb_FeedController"
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
        migrationResult.Successful.Should().BeTrue("DbUp migrations must execute cleanly before integration tests");

        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                SchemasToInclude = ["post", "workpackage", "request", "product", "customer", "organization", "identity"],
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

    [Fact]
    public async Task Unauthenticated_requests_to_feed_and_posts_endpoints_return_401_Unauthorized()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        using var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var postId = Guid.NewGuid();

        (await client.GetAsync("/api/v1/feed"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.GetAsync($"/api/v1/posts/{postId}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.GetAsync($"/api/v1/posts/{postId}/comments"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.GetAsync($"/api/v1/posts/{postId}/reactions"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PostAsJsonAsync("/api/v1/posts", new
        {
            title = "Post Title",
            content = "Post Content"
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PostAsJsonAsync($"/api/v1/posts/{postId}/comments", new
        {
            content = "Comment Content"
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PostAsJsonAsync($"/api/v1/posts/{postId}/reactions", new
        {
            reactionType = PostReactionTypes.Seen
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.DeleteAsync($"/api/v1/posts/{postId}/reactions/{PostReactionTypes.Seen}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PostAsJsonAsync($"/api/v1/posts/{postId}/visibility", new
        {
            visibility = PostVisibilityNames.Hidden
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PostAsync($"/api/v1/posts/{postId}/archive", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Posts_and_Feed_endpoints_support_post_creation_requestId_inheritance_comments_reactions_and_filtered_feed_queries()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        var seeded = await CreateAuthenticatedClientAndSeedContextAsync();
        using var client = seeded.Client;

        // 1. POST /api/v1/posts with ONLY title, body, and requestId (no explicit customerId, productId, or authorPersonId)
        //    Must return 201 Created, resolve authorPersonId from authenticated session, and automatically inherit
        //    CustomerId and ProductId from the referenced Request into post.Posts and post.FeedItems!
        var createPost1Response = await client.PostAsJsonAsync("/api/v1/posts", new
        {
            title = "Root cause identified for INA-CBGs tariff grouping mismatch",
            body = "Verified ICD-10 secondary diagnosis mapping table for inpatient claims at RSUP Sardjito.",
            requestId = seeded.Request1Id
        });
        createPost1Response.StatusCode.Should().Be(HttpStatusCode.Created);
        createPost1Response.Headers.Location.Should().NotBeNull();

        var post1Json = await createPost1Response.Content.ReadFromJsonAsync<JsonElement>();
        var post1Id = post1Json.GetProperty("id").GetGuid();
        post1Id.Should().NotBeEmpty();
        post1Json.GetProperty("title").GetString().Should().Be("Root cause identified for INA-CBGs tariff grouping mismatch");
        post1Json.GetProperty("content").GetString().Should().Be("Verified ICD-10 secondary diagnosis mapping table for inpatient claims at RSUP Sardjito.");
        post1Json.GetProperty("body").GetString().Should().Be("Verified ICD-10 secondary diagnosis mapping table for inpatient claims at RSUP Sardjito.");
        post1Json.GetProperty("authorPersonId").GetGuid().Should().Be(seeded.Author1Id);
        post1Json.GetProperty("authorName").GetString().Should().Be("Rina Kartika");
        post1Json.GetProperty("requestId").GetGuid().Should().Be(seeded.Request1Id);
        post1Json.GetProperty("customerId").GetGuid().Should().Be(seeded.Customer1Id);
        post1Json.GetProperty("customerName").GetString().Should().Be("RSUP Dr. Sardjito");
        post1Json.GetProperty("productId").GetGuid().Should().Be(seeded.Product1Id);
        post1Json.GetProperty("productName").GetString().Should().Be("MyHospital Billing");

        // Verify post.FeedItems row directly in SQL Server has CustomerId, ProductId, RequestId, and PostId populated
        await using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var feedRow = await conn.QuerySingleAsync<(Guid PostId, Guid? CustomerId, Guid? ProductId, Guid? RequestId, string? CustomerName, string? ProductName)>(
                """
                SELECT [PostId], [CustomerId], [ProductId], [RequestId], [CustomerName], [ProductName]
                FROM [post].[FeedItems]
                WHERE [PostId] = @PostId;
                """,
                new { PostId = post1Id });

            feedRow.PostId.Should().Be(post1Id);
            feedRow.CustomerId.Should().Be(seeded.Customer1Id);
            feedRow.ProductId.Should().Be(seeded.Product1Id);
            feedRow.RequestId.Should().Be(seeded.Request1Id);
            feedRow.CustomerName.Should().Be("RSUP Dr. Sardjito");
            feedRow.ProductName.Should().Be("MyHospital Billing");
        }

        await Task.Delay(15);

        // 2. POST /api/v1/posts with explicit customerId, productId, content, and exception badge
        var createPost2Response = await client.PostAsJsonAsync("/api/v1/posts", new
        {
            title = "Escalation alert: Pharmacy stock synchronization timeout at RSUD Jogja",
            content = "Nightly batch synchronization exceeded 30s threshold during peak dispensing window.",
            customerId = seeded.Customer2Id,
            productId = seeded.Product2Id,
            isException = true,
            exceptionType = PostExceptionTypes.Escalation
        });
        createPost2Response.StatusCode.Should().Be(HttpStatusCode.Created);
        var post2Json = await createPost2Response.Content.ReadFromJsonAsync<JsonElement>();
        var post2Id = post2Json.GetProperty("id").GetGuid();
        post2Json.GetProperty("isException").GetBoolean().Should().BeTrue();
        post2Json.GetProperty("exceptionType").GetString().Should().Be(PostExceptionTypes.Escalation);

        // 3. POST /api/v1/posts/{id}/comments -> 201 Created
        var createComment1Response = await client.PostAsJsonAsync($"/api/v1/posts/{post1Id}/comments", new
        {
            content = "Hotfix migration script prepared and verified on staging database."
        });
        createComment1Response.StatusCode.Should().Be(HttpStatusCode.Created);
        var comment1Json = await createComment1Response.Content.ReadFromJsonAsync<JsonElement>();
        var comment1Id = comment1Json.GetProperty("id").GetGuid();
        comment1Id.Should().NotBeEmpty();
        comment1Json.GetProperty("postId").GetGuid().Should().Be(post1Id);
        comment1Json.GetProperty("authorPersonId").GetGuid().Should().Be(seeded.Author1Id);
        comment1Json.GetProperty("authorName").GetString().Should().Be("Rina Kartika");
        comment1Json.GetProperty("content").GetString().Should().Be("Hotfix migration script prepared and verified on staging database.");

        var createComment2Response = await client.PostAsJsonAsync($"/api/v1/posts/{post1Id}/comments", new
        {
            body = "Customer PIC confirmed maintenance window at 21:00 WIB.",
            authorPersonId = seeded.Author2Id
        });
        createComment2Response.StatusCode.Should().Be(HttpStatusCode.Created);

        // 4. GET /api/v1/posts/{id}/comments -> 200 OK in chronological order
        var getCommentsResponse = await client.GetAsync($"/api/v1/posts/{post1Id}/comments");
        getCommentsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var commentsJson = await getCommentsResponse.Content.ReadFromJsonAsync<JsonElement>();
        commentsJson.GetArrayLength().Should().Be(2);
        commentsJson[0].GetProperty("content").GetString().Should().Be("Hotfix migration script prepared and verified on staging database.");
        commentsJson[1].GetProperty("content").GetString().Should().Be("Customer PIC confirmed maintenance window at 21:00 WIB.");
        commentsJson[1].GetProperty("authorName").GetString().Should().Be("Budi Santoso");

        // 5. POST /api/v1/posts/{id}/reactions -> 200 OK
        var addReaction1Response = await client.PostAsJsonAsync($"/api/v1/posts/{post1Id}/reactions", new
        {
            reactionType = PostReactionTypes.Seen
        });
        addReaction1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reaction1Json = await addReaction1Response.Content.ReadFromJsonAsync<JsonElement>();
        reaction1Json.GetProperty("postId").GetGuid().Should().Be(post1Id);
        reaction1Json.GetProperty("personId").GetGuid().Should().Be(seeded.Author1Id);
        reaction1Json.GetProperty("reactionType").GetString().Should().Be(PostReactionTypes.Seen);
        reaction1Json.GetProperty("isActive").GetBoolean().Should().BeTrue();

        var addReaction2Response = await client.PostAsJsonAsync($"/api/v1/posts/{post1Id}/reactions", new
        {
            type = PostReactionTypes.HaveIdea,
            personId = seeded.Author2Id
        });
        addReaction2Response.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. GET /api/v1/posts/{id}/reactions -> 200 OK
        var getReactionsResponse = await client.GetAsync($"/api/v1/posts/{post1Id}/reactions");
        getReactionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reactionsJson = await getReactionsResponse.Content.ReadFromJsonAsync<JsonElement>();
        reactionsJson.GetArrayLength().Should().Be(2);

        // 7. GET /api/v1/posts/{id} -> 200 OK with comments, reactions, and references
        var getPostDetailsResponse = await client.GetAsync($"/api/v1/posts/{post1Id}");
        getPostDetailsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var postDetailsJson = await getPostDetailsResponse.Content.ReadFromJsonAsync<JsonElement>();
        postDetailsJson.GetProperty("id").GetGuid().Should().Be(post1Id);
        postDetailsJson.GetProperty("commentCount").GetInt32().Should().Be(2);
        postDetailsJson.GetProperty("reactionCount").GetInt32().Should().Be(2);
        postDetailsJson.GetProperty("comments").GetArrayLength().Should().Be(2);
        postDetailsJson.GetProperty("reactions").GetArrayLength().Should().Be(2);
        postDetailsJson.GetProperty("references").GetArrayLength().Should().BeGreaterThanOrEqualTo(3);

        // 8. DELETE /api/v1/posts/{id}/reactions/{reactionType} -> 200 OK (soft-removes SEEN)
        var removeReactionResponse = await client.DeleteAsync($"/api/v1/posts/{post1Id}/reactions/{PostReactionTypes.Seen}");
        removeReactionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterRemoveJson = await removeReactionResponse.Content.ReadFromJsonAsync<JsonElement>();
        afterRemoveJson.GetProperty("reactionCount").GetInt32().Should().Be(1);

        var remainingReactionsResponse = await client.GetAsync($"/api/v1/posts/{post1Id}/reactions");
        remainingReactionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var remainingReactionsJson = await remainingReactionsResponse.Content.ReadFromJsonAsync<JsonElement>();
        remainingReactionsJson.GetArrayLength().Should().Be(1);
        remainingReactionsJson[0].GetProperty("reactionType").GetString().Should().Be(PostReactionTypes.HaveIdea);

        // 9. GET /api/v1/feed -> 200 OK (unfiltered, filtered by CustomerId, ProductId, IsException, and paginated)
        var feedAllResponse = await client.GetAsync("/api/v1/feed?pageSize=20&offset=0");
        feedAllResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var feedAllJson = await feedAllResponse.Content.ReadFromJsonAsync<JsonElement>();
        feedAllJson.GetProperty("totalCount").GetInt32().Should().Be(2);
        var feedAllItems = feedAllJson.GetProperty("items");
        feedAllItems.GetArrayLength().Should().Be(2);
        // Ordered by CreatedAt DESC -> post2 first, post1 second
        feedAllItems[0].GetProperty("postId").GetGuid().Should().Be(post2Id);
        feedAllItems[1].GetProperty("postId").GetGuid().Should().Be(post1Id);
        feedAllItems[1].GetProperty("commentCount").GetInt32().Should().Be(2);
        feedAllItems[1].GetProperty("latestCommentExcerpt").GetString().Should().Be("Customer PIC confirmed maintenance window at 21:00 WIB.");
        feedAllItems[1].GetProperty("reactionCount").GetInt32().Should().Be(1);

        // Filter by CustomerId
        var feedCustomer1Response = await client.GetAsync($"/api/v1/feed?customerId={seeded.Customer1Id}");
        feedCustomer1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var feedCustomer1Json = await feedCustomer1Response.Content.ReadFromJsonAsync<JsonElement>();
        feedCustomer1Json.GetProperty("totalCount").GetInt32().Should().Be(1);
        feedCustomer1Json.GetProperty("items")[0].GetProperty("postId").GetGuid().Should().Be(post1Id);

        // Filter by ProductId
        var feedProduct2Response = await client.GetAsync($"/api/v1/feed?productId={seeded.Product2Id}");
        feedProduct2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var feedProduct2Json = await feedProduct2Response.Content.ReadFromJsonAsync<JsonElement>();
        feedProduct2Json.GetProperty("totalCount").GetInt32().Should().Be(1);
        feedProduct2Json.GetProperty("items")[0].GetProperty("postId").GetGuid().Should().Be(post2Id);

        // Filter by IsException = true
        var feedExceptionsResponse = await client.GetAsync("/api/v1/feed?isException=true");
        feedExceptionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var feedExceptionsJson = await feedExceptionsResponse.Content.ReadFromJsonAsync<JsonElement>();
        feedExceptionsJson.GetProperty("totalCount").GetInt32().Should().Be(1);
        feedExceptionsJson.GetProperty("items")[0].GetProperty("postId").GetGuid().Should().Be(post2Id);
        feedExceptionsJson.GetProperty("items")[0].GetProperty("isException").GetBoolean().Should().BeTrue();
        feedExceptionsJson.GetProperty("items")[0].GetProperty("exceptionType").GetString().Should().Be(PostExceptionTypes.Escalation);

        // Pagination (PageSize=1, Offset=0 and Offset=1)
        var page1Response = await client.GetAsync("/api/v1/feed?pageSize=1&offset=0");
        page1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page1Json = await page1Response.Content.ReadFromJsonAsync<JsonElement>();
        page1Json.GetProperty("totalCount").GetInt32().Should().Be(2);
        page1Json.GetProperty("hasMore").GetBoolean().Should().BeTrue();
        page1Json.GetProperty("items").GetArrayLength().Should().Be(1);
        page1Json.GetProperty("items")[0].GetProperty("postId").GetGuid().Should().Be(post2Id);

        var page2Response = await client.GetAsync("/api/v1/feed?pageSize=1&offset=1");
        page2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page2Json = await page2Response.Content.ReadFromJsonAsync<JsonElement>();
        page2Json.GetProperty("totalCount").GetInt32().Should().Be(2);
        page2Json.GetProperty("hasMore").GetBoolean().Should().BeFalse();
        page2Json.GetProperty("items").GetArrayLength().Should().Be(1);
        page2Json.GetProperty("items")[0].GetProperty("postId").GetGuid().Should().Be(post1Id);
    }

    [Fact]
    public async Task Feed_and_Posts_endpoints_return_404_NotFound_and_400_BadRequest_ProblemDetails_for_invalid_requests()
    {
        _sqlServerAvailable.Should().BeTrue("SQL Server test instance must be available");
        _factory.Should().NotBeNull();

        var seeded = await CreateAuthenticatedClientAndSeedContextAsync();
        using var client = seeded.Client;

        var unknownId = Guid.NewGuid();

        // 404 NotFound for unknown PostId on GET /api/v1/posts/{id}, /comments, /reactions
        var notFoundPost = await client.GetAsync($"/api/v1/posts/{unknownId}");
        notFoundPost.StatusCode.Should().Be(HttpStatusCode.NotFound);
        notFoundPost.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var notFoundComments = await client.GetAsync($"/api/v1/posts/{unknownId}/comments");
        notFoundComments.StatusCode.Should().Be(HttpStatusCode.NotFound);
        notFoundComments.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var notFoundReactions = await client.GetAsync($"/api/v1/posts/{unknownId}/reactions");
        notFoundReactions.StatusCode.Should().Be(HttpStatusCode.NotFound);
        notFoundReactions.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        // 400 BadRequest when creating a post with empty title/content
        var badCreatePost = await client.PostAsJsonAsync("/api/v1/posts", new
        {
            title = "",
            content = ""
        });
        badCreatePost.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badCreatePost.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        // Create a valid post to test invalid comment/reaction payloads
        var validCreate = await client.PostAsJsonAsync("/api/v1/posts", new
        {
            title = "Valid Operational Post",
            content = "Operational post body for validation testing."
        });
        validCreate.StatusCode.Should().Be(HttpStatusCode.Created);
        var validPostId = (await validCreate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // 400 BadRequest when adding an empty comment
        var badComment = await client.PostAsJsonAsync($"/api/v1/posts/{validPostId}/comments", new
        {
            content = "   "
        });
        badComment.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badComment.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        // 400 BadRequest when adding an unsupported reactionType
        var badReaction = await client.PostAsJsonAsync($"/api/v1/posts/{validPostId}/reactions", new
        {
            reactionType = "INVALID_REACTION_TYPE"
        });
        badReaction.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badReaction.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        // 400 BadRequest when querying feed with invalid exceptionType
        var badFeedFilter = await client.GetAsync("/api/v1/feed?exceptionType=NOT_AN_EXCEPTION");
        badFeedFilter.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badFeedFilter.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    private async Task<SeededFeedAndPostTestContext> CreateAuthenticatedClientAndSeedContextAsync()
    {
        Guid author1Id;
        Guid author2Id;
        Guid customer1Id;
        Guid customer2Id;
        Guid product1Id;
        Guid product2Id;
        Guid request1Id;
        string sessionToken;

        using (var scope = _factory!.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var userAccountRepo = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
            var currentContext = scope.ServiceProvider.GetRequiredService<ICurrentContextProvider>() as CurrentContextProvider;

            var author1 = await mediator.Send(new CreatePersonCommand(
                "Rina",
                "Kartika",
                $"rina.{Guid.NewGuid():N}@cakra.id"));
            var author2 = await mediator.Send(new CreatePersonCommand(
                "Budi",
                "Santoso",
                $"budi.{Guid.NewGuid():N}@cakra.id"));
            author1Id = author1.Id;
            author2Id = author2.Id;

            var customer1 = await mediator.Send(new CreateCustomerCommand(
                $"CUST1-{Guid.NewGuid():N}"[..15],
                "RSUP Dr. Sardjito",
                HasActiveMaintenanceContract: true));
            var customer2 = await mediator.Send(new CreateCustomerCommand(
                $"CUST2-{Guid.NewGuid():N}"[..15],
                "RSUD Kota Yogyakarta",
                HasActiveMaintenanceContract: true));
            customer1Id = customer1.Id;
            customer2Id = customer2.Id;

            var product1 = await mediator.Send(new CreateProductCommand(
                $"PRD1-{Guid.NewGuid():N}"[..14],
                "MyHospital Billing",
                "Hospital Billing & INA-CBGs Integration",
                author1Id));
            var product2 = await mediator.Send(new CreateProductCommand(
                $"PRD2-{Guid.NewGuid():N}"[..14],
                "PenaEl Pharmacy",
                "Pharmacy Inventory & e-Prescription",
                author1Id));
            product1Id = product1.Id;
            product2Id = product2.Id;

            currentContext?.Initialize(Guid.NewGuid(), author1Id, new[] { "Implementator", "Programmer" });

            var req1 = await mediator.Send(new RecordRequestCommand(
                Title: "INA-CBGs tariff grouping validation fix",
                Description: "Ensure inpatient procedure codes map to updated tariff table.",
                CustomerId: customer1Id,
                ProductId: product1Id,
                RequestType: "Bug",
                Priority: "HIGH"));
            request1Id = req1.Id;

            var username = $"rina.{Guid.NewGuid():N}"[..20];
            var user = new UserAccount
            {
                Id = Guid.NewGuid(),
                PersonId = author1Id,
                Username = username,
                Email = author1.Email,
                Status = UserAccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = authService.HashPassword(user, "Password123!");
            await userAccountRepo.AddAsync(user);

            var loginResult = await authService.LoginAsync(username, "Password123!");
            loginResult.Succeeded.Should().BeTrue();
            sessionToken = loginResult.SessionToken!;
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Add("Cookie", $"{CakraAuthenticationDefaults.CookieName}={sessionToken}");

        return new SeededFeedAndPostTestContext(
            client,
            author1Id,
            author2Id,
            customer1Id,
            customer2Id,
            product1Id,
            product2Id,
            request1Id);
    }

    private sealed record SeededFeedAndPostTestContext(
        HttpClient Client,
        Guid Author1Id,
        Guid Author2Id,
        Guid Customer1Id,
        Guid Customer2Id,
        Guid Product1Id,
        Guid Product2Id,
        Guid Request1Id);
}
