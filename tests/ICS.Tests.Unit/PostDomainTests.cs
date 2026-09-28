namespace ICS.Tests.Unit;

using System;
using System.Linq;
using FluentAssertions;
using ICS.Modules.Post.Domain;
using ICS.Modules.Post.Domain.Events;
using ICS.Modules.Post.Domain.Exceptions;
using Xunit;

/// <summary>
/// Unit tests verifying domain model invariants and domain event emission in Post module.
/// Architecture §12, §16, §17, §20; post-domain.md.
/// </summary>
public class PostDomainTests
{
    [Fact]
    public void Post_CreateHumanAuthored_ShouldInitializeCorrectly_AndEmitPostCreatedEvent()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorPersonId = Guid.NewGuid();
        var referenceType = PostReference.ReferenceTypes.Request;
        var targetId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var post = Post.CreateHumanAuthored(
            postId,
            "Test Title",
            "Test Content",
            authorPersonId,
            new[] { (referenceType, targetId) },
            now);

        // Assert
        post.Id.Should().Be(postId);
        post.Title.Should().Be("Test Title");
        post.Content.Should().Be("Test Content");
        post.Source.Should().Be(PostSource.HumanAuthored);
        post.Status.Should().Be(PostStatus.Active);
        post.Visibility.Should().Be(PostVisibility.Visible);
        post.AuthorPersonId.Should().Be(authorPersonId);
        post.CreatedAt.Should().Be(now);

        post.DomainEvents.Should().HaveCount(1);
        var createdEvent = post.DomainEvents.First() as PostCreated;
        createdEvent.Should().NotBeNull();
        createdEvent!.PostId.Should().Be(postId);
        createdEvent.Title.Should().Be("Test Title");
        createdEvent.Content.Should().Be("Test Content");
        createdEvent.Source.Should().Be(PostSource.HumanAuthored);
        createdEvent.AuthorPersonId.Should().Be(authorPersonId);
        createdEvent.Status.Should().Be(PostStatus.Active);
        createdEvent.Visibility.Should().Be(PostVisibility.Visible);
        createdEvent.References.Should().HaveCount(1);
        createdEvent.References.First().ReferenceType.Should().Be(referenceType);
        createdEvent.References.First().ReferenceId.Should().Be(targetId);
    }

    [Fact]
    public void Post_CreateSystemAuthored_ShouldInitializeCorrectly_AndEmitPostCreatedEvent()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var sourceEvent = "SystemEvent";
        var now = DateTime.UtcNow;

        // Act
        var post = Post.CreateSystemAuthored(
            postId,
            "Test Title",
            "Test Content",
            sourceEvent,
            now);

        // Assert
        post.Id.Should().Be(postId);
        post.Title.Should().Be("Test Title");
        post.Content.Should().Be("Test Content");
        post.Source.Should().Be(PostSource.SystemGenerated);
        post.Status.Should().Be(PostStatus.Active);
        post.Visibility.Should().Be(PostVisibility.Visible);
        post.AuthorPersonId.Should().BeNull();
        post.CreatedAt.Should().Be(now);

        post.DomainEvents.Should().HaveCount(1);
        var createdEvent = post.DomainEvents.First() as PostCreated;
        createdEvent.Should().NotBeNull();
        createdEvent!.PostId.Should().Be(postId);
        createdEvent.Title.Should().Be("Test Title");
        createdEvent.Content.Should().Be("Test Content");
        createdEvent.Source.Should().Be(PostSource.SystemGenerated);
        createdEvent.AuthorPersonId.Should().BeNull();
        createdEvent.Status.Should().Be(PostStatus.Active);
        createdEvent.Visibility.Should().Be(PostVisibility.Visible);
    }

    [Theory]
    [InlineData("", "Test Content")]
    [InlineData("   ", "Test Content")]
    [InlineData("Test Title", "")]
    [InlineData("Test Title", "   ")]
    public void Post_CreateHumanAuthored_WithInvalidArguments_ShouldThrowArgumentException(string title, string content)
    {
        var act = () => Post.CreateHumanAuthored(
            Guid.NewGuid(),
            title,
            content,
            Guid.NewGuid(),
            null,
            DateTime.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Post_CreateHumanAuthored_WithEmptyAuthor_ShouldThrowArgumentException()
    {
        var act = () => Post.CreateHumanAuthored(
            Guid.NewGuid(),
            "Test Title",
            "Test Content",
            Guid.Empty,
            null,
            DateTime.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("authorPersonId");
    }

    [Fact]
    public void Post_AddReference_ShouldAddReference_AndUpdateUpdatedAt()
    {
        // Arrange
        var post = Post.CreateHumanAuthored(
            Guid.NewGuid(),
            "Test Title",
            "Test Content",
            Guid.NewGuid(),
            null,
            DateTime.UtcNow);
        post.ClearDomainEvents();
        var referenceType = PostReference.ReferenceTypes.Customer;
        var targetId = Guid.NewGuid();
        var now = DateTime.UtcNow.AddMinutes(5);

        // Act
        post.AddReference(referenceType, targetId, now);

        // Assert
        post.References.Should().HaveCount(1);
        post.References.First().ReferenceType.Should().Be(referenceType);
        post.References.First().TargetId.Should().Be(targetId);
        post.UpdatedAt.Should().Be(now);
        post.DomainEvents.Should().BeEmpty(); // AddReference doesn't emit domain events
    }

    [Fact]
    public void Post_AddComment_ShouldAddComment_AndUpdateUpdatedAt_AndEmitCommentAddedEvent()
    {
        // Arrange
        var post = Post.CreateHumanAuthored(
            Guid.NewGuid(),
            "Test Title",
            "Test Content",
            Guid.NewGuid(),
            null,
            DateTime.UtcNow);
        post.ClearDomainEvents();
        var authorPersonId = Guid.NewGuid();
        var now = DateTime.UtcNow.AddMinutes(10);

        // Act
        post.AddComment("Test Comment", authorPersonId, now);

        // Assert
        post.Comments.Should().HaveCount(1);
        post.Comments.First().Content.Should().Be("Test Comment");
        post.Comments.First().AuthorPersonId.Should().Be(authorPersonId);
        post.UpdatedAt.Should().Be(now);
        post.DomainEvents.Should().HaveCount(1);
        var commentAddedEvent = post.DomainEvents.First() as CommentAdded;
        commentAddedEvent.Should().NotBeNull();
        commentAddedEvent!.Content.Should().Be("Test Comment");
        commentAddedEvent.AuthorPersonId.Should().Be(authorPersonId);
        commentAddedEvent.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void Post_AddReaction_ShouldAddReaction_AndUpdateUpdatedAt_AndEmitReactionAddedEvent()
    {
        // Arrange
        var post = Post.CreateHumanAuthored(
            Guid.NewGuid(),
            "Test Title",
            "Test Content",
            Guid.NewGuid(),
            null,
            DateTime.UtcNow);
        post.ClearDomainEvents();
        var personId = Guid.NewGuid();
        var reactionType = Reaction.Types.Seen;
        var now = DateTime.UtcNow.AddMinutes(15);

        // Act
        post.AddReaction(personId, reactionType, now);

        // Assert
        post.Reactions.Should().HaveCount(1);
        post.Reactions.First().PersonId.Should().Be(personId);
        post.Reactions.First().Type.Should().Be(reactionType);
        post.UpdatedAt.Should().Be(now);
        post.DomainEvents.Should().HaveCount(1);
        var reactionAddedEvent = post.DomainEvents.First() as ReactionAdded;
        reactionAddedEvent.Should().NotBeNull();
        reactionAddedEvent!.PersonId.Should().Be(personId);
        reactionAddedEvent.Type.Should().Be(reactionType);
        reactionAddedEvent.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void Post_AddReaction_DuplicateReactionType_ShouldThrowInvalidReactionException()
    {
        // Arrange
        var post = Post.CreateHumanAuthored(
            Guid.NewGuid(),
            "Test Title",
            "Test Content",
            Guid.NewGuid(),
            null,
            DateTime.UtcNow);
        var personId = Guid.NewGuid();
        var reactionType = Reaction.Types.Seen;
        var now = DateTime.UtcNow;

        // Act - Add first reaction
        post.AddReaction(personId, reactionType, now);

        // Act - Try to add duplicate
        var act = () => post.AddReaction(personId, reactionType, now.AddMinutes(1));

        // Assert
        act.Should().Throw<InvalidReactionException>()
            .WithMessage($"Person '{personId}' has already expressed a '{reactionType}' reaction on this Post.");
    }

    [Fact]
    public void Post_RemoveReaction_ShouldRemoveReaction_AndUpdateUpdatedAt_AndEmitReactionRemovedEvent()
    {
        // Arrange
        var post = Post.CreateHumanAuthored(
            Guid.NewGuid(),
            "Test Title",
            "Test Content",
            Guid.NewGuid(),
            null,
            DateTime.UtcNow);
        var personId = Guid.NewGuid();
        var reactionType = Reaction.Types.Seen;
        var now = DateTime.UtcNow;
        var removeTime = now.AddMinutes(5);

        // Add reaction first
        post.AddReaction(personId, reactionType, now);
        post.ClearDomainEvents();

        // Act
        post.RemoveReaction(personId, reactionType, removeTime);

        // Assert
        post.Reactions.Should().BeEmpty();
        post.UpdatedAt.Should().Be(removeTime);
        post.DomainEvents.Should().HaveCount(1);
        var reactionRemovedEvent = post.DomainEvents.First() as ReactionRemoved;
        reactionRemovedEvent.Should().NotBeNull();
        reactionRemovedEvent!.PersonId.Should().Be(personId);
        reactionRemovedEvent.Type.Should().Be(reactionType);
        reactionRemovedEvent.RemovedAt.Should().Be(removeTime);
    }

    [Fact]
    public void Post_RemoveReaction_NonExistentReaction_ShouldThrowInvalidReactionException()
    {
        // Arrange
        var post = Post.CreateHumanAuthored(
            Guid.NewGuid(),
            "Test Title",
            "Test Content",
            Guid.NewGuid(),
            null,
            DateTime.UtcNow);
        var personId = Guid.NewGuid();
        var reactionType = Reaction.Types.Seen;
        var now = DateTime.UtcNow;

        // Act
        var act = () => post.RemoveReaction(personId, reactionType, now);

        // Assert
        act.Should().Throw<InvalidReactionException>()
            .WithMessage($"No matching '{reactionType}' reaction found for Person '{personId}' on Post '{post.Id}'.");
    }

    [Fact]
    public void Post_ChangeVisibility_ShouldChangeVisibility_AndUpdateUpdatedAt_AndEmitPostVisibilityChangedEvent()
    {
        // Arrange
        var post = Post.CreateHumanAuthored(
            Guid.NewGuid(),
            "Test Title",
            "Test Content",
            Guid.NewGuid(),
            null,
            DateTime.UtcNow);
        post.ClearDomainEvents();
        var newVisibility = PostVisibility.Hidden;
        var changedByPersonId = Guid.NewGuid();
        var now = DateTime.UtcNow.AddMinutes(20);

        // Act
        post.ChangeVisibility(newVisibility, changedByPersonId, now);

        // Assert
        post.Visibility.Should().Be(newVisibility);
        post.UpdatedAt.Should().Be(now);
        post.DomainEvents.Should().HaveCount(1);
        var visibilityChangedEvent = post.DomainEvents.First() as PostVisibilityChanged;
        visibilityChangedEvent.Should().NotBeNull();
        visibilityChangedEvent!.PreviousVisibility.Should().Be(PostVisibility.Visible);
        visibilityChangedEvent.NewVisibility.Should().Be(newVisibility);
        visibilityChangedEvent.ChangedByPersonId.Should().Be(changedByPersonId);
        visibilityChangedEvent.ChangedAt.Should().Be(now);
    }

    [Fact]
    public void Post_Archive_ShouldArchivePost_AndUpdateUpdatedAt_AndEmitPostArchivedEvent()
    {
        // Arrange
        var post = Post.CreateHumanAuthored(
            Guid.NewGuid(),
            "Test Title",
            "Test Content",
            Guid.NewGuid(),
            null,
            DateTime.UtcNow);
        post.ClearDomainEvents();
        var archivedByPersonId = Guid.NewGuid();
        var now = DateTime.UtcNow.AddMinutes(25);

        // Act
        post.Archive(archivedByPersonId, now);

        // Assert
        post.Status.Should().Be(PostStatus.Archived);
        post.ArchivedAt.Should().Be(now);
        post.UpdatedAt.Should().Be(now);
        post.DomainEvents.Should().HaveCount(1);
        var archivedEvent = post.DomainEvents.First() as PostArchived;
        archivedEvent.Should().NotBeNull();
        archivedEvent!.PostId.Should().Be(post.Id);
        archivedEvent.ArchivedByPersonId.Should().Be(archivedByPersonId);
        archivedEvent.ArchivedAt.Should().Be(now);
    }

    [Fact]
    public void Post_Restore_ShouldRestorePost_AndUpdateUpdatedAt()
    {
        // Arrange
        var post = Post.CreateHumanAuthored(
            Guid.NewGuid(),
            "Test Title",
            "Test Content",
            Guid.NewGuid(),
            null,
            DateTime.UtcNow);
        post.Archive(Guid.NewGuid(), DateTime.UtcNow);
        post.ClearDomainEvents();
        var now = DateTime.UtcNow.AddMinutes(30);

        // Act
        post.Restore(Guid.NewGuid(), now);

        // Assert
        post.Status.Should().Be(PostStatus.Active);
        post.ArchivedAt.Should().BeNull();
        post.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void Post_IsArchived_ShouldReturnCorrectValue()
    {
        // Arrange
        var post = Post.CreateHumanAuthored(
            Guid.NewGuid(),
            "Test Title",
            "Test Content",
            Guid.NewGuid(),
            null,
            DateTime.UtcNow);

        // Act & Assert
        post.IsArchived.Should().BeFalse();

        // Act
        post.Archive(Guid.NewGuid(), DateTime.UtcNow);

        // Assert
        post.IsArchived.Should().BeTrue();
    }

    [Fact]
    public void Post_IsActive_ShouldReturnCorrectValue()
    {
        // Arrange
        var post = Post.CreateHumanAuthored(
            Guid.NewGuid(),
            "Test Title",
            "Test Content",
            Guid.NewGuid(),
            null,
            DateTime.UtcNow);

        // Act & Assert
        post.IsActive.Should().BeTrue();

        // Act
        post.Archive(Guid.NewGuid(), DateTime.UtcNow);

        // Assert
        post.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Post_IsVisible_ShouldReturnCorrectValue()
    {
        // Arrange
        var post = Post.CreateHumanAuthored(
            Guid.NewGuid(),
            "Test Title",
            "Test Content",
            Guid.NewGuid(),
            null,
            DateTime.UtcNow);

        // Act & Assert
        post.IsVisible.Should().BeTrue();

        // Act
        post.ChangeVisibility(PostVisibility.Hidden, Guid.NewGuid(), DateTime.UtcNow);

        // Assert
        post.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void Post_IsHidden_ShouldReturnCorrectValue()
    {
        // Arrange
        var post = Post.CreateHumanAuthored(
            Guid.NewGuid(),
            "Test Title",
            "Test Content",
            Guid.NewGuid(),
            null,
            DateTime.UtcNow);

        // Act & Assert
        post.IsHidden.Should().BeFalse();

        // Act
        post.ChangeVisibility(PostVisibility.Hidden, Guid.NewGuid(), DateTime.UtcNow);

        // Assert
        post.IsHidden.Should().BeTrue();
    }
}