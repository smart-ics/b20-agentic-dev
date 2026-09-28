using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Domain.Events;
using Cakra.Modules.Request.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Request;

public sealed class RequestStateMachineTests
{
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _secondOwnerId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _workPackageId = Guid.NewGuid();

    private Cakra.Modules.Request.Domain.Request CreateCapturedRequest()
    {
        return Cakra.Modules.Request.Domain.Request.Record(
            id: Guid.NewGuid(),
            title: "BPJS billing validation discrepancy",
            description: "Billing claims intermittently fail BPJS validation checks.",
            requestType: "Bug",
            actorPersonId: _actorId,
            customerId: _customerId,
            productId: _productId,
            workPackageId: _workPackageId,
            priority: "HIGH");
    }

    private Cakra.Modules.Request.Domain.Request CreateEvaluatingRequest()
    {
        var request = CreateCapturedRequest();
        request.AssignOwner(_ownerId, _actorId, "Assigned to primary programmer");
        request.ClearDomainEvents();
        return request;
    }

    private Cakra.Modules.Request.Domain.Request CreateAcceptedRequest()
    {
        var request = CreateEvaluatingRequest();
        request.Accept(_ownerId, "Responsibility accepted");
        request.ClearDomainEvents();
        return request;
    }

    private Cakra.Modules.Request.Domain.Request CreateInProgressRequest()
    {
        var request = CreateAcceptedRequest();
        request.StartProgress(_ownerId, "Starting investigation and fix");
        request.ClearDomainEvents();
        return request;
    }

    private Cakra.Modules.Request.Domain.Request CreateEscalatedRequestFromEvaluating()
    {
        var request = CreateEvaluatingRequest();
        request.Escalate("Requires module architect review", _ownerId);
        request.ClearDomainEvents();
        return request;
    }

    private Cakra.Modules.Request.Domain.Request CreateEscalatedRequestFromInProgress()
    {
        var request = CreateInProgressRequest();
        request.Escalate("Resource constraint and architecture blocker", _ownerId);
        request.ClearDomainEvents();
        return request;
    }

    #region 1. Record & Initialization

    [Fact]
    public void Record_WithValidParameters_InitializesCapturedRequestAndEmitsEvent()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var request = Cakra.Modules.Request.Domain.Request.Record(
            id: id,
            title: "Fix pharmacy report",
            description: "Pharmacy inventory reports incorrect totals.",
            requestType: "Bug",
            actorPersonId: _actorId,
            customerId: _customerId,
            productId: _productId,
            workPackageId: _workPackageId,
            priority: "URGENT",
            utcNow: now);

        request.Id.Should().Be(id);
        request.Title.Should().Be("Fix pharmacy report");
        request.Description.Should().Be("Pharmacy inventory reports incorrect totals.");
        request.RequestType.Should().Be("Bug");
        request.Status.Should().Be(RequestStatus.Captured);
        request.Priority.Should().Be("URGENT");
        request.OwnerPersonId.Should().BeNull();
        request.CustomerId.Should().Be(_customerId);
        request.ProductId.Should().Be(_productId);
        request.WorkPackageId.Should().Be(_workPackageId);
        request.CreatedAt.Should().Be(now);
        request.UpdatedAt.Should().BeNull();
        request.Resolution.Should().BeNull();

        request.Assignments.Should().ContainSingle();
        var initialAudit = request.Assignments.First();
        initialAudit.RequestId.Should().Be(id);
        initialAudit.PreviousStatus.Should().BeNull();
        initialAudit.NewStatus.Should().Be(RequestStatus.Captured);
        initialAudit.ActorPersonId.Should().Be(_actorId);
        initialAudit.AssignedOwnerPersonId.Should().BeNull();

        request.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RequestRecorded>()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = id,
                Title = "Fix pharmacy report",
                Description = "Pharmacy inventory reports incorrect totals.",
                RequestType = "Bug",
                ActorPersonId = _actorId,
                CustomerId = (Guid?)_customerId,
                ProductId = (Guid?)_productId,
                WorkPackageId = (Guid?)_workPackageId,
                Priority = "URGENT",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_WithEmptyTitle_ThrowsValidationException(string title)
    {
        var act = () => Cakra.Modules.Request.Domain.Request.Record(
            Guid.NewGuid(), title, "Description", "Bug", _actorId);

        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("title");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_WithEmptyDescription_ThrowsValidationException(string description)
    {
        var act = () => Cakra.Modules.Request.Domain.Request.Record(
            Guid.NewGuid(), "Title", description, "Bug", _actorId);

        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("description");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_WithEmptyRequestType_ThrowsValidationException(string type)
    {
        var act = () => Cakra.Modules.Request.Domain.Request.Record(
            Guid.NewGuid(), "Title", "Description", type, _actorId);

        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("requestType");
    }

    [Fact]
    public void Record_WithEmptyId_ThrowsValidationException()
    {
        var act = () => Cakra.Modules.Request.Domain.Request.Record(
            Guid.Empty, "Title", "Description", "Bug", _actorId);

        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("id");
    }

    [Fact]
    public void Record_WithEmptyActorId_ThrowsValidationException()
    {
        var act = () => Cakra.Modules.Request.Domain.Request.Record(
            Guid.NewGuid(), "Title", "Description", "Bug", Guid.Empty);

        act.Should().Throw<RequestDomainValidationException>()
            .WithParameterName("actorPersonId");
    }

    #endregion

    #region 2. CAPTURED -> EVALUATING & Invalid Transitions from CAPTURED

    [Fact]
    public void AssignOwner_FromCaptured_TransitionsToEvaluatingAndEmitsEvent()
    {
        var request = CreateCapturedRequest();
        var now = DateTime.UtcNow;

        request.AssignOwner(_ownerId, _actorId, "Assigning to Alice", now);

        request.Status.Should().Be(RequestStatus.Evaluating);
        request.OwnerPersonId.Should().Be(_ownerId);
        request.UpdatedAt.Should().Be(now);

        request.Assignments.Should().HaveCount(2);
        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.Captured);
        audit.NewStatus.Should().Be(RequestStatus.Evaluating);
        audit.PreviousOwnerPersonId.Should().BeNull();
        audit.AssignedOwnerPersonId.Should().Be(_ownerId);
        audit.ActorPersonId.Should().Be(_actorId);

        request.DomainEvents.OfType<RequestAssigned>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                OwnerPersonId = _ownerId,
                PreviousOwnerPersonId = (Guid?)null,
                ActorPersonId = _actorId,
                PreviousStatus = RequestStatus.Captured,
                NewStatus = RequestStatus.Evaluating,
                Notes = "Assigning to Alice",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void Accept_FromCaptured_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateCapturedRequest();
        var act = () => request.Accept(_actorId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Captured && e.TargetStatus == RequestStatus.Accepted);
    }

    [Fact]
    public void Reject_FromCaptured_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateCapturedRequest();
        var act = () => request.Reject("Invalid request", _actorId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Captured && e.TargetStatus == RequestStatus.Rejected);
    }

    [Fact]
    public void Escalate_FromCaptured_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateCapturedRequest();
        var act = () => request.Escalate("Premature escalation", _actorId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Captured && e.TargetStatus == RequestStatus.Escalated);
    }

    [Fact]
    public void StartProgress_FromCaptured_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateCapturedRequest();
        var act = () => request.StartProgress(_actorId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Captured && e.TargetStatus == RequestStatus.InProgress);
    }

    [Fact]
    public void Complete_FromCaptured_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateCapturedRequest();
        var act = () => request.Complete("Done prematurely", _actorId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Captured && e.TargetStatus == RequestStatus.Completed);
    }

    [Fact]
    public void Evaluate_FromCaptured_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateCapturedRequest();
        var act = () => request.Evaluate("Notes", _actorId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Captured);
    }

    [Fact]
    public void ApplyManagementDecision_FromCaptured_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateCapturedRequest();
        var act = () => request.ApplyManagementDecision(RequestStatus.Evaluating, "Notes", _actorId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Captured);
    }

    #endregion

    #region 3. EVALUATING Transitions & Rejections

    [Fact]
    public void Evaluate_InEvaluatingState_RecordsNotesAndEmitsEventWithoutChangingStatus()
    {
        var request = CreateEvaluatingRequest();
        var now = DateTime.UtcNow;

        request.Evaluate("Feasible to fix in 4 hours; root cause identified in billing worker.", _ownerId, now);

        request.Status.Should().Be(RequestStatus.Evaluating);
        request.EvaluationNotes.Should().Be("Feasible to fix in 4 hours; root cause identified in billing worker.");
        request.UpdatedAt.Should().Be(now);

        request.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RequestEvaluated>()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                EvaluatedByPersonId = _ownerId,
                EvaluationNotes = "Feasible to fix in 4 hours; root cause identified in billing worker.",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void Accept_FromEvaluating_TransitionsToAcceptedAndEmitsEvent()
    {
        var request = CreateEvaluatingRequest();
        var now = DateTime.UtcNow;

        request.Accept(_ownerId, "Accepting ownership for resolution", now);

        request.Status.Should().Be(RequestStatus.Accepted);
        request.UpdatedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.Evaluating);
        audit.NewStatus.Should().Be(RequestStatus.Accepted);
        audit.ActorPersonId.Should().Be(_ownerId);

        request.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RequestAccepted>()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                OwnerPersonId = _ownerId,
                Notes = "Accepting ownership for resolution",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void Reject_FromEvaluating_TransitionsToRejectedCreatesResolutionAndEmitsEvent()
    {
        var request = CreateEvaluatingRequest();
        var now = DateTime.UtcNow;

        request.Reject("Feature request is out of product scope and violates BPJS policy.", _ownerId, now);

        request.Status.Should().Be(RequestStatus.Rejected);
        request.UpdatedAt.Should().Be(now);
        request.Resolution.Should().NotBeNull();
        request.Resolution!.Outcome.Should().Be(ResolutionOutcomeNames.Rejected);
        request.Resolution.Description.Should().Be("Feature request is out of product scope and violates BPJS policy.");
        request.Resolution.ResolvedBy.Should().Be(_ownerId);
        request.Resolution.ResolvedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.Evaluating);
        audit.NewStatus.Should().Be(RequestStatus.Rejected);

        request.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RequestRejected>()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                RejectedByPersonId = _ownerId,
                RejectionReason = "Feature request is out of product scope and violates BPJS policy.",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void Escalate_FromEvaluating_TransitionsToEscalatedAndEmitsEvent()
    {
        var request = CreateEvaluatingRequest();
        var now = DateTime.UtcNow;

        request.Escalate("Requires DBA permissions to alter partitioned table.", _ownerId, now);

        request.Status.Should().Be(RequestStatus.Escalated);
        request.EscalationReason.Should().Be("Requires DBA permissions to alter partitioned table.");
        request.UpdatedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.Evaluating);
        audit.NewStatus.Should().Be(RequestStatus.Escalated);

        request.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RequestEscalated>()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                EscalatedByPersonId = _ownerId,
                EscalationReason = "Requires DBA permissions to alter partitioned table.",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void StartProgress_FromEvaluating_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateEvaluatingRequest();
        var act = () => request.StartProgress(_ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Evaluating && e.TargetStatus == RequestStatus.InProgress);
    }

    [Fact]
    public void Complete_FromEvaluating_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateEvaluatingRequest();
        var act = () => request.Complete("Done", _ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Evaluating && e.TargetStatus == RequestStatus.Completed);
    }

    [Fact]
    public void ReassignOwner_InEvaluating_UpdatesOwnerAndEmitsEventWithoutChangingStatus()
    {
        var request = CreateEvaluatingRequest();
        var now = DateTime.UtcNow;

        request.ReassignOwner(_secondOwnerId, _actorId, notes: "Reassigning to Bob", utcNow: now);

        request.Status.Should().Be(RequestStatus.Evaluating);
        request.OwnerPersonId.Should().Be(_secondOwnerId);
        request.UpdatedAt.Should().Be(now);

        request.DomainEvents.OfType<RequestAssigned>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                OwnerPersonId = _secondOwnerId,
                PreviousOwnerPersonId = (Guid?)_ownerId,
                ActorPersonId = _actorId,
                PreviousStatus = RequestStatus.Evaluating,
                NewStatus = RequestStatus.Evaluating,
                Notes = "Reassigning to Bob",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void ReassignOwner_WithSameOwner_ThrowsInvalidOperationException()
    {
        var request = CreateEvaluatingRequest();
        var act = () => request.ReassignOwner(_ownerId, _actorId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*identical*");
    }

    #endregion

    #region 4. ACCEPTED Transitions & Rejections

    [Fact]
    public void StartProgress_FromAccepted_TransitionsToInProgress()
    {
        var request = CreateAcceptedRequest();
        var now = DateTime.UtcNow;

        request.StartProgress(_ownerId, "Beginning development", now);

        request.Status.Should().Be(RequestStatus.InProgress);
        request.UpdatedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.Accepted);
        audit.NewStatus.Should().Be(RequestStatus.InProgress);
        audit.ActorPersonId.Should().Be(_ownerId);
    }

    [Fact]
    public void Accept_FromAccepted_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateAcceptedRequest();
        var act = () => request.Accept(_ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Accepted && e.TargetStatus == RequestStatus.Accepted);
    }

    [Fact]
    public void Reject_FromAccepted_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateAcceptedRequest();
        var act = () => request.Reject("Reject after accepted", _ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Accepted && e.TargetStatus == RequestStatus.Rejected);
    }

    [Fact]
    public void Escalate_FromAccepted_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateAcceptedRequest();
        var act = () => request.Escalate("Escalate directly from accepted", _ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Accepted && e.TargetStatus == RequestStatus.Escalated);
    }

    [Fact]
    public void Complete_FromAccepted_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateAcceptedRequest();
        var act = () => request.Complete("Complete without starting work", _ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Accepted && e.TargetStatus == RequestStatus.Completed);
    }

    [Fact]
    public void Evaluate_FromAccepted_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateAcceptedRequest();
        var act = () => request.Evaluate("Evaluating after accept", _ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Accepted);
    }

    #endregion

    #region 5. IN_PROGRESS Transitions & Rejections

    [Fact]
    public void Escalate_FromInProgress_TransitionsToEscalatedAndEmitsEvent()
    {
        var request = CreateInProgressRequest();
        var now = DateTime.UtcNow;

        request.Escalate("Third party API is returning 503 Service Unavailable.", _ownerId, now);

        request.Status.Should().Be(RequestStatus.Escalated);
        request.EscalationReason.Should().Be("Third party API is returning 503 Service Unavailable.");
        request.UpdatedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.InProgress);
        audit.NewStatus.Should().Be(RequestStatus.Escalated);

        request.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RequestEscalated>()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                EscalatedByPersonId = _ownerId,
                EscalationReason = "Third party API is returning 503 Service Unavailable.",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void Complete_FromInProgress_TransitionsToCompletedCreatesResolutionAndEmitsEvent()
    {
        var request = CreateInProgressRequest();
        var now = DateTime.UtcNow;

        request.Complete("Applied BPJS header sanitization and verified with integration test suite.", _ownerId, now);

        request.Status.Should().Be(RequestStatus.Completed);
        request.UpdatedAt.Should().Be(now);
        request.Resolution.Should().NotBeNull();
        request.Resolution!.Outcome.Should().Be(ResolutionOutcomeNames.Completed);
        request.Resolution.Description.Should().Be("Applied BPJS header sanitization and verified with integration test suite.");
        request.Resolution.ResolvedBy.Should().Be(_ownerId);
        request.Resolution.ResolvedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.InProgress);
        audit.NewStatus.Should().Be(RequestStatus.Completed);

        request.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RequestCompleted>()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                CompletedByPersonId = _ownerId,
                ResolutionDescription = "Applied BPJS header sanitization and verified with integration test suite.",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void RequestManagementDecision_FromInProgress_RecordsDecisionAndEmitsEventWithoutChangingStatus()
    {
        var request = CreateInProgressRequest();
        var now = DateTime.UtcNow;

        request.RequestManagementDecision("Should we backport fix to v2.4 branch?", _ownerId, now);

        request.Status.Should().Be(RequestStatus.InProgress);
        request.ManagementDecisionNotes.Should().Be("Should we backport fix to v2.4 branch?");
        request.UpdatedAt.Should().Be(now);

        request.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ManagementDecisionRequested>()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                RequestedByPersonId = _ownerId,
                DecisionDetails = "Should we backport fix to v2.4 branch?",
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void Accept_FromInProgress_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateInProgressRequest();
        var act = () => request.Accept(_ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.InProgress && e.TargetStatus == RequestStatus.Accepted);
    }

    [Fact]
    public void Reject_FromInProgress_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateInProgressRequest();
        var act = () => request.Reject("Reject while in progress", _ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.InProgress && e.TargetStatus == RequestStatus.Rejected);
    }

    [Fact]
    public void StartProgress_FromInProgress_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateInProgressRequest();
        var act = () => request.StartProgress(_ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.InProgress && e.TargetStatus == RequestStatus.InProgress);
    }

    [Fact]
    public void Evaluate_FromInProgress_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateInProgressRequest();
        var act = () => request.Evaluate("Evaluating during work", _ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.InProgress);
    }

    [Fact]
    public void ApplyManagementDecision_FromInProgress_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateInProgressRequest();
        var act = () => request.ApplyManagementDecision(RequestStatus.Evaluating, "Notes", _ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.InProgress);
    }

    #endregion

    #region 6. ESCALATED Transitions & Rejections

    [Fact]
    public void ReassignOwner_FromEscalated_DefaultTransitionsToEvaluating()
    {
        var request = CreateEscalatedRequestFromInProgress();
        var now = DateTime.UtcNow;

        request.ReassignOwner(_secondOwnerId, _actorId, utcNow: now);

        request.Status.Should().Be(RequestStatus.Evaluating);
        request.OwnerPersonId.Should().Be(_secondOwnerId);
        request.UpdatedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.Escalated);
        audit.NewStatus.Should().Be(RequestStatus.Evaluating);
        audit.AssignedOwnerPersonId.Should().Be(_secondOwnerId);

        request.DomainEvents.OfType<RequestAssigned>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                OwnerPersonId = _secondOwnerId,
                PreviousOwnerPersonId = (Guid?)_ownerId,
                ActorPersonId = _actorId,
                PreviousStatus = RequestStatus.Escalated,
                NewStatus = RequestStatus.Evaluating,
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void ReassignOwner_FromEscalated_WithInProgressTarget_TransitionsToInProgress()
    {
        var request = CreateEscalatedRequestFromInProgress();
        var now = DateTime.UtcNow;

        request.ReassignOwner(_secondOwnerId, _actorId, targetStatusForEscalated: RequestStatus.InProgress, utcNow: now);

        request.Status.Should().Be(RequestStatus.InProgress);
        request.OwnerPersonId.Should().Be(_secondOwnerId);
        request.UpdatedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.Escalated);
        audit.NewStatus.Should().Be(RequestStatus.InProgress);

        request.DomainEvents.OfType<RequestAssigned>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                RequestId = request.Id,
                OwnerPersonId = _secondOwnerId,
                PreviousOwnerPersonId = (Guid?)_ownerId,
                ActorPersonId = _actorId,
                PreviousStatus = RequestStatus.Escalated,
                NewStatus = RequestStatus.InProgress,
                OccurredAtUtc = now
            }, options => options.ExcludingMissingMembers());
    }

    [Theory]
    [InlineData(RequestStatus.Captured)]
    [InlineData(RequestStatus.Accepted)]
    [InlineData(RequestStatus.Rejected)]
    [InlineData(RequestStatus.Completed)]
    [InlineData(RequestStatus.Escalated)]
    public void ReassignOwner_FromEscalated_WithInvalidTarget_ThrowsInvalidRequestStateTransitionException(RequestStatus invalidTarget)
    {
        var request = CreateEscalatedRequestFromInProgress();
        var act = () => request.ReassignOwner(_secondOwnerId, _actorId, targetStatusForEscalated: invalidTarget);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Escalated && e.TargetStatus == invalidTarget);
    }

    [Fact]
    public void AssignOwner_FromEscalated_TransitionsToEvaluating()
    {
        var request = CreateEscalatedRequestFromInProgress();
        var now = DateTime.UtcNow;

        request.AssignOwner(_secondOwnerId, _actorId, "Manager reassigned escalation", now);

        request.Status.Should().Be(RequestStatus.Evaluating);
        request.OwnerPersonId.Should().Be(_secondOwnerId);
        request.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void ApplyManagementDecision_FromEscalated_ToEvaluating_Succeeds()
    {
        var request = CreateEscalatedRequestFromInProgress();
        var now = DateTime.UtcNow;

        request.ApplyManagementDecision(RequestStatus.Evaluating, "Return to evaluating under revised scope", _actorId, now);

        request.Status.Should().Be(RequestStatus.Evaluating);
        request.ManagementDecisionNotes.Should().Be("Return to evaluating under revised scope");
        request.UpdatedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.Escalated);
        audit.NewStatus.Should().Be(RequestStatus.Evaluating);
    }

    [Fact]
    public void ApplyManagementDecision_FromEscalated_ToInProgress_Succeeds()
    {
        var request = CreateEscalatedRequestFromInProgress();
        var now = DateTime.UtcNow;

        request.ApplyManagementDecision(RequestStatus.InProgress, "Management approved overtime; resume development immediately", _actorId, now);

        request.Status.Should().Be(RequestStatus.InProgress);
        request.ManagementDecisionNotes.Should().Be("Management approved overtime; resume development immediately");
        request.UpdatedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.Escalated);
        audit.NewStatus.Should().Be(RequestStatus.InProgress);
    }

    [Theory]
    [InlineData(RequestStatus.Captured)]
    [InlineData(RequestStatus.Accepted)]
    [InlineData(RequestStatus.Rejected)]
    [InlineData(RequestStatus.Completed)]
    [InlineData(RequestStatus.Escalated)]
    public void ApplyManagementDecision_WithInvalidTargetStatus_ThrowsInvalidRequestStateTransitionException(RequestStatus invalidTarget)
    {
        var request = CreateEscalatedRequestFromInProgress();
        var act = () => request.ApplyManagementDecision(invalidTarget, "Notes", _actorId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Escalated && e.TargetStatus == invalidTarget);
    }

    [Fact]
    public void StartProgress_FromEscalated_ResumesInProgress()
    {
        var request = CreateEscalatedRequestFromInProgress();
        var now = DateTime.UtcNow;

        request.StartProgress(_ownerId, "Resumed after issue unblocked", now);

        request.Status.Should().Be(RequestStatus.InProgress);
        request.UpdatedAt.Should().Be(now);

        var audit = request.Assignments.Last();
        audit.PreviousStatus.Should().Be(RequestStatus.Escalated);
        audit.NewStatus.Should().Be(RequestStatus.InProgress);
    }

    [Fact]
    public void ResumeProgress_FromEscalated_ResumesInProgress()
    {
        var request = CreateEscalatedRequestFromInProgress();
        var now = DateTime.UtcNow;

        request.ResumeProgress(_ownerId, "Resuming progress", now);

        request.Status.Should().Be(RequestStatus.InProgress);
        request.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void Accept_FromEscalated_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateEscalatedRequestFromInProgress();
        var act = () => request.Accept(_ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Escalated && e.TargetStatus == RequestStatus.Accepted);
    }

    [Fact]
    public void Reject_FromEscalated_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateEscalatedRequestFromInProgress();
        var act = () => request.Reject("Reject while escalated", _ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Escalated && e.TargetStatus == RequestStatus.Rejected);
    }

    [Fact]
    public void Escalate_FromEscalated_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateEscalatedRequestFromInProgress();
        var act = () => request.Escalate("Re-escalating", _ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Escalated && e.TargetStatus == RequestStatus.Escalated);
    }

    [Fact]
    public void Complete_FromEscalated_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateEscalatedRequestFromInProgress();
        var act = () => request.Complete("Complete directly from escalated", _ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Escalated && e.TargetStatus == RequestStatus.Completed);
    }

    [Fact]
    public void Evaluate_FromEscalated_ThrowsInvalidRequestStateTransitionException()
    {
        var request = CreateEscalatedRequestFromInProgress();
        var act = () => request.Evaluate("Evaluating while escalated", _ownerId);

        act.Should().Throw<InvalidRequestStateTransitionException>()
            .Where(e => e.CurrentStatus == RequestStatus.Escalated);
    }

    #endregion

    #region 7. Terminal States (REJECTED and COMPLETED) Rejection

    [Fact]
    public void RejectedState_RejectsAllMutations()
    {
        var request = CreateEvaluatingRequest();
        request.Reject("Not valid", _ownerId);

        request.Status.Should().Be(RequestStatus.Rejected);

        // Attempting each mutation throws InvalidRequestStateTransitionException
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.AssignOwner(_secondOwnerId, _actorId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.ReassignOwner(_secondOwnerId, _actorId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.Accept(_ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.Reject("Double reject", _ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.Escalate("Escalate rejected", _ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.StartProgress(_ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.Complete("Complete rejected", _ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.Evaluate("Evaluate rejected", _ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.RequestManagementDecision("Decision rejected", _ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.ApplyManagementDecision(RequestStatus.Evaluating, "Notes", _actorId));
    }

    [Fact]
    public void CompletedState_RejectsAllMutations()
    {
        var request = CreateInProgressRequest();
        request.Complete("Finished successfully", _ownerId);

        request.Status.Should().Be(RequestStatus.Completed);

        // Attempting each mutation throws InvalidRequestStateTransitionException
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.AssignOwner(_secondOwnerId, _actorId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.ReassignOwner(_secondOwnerId, _actorId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.Accept(_ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.Reject("Reject completed", _ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.Escalate("Escalate completed", _ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.StartProgress(_ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.Complete("Double complete", _ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.Evaluate("Evaluate completed", _ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.RequestManagementDecision("Decision completed", _ownerId));
        Assert.Throws<InvalidRequestStateTransitionException>(() => request.ApplyManagementDecision(RequestStatus.InProgress, "Notes", _actorId));
    }

    #endregion

    #region 8. End-to-End Lifecycle Scenarios

    [Fact]
    public void HappyPath_FullLifecycle_Captured_Evaluating_Accepted_InProgress_Completed()
    {
        // 1. CAPTURED
        var request = CreateCapturedRequest();
        request.Status.Should().Be(RequestStatus.Captured);
        request.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RequestRecorded>();

        // 2. CAPTURED -> EVALUATING
        request.AssignOwner(_ownerId, _actorId, "Assigned to Alice");
        request.Status.Should().Be(RequestStatus.Evaluating);
        request.DomainEvents.OfType<RequestAssigned>().Should().ContainSingle();

        // 3. Evaluation in EVALUATING
        request.Evaluate("Evaluation complete; plan is approved", _ownerId);
        request.Status.Should().Be(RequestStatus.Evaluating);
        request.DomainEvents.OfType<RequestEvaluated>().Should().ContainSingle();

        // 4. EVALUATING -> ACCEPTED
        request.Accept(_ownerId, "Accepted for execution");
        request.Status.Should().Be(RequestStatus.Accepted);
        request.DomainEvents.OfType<RequestAccepted>().Should().ContainSingle();

        // 5. ACCEPTED -> IN_PROGRESS
        request.StartProgress(_ownerId, "Coding started");
        request.Status.Should().Be(RequestStatus.InProgress);

        // 6. IN_PROGRESS -> COMPLETED
        request.Complete("Resolved, validated, and deployed to staging", _ownerId);
        request.Status.Should().Be(RequestStatus.Completed);
        request.Resolution.Should().NotBeNull();
        request.Resolution!.Outcome.Should().Be(ResolutionOutcomeNames.Completed);
        request.DomainEvents.OfType<RequestCompleted>().Should().ContainSingle();

        // All 5 audit entries recorded
        request.Assignments.Should().HaveCount(5);
        request.Assignments.Select(a => a.NewStatus).Should().ContainInOrder(
            RequestStatus.Captured,
            RequestStatus.Evaluating,
            RequestStatus.Accepted,
            RequestStatus.InProgress,
            RequestStatus.Completed);
    }

    [Fact]
    public void HappyPath_TriageRejection_Captured_Evaluating_Rejected()
    {
        var request = CreateCapturedRequest();
        request.AssignOwner(_ownerId, _actorId);
        request.Evaluate("Evaluated: Not feasible within system architecture", _ownerId);
        request.Reject("Declined per architectural standards", _ownerId);

        request.Status.Should().Be(RequestStatus.Rejected);
        request.Resolution.Should().NotBeNull();
        request.Resolution!.Outcome.Should().Be(ResolutionOutcomeNames.Rejected);
        request.Resolution.Description.Should().Be("Declined per architectural standards");

        request.DomainEvents.OfType<RequestRejected>().Should().ContainSingle();
        request.Assignments.Select(a => a.NewStatus).Should().ContainInOrder(
            RequestStatus.Captured,
            RequestStatus.Evaluating,
            RequestStatus.Rejected);
    }

    [Fact]
    public void HappyPath_EscalationAndReassignment_ToEvaluating_ThenCompleted()
    {
        var request = CreateCapturedRequest();
        request.AssignOwner(_ownerId, _actorId);
        request.Escalate("Requires specialized database architect", _ownerId);
        request.Status.Should().Be(RequestStatus.Escalated);

        // Reassign to second owner -> returns to EVALUATING
        request.ReassignOwner(_secondOwnerId, _actorId, notes: "Reassigned to DB Architect");
        request.Status.Should().Be(RequestStatus.Evaluating);
        request.OwnerPersonId.Should().Be(_secondOwnerId);

        request.Accept(_secondOwnerId);
        request.StartProgress(_secondOwnerId);
        request.Complete("Database partition migration applied successfully", _secondOwnerId);

        request.Status.Should().Be(RequestStatus.Completed);
        request.Resolution!.Outcome.Should().Be(ResolutionOutcomeNames.Completed);
    }

    [Fact]
    public void HappyPath_EscalationDuringInProgress_ResolvedByManagementDecision_ThenCompleted()
    {
        var request = CreateCapturedRequest();
        request.AssignOwner(_ownerId, _actorId);
        request.Accept(_ownerId);
        request.StartProgress(_ownerId);

        // Escalate from IN_PROGRESS
        request.Escalate("Customer requested scope expansion exceeding sprint limit", _ownerId);
        request.Status.Should().Be(RequestStatus.Escalated);

        // Request management decision
        request.RequestManagementDecision("Approve 2 additional days or postpone to next release?", _ownerId);
        request.DomainEvents.OfType<ManagementDecisionRequested>().Should().ContainSingle();

        // Management applies decision -> returns to IN_PROGRESS
        request.ApplyManagementDecision(RequestStatus.InProgress, "2 additional days approved by COO", _actorId);
        request.Status.Should().Be(RequestStatus.InProgress);

        request.Complete("Completed under approved schedule extension", _ownerId);
        request.Status.Should().Be(RequestStatus.Completed);
    }

    #endregion

    #region 9. Entity & Base Class Invariant Verification

    [Fact]
    public void ClearDomainEvents_EmptiesDomainEventsCollection()
    {
        var request = CreateCapturedRequest();
        request.DomainEvents.Should().NotBeEmpty();

        request.ClearDomainEvents();
        request.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RequestResolution_Create_WithValidArgs_PopulatesProperties()
    {
        var reqId = Guid.NewGuid();
        var resolvedBy = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var resolution = RequestResolution.Create(reqId, "COMPLETED", "All tests passed", resolvedBy, now);

        resolution.Id.Should().NotBeEmpty();
        resolution.RequestId.Should().Be(reqId);
        resolution.Outcome.Should().Be("COMPLETED");
        resolution.Description.Should().Be("All tests passed");
        resolution.ResolvedBy.Should().Be(resolvedBy);
        resolution.ResolvedAt.Should().Be(now);
        resolution.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void RequestAssignment_Create_WithValidArgs_PopulatesProperties()
    {
        var reqId = Guid.NewGuid();
        var prevOwner = Guid.NewGuid();
        var newOwner = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var assignment = RequestAssignment.Create(
            reqId, prevOwner, newOwner, actor, RequestStatus.Evaluating, RequestStatus.Accepted, now, "Notes");

        assignment.Id.Should().NotBeEmpty();
        assignment.RequestId.Should().Be(reqId);
        assignment.PreviousOwnerPersonId.Should().Be(prevOwner);
        assignment.AssignedOwnerPersonId.Should().Be(newOwner);
        assignment.ActorPersonId.Should().Be(actor);
        assignment.PreviousStatus.Should().Be(RequestStatus.Evaluating);
        assignment.NewStatus.Should().Be(RequestStatus.Accepted);
        assignment.AssignedAtUtc.Should().Be(now);
        assignment.Notes.Should().Be("Notes");
        assignment.CreatedAt.Should().Be(now);
    }

    #endregion
}
