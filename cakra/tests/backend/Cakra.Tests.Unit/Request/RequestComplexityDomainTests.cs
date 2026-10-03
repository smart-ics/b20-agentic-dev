using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Domain.Events;
using Cakra.Modules.Request.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Request;

public sealed class RequestComplexityDomainTests
{
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    private Cakra.Modules.Request.Domain.Request CreateRequest(int? complexity = null)
    {
        return Cakra.Modules.Request.Domain.Request.Record(
            id: Guid.NewGuid(),
            title: "Test Request Title",
            description: "Test Request Description",
            requestType: "Feature",
            actorPersonId: _actorId,
            complexity: complexity);
    }

    [Fact]
    public void Record_WithoutComplexity_DefaultsTo1()
    {
        // Act
        var request = CreateRequest(null);

        // Assert
        request.Complexity.Should().Be(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Record_WithValidComplexity_SetsCorrectValue(int complexity)
    {
        // Act
        var request = CreateRequest(complexity);

        // Assert
        request.Complexity.Should().Be(complexity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(99)]
    public void Record_WithOutOfRangeComplexity_ThrowsValidationException(int invalidComplexity)
    {
        // Act
        var act = () => CreateRequest(invalidComplexity);

        // Assert
        act.Should().Throw<RequestDomainValidationException>()
            .WithMessage("*Complexity must be an integer between 1 and 5.*");
    }

    [Fact]
    public void SetComplexity_WhenValidInCapturedState_UpdatesValueAndEmitsEvent()
    {
        // Arrange
        var request = CreateRequest(1);
        request.ClearDomainEvents();
        var now = DateTime.UtcNow;

        // Act
        request.SetComplexity(4, _actorId, "Requires significant database redesign", now);

        // Assert
        request.Complexity.Should().Be(4);
        request.UpdatedAt.Should().Be(now);

        var events = request.DomainEvents.OfType<RequestComplexityUpdated>().ToList();
        events.Should().HaveCount(1);
        var evt = events[0];
        evt.RequestId.Should().Be(request.Id);
        evt.PreviousComplexity.Should().Be(1);
        evt.NewComplexity.Should().Be(4);
        evt.ActorPersonId.Should().Be(_actorId);
        evt.Reason.Should().Be("Requires significant database redesign");
        evt.OccurredAtUtc.Should().Be(now);
    }

    [Fact]
    public void SetComplexity_WhenSameValue_DoesNotEmitEvent()
    {
        // Arrange
        var request = CreateRequest(3);
        request.ClearDomainEvents();

        // Act
        request.SetComplexity(3, _actorId, "No actual change");

        // Assert
        request.Complexity.Should().Be(3);
        request.DomainEvents.OfType<RequestComplexityUpdated>().Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(6)]
    public void SetComplexity_WithOutOfRangeValue_ThrowsValidationException(int invalidComplexity)
    {
        // Arrange
        var request = CreateRequest(1);

        // Act
        var act = () => request.SetComplexity(invalidComplexity, _actorId);

        // Assert
        act.Should().Throw<RequestDomainValidationException>();
    }

    [Fact]
    public void SetComplexity_WithEmptyActorId_ThrowsValidationException()
    {
        // Arrange
        var request = CreateRequest(1);

        // Act
        var act = () => request.SetComplexity(2, Guid.Empty);

        // Assert
        act.Should().Throw<RequestDomainValidationException>()
            .WithMessage("*ActorPersonId cannot be empty.*");
    }

    [Fact]
    public void SetComplexity_InEvaluatingState_Succeeds()
    {
        // Arrange
        var request = CreateRequest(1);
        request.AssignOwner(_ownerId, _actorId);
        request.Status.Should().Be(RequestStatus.Evaluating);

        // Act
        request.SetComplexity(3, _actorId);

        // Assert
        request.Complexity.Should().Be(3);
    }

    [Fact]
    public void SetComplexity_InAcceptedState_Succeeds()
    {
        // Arrange
        var request = CreateRequest(1);
        request.AssignOwner(_ownerId, _actorId);
        request.Accept(_ownerId);
        request.Status.Should().Be(RequestStatus.Accepted);

        // Act
        request.SetComplexity(5, _actorId, "Discovered architecture overhaul needed");

        // Assert
        request.Complexity.Should().Be(5);
    }

    [Fact]
    public void SetComplexity_WhenCompleted_ThrowsInvalidRequestStateTransitionException()
    {
        // Arrange
        var request = CreateRequest(1);
        request.AssignOwner(_ownerId, _actorId);
        request.Accept(_ownerId);
        request.StartProgress(_ownerId);
        request.Complete("Demand resolved", _ownerId);
        request.Status.Should().Be(RequestStatus.Completed);

        // Act
        var act = () => request.SetComplexity(2, _actorId);

        // Assert
        act.Should().Throw<InvalidRequestStateTransitionException>()
            .WithMessage("*Cannot change complexity on a closed request.*");
    }

    [Fact]
    public void SetComplexity_WhenRejected_ThrowsInvalidRequestStateTransitionException()
    {
        // Arrange
        var request = CreateRequest(1);
        request.AssignOwner(_ownerId, _actorId);
        request.Reject("Out of operational scope", _ownerId);
        request.Status.Should().Be(RequestStatus.Rejected);

        // Act
        var act = () => request.SetComplexity(2, _actorId);

        // Assert
        act.Should().Throw<InvalidRequestStateTransitionException>()
            .WithMessage("*Cannot change complexity on a closed request.*");
    }
}
