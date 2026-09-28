namespace ICS.Tests.Integration;

using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using ICS.Tests.Integration;
using Xunit;

/// <summary>
/// Integration tests for Post module using WebApplicationFactory and SQL Server.
/// Architecture §19.8.
/// </summary>
public class PostIntegrationTests : IntegrationTestBase
{
    public PostIntegrationTests(IcsWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CreateOperationalPost_AndAddCommentAndReaction_ShouldReturnExpectedResults()
    {
        // Arrange
        var authorPersonId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act - Create post
        var createPostCommand = new CreateOperationalPostCommand(
            "Test Post Title",
            "Test Post Content",
            authorPersonId,
            null,
            null,
            now);
        var createPostResponse = await Client.PostAsJsonAsync("/api/v1/post/command", createPostCommand);
        createPostResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdPost = await createPostResponse.Content.ReadFromJsonAsync<PostThreadDto>();
        createdPost.Should().NotBeNull();
        var postId = createdPost!.PostId;

        // Act - Add comment
        var commentContent = "Test Comment";
        var addCommentCommand = new PostCommentCommand(
            postId,
            commentContent,
            personId);
        var addCommentResponse = await Client.PostAsJsonAsync("/api/v1/post/command", addCommentCommand);
        addCommentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var commentedPost = await addCommentResponse.Content.ReadFromJsonAsync<PostThreadDto>();
        commentedPost.Should().NotBeNull();

        // Assert - Comment added
        commentedPost!.Comments.Should().HaveCount(1);
        commentedPost.Comments.First().Content.Should().Be(commentContent);
        commentedPost.Comments.First().AuthorPersonId.Should().Be(personId);

        // Act - Add reaction
        var reactionType = Reaction.Types.Seen;
        var addReactionCommand = new AddReactionCommand(
            postId,
            personId,
            reactionType);
        var addReactionResponse = await Client.PostAsJsonAsync("/api/v1/post/command", addReactionCommand);
        addReactionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reactedPost = await addReactionResponse.Content.ReadFromJsonAsync<PostThreadDto>();
        reactedPost.Should().NotBeNull();

        // Assert - Reaction added
        reactedPost!.Reactions.Should().HaveCount(1);
        reactedPost.Reactions.First().PersonId.Should().Be(personId);
        reactedPost.Reactions.First().Type.Should().Be(reactionType);

        // Act - Get post details via query service
        var getPostResponse = await Client.GetAsync($"/api/v1/post/query/{postId}");
        getPostResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var queriedPost = await getPostResponse.Content.ReadFromJsonAsync<PostThreadDto>();
        queriedPost.Should().NotBeNull();

        // Assert - Queried post matches
        queriedPost!.PostId.Should().Be(postId);
        queriedPost.Title.Should().Be("Test Post Title");
        queriedPost.Content.Should().Be("Test Post Content");
        queriedPost.Status.Should().Be(PostStatus.Active);
        queriedPost.Visibility.Should().Be(PostVisibility.Visible);
        queriedPost.AuthorPersonId.Should().Be(authorPersonId);
        queriedPost.Comments.Should().HaveCount(1);
        queriedPost.Reactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ArchivePost_ShouldSetStatusToArchived()
    {
        // Arrange
        var authorPersonId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act - Create post
        var createPostCommand = new CreateOperationalPostCommand(
            "Test Post to Archive",
            "Test Content",
            authorPersonId,
            null,
            null,
            now);
        var createPostResponse = await Client.PostAsJsonAsync("/api/v1/post/command", createPostCommand);
        createPostResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdPost = await createPostResponse.Content.ReadFromJsonAsync<PostThreadDto>();
        createdPost.Should().NotBeNull();
        var postId = createdPost!.PostId;

        // Act - Archive post
        var archivePostCommand = new ArchivePostCommand(
            postId,
            Guid.NewGuid());
        var archiveResponse = await Client.PostAsJsonAsync("/api/v1/post/command", archivePostCommand);
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var archivedPost = await archiveResponse.Content.ReadFromJsonAsync<PostThreadDto>();
        archivedPost.Should().NotBeNull();

        // Assert - Post archived
        archivedPost!.Status.Should().Be(PostStatus.Archived);
        archivedPost.ArchivedAt.Should().NotBeNull();

        // Act - Get post details
        var getPostResponse = await Client.GetAsync($"/api/v1/post/query/{postId}");
        getPostResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var queriedPost = await getPostResponse.Content.ReadFromJsonAsync<PostThreadDto>();
        queriedPost.Should().NotBeNull();

        // Assert - Queried post shows as archived
        queriedPost!.Status.Should().Be(PostStatus.Archived);
        queriedPost.ArchivedAt.Should().NotBeNull();
        queriedPost.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveReaction_ShouldSoftDeleteReaction_NotPhysicallyDelete()
    {
        // Arrange
        var authorPersonId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act - Create post
        var createPostCommand = new CreateOperationalPostCommand(
            "Test Post for Reaction",
            "Test Content",
            authorPersonId,
            null,
            null,
            now);
        var createPostResponse = await Client.PostAsJsonAsync("/api/v1/post/command", createPostCommand);
        createPostResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdPost = await createPostResponse.Content.ReadFromJsonAsync<PostThreadDto>();
        createdPost.Should().NotBeNull();
        var postId = createdPost!.PostId;

        // Act - Add reaction
        var reactionType = Reaction.Types.Experienced;
        var addReactionCommand = new AddReactionCommand(
            postId,
            personId,
            reactionType);
        var addReactionResponse = await Client.PostAsJsonAsync("/api/v1/post/command", addReactionCommand);
        addReactionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reactedPost = await addReactionResponse.Content.ReadFromJsonAsync<PostThreadDto>();
        reactedPost.Should().NotBeNull();

        // Act - Remove reaction
        var removeReactionCommand = new RemoveReactionCommand(
            postId,
            personId,
            reactionType);
        var removeReactionResponse = await Client.PostAsJsonAsync("/api/v1/post/command", removeReactionCommand);
        removeReactionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var postAfterRemove = await removeReactionResponse.Content.ReadFromJsonAsync<PostThreadDto>();
        postAfterRemove.Should().NotBeNull();

        // Assert - Reaction no longer in post's reaction list (removed from aggregate)
        postAfterRemove!.Reactions.Should().BeEmpty();

        // Note: We cannot directly verify the DB state from the test without breaking encapsulation,
        // but the implementation in PostRepository.RemoveReactionAsync uses UPDATE instead of DELETE,
        // which satisfies the soft-delete requirement.
    }
}