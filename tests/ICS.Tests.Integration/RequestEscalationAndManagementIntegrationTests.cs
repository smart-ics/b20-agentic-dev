namespace ICS.Tests.Integration;

using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using ICS.Core.Data;
using ICS.Modules.Customer.Application;
using ICS.Modules.Organization.Application;
using ICS.Modules.Product.Application;
using ICS.Modules.Request;
using ICS.Modules.Request.Application;
using ICS.Modules.Request.Domain;
using ICS.Modules.Request.Domain.Events;
using ICS.Modules.Request.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Integration tests verifying slice P4-S18: Request Module — Escalation & Management Commands.
/// Tests MediatR command handlers for EscalateRequest (UC-REQ-006), RequestManagementDecision (UC-REQ-007),
/// and ReassignRequestOwnership (UC-MGT-001) in RequestService, Dapper parameterized SQL persistence,
/// cross-module validations against OrganizationQueryService, domain events, and state change audit logging.
/// Architecture §7, §8, §15, §16, §17, §18, §19.3, §20; request-domain.md.
/// </summary>
public class RequestEscalationAndManagementIntegrationTests : IntegrationTestBase
{
    public RequestEscalationAndManagementIntegrationTests(IcsWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task EscalateRequest_WhenOwnerEscalates_TransitionsToEscalated_PersistsReasonAndActorViaDapper_EmitsRequestEscalated()
    {
        // Arrange
        TestDomainEventCollector.Clear();
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();
        var requestQueryService = scope.ServiceProvider.GetRequiredService<IRequestQueryService>();
        var dbConnectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

        var owner = await organizationService.CreatePersonAsync("Lead Dev", $"lead.{Guid.NewGuid():N}@example.com");

        var recorded = await requestService.RecordRequestAsync(
            title: "Performance issue in claim validation",
            description: "High CPU usage during month-end billing runs.",
            type: Request.TypeBug,
            priority: Request.PriorityHigh,
            assignedByPersonId: owner.PersonId,
            initialOwnerPersonId: owner.PersonId);

        await requestService.EvaluateRequestAsync(recorded.RequestId, owner.PersonId);
        await requestService.AcceptRequestResponsibilityAsync(recorded.RequestId, owner.PersonId);
        await requestService.StartRequestProgressAsync(recorded.RequestId, owner.PersonId);

        // Act: UC-REQ-006: EscalateRequest
        var escalationReason = "Requires DBA indexing and schema locks during live production.";
        var assistance = "Database administrator assistance needed";
        var escalated = await requestService.EscalateRequestAsync(
            recorded.RequestId,
            owner.PersonId,
            reason: escalationReason,
            requiredAssistance: assistance);

        // Assert: DTO and aggregate state
        escalated.Status.Should().Be(RequestStatus.Escalated);
        escalated.EscalationReason.Should().Be(escalationReason);
        escalated.EscalatedByPersonId.Should().Be(owner.PersonId);
        escalated.EscalatedByName.Should().Be(owner.Name);
        escalated.EscalatedAt.Should().NotBeNull();
        // Ownership remains intact per Actor Model Assignment Rule 7
        escalated.OwnerPersonId.Should().Be(owner.PersonId);
        escalated.OwnerName.Should().Be(owner.Name);

        // Assert: Domain event emitted
        var escEvent = TestDomainEventCollector.PublishedEvents.OfType<RequestEscalated>()
            .FirstOrDefault(e => e.RequestId == recorded.RequestId);
        escEvent.Should().NotBeNull();
        escEvent!.EscalatedByPersonId.Should().Be(owner.PersonId);
        escEvent.Reason.Should().Be(escalationReason);
        escEvent.RequiredAssistance.Should().Be(assistance);

        // Assert: Audit state history logged
        var stateHistory = await requestQueryService.GetRequestStateHistoryAsync(recorded.RequestId);
        stateHistory.Should().Contain(h => h.FromStatus == RequestStatus.InProgress
            && h.ToStatus == RequestStatus.Escalated
            && h.ActorPersonId == owner.PersonId
            && h.Reason == escalationReason);

        // Assert: Direct Dapper SQL verification against SQL Server [request].[Requests] table
        await using var connection = await dbConnectionFactory.CreateOpenConnectionAsync();
        var row = await connection.QuerySingleAsync<dynamic>(
            "SELECT Status, EscalationReason, EscalatedByPersonId, EscalatedAt, OwnerPersonId FROM [request].[Requests] WHERE RequestId = @RequestId",
            new { RequestId = recorded.RequestId });

        ((string)row.Status).Should().Be(RequestStatus.Escalated);
        ((string)row.EscalationReason).Should().Be(escalationReason);
        ((Guid)row.EscalatedByPersonId).Should().Be(owner.PersonId);
        ((DateTime?)row.EscalatedAt).Should().NotBeNull();
        ((Guid)row.OwnerPersonId).Should().Be(owner.PersonId);
    }

    [Fact]
    public async Task EscalateRequest_WhenActorIsNotDesignatedOwner_ThrowsInvalidOperationException()
    {
        // Arrange
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();

        var owner = await organizationService.CreatePersonAsync("Designated Owner", $"owner.{Guid.NewGuid():N}@example.com");
        var otherPerson = await organizationService.CreatePersonAsync("Unauthorized User", $"unauth.{Guid.NewGuid():N}@example.com");

        var recorded = await requestService.RecordRequestAsync(
            title: "Security patch application",
            description: "Apply OpenSSL CVE remediation.",
            type: Request.TypeBug,
            priority: Request.PriorityCritical,
            assignedByPersonId: owner.PersonId,
            initialOwnerPersonId: owner.PersonId);

        await requestService.EvaluateRequestAsync(recorded.RequestId, owner.PersonId);
        await requestService.AcceptRequestResponsibilityAsync(recorded.RequestId, owner.PersonId);

        // Act & Assert: Only designated owner may escalate
        var act = async () => await requestService.EscalateRequestAsync(
            recorded.RequestId,
            otherPerson.PersonId,
            reason: "Non-owner trying to escalate");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*designated Request Owner*");
    }

    [Fact]
    public async Task EscalateRequest_WhenRequestIsClosed_ThrowsInvalidRequestStateTransitionException()
    {
        // Arrange
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();

        var owner = await organizationService.CreatePersonAsync("Dev Guy", $"devguy.{Guid.NewGuid():N}@example.com");

        var recorded = await requestService.RecordRequestAsync(
            title: "Obsolete request",
            description: "No longer relevant.",
            type: Request.TypeSupport,
            assignedByPersonId: owner.PersonId,
            initialOwnerPersonId: owner.PersonId);

        // Reject request during triage
        await requestService.RejectRequestAsync(recorded.RequestId, owner.PersonId, "Not actionable");

        // Act & Assert: Escalating a rejected/closed request must fail
        var act = async () => await requestService.EscalateRequestAsync(
            recorded.RequestId,
            owner.PersonId,
            reason: "Attempting to escalate a rejected request");

        await act.Should().ThrowAsync<InvalidRequestStateTransitionException>();
    }

    [Fact]
    public async Task RequestManagementDecision_WhenOwnerRequests_RecordsDecisionDocket_EmitsManagementDecisionRequested_PreservesLifecycleState()
    {
        // Arrange
        TestDomainEventCollector.Clear();
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();
        var requestQueryService = scope.ServiceProvider.GetRequiredService<IRequestQueryService>();
        var dbConnectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

        var owner = await organizationService.CreatePersonAsync("Implementer Lead", $"implead.{Guid.NewGuid():N}@example.com");

        var recorded = await requestService.RecordRequestAsync(
            title: "Custom reporting export format",
            description: "Customer requires unstandardized format.",
            type: Request.TypeFeature,
            assignedByPersonId: owner.PersonId,
            initialOwnerPersonId: owner.PersonId);

        await requestService.EvaluateRequestAsync(recorded.RequestId, owner.PersonId);
        await requestService.AcceptRequestResponsibilityAsync(recorded.RequestId, owner.PersonId);
        await requestService.StartRequestProgressAsync(recorded.RequestId, owner.PersonId);

        // Act: UC-REQ-007: RequestManagementDecision
        var question = "Should we build an ad-hoc XML export or require standard JSON export?";
        var options = "Option 1: Build XML (3 days); Option 2: Decline and provide JSON (0 days)";
        var impact = "Option 1 delays milestone by 3 days";

        var decisionResult = await requestService.RequestManagementDecisionAsync(
            recorded.RequestId,
            owner.PersonId,
            question: question,
            options: options,
            impact: impact);

        // Assert: Lifecycle state preserved as IN_PROGRESS, but operational condition elevated
        decisionResult.Status.Should().Be(RequestStatus.InProgress);
        decisionResult.IsAwaitingManagementDecision.Should().BeTrue();
        decisionResult.ManagementDecisionQuestion.Should().Be(question);
        decisionResult.ManagementDecisionOptions.Should().Be(options);
        decisionResult.ManagementDecisionImpact.Should().Be(impact);
        decisionResult.ManagementDecisionRequestedAt.Should().NotBeNull();

        // Assert: Domain event emitted
        var mgtEvent = TestDomainEventCollector.PublishedEvents.OfType<ManagementDecisionRequested>()
            .FirstOrDefault(e => e.RequestId == recorded.RequestId);
        mgtEvent.Should().NotBeNull();
        mgtEvent!.RequestedByPersonId.Should().Be(owner.PersonId);
        mgtEvent.Question.Should().Be(question);
        mgtEvent.Options.Should().Be(options);
        mgtEvent.Impact.Should().Be(impact);

        // Assert: Audit history logged
        var stateHistory = await requestQueryService.GetRequestStateHistoryAsync(recorded.RequestId);
        stateHistory.Should().Contain(h => h.ActorPersonId == owner.PersonId
            && h.Reason!.Contains(question));

        // Assert: Direct Dapper SQL verification against [request].[Requests] table
        await using var connection = await dbConnectionFactory.CreateOpenConnectionAsync();
        var row = await connection.QuerySingleAsync<dynamic>(
            @"SELECT Status, IsAwaitingManagementDecision, ManagementDecisionQuestion, ManagementDecisionOptions, 
                     ManagementDecisionImpact, ManagementDecisionRequestedAt 
              FROM [request].[Requests] WHERE RequestId = @RequestId",
            new { RequestId = recorded.RequestId });

        ((string)row.Status).Should().Be(RequestStatus.InProgress);
        ((bool)row.IsAwaitingManagementDecision).Should().BeTrue();
        ((string)row.ManagementDecisionQuestion).Should().Be(question);
        ((string)row.ManagementDecisionOptions).Should().Be(options);
        ((string)row.ManagementDecisionImpact).Should().Be(impact);
        ((DateTime?)row.ManagementDecisionRequestedAt).Should().NotBeNull();
    }

    [Fact]
    public async Task RequestManagementDecision_WhenActorIsNotDesignatedOwner_ThrowsInvalidOperationException()
    {
        // Arrange
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();

        var owner = await organizationService.CreatePersonAsync("Owner Dev", $"ownerdev.{Guid.NewGuid():N}@example.com");
        var other = await organizationService.CreatePersonAsync("Non Owner", $"nonowner.{Guid.NewGuid():N}@example.com");

        var recorded = await requestService.RecordRequestAsync(
            title: "Payment gateway integration",
            description: "Gateway webhook configuration.",
            type: Request.TypeFeature,
            assignedByPersonId: owner.PersonId,
            initialOwnerPersonId: owner.PersonId);

        await requestService.EvaluateRequestAsync(recorded.RequestId, owner.PersonId);
        await requestService.AcceptRequestResponsibilityAsync(recorded.RequestId, owner.PersonId);

        // Act & Assert
        var act = async () => await requestService.RequestManagementDecisionAsync(
            recorded.RequestId,
            other.PersonId,
            question: "Unauthorized question?");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*designated Request Owner*");
    }

    [Fact]
    public async Task ReassignRequestOwnership_WhenManagerReassigns_UpdatesOwner_PersistsNewAssignment_DeactivatesPrevious_EmitsRequestAssigned()
    {
        // Arrange
        TestDomainEventCollector.Clear();
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();
        var requestQueryService = scope.ServiceProvider.GetRequiredService<IRequestQueryService>();
        var dbConnectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

        var manager = await organizationService.CreatePersonAsync("Operational Manager", $"mgr.{Guid.NewGuid():N}@example.com");
        var devA = await organizationService.CreatePersonAsync("Programmer Alpha", $"alpha.{Guid.NewGuid():N}@example.com");
        var devB = await organizationService.CreatePersonAsync("Programmer Beta", $"beta.{Guid.NewGuid():N}@example.com");

        var recorded = await requestService.RecordRequestAsync(
            title: "Optimize batch billing query",
            description: "Timeout occurs when over 1000 records processed.",
            type: Request.TypeBug,
            assignedByPersonId: manager.PersonId,
            initialOwnerPersonId: devA.PersonId);

        await requestService.EvaluateRequestAsync(recorded.RequestId, devA.PersonId);
        await requestService.AcceptRequestResponsibilityAsync(recorded.RequestId, devA.PersonId);
        await requestService.StartRequestProgressAsync(recorded.RequestId, devA.PersonId);

        TestDomainEventCollector.Clear();

        // Act: UC-MGT-001: ReassignRequestOwnership
        var reassignReason = "Rebalancing active workload across squad members.";
        var reassigned = await requestService.ReassignRequestOwnershipAsync(
            recorded.RequestId,
            newOwnerPersonId: devB.PersonId,
            reassignedByPersonId: manager.PersonId,
            reason: reassignReason);

        // Assert: Returned DTO reflects new ownership and history
        reassigned.OwnerPersonId.Should().Be(devB.PersonId);
        reassigned.OwnerName.Should().Be(devB.Name);
        reassigned.Assignments.Should().HaveCount(2);

        var prevAssignment = reassigned.Assignments.First(a => a.OwnerPersonId == devA.PersonId);
        prevAssignment.IsActive.Should().BeFalse();

        var newAssignment = reassigned.Assignments.First(a => a.OwnerPersonId == devB.PersonId);
        newAssignment.IsActive.Should().BeTrue();
        newAssignment.AssignedByPersonId.Should().Be(manager.PersonId);
        newAssignment.AssignedByName.Should().Be(manager.Name);
        newAssignment.Note.Should().Be(reassignReason);

        // Assert: Domain event RequestAssigned emitted
        var assignEvent = TestDomainEventCollector.PublishedEvents.OfType<RequestAssigned>()
            .FirstOrDefault(e => e.RequestId == recorded.RequestId);
        assignEvent.Should().NotBeNull();
        assignEvent!.PreviousOwnerPersonId.Should().Be(devA.PersonId);
        assignEvent.NewOwnerPersonId.Should().Be(devB.PersonId);
        assignEvent.AssignedByPersonId.Should().Be(manager.PersonId);
        assignEvent.Note.Should().Be(reassignReason);

        // Assert: Audit state history reflects reassignment
        var stateHistory = await requestQueryService.GetRequestStateHistoryAsync(recorded.RequestId);
        stateHistory.Should().Contain(h => h.ActorPersonId == manager.PersonId
            && h.Reason!.Contains(devB.Name)
            && h.Reason.Contains(reassignReason));

        // Assert: Direct Dapper SQL queries against SQL Server
        await using var connection = await dbConnectionFactory.CreateOpenConnectionAsync();
        var reqRow = await connection.QuerySingleAsync<dynamic>(
            "SELECT OwnerPersonId FROM [request].[Requests] WHERE RequestId = @RequestId",
            new { RequestId = recorded.RequestId });
        ((Guid)reqRow.OwnerPersonId).Should().Be(devB.PersonId);

        var assignmentRows = (await connection.QueryAsync<dynamic>(
            "SELECT OwnerPersonId, AssignedByPersonId, IsActive, Note FROM [request].[RequestAssignments] WHERE RequestId = @RequestId ORDER BY AssignedAt ASC",
            new { RequestId = recorded.RequestId })).ToList();

        assignmentRows.Should().HaveCount(2);
        ((Guid)assignmentRows[0].OwnerPersonId).Should().Be(devA.PersonId);
        ((bool)assignmentRows[0].IsActive).Should().BeFalse();

        ((Guid)assignmentRows[1].OwnerPersonId).Should().Be(devB.PersonId);
        ((Guid)assignmentRows[1].AssignedByPersonId).Should().Be(manager.PersonId);
        ((bool)assignmentRows[1].IsActive).Should().BeTrue();
        ((string)assignmentRows[1].Note).Should().Be(reassignReason);
    }

    [Fact]
    public async Task ReassignRequestOwnership_WhenRequestIsEscalated_PreservesEscalatedStatus_AndAssignsNewOwner()
    {
        // Arrange
        TestDomainEventCollector.Clear();
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();

        var manager = await organizationService.CreatePersonAsync("Incident Manager", $"incmgr.{Guid.NewGuid():N}@example.com");
        var devA = await organizationService.CreatePersonAsync("Junior Dev", $"jr.{Guid.NewGuid():N}@example.com");
        var devSenior = await organizationService.CreatePersonAsync("Principal Dev", $"sr.{Guid.NewGuid():N}@example.com");

        var recorded = await requestService.RecordRequestAsync(
            title: "Memory leak in background processor",
            description: "OOM killer terminates pod every 4 hours.",
            type: Request.TypeBug,
            priority: Request.PriorityCritical,
            assignedByPersonId: manager.PersonId,
            initialOwnerPersonId: devA.PersonId);

        await requestService.EvaluateRequestAsync(recorded.RequestId, devA.PersonId);
        await requestService.AcceptRequestResponsibilityAsync(recorded.RequestId, devA.PersonId);
        await requestService.StartRequestProgressAsync(recorded.RequestId, devA.PersonId);
        await requestService.EscalateRequestAsync(recorded.RequestId, devA.PersonId, "Memory profiling requires senior kernel debugging");

        TestDomainEventCollector.Clear();

        // Act: Management reassigns escalated request to Principal Dev
        var reassigned = await requestService.ReassignRequestOwnershipAsync(
            recorded.RequestId,
            newOwnerPersonId: devSenior.PersonId,
            reassignedByPersonId: manager.PersonId,
            reason: "Assigning to Principal Dev with kernel diagnostics expertise");

        // Assert: Escalated status preserved
        reassigned.Status.Should().Be(RequestStatus.Escalated);
        reassigned.EscalationReason.Should().Be("Memory profiling requires senior kernel debugging");
        reassigned.OwnerPersonId.Should().Be(devSenior.PersonId);
        reassigned.OwnerName.Should().Be(devSenior.Name);

        var assignEvent = TestDomainEventCollector.PublishedEvents.OfType<RequestAssigned>()
            .FirstOrDefault(e => e.RequestId == recorded.RequestId);
        assignEvent.Should().NotBeNull();
        assignEvent!.NewOwnerPersonId.Should().Be(devSenior.PersonId);
    }

    [Fact]
    public async Task ReassignRequestOwnership_ToSameCurrentOwner_ThrowsInvalidOperationException()
    {
        // Arrange
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();

        var manager = await organizationService.CreatePersonAsync("Manager", $"m.{Guid.NewGuid():N}@example.com");
        var dev = await organizationService.CreatePersonAsync("Current Dev", $"cdev.{Guid.NewGuid():N}@example.com");

        var recorded = await requestService.RecordRequestAsync(
            title: "Form field validation bug",
            description: "Postal code field validation regex error.",
            type: Request.TypeBug,
            assignedByPersonId: manager.PersonId,
            initialOwnerPersonId: dev.PersonId);

        // Act & Assert: Redundant no-op reassignment to same owner must be rejected (FEAT-REQ-002)
        var act = async () => await requestService.ReassignRequestOwnershipAsync(
            recorded.RequestId,
            newOwnerPersonId: dev.PersonId,
            reassignedByPersonId: manager.PersonId,
            reason: "Redundant reassignment attempt");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already assigned*");
    }

    [Fact]
    public async Task ReassignRequestOwnership_ToInactiveOrNonExistentPerson_ThrowsInvalidOperationException()
    {
        // Arrange
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();

        var manager = await organizationService.CreatePersonAsync("Manager", $"mgr2.{Guid.NewGuid():N}@example.com");
        var activeDev = await organizationService.CreatePersonAsync("Active Dev", $"actdev.{Guid.NewGuid():N}@example.com");
        var inactiveDev = await organizationService.CreatePersonAsync("Inactive Dev", $"inactdev.{Guid.NewGuid():N}@example.com");
        await organizationService.DeactivatePersonAsync(inactiveDev.PersonId);

        var recorded = await requestService.RecordRequestAsync(
            title: "Export report issue",
            description: "Description",
            type: Request.TypeBug,
            assignedByPersonId: manager.PersonId,
            initialOwnerPersonId: activeDev.PersonId);

        // Act & Assert 1: Non-existent person
        var nonExistentId = Guid.NewGuid();
        var actNonExistent = async () => await requestService.ReassignRequestOwnershipAsync(
            recorded.RequestId,
            newOwnerPersonId: nonExistentId,
            reassignedByPersonId: manager.PersonId);

        await actNonExistent.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not exist in Organization*");

        // Act & Assert 2: Inactive person
        var actInactive = async () => await requestService.ReassignRequestOwnershipAsync(
            recorded.RequestId,
            newOwnerPersonId: inactiveDev.PersonId,
            reassignedByPersonId: manager.PersonId);

        await actInactive.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not active in Organization*");
    }

    [Fact]
    public async Task ReassignRequestOwnership_WhenRequestIsClosed_ThrowsInvalidOperationException()
    {
        // Arrange
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();

        var manager = await organizationService.CreatePersonAsync("Manager", $"mgr3.{Guid.NewGuid():N}@example.com");
        var devA = await organizationService.CreatePersonAsync("Dev A", $"deva.{Guid.NewGuid():N}@example.com");
        var devB = await organizationService.CreatePersonAsync("Dev B", $"devb.{Guid.NewGuid():N}@example.com");

        var recorded = await requestService.RecordRequestAsync(
            title: "Task to complete",
            description: "Description",
            type: Request.TypeBug,
            assignedByPersonId: manager.PersonId,
            initialOwnerPersonId: devA.PersonId);

        await requestService.EvaluateRequestAsync(recorded.RequestId, devA.PersonId);
        await requestService.AcceptRequestResponsibilityAsync(recorded.RequestId, devA.PersonId);
        await requestService.StartRequestProgressAsync(recorded.RequestId, devA.PersonId);
        await requestService.ReviewRequestCompletionAsync(recorded.RequestId, manager.PersonId, acceptResolution: true, summaryOrFeedback: "Resolved cleanly");

        // Act & Assert: Reassigning a completed request must fail
        var act = async () => await requestService.ReassignRequestOwnershipAsync(
            recorded.RequestId,
            newOwnerPersonId: devB.PersonId,
            reassignedByPersonId: manager.PersonId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed/terminal Request*");
    }

    [Fact]
    public async Task ResolveEscalation_FromEscalatedState_TransitionsBackToInProgress_RecordsAudit()
    {
        // Arrange
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();
        var requestQueryService = scope.ServiceProvider.GetRequiredService<IRequestQueryService>();

        var dev = await organizationService.CreatePersonAsync("Dev Engineer", $"eng.{Guid.NewGuid():N}@example.com");

        var recorded = await requestService.RecordRequestAsync(
            title: "Blocked task on external vendor",
            description: "Vendor API down.",
            type: Request.TypeSupport,
            assignedByPersonId: dev.PersonId,
            initialOwnerPersonId: dev.PersonId);

        await requestService.EvaluateRequestAsync(recorded.RequestId, dev.PersonId);
        await requestService.AcceptRequestResponsibilityAsync(recorded.RequestId, dev.PersonId);
        await requestService.StartRequestProgressAsync(recorded.RequestId, dev.PersonId);
        await requestService.EscalateRequestAsync(recorded.RequestId, dev.PersonId, "Vendor API unresponsive");

        // Act: ResolveEscalation
        var resolveNotes = "Vendor service restored; progress resumed.";
        var resolved = await requestService.ResolveEscalationAsync(
            recorded.RequestId,
            actorPersonId: dev.PersonId,
            notes: resolveNotes);

        // Assert
        resolved.Status.Should().Be(RequestStatus.InProgress);

        var stateHistory = await requestQueryService.GetRequestStateHistoryAsync(recorded.RequestId);
        stateHistory.Should().Contain(h => h.FromStatus == RequestStatus.Escalated
            && h.ToStatus == RequestStatus.InProgress
            && h.Reason == resolveNotes);
    }
}
