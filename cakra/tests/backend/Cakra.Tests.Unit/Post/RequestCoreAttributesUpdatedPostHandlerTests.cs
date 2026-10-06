using Cakra.Modules.Post.Domain;
using Cakra.Modules.Post.Domain.Exceptions;
using Cakra.Modules.Post.Persistence;
using Cakra.Modules.Post.Services;
using Cakra.Modules.Request.Domain.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using PostEntity = Cakra.Modules.Post.Domain.Post;

namespace Cakra.Tests.Unit.Post;

public sealed class RequestCoreAttributesUpdatedPostHandlerTests
{
    [Fact]
    public void UpdateContent_ValidData_UpdatesTitleContentAndUpdatedAt()
    {
        // Arrange
        var post = PostEntity.RecordSystemPost(
            title: "Original Title",
            content: "Original Content",
            sourceEventType: "RequestRecorded");

        var updateTime = DateTime.UtcNow.AddMinutes(5);

        // Act
        post.UpdateContent(
            title: "Updated Title",
            content: "Updated Content",
            updatedAtUtc: updateTime);

        // Assert
        post.Title.Should().Be("Updated Title");
        post.Content.Should().Be("Updated Content");
        post.Body.Should().Be("Updated Content");
        post.UpdatedAt.Should().Be(updateTime);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateContent_EmptyTitle_ThrowsPostDomainValidationException(string? invalidTitle)
    {
        // Arrange
        var post = PostEntity.RecordSystemPost(
            title: "Original Title",
            content: "Original Content",
            sourceEventType: "RequestRecorded");

        // Act
        var act = () => post.UpdateContent(invalidTitle!, "Valid Content");

        // Assert
        act.Should().Throw<PostDomainValidationException>()
            .WithParameterName("title");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateContent_EmptyContent_ThrowsPostDomainValidationException(string? invalidContent)
    {
        // Arrange
        var post = PostEntity.RecordSystemPost(
            title: "Original Title",
            content: "Original Content",
            sourceEventType: "RequestRecorded");

        // Act
        var act = () => post.UpdateContent("Valid Title", invalidContent!);

        // Assert
        act.Should().Throw<PostDomainValidationException>()
            .WithParameterName("content");
    }

    [Fact]
    public async Task Handle_WhenRootPostExists_UpdatesTitleContentAndSaves()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        var repo = new InMemoryPostRepository();
        var rootPost = PostEntity.RecordSystemPost(
            title: "Request: Initial Request Title",
            content: "Initial Request Description",
            sourceEventType: "RequestRecorded",
            requestId: requestId);

        await repo.AddAsync(rootPost);

        var handler = new RequestCoreAttributesUpdatedPostHandler(
            repo,
            NullLogger<RequestCoreAttributesUpdatedPostHandler>.Instance);

        var notification = new RequestCoreAttributesUpdated(
            RequestId: requestId,
            Title: "Edited Request Title",
            Description: "Edited Request Description",
            Priority: "HIGH",
            RequestType: "FEATURE",
            ActorPersonId: actorId,
            OccurredAtUtc: occurredAt);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        repo.UpdatedPosts.Should().HaveCount(1);
        var updated = repo.UpdatedPosts[0];
        updated.Id.Should().Be(rootPost.Id);
        updated.Title.Should().Be("Request: Edited Request Title");
        updated.Content.Should().Be("Edited Request Description");
        updated.UpdatedAt.Should().Be(occurredAt);
    }

    [Fact]
    public async Task Handle_WhenNoRootPostExists_DoesNotThrowAndDoesNotUpdate()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var repo = new InMemoryPostRepository();
        var handler = new RequestCoreAttributesUpdatedPostHandler(
            repo,
            NullLogger<RequestCoreAttributesUpdatedPostHandler>.Instance);

        var notification = new RequestCoreAttributesUpdated(
            RequestId: requestId,
            Title: "Edited Request Title",
            Description: "Edited Request Description",
            Priority: "NORMAL",
            RequestType: "GENERAL",
            ActorPersonId: Guid.NewGuid(),
            OccurredAtUtc: DateTime.UtcNow);

        // Act
        var act = () => handler.Handle(notification, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        repo.UpdatedPosts.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenOtherPostsExistForRequest_OnlyUpdatesRootPost()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var repo = new InMemoryPostRepository();

        var rootPost = PostEntity.RecordSystemPost(
            title: "Request: Root Title",
            content: "Root Description",
            sourceEventType: "RequestRecorded",
            requestId: requestId);

        var nonRootPost = PostEntity.RecordSystemPost(
            title: "Work Package Started",
            content: "Work Package details",
            sourceEventType: "WorkStarted",
            requestId: requestId);

        await repo.AddAsync(rootPost);
        await repo.AddAsync(nonRootPost);

        var handler = new RequestCoreAttributesUpdatedPostHandler(
            repo,
            NullLogger<RequestCoreAttributesUpdatedPostHandler>.Instance);

        var notification = new RequestCoreAttributesUpdated(
            RequestId: requestId,
            Title: "New Title",
            Description: "New Description",
            Priority: "URGENT",
            RequestType: "BUG",
            ActorPersonId: Guid.NewGuid(),
            OccurredAtUtc: DateTime.UtcNow);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        repo.UpdatedPosts.Should().HaveCount(1);
        repo.UpdatedPosts[0].Id.Should().Be(rootPost.Id);
        repo.UpdatedPosts[0].Title.Should().Be("Request: New Title");
        repo.UpdatedPosts[0].Content.Should().Be("New Description");
    }

    [Fact]
    public async Task Handle_NullNotification_ThrowsArgumentNullException()
    {
        // Arrange
        var repo = new InMemoryPostRepository();
        var handler = new RequestCoreAttributesUpdatedPostHandler(
            repo,
            NullLogger<RequestCoreAttributesUpdatedPostHandler>.Instance);

        // Act
        var act = () => handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private sealed class InMemoryPostRepository : IPostRepository
    {
        private readonly List<PostEntity> _posts = new();
        public List<PostEntity> UpdatedPosts { get; } = new();

        public Task<PostEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_posts.FirstOrDefault(p => p.Id == id));

        public Task<IReadOnlyList<PostEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PostEntity>>(_posts.ToList());

        public Task AddAsync(PostEntity entity, CancellationToken cancellationToken = default)
        {
            _posts.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PostEntity entity, CancellationToken cancellationToken = default)
        {
            UpdatedPosts.Add(entity);
            var index = _posts.FindIndex(p => p.Id == entity.Id);
            if (index >= 0)
            {
                _posts[index] = entity;
            }
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _posts.RemoveAll(p => p.Id == id);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PostEntity>> GetByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PostEntity>>(_posts.Where(p => p.RequestId == requestId).ToList());

        public Task AddCommentAsync(Comment comment, DateTime postUpdatedAtUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddReactionAsync(Reaction reaction, DateTime postUpdatedAtUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateReactionAsync(Reaction reaction, DateTime postUpdatedAtUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddReferenceAsync(PostReference reference, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
