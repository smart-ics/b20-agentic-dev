using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Domain.Events;
using Cakra.Modules.Request.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Request;

public sealed class RequestCoreAttributesDomainTests
{
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    private Cakra.Modules.Request.Domain.Request CreateCapturedRequest()
    {
        return Cakra.Modules.Request.Domain.Request.Record(
            id: Guid.NewGuid(),
            title: "Original Request Title",
            description: "Original Request Description",
            requestType: "GENERAL",
            actorPersonId: _actorId,
            priority: "NORMAL");
    }

    [Fact]
    public void UpdateCoreAttributes_ValidData_UpdatesAttributesAndEmitsDomainEvent()
    {
        // Arrange
        var request = CreateCapturedRequest();
        var updateTime = DateTime.UtcNow.AddMinutes(5);

        // Act
        request.UpdateCoreAttributes(
            title: "Updated Title",
            description: "Updated Description details",
            priority: "HIGH",
            requestType: "BUG",
            actorPersonId: _actorId,
            utcNow: updateTime);

        // Assert
        request.Title.Should().Be("Updated Title");
        request.Description.Should().Be("Updated Description details");
        request.Priority.Should().Be("HIGH");
        request.RequestType.Should().Be("BUG");
        request.UpdatedAt.Should().Be(updateTime);

        var events = request.DomainEvents.OfType<RequestCoreAttributesUpdated>().ToList();
        events.Should().HaveCount(1);

        var domainEvent = events.Single();
        domainEvent.RequestId.Should().Be(request.Id);
        domainEvent.Title.Should().Be("Updated Title");
        domainEvent.Description.Should().Be("Updated Description details");
        domainEvent.Priority.Should().Be("HIGH");
        domainEvent.RequestType.Should().Be("BUG");
        domainEvent.ActorPersonId.Should().Be(_actorId);
        domainEvent.OccurredAtUtc.Should().Be(updateTime);
        domainEvent.EventId.Should().NotBeEmpty();
    }

    [Fact]
    public void UpdateCoreAttributes_NormalizesPriorityAndRequestType()
    {
        // Arrange
        var request = CreateCapturedRequest();

        // Act
        request.UpdateCoreAttributes(
            title: "  Padded Title  ",
            description: "  Padded Description  ",
            priority: "  urgent  ",
            requestType: "  feature  ",
            actorPersonId: _actorId);

        // Assert
        request.Title.Should().Be("Padded Title");
        request.Description.Should().Be("Padded Description");
        request.Priority.Should().Be("URGENT");
        request.RequestType.Should().Be("FEATURE");
    }

    [Fact]
    public void UpdateCoreAttributes_EmptyPriorityAndType_DefaultsToNormalAndGeneral()
    {
        // Arrange
        var request = CreateCapturedRequest();

        // Act
        request.UpdateCoreAttributes(
            title: "Title",
            description: "Description",
            priority: "",
            requestType: "   ",
            actorPersonId: _actorId);

        // Assert
        request.Priority.Should().Be("NORMAL");
        request.RequestType.Should().Be("GENERAL");
    }

    [Fact]
    public void UpdateCoreAttributes_WhenCompleted_ThrowsInvalidRequestStateTransitionException()
    {
        // Arrange
        var request = CreateCapturedRequest();
        request.AssignOwner(_ownerId, _actorId);
        request.StartWork(_ownerId);
        request.Complete("Work done", _ownerId);

        // Act
        var act = () => request.UpdateCoreAttributes(
            title: "New Title",
            description: "New Description",
            priority: "HIGH",
            requestType: "BUG",
            actorPersonId: _actorId);

        // Assert
        act.Should().Throw<InvalidRequestStateTransitionException>()
            .WithMessage($"*Cannot edit core attributes of closed request '{request.Id}' in status 'Completed'*");
    }

    [Fact]
    public void UpdateCoreAttributes_WhenCancelled_ThrowsInvalidRequestStateTransitionException()
    {
        // Arrange
        var request = CreateCapturedRequest();
        request.Cancel("Not needed", _actorId);

        // Act
        var act = () => request.UpdateCoreAttributes(
            title: "New Title",
            description: "New Description",
            priority: "HIGH",
            requestType: "BUG",
            actorPersonId: _actorId);

        // Assert
        act.Should().Throw<InvalidRequestStateTransitionException>()
            .WithMessage($"*Cannot edit core attributes of closed request '{request.Id}' in status 'Cancelled'*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateCoreAttributes_EmptyTitle_ThrowsValidationException(string? invalidTitle)
    {
        // Arrange
        var request = CreateCapturedRequest();

        // Act
        var act = () => request.UpdateCoreAttributes(
            title: invalidTitle!,
            description: "Valid Description",
            priority: "NORMAL",
            requestType: "GENERAL",
            actorPersonId: _actorId);

        // Assert
        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("title");
    }

    [Fact]
    public void UpdateCoreAttributes_TitleExceeding255Chars_ThrowsValidationException()
    {
        // Arrange
        var request = CreateCapturedRequest();
        var longTitle = new string('A', 256);

        // Act
        var act = () => request.UpdateCoreAttributes(
            title: longTitle,
            description: "Valid Description",
            priority: "NORMAL",
            requestType: "GENERAL",
            actorPersonId: _actorId);

        // Assert
        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("title");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateCoreAttributes_EmptyDescription_ThrowsValidationException(string? invalidDescription)
    {
        // Arrange
        var request = CreateCapturedRequest();

        // Act
        var act = () => request.UpdateCoreAttributes(
            title: "Valid Title",
            description: invalidDescription!,
            priority: "NORMAL",
            requestType: "GENERAL",
            actorPersonId: _actorId);

        // Assert
        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("description");
    }

    [Fact]
    public void UpdateCoreAttributes_EmptyActorPersonId_ThrowsValidationException()
    {
        // Arrange
        var request = CreateCapturedRequest();

        // Act
        var act = () => request.UpdateCoreAttributes(
            title: "Valid Title",
            description: "Valid Description",
            priority: "NORMAL",
            requestType: "GENERAL",
            actorPersonId: Guid.Empty);

        // Assert
        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("actorPersonId");
    }
}
