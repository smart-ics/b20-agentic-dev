namespace ICS.Tests.Unit;

using FluentAssertions;
using ICS.Modules.Request.Domain;
using ICS.Modules.Request.Domain.Events;
using ICS.Modules.Request.Domain.Exceptions;
using Xunit;

public class RequestDomainTests
{
    private readonly Guid _testRequestId = Guid.NewGuid();
    private readonly Guid _testRequesterPersonId = Guid.NewGuid();
    private readonly Guid _testOwnerPersonId = Guid.NewGuid();
    private readonly Guid _testAssignedByPersonId = Guid.NewGuid();
    private readonly DateTime _baseTime = new(2026, 9, 28, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Record_WithValidParameters_CreatesCapturedRequestAndEmitsRequestRecorded()
    {
        // Act
        var request = Request.Record(
            _testRequestId,
            "Add BPJS Validation",
            "Customer requires automated BPJS claim validation on checkout",
            Request.TypeFeature,
            Request.PriorityHigh,
            _testRequesterPersonId,
            requesterContactId: null,
            requesterName: "Dr. Sutopo",
            customerId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            workPackageId: null,
            recordedAt: _baseTime);

        // Assert
        request.Id.Should().Be(_testRequestId);
        request.Title.Should().Be("Add BPJS Validation");
        request.Description.Should().Be("Customer requires automated BPJS claim validation on checkout");
        request.Type.Should().Be(Request.TypeFeature);
        request.Priority.Should().Be(Request.PriorityHigh);
        request.Status.Should().Be(RequestStatus.Captured);
        request.RequesterPersonId.Should().Be(_testRequesterPersonId);
        request.RequesterName.Should().Be("Dr. Sutopo");
        request.IsTerminal.Should().BeFalse();
        request.IsActive.Should().BeFalse();

        request.DomainEvents.Should().ContainSingle(e => e is RequestRecorded)
            .Which.Should().BeOfType<RequestRecorded>()
            .Which.Should().Match<RequestRecorded>(e =>
                e.RequestId == _testRequestId &&
                e.Title == "Add BPJS Validation" &&
                e.Type == Request.TypeFeature &&
                e.RequesterPersonId == _testRequesterPersonId);
    }

    [Theory]
    [InlineData("", "Description", "BUG")]
    [InlineData("   ", "Description", "BUG")]
    [InlineData("Title", "", "BUG")]
    [InlineData("Title", "   ", "BUG")]
    [InlineData("Title", "Description", "")]
    [InlineData("Title", "Description", "   ")]
    public void Record_WithInvalidInputs_ThrowsArgumentException(string title, string description, string type)
    {
        // Act
        var act = () => Request.Record(
            Guid.NewGuid(),
            title,
            description,
            type,
            Request.PriorityMedium,
            _testRequesterPersonId,
            null,
            null,
            null,
            null,
            null,
            _baseTime);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Record_WithInitialOwner_AssignsOwnerAndEmitsBothRecordedAndAssignedEvents()
    {
        // Act
        var request = Request.Record(
            _testRequestId,
            "Urgent Server Outage",
            "Main web service unreachable",
            Request.TypeBug,
            Request.PriorityCritical,
            _testRequesterPersonId,
            null,
            "Admin",
            null,
            null,
            null,
            _baseTime,
            initialOwnerPersonId: _testOwnerPersonId,
            assignedByPersonId: _testAssignedByPersonId);

        // Assert
        request.OwnerPersonId.Should().Be(_testOwnerPersonId);
        request.Assignments.Should().HaveCount(1);
        request.Assignments.First().OwnerPersonId.Should().Be(_testOwnerPersonId);
        request.Assignments.First().IsActive.Should().BeTrue();

        request.DomainEvents.Should().ContainSingle(e => e is RequestRecorded);
        request.DomainEvents.Should().ContainSingle(e => e is RequestAssigned)
            .Which.Should().BeOfType<RequestAssigned>()
            .Which.Should().Match<RequestAssigned>(e =>
                e.RequestId == _testRequestId &&
                e.PreviousOwnerPersonId == null &&
                e.NewOwnerPersonId == _testOwnerPersonId &&
                e.AssignedByPersonId == _testAssignedByPersonId);
    }

    [Fact]
    public void AssignOwner_InCapturedState_RecordsAssignmentAndEmitsRequestAssigned()
    {
        // Arrange
        var request = CreateCapturedRequest();
        var assignTime = _baseTime.AddMinutes(10);

        // Act
        request.AssignOwner(_testOwnerPersonId, _testAssignedByPersonId, assignTime, "Assigned to Lead Programmer");

        // Assert
        request.OwnerPersonId.Should().Be(_testOwnerPersonId);
        request.UpdatedAt.Should().Be(assignTime);
        request.Assignments.Should().ContainSingle(a =>
            a.OwnerPersonId == _testOwnerPersonId &&
            a.AssignedByPersonId == _testAssignedByPersonId &&
            a.Note == "Assigned to Lead Programmer" &&
            a.IsActive);

        request.DomainEvents.Should().ContainSingle(e => e is RequestAssigned)
            .Which.Should().BeOfType<RequestAssigned>()
            .Which.Should().Match<RequestAssigned>(e =>
                e.RequestId == request.Id &&
                e.PreviousOwnerPersonId == null &&
                e.NewOwnerPersonId == _testOwnerPersonId &&
                e.AssignedByPersonId == _testAssignedByPersonId &&
                e.Note == "Assigned to Lead Programmer");
    }

    [Fact]
    public void AssignOwner_ReassigningNewOwner_DeactivatesPreviousAssignmentAndPreservesHistory()
    {
        // Arrange
        var request = CreateCapturedRequest();
        var firstTime = _baseTime.AddMinutes(10);
        var secondTime = _baseTime.AddMinutes(30);
        var secondOwnerId = Guid.NewGuid();

        request.AssignOwner(_testOwnerPersonId, _testAssignedByPersonId, firstTime);

        // Act
        request.AssignOwner(secondOwnerId, _testAssignedByPersonId, secondTime, "Reassigned workload balance");

        // Assert
        request.OwnerPersonId.Should().Be(secondOwnerId);
        request.Assignments.Should().HaveCount(2);

        var firstAssignment = request.Assignments.First(a => a.OwnerPersonId == _testOwnerPersonId);
        firstAssignment.IsActive.Should().BeFalse();
        firstAssignment.UpdatedAt.Should().Be(secondTime);

        var secondAssignment = request.Assignments.First(a => a.OwnerPersonId == secondOwnerId);
        secondAssignment.IsActive.Should().BeTrue();
        secondAssignment.Note.Should().Be("Reassigned workload balance");

        var assignedEvents = request.DomainEvents.OfType<RequestAssigned>().ToList();
        assignedEvents.Should().HaveCount(2);
        assignedEvents[1].PreviousOwnerPersonId.Should().Be(_testOwnerPersonId);
        assignedEvents[1].NewOwnerPersonId.Should().Be(secondOwnerId);
    }

    [Fact]
    public void FullLifecycle_CapturedToCompleted_FollowsStandardTransitionsAndEmitsEvents()
    {
        // Arrange
        var request = CreateCapturedRequest();
        request.ClearDomainEvents();

        // 1. Evaluate: CAPTURED → EVALUATING
        var evalTime = _baseTime.AddMinutes(5);
        request.Evaluate(_testOwnerPersonId, evalTime, "Evaluating scope and feasibility");
        request.Status.Should().Be(RequestStatus.Evaluating);
        request.IsActive.Should().BeTrue();
        request.DomainEvents.Should().ContainSingle(e => e is RequestEvaluated)
            .Which.Should().BeOfType<RequestEvaluated>()
            .Which.Should().Match<RequestEvaluated>(e =>
                e.RequestId == request.Id &&
                e.EvaluatedByPersonId == _testOwnerPersonId &&
                e.Notes == "Evaluating scope and feasibility");

        // 2. Accept: EVALUATING → ACCEPTED
        var acceptTime = _baseTime.AddMinutes(15);
        request.Accept(_testOwnerPersonId, acceptTime, "Accepted for implementation");
        request.Status.Should().Be(RequestStatus.Accepted);
        request.IsActive.Should().BeTrue();
        request.DomainEvents.Should().ContainSingle(e => e is RequestAccepted)
            .Which.Should().BeOfType<RequestAccepted>()
            .Which.Should().Match<RequestAccepted>(e =>
                e.RequestId == request.Id &&
                e.AcceptedByPersonId == _testOwnerPersonId &&
                e.Notes == "Accepted for implementation");

        // 3. Start Progress: ACCEPTED → IN_PROGRESS
        var progressTime = _baseTime.AddHours(1);
        request.StartProgress(_testOwnerPersonId, progressTime);
        request.Status.Should().Be(RequestStatus.InProgress);
        request.IsActive.Should().BeTrue();

        // 4. Complete: IN_PROGRESS → COMPLETED
        var completeTime = _baseTime.AddHours(5);
        request.Complete(_testAssignedByPersonId, "Feature developed and verified in staging", completeTime);
        request.Status.Should().Be(RequestStatus.Completed);
        request.IsTerminal.Should().BeTrue();
        request.IsActive.Should().BeFalse();
        request.ClosedAt.Should().Be(completeTime);
        request.Resolution.Should().NotBeNull();
        request.Resolution!.Outcome.Should().Be(RequestResolution.OutcomeResolved);
        request.Resolution.Summary.Should().Be("Feature developed and verified in staging");
        request.Resolution.ResolvedByPersonId.Should().Be(_testAssignedByPersonId);

        request.DomainEvents.Should().ContainSingle(e => e is RequestCompleted)
            .Which.Should().BeOfType<RequestCompleted>()
            .Which.Should().Match<RequestCompleted>(e =>
                e.RequestId == request.Id &&
                e.CompletedByPersonId == _testAssignedByPersonId &&
                e.Summary == "Feature developed and verified in staging" &&
                e.Outcome == RequestResolution.OutcomeResolved);
    }

    [Fact]
    public void Reject_FromEvaluatingState_TransitionsToRejectedAndEmitsRequestRejected()
    {
        // Arrange
        var request = CreateCapturedRequest();
        request.Evaluate(_testOwnerPersonId, _baseTime.AddMinutes(5));
        request.ClearDomainEvents();

        // Act
        var rejectTime = _baseTime.AddMinutes(20);
        request.Reject(_testOwnerPersonId, "Out of contractual scope and unsupported", rejectTime);

        // Assert
        request.Status.Should().Be(RequestStatus.Rejected);
        request.IsTerminal.Should().BeTrue();
        request.IsActive.Should().BeFalse();
        request.ClosedAt.Should().Be(rejectTime);
        request.Resolution.Should().NotBeNull();
        request.Resolution!.Outcome.Should().Be(RequestResolution.OutcomeRejected);
        request.Resolution.Summary.Should().Be("Out of contractual scope and unsupported");
        request.Resolution.ResolvedByPersonId.Should().Be(_testOwnerPersonId);

        request.DomainEvents.Should().ContainSingle(e => e is RequestRejected)
            .Which.Should().BeOfType<RequestRejected>()
            .Which.Should().Match<RequestRejected>(e =>
                e.RequestId == request.Id &&
                e.RejectedByPersonId == _testOwnerPersonId &&
                e.Reason == "Out of contractual scope and unsupported");
    }

    [Fact]
    public void Reject_FromCapturedState_DirectRejectionSucceeds()
    {
        // Arrange
        var request = CreateCapturedRequest();
        var rejectTime = _baseTime.AddMinutes(10);

        // Act
        request.Reject(_testAssignedByPersonId, "Duplicate of REQ-0012", rejectTime);

        // Assert
        request.Status.Should().Be(RequestStatus.Rejected);
        request.IsTerminal.Should().BeTrue();
        request.Resolution!.Outcome.Should().Be(RequestResolution.OutcomeRejected);
        request.Resolution.Summary.Should().Be("Duplicate of REQ-0012");
    }

    [Fact]
    public void Escalate_FromInProgressState_TransitionsToEscalatedAndEmitsRequestEscalated()
    {
        // Arrange
        var request = CreateCapturedRequest();
        request.Evaluate(_testOwnerPersonId, _baseTime.AddMinutes(5));
        request.Accept(_testOwnerPersonId, _baseTime.AddMinutes(10));
        request.StartProgress(_testOwnerPersonId, _baseTime.AddHours(1));
        request.ClearDomainEvents();

        // Act
        var escalateTime = _baseTime.AddHours(2);
        request.Escalate(_testOwnerPersonId, "Requires database schema migration approval from DBA", "DBA assistance needed", escalateTime);

        // Assert
        request.Status.Should().Be(RequestStatus.Escalated);
        request.IsActive.Should().BeTrue();
        request.EscalationReason.Should().Be("Requires database schema migration approval from DBA");
        request.EscalatedByPersonId.Should().Be(_testOwnerPersonId);
        request.EscalatedAt.Should().Be(escalateTime);

        request.DomainEvents.Should().ContainSingle(e => e is RequestEscalated)
            .Which.Should().BeOfType<RequestEscalated>()
            .Which.Should().Match<RequestEscalated>(e =>
                e.RequestId == request.Id &&
                e.EscalatedByPersonId == _testOwnerPersonId &&
                e.Reason == "Requires database schema migration approval from DBA" &&
                e.RequiredAssistance == "DBA assistance needed");
    }

    [Fact]
    public void Escalate_FromAcceptedState_TransitionsToEscalated()
    {
        // Arrange
        var request = CreateCapturedRequest();
        request.Evaluate(_testOwnerPersonId, _baseTime.AddMinutes(5));
        request.Accept(_testOwnerPersonId, _baseTime.AddMinutes(10));

        // Act
        request.Escalate(_testOwnerPersonId, "Immediate architecture review required", null, _baseTime.AddMinutes(15));

        // Assert
        request.Status.Should().Be(RequestStatus.Escalated);
    }

    [Fact]
    public void ResolveEscalation_FromEscalatedState_ReturnsStatusToInProgress()
    {
        // Arrange
        var request = CreateCapturedRequest();
        request.Evaluate(_testOwnerPersonId, _baseTime.AddMinutes(5));
        request.Accept(_testOwnerPersonId, _baseTime.AddMinutes(10));
        request.StartProgress(_testOwnerPersonId, _baseTime.AddHours(1));
        request.Escalate(_testOwnerPersonId, "Blocked on DBA", null, _baseTime.AddHours(2));

        // Act
        var resolveTime = _baseTime.AddHours(3);
        request.ResolveEscalation(_testOwnerPersonId, resolveTime);

        // Assert
        request.Status.Should().Be(RequestStatus.InProgress);
        request.UpdatedAt.Should().Be(resolveTime);
    }

    [Fact]
    public void Complete_FromEscalatedState_TransitionsToCompleted()
    {
        // Arrange
        var request = CreateCapturedRequest();
        request.Evaluate(_testOwnerPersonId, _baseTime.AddMinutes(5));
        request.Accept(_testOwnerPersonId, _baseTime.AddMinutes(10));
        request.StartProgress(_testOwnerPersonId, _baseTime.AddHours(1));
        request.Escalate(_testOwnerPersonId, "Executive intervention", null, _baseTime.AddHours(2));

        // Act
        var completeTime = _baseTime.AddHours(4);
        request.Complete(_testAssignedByPersonId, "Resolved by management directive", completeTime);

        // Assert
        request.Status.Should().Be(RequestStatus.Completed);
        request.IsTerminal.Should().BeTrue();
        request.Resolution!.Outcome.Should().Be(RequestResolution.OutcomeResolved);
    }

    [Fact]
    public void RequestManagementDecision_OnActiveRequest_SetsConditionAndEmitsEventWithoutAlteringLifecycleState()
    {
        // Arrange
        var request = CreateCapturedRequest();
        request.Evaluate(_testOwnerPersonId, _baseTime.AddMinutes(5));
        request.Accept(_testOwnerPersonId, _baseTime.AddMinutes(10));
        request.StartProgress(_testOwnerPersonId, _baseTime.AddHours(1));
        request.ClearDomainEvents();

        // Act
        var decisionTime = _baseTime.AddHours(2);
        request.RequestManagementDecision(
            _testOwnerPersonId,
            "Should we absorb the additional custom API integration cost?",
            "Option A: Absorb cost; Option B: Bill customer",
            "Impacts delivery timeline by 2 days",
            decisionTime);

        // Assert
        request.Status.Should().Be(RequestStatus.InProgress); // Lifecycle state preserved
        request.IsAwaitingManagementDecision.Should().BeTrue();
        request.ManagementDecisionQuestion.Should().Be("Should we absorb the additional custom API integration cost?");
        request.ManagementDecisionOptions.Should().Be("Option A: Absorb cost; Option B: Bill customer");
        request.ManagementDecisionImpact.Should().Be("Impacts delivery timeline by 2 days");
        request.ManagementDecisionRequestedAt.Should().Be(decisionTime);

        request.DomainEvents.Should().ContainSingle(e => e is ManagementDecisionRequested)
            .Which.Should().BeOfType<ManagementDecisionRequested>()
            .Which.Should().Match<ManagementDecisionRequested>(e =>
                e.RequestId == request.Id &&
                e.RequestedByPersonId == _testOwnerPersonId &&
                e.Question == "Should we absorb the additional custom API integration cost?" &&
                e.Options == "Option A: Absorb cost; Option B: Bill customer" &&
                e.Impact == "Impacts delivery timeline by 2 days");
    }

    [Fact]
    public void RequestRework_DuringCompletionReview_KeepsStatusInProgressWithUpdatedTimestamp()
    {
        // Arrange
        var request = CreateCapturedRequest();
        request.Evaluate(_testOwnerPersonId, _baseTime.AddMinutes(5));
        request.Accept(_testOwnerPersonId, _baseTime.AddMinutes(10));
        request.StartProgress(_testOwnerPersonId, _baseTime.AddHours(1));

        // Act
        var reworkTime = _baseTime.AddHours(3);
        request.RequestRework(_testAssignedByPersonId, "Report layout has alignment defects on page 2", reworkTime);

        // Assert
        request.Status.Should().Be(RequestStatus.InProgress);
        request.UpdatedAt.Should().Be(reworkTime);
    }

    #region Invalid Transitions & Negative Tests

    [Theory]
    [InlineData(RequestStatus.Accepted)]
    [InlineData(RequestStatus.InProgress)]
    [InlineData(RequestStatus.Escalated)]
    [InlineData(RequestStatus.Completed)]
    public void InvalidTransitions_FromCapturedState_ThrowsInvalidRequestStateTransitionException(string invalidTarget)
    {
        // Arrange
        var request = CreateCapturedRequest();

        // Act & Assert
        var act = () =>
        {
            switch (invalidTarget)
            {
                case RequestStatus.Accepted:
                    request.Accept(_testOwnerPersonId, _baseTime);
                    break;
                case RequestStatus.InProgress:
                    request.StartProgress(_testOwnerPersonId, _baseTime);
                    break;
                case RequestStatus.Escalated:
                    request.Escalate(_testOwnerPersonId, "Reason", null, _baseTime);
                    break;
                case RequestStatus.Completed:
                    request.Complete(_testOwnerPersonId, "Summary", _baseTime);
                    break;
            }
        };

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Which.CurrentStatus.Should().Be(RequestStatus.Captured);
    }

    [Theory]
    [InlineData(RequestStatus.InProgress)]
    [InlineData(RequestStatus.Escalated)]
    [InlineData(RequestStatus.Completed)]
    public void InvalidTransitions_FromEvaluatingState_ThrowsInvalidRequestStateTransitionException(string invalidTarget)
    {
        // Arrange
        var request = CreateCapturedRequest();
        request.Evaluate(_testOwnerPersonId, _baseTime.AddMinutes(5));

        // Act & Assert
        var act = () =>
        {
            switch (invalidTarget)
            {
                case RequestStatus.InProgress:
                    request.StartProgress(_testOwnerPersonId, _baseTime);
                    break;
                case RequestStatus.Escalated:
                    request.Escalate(_testOwnerPersonId, "Reason", null, _baseTime);
                    break;
                case RequestStatus.Completed:
                    request.Complete(_testOwnerPersonId, "Summary", _baseTime);
                    break;
            }
        };

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Which.CurrentStatus.Should().Be(RequestStatus.Evaluating);
    }

    [Fact]
    public void InvalidTransition_FromAcceptedToCompleted_WithoutInProgress_ThrowsException()
    {
        // Arrange
        var request = CreateCapturedRequest();
        request.Evaluate(_testOwnerPersonId, _baseTime.AddMinutes(5));
        request.Accept(_testOwnerPersonId, _baseTime.AddMinutes(10));

        // Act & Assert
        var act = () => request.Complete(_testOwnerPersonId, "Summary", _baseTime.AddHours(1));
        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Which.CurrentStatus.Should().Be(RequestStatus.Accepted);
    }

    [Fact]
    public void InvalidTransition_FromCompletedState_ThrowsException()
    {
        // Arrange
        var request = CreateCompletedRequest();

        // Act & Assert
        var actEvaluate = () => request.Evaluate(_testOwnerPersonId, _baseTime);
        var actAccept = () => request.Accept(_testOwnerPersonId, _baseTime);
        var actStart = () => request.StartProgress(_testOwnerPersonId, _baseTime);
        var actEscalate = () => request.Escalate(_testOwnerPersonId, "Reason", null, _baseTime);
        var actReject = () => request.Reject(_testOwnerPersonId, "Reason", _baseTime);

        actEvaluate.Should().Throw<InvalidRequestStateTransitionException>();
        actAccept.Should().Throw<InvalidRequestStateTransitionException>();
        actStart.Should().Throw<InvalidRequestStateTransitionException>();
        actEscalate.Should().Throw<InvalidRequestStateTransitionException>();
        actReject.Should().Throw<InvalidRequestStateTransitionException>();
    }

    [Fact]
    public void InvalidTransition_FromRejectedState_ThrowsException()
    {
        // Arrange
        var request = CreateCapturedRequest();
        request.Reject(_testOwnerPersonId, "Duplicate", _baseTime.AddMinutes(10));

        // Act & Assert
        var actEvaluate = () => request.Evaluate(_testOwnerPersonId, _baseTime);
        var actAccept = () => request.Accept(_testOwnerPersonId, _baseTime);
        var actStart = () => request.StartProgress(_testOwnerPersonId, _baseTime);
        var actEscalate = () => request.Escalate(_testOwnerPersonId, "Reason", null, _baseTime);
        var actComplete = () => request.Complete(_testOwnerPersonId, "Summary", _baseTime);

        actEvaluate.Should().Throw<InvalidRequestStateTransitionException>();
        actAccept.Should().Throw<InvalidRequestStateTransitionException>();
        actStart.Should().Throw<InvalidRequestStateTransitionException>();
        actEscalate.Should().Throw<InvalidRequestStateTransitionException>();
        actComplete.Should().Throw<InvalidRequestStateTransitionException>();
    }

    [Fact]
    public void AssignOwner_WhenRequestIsClosed_ThrowsRequestDomainException()
    {
        // Arrange
        var request = CreateCompletedRequest();

        // Act & Assert
        var act = () => request.AssignOwner(Guid.NewGuid(), _testAssignedByPersonId, _baseTime);
        act.Should().Throw<RequestDomainException>()
            .WithMessage("*closed*");
    }

    [Fact]
    public void RequestManagementDecision_WhenRequestIsClosed_ThrowsRequestDomainException()
    {
        // Arrange
        var request = CreateCompletedRequest();

        // Act & Assert
        var act = () => request.RequestManagementDecision(_testOwnerPersonId, "Question?", null, null, _baseTime);
        act.Should().Throw<RequestDomainException>()
            .WithMessage("*closed*");
    }

    [Fact]
    public void RequestRework_WhenRequestIsClosed_ThrowsRequestDomainException()
    {
        // Arrange
        var request = CreateCompletedRequest();

        // Act & Assert
        var act = () => request.RequestRework(_testAssignedByPersonId, "Rework feedback", _baseTime);
        act.Should().Throw<RequestDomainException>()
            .WithMessage("*closed*");
    }

    [Fact]
    public void UpdateDetails_WhenRequestIsClosed_ThrowsRequestDomainException()
    {
        // Arrange
        var request = CreateCompletedRequest();

        // Act & Assert
        var act = () => request.UpdateDetails("New Title", "New Desc", Request.TypeBug, _baseTime);
        act.Should().Throw<RequestDomainException>();
    }

    #endregion

    #region StateMachine Tests

    [Theory]
    [InlineData(RequestStatus.Captured, RequestStatus.Evaluating, true)]
    [InlineData(RequestStatus.Captured, RequestStatus.Rejected, true)]
    [InlineData(RequestStatus.Captured, RequestStatus.Accepted, false)]
    [InlineData(RequestStatus.Captured, RequestStatus.InProgress, false)]
    [InlineData(RequestStatus.Captured, RequestStatus.Escalated, false)]
    [InlineData(RequestStatus.Captured, RequestStatus.Completed, false)]
    [InlineData(RequestStatus.Evaluating, RequestStatus.Accepted, true)]
    [InlineData(RequestStatus.Evaluating, RequestStatus.Rejected, true)]
    [InlineData(RequestStatus.Evaluating, RequestStatus.InProgress, false)]
    [InlineData(RequestStatus.Evaluating, RequestStatus.Completed, false)]
    [InlineData(RequestStatus.Accepted, RequestStatus.InProgress, true)]
    [InlineData(RequestStatus.Accepted, RequestStatus.Escalated, true)]
    [InlineData(RequestStatus.Accepted, RequestStatus.Completed, false)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Escalated, true)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Completed, true)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Accepted, false)]
    [InlineData(RequestStatus.Escalated, RequestStatus.InProgress, true)]
    [InlineData(RequestStatus.Escalated, RequestStatus.Completed, true)]
    [InlineData(RequestStatus.Escalated, RequestStatus.Rejected, true)]
    [InlineData(RequestStatus.Completed, RequestStatus.InProgress, false)]
    [InlineData(RequestStatus.Rejected, RequestStatus.InProgress, false)]
    public void RequestStateMachine_CanTransition_EvaluatesCorrectly(string from, string to, bool expected)
    {
        RequestStateMachine.CanTransition(from, to).Should().Be(expected);
    }

    [Fact]
    public void RequestStateMachine_EnsureValidTransition_ThrowsDetailedExceptionOnInvalidTransition()
    {
        var requestId = Guid.NewGuid();
        var act = () => RequestStateMachine.EnsureValidTransition(requestId, RequestStatus.Captured, RequestStatus.Completed);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Which.Should().Match<InvalidRequestStateTransitionException>(e =>
                e.RequestId == requestId &&
                e.CurrentStatus == RequestStatus.Captured &&
                e.TargetStatus == RequestStatus.Completed);
    }

    [Fact]
    public void RequestStateMachine_GetAllowedTransitions_ReturnsExpectedTargets()
    {
        var capturedAllowed = RequestStateMachine.GetAllowedTransitions(RequestStatus.Captured);
        capturedAllowed.Should().BeEquivalentTo(new[] { RequestStatus.Evaluating, RequestStatus.Rejected });

        var inProgressAllowed = RequestStateMachine.GetAllowedTransitions(RequestStatus.InProgress);
        inProgressAllowed.Should().BeEquivalentTo(new[] { RequestStatus.Escalated, RequestStatus.Completed });

        var completedAllowed = RequestStateMachine.GetAllowedTransitions(RequestStatus.Completed);
        completedAllowed.Should().BeEmpty();
    }

    #endregion

    #region Entity Invariants & Value Tests

    [Fact]
    public void RequestResolution_CreateResolved_ValidatesInputs()
    {
        var resId = Guid.NewGuid();
        var reqId = Guid.NewGuid();
        var resolvedBy = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var resolution = RequestResolution.CreateResolved(resId, reqId, "Fixed root cause", resolvedBy, now);

        resolution.Id.Should().Be(resId);
        resolution.RequestId.Should().Be(reqId);
        resolution.Outcome.Should().Be(RequestResolution.OutcomeResolved);
        resolution.Summary.Should().Be("Fixed root cause");
        resolution.ResolvedByPersonId.Should().Be(resolvedBy);
        resolution.ResolvedAt.Should().Be(now);

        // Invariant failure
        var actEmptyReq = () => RequestResolution.CreateResolved(resId, Guid.Empty, "Summary", resolvedBy, now);
        actEmptyReq.Should().Throw<ArgumentException>();

        var actEmptySummary = () => RequestResolution.CreateResolved(resId, reqId, "   ", resolvedBy, now);
        actEmptySummary.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequestAssignment_ValidatesInputsAndDeactivates()
    {
        var assignId = Guid.NewGuid();
        var reqId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var assignerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var assignment = new RequestAssignment(assignId, reqId, ownerId, assignerId, now, "Initial");

        assignment.Id.Should().Be(assignId);
        assignment.RequestId.Should().Be(reqId);
        assignment.OwnerPersonId.Should().Be(ownerId);
        assignment.AssignedByPersonId.Should().Be(assignerId);
        assignment.IsActive.Should().BeTrue();
        assignment.Note.Should().Be("Initial");

        var deactTime = now.AddMinutes(5);
        assignment.Deactivate(deactTime);
        assignment.IsActive.Should().BeFalse();
        assignment.UpdatedAt.Should().Be(deactTime);

        // Invariant failure
        var actEmptyOwner = () => new RequestAssignment(assignId, reqId, Guid.Empty, assignerId, now);
        actEmptyOwner.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Request_AttributeUpdates_WorkOnActiveRequest()
    {
        var request = CreateCapturedRequest();
        var updateTime = _baseTime.AddMinutes(10);

        request.UpdateDetails("Updated Title", "Updated Description", Request.TypeChangeRequest, updateTime);
        request.Title.Should().Be("Updated Title");
        request.Description.Should().Be("Updated Description");
        request.Type.Should().Be(Request.TypeChangeRequest);

        request.ChangePriority(Request.PriorityCritical, updateTime);
        request.Priority.Should().Be(Request.PriorityCritical);

        var wpId = Guid.NewGuid();
        request.AssignWorkPackage(wpId, updateTime);
        request.WorkPackageId.Should().Be(wpId);
    }

    #endregion

    private Request CreateCapturedRequest()
    {
        return Request.Record(
            _testRequestId,
            "Billing Integration Issue",
            "Transactions failing intermittently during peak hours",
            Request.TypeBug,
            Request.PriorityHigh,
            _testRequesterPersonId,
            null,
            "Finance Contact",
            customerId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            workPackageId: null,
            recordedAt: _baseTime);
    }

    private Request CreateCompletedRequest()
    {
        var request = CreateCapturedRequest();
        request.Evaluate(_testOwnerPersonId, _baseTime.AddMinutes(5));
        request.Accept(_testOwnerPersonId, _baseTime.AddMinutes(10));
        request.StartProgress(_testOwnerPersonId, _baseTime.AddHours(1));
        request.Complete(_testAssignedByPersonId, "Completed work", _baseTime.AddHours(3));
        return request;
    }
}
