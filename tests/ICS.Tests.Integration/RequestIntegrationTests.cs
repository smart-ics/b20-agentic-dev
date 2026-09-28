namespace ICS.Tests.Integration;

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using ICS.Modules.Customer.Application;
using ICS.Modules.Organization.Application;
using ICS.Modules.Product.Application;
using ICS.Modules.Request;
using ICS.Modules.Request.Application;
using ICS.Modules.Request.Application.DTOs;
using ICS.Modules.Request.Domain;
using ICS.Modules.Request.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Integration tests verifying the Request module:
/// Application services (IRequestService, IRequestQueryService), MediatR command & query handlers,
/// explicit Dapper SQL repositories, state machine lifecycle transitions (Record → Assign → Evaluate → Accept → StartProgress → Complete),
/// rejection, escalation, management decision elevation, completion review rework, cross-module validation against Customer/Product/Organization,
/// state change audit logging, and Respawn test database isolation.
/// Architecture §7, §8, §15, §16, §17, §18, §19.3, §20; request-domain.md.
/// </summary>
public class RequestIntegrationTests : IntegrationTestBase
{
    public RequestIntegrationTests(IcsWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task FullRequestLifecycle_Record_Assign_Evaluate_Accept_StartProgress_Complete_ShouldSucceed()
    {
        // Arrange
        TestDomainEventCollector.Clear();
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();
        var requestQueryService = scope.ServiceProvider.GetRequiredService<IRequestQueryService>();

        // 1. Setup master data in Organization, Customer, Product
        var requester = await organizationService.CreatePersonAsync("Alice Requester", $"alice.{Guid.NewGuid():N}@example.com");
        var assignee = await organizationService.CreatePersonAsync("Bob Programmer", $"bob.{Guid.NewGuid():N}@example.com");

        var customerCode = $"CUST-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var customer = await customerService.CreateCustomerAsync(customerCode, "General Hospital", hasActiveMaintenanceContract: true);

        var productCode = $"PRD-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var product = await productService.CreateProductAsync(productCode, "E-Claims Suite", "Health insurance claim processing", assignee.PersonId);

        // 2. UC-REQ-001: RecordRequest
        var recorded = await requestService.RecordRequestAsync(
            title: "Add BPJS validation to billing",
            description: "Billing submissions require patient BPJS card validation before dispatch.",
            type: Request.TypeFeature,
            priority: Request.PriorityHigh,
            requesterPersonId: requester.PersonId,
            customerId: customer.CustomerId,
            productId: product.ProductId,
            assignedByPersonId: requester.PersonId);

        recorded.Should().NotBeNull();
        recorded.RequestId.Should().NotBeEmpty();
        recorded.Status.Should().Be(RequestStatus.Captured);
        recorded.Priority.Should().Be(Request.PriorityHigh);
        recorded.CustomerName.Should().Be("General Hospital");
        recorded.ProductName.Should().Be("E-Claims Suite");
        recorded.IsTerminal.Should().BeFalse();
        recorded.IsActive.Should().BeFalse();

        var recordedEvent = TestDomainEventCollector.PublishedEvents.OfType<RequestRecorded>()
            .FirstOrDefault(e => e.RequestId == recorded.RequestId);
        recordedEvent.Should().NotBeNull();
        recordedEvent!.Title.Should().Be("Add BPJS validation to billing");

        // 3. UC-REQ-002: AssignRequestOwner
        var assigned = await requestService.AssignRequestOwnerAsync(
            recorded.RequestId,
            assignee.PersonId,
            requester.PersonId,
            note: "Assigning to senior billing developer.");

        assigned.OwnerPersonId.Should().Be(assignee.PersonId);
        assigned.OwnerName.Should().Be(assignee.Name);
        assigned.Assignments.Should().HaveCount(1);
        assigned.Assignments![0].OwnerPersonId.Should().Be(assignee.PersonId);
        assigned.Assignments[0].IsActive.Should().BeTrue();

        var assignedEvent = TestDomainEventCollector.PublishedEvents.OfType<RequestAssigned>()
            .FirstOrDefault(e => e.RequestId == recorded.RequestId);
        assignedEvent.Should().NotBeNull();
        assignedEvent!.NewOwnerPersonId.Should().Be(assignee.PersonId);

        // 4. UC-REQ-003: EvaluateRequest
        var evaluated = await requestService.EvaluateRequestAsync(
            recorded.RequestId,
            assignee.PersonId,
            notes: "Feasibility verified. External BPJS API endpoints are responsive.");

        evaluated.Status.Should().Be(RequestStatus.Evaluating);

        var evaluatedEvent = TestDomainEventCollector.PublishedEvents.OfType<RequestEvaluated>()
            .FirstOrDefault(e => e.RequestId == recorded.RequestId);
        evaluatedEvent.Should().NotBeNull();
        evaluatedEvent!.EvaluatedByPersonId.Should().Be(assignee.PersonId);

        // 5. UC-REQ-004: AcceptRequestResponsibility
        var accepted = await requestService.AcceptRequestResponsibilityAsync(
            recorded.RequestId,
            assignee.PersonId,
            notes: "Responsibility accepted for next sprint.");

        accepted.Status.Should().Be(RequestStatus.Accepted);

        var acceptedEvent = TestDomainEventCollector.PublishedEvents.OfType<RequestAccepted>()
            .FirstOrDefault(e => e.RequestId == recorded.RequestId);
        acceptedEvent.Should().NotBeNull();
        acceptedEvent!.AcceptedByPersonId.Should().Be(assignee.PersonId);

        // 6. StartProgress (IN_PROGRESS)
        var inProgress = await requestService.StartRequestProgressAsync(
            recorded.RequestId,
            assignee.PersonId,
            notes: "Implementation started.");

        inProgress.Status.Should().Be(RequestStatus.InProgress);

        // 7. UC-REQ-008: ReviewRequestCompletion (Accept Resolution)
        var completed = await requestService.ReviewRequestCompletionAsync(
            recorded.RequestId,
            requester.PersonId,
            acceptResolution: true,
            summaryOrFeedback: "BPJS card validation implemented, tested, and deployed to staging.",
            outcome: RequestResolution.OutcomeResolved);

        completed.Status.Should().Be(RequestStatus.Completed);
        completed.IsTerminal.Should().BeTrue();
        completed.ClosedAt.Should().NotBeNull();
        completed.Resolution.Should().NotBeNull();
        completed.Resolution!.Outcome.Should().Be(RequestResolution.OutcomeResolved);
        completed.Resolution.Summary.Should().Be("BPJS card validation implemented, tested, and deployed to staging.");
        completed.Resolution.ResolvedByPersonId.Should().Be(requester.PersonId);
        completed.Resolution.ResolvedByName.Should().Be(requester.Name);

        var completedEvent = TestDomainEventCollector.PublishedEvents.OfType<RequestCompleted>()
            .FirstOrDefault(e => e.RequestId == recorded.RequestId);
        completedEvent.Should().NotBeNull();
        completedEvent!.Outcome.Should().Be(RequestResolution.OutcomeResolved);

        // 8. Verify published query service GetRequestById
        var fetched = await requestQueryService.GetRequestByIdAsync(recorded.RequestId);
        fetched.Should().NotBeNull();
        fetched!.RequestId.Should().Be(recorded.RequestId);
        fetched.Status.Should().Be(RequestStatus.Completed);
        fetched.OwnerName.Should().Be(assignee.Name);
        fetched.CustomerName.Should().Be("General Hospital");
        fetched.ProductName.Should().Be("E-Claims Suite");
        fetched.Resolution.Should().NotBeNull();
        fetched.Resolution!.Outcome.Should().Be(RequestResolution.OutcomeResolved);
        fetched.Assignments.Should().HaveCount(1);

        // 9. Verify state audit history
        var stateHistory = await requestQueryService.GetRequestStateHistoryAsync(recorded.RequestId);
        stateHistory.Should().NotBeNull();
        stateHistory.Count.Should().BeGreaterThanOrEqualTo(5);

        // Chronological order verification
        var transitions = stateHistory.Select(h => (h.FromStatus, h.ToStatus)).ToList();
        transitions.Should().Contain((null, RequestStatus.Captured));
        transitions.Should().Contain((RequestStatus.Captured, RequestStatus.Evaluating));
        transitions.Should().Contain((RequestStatus.Evaluating, RequestStatus.Accepted));
        transitions.Should().Contain((RequestStatus.Accepted, RequestStatus.InProgress));
        transitions.Should().Contain((RequestStatus.InProgress, RequestStatus.Completed));
    }

    [Fact]
    public async Task ReviewRequestCompletion_WithRework_ShouldRemainInProgress_AndRecordAudit()
    {
        // Arrange
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();
        var requestQueryService = scope.ServiceProvider.GetRequiredService<IRequestQueryService>();

        var user = await organizationService.CreatePersonAsync("Dev One", $"dev1.{Guid.NewGuid():N}@example.com");

        var recorded = await requestService.RecordRequestAsync(
            title: "Fix pharmacy stock calculation",
            description: "Stock ledger does not deduct expired medication units.",
            type: Request.TypeBug,
            priority: Request.PriorityMedium,
            assignedByPersonId: user.PersonId,
            initialOwnerPersonId: user.PersonId);

        await requestService.EvaluateRequestAsync(recorded.RequestId, user.PersonId);
        await requestService.AcceptRequestResponsibilityAsync(recorded.RequestId, user.PersonId);
        await requestService.StartRequestProgressAsync(recorded.RequestId, user.PersonId);

        // Act: Reviewer rejects completion and requests rework (UC-REQ-008 rework path)
        var reworked = await requestService.ReviewRequestCompletionAsync(
            recorded.RequestId,
            user.PersonId,
            acceptResolution: false,
            summaryOrFeedback: "Validation failed on edge case with negative quantities.");

        // Assert
        reworked.Status.Should().Be(RequestStatus.InProgress);
        reworked.ClosedAt.Should().BeNull();
        reworked.Resolution.Should().BeNull();

        var history = await requestQueryService.GetRequestStateHistoryAsync(recorded.RequestId);
        history.Should().Contain(h => h.FromStatus == RequestStatus.InProgress
            && h.ToStatus == RequestStatus.InProgress
            && h.Reason!.Contains("negative quantities"));
    }

    [Fact]
    public async Task RejectRequest_DuringTriage_ShouldTransitionToRejected_WithResolution_AndAuditLog()
    {
        // Arrange
        TestDomainEventCollector.Clear();
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();
        var requestQueryService = scope.ServiceProvider.GetRequiredService<IRequestQueryService>();

        var reviewer = await organizationService.CreatePersonAsync("Triage Lead", $"lead.{Guid.NewGuid():N}@example.com");

        var recorded = await requestService.RecordRequestAsync(
            title: "Synthetic invalid feature request",
            description: "Demand is out of scope and violates system policy.",
            type: Request.TypeFeature,
            assignedByPersonId: reviewer.PersonId);

        // Act: UC-REQ-005: RejectRequest
        var rejected = await requestService.RejectRequestAsync(
            recorded.RequestId,
            reviewer.PersonId,
            reason: "Out of scope per contractual service catalog.");

        // Assert
        rejected.Status.Should().Be(RequestStatus.Rejected);
        rejected.IsTerminal.Should().BeTrue();
        rejected.ClosedAt.Should().NotBeNull();
        rejected.Resolution.Should().NotBeNull();
        rejected.Resolution!.Outcome.Should().Be(RequestResolution.OutcomeRejected);
        rejected.Resolution.Summary.Should().Be("Out of scope per contractual service catalog.");
        rejected.Resolution.ResolvedByPersonId.Should().Be(reviewer.PersonId);

        var rejectedEvent = TestDomainEventCollector.PublishedEvents.OfType<RequestRejected>()
            .FirstOrDefault(e => e.RequestId == recorded.RequestId);
        rejectedEvent.Should().NotBeNull();
        rejectedEvent!.Reason.Should().Be("Out of scope per contractual service catalog.");

        var history = await requestQueryService.GetRequestStateHistoryAsync(recorded.RequestId);
        history.Should().Contain(h => h.ToStatus == RequestStatus.Rejected
            && h.ActorPersonId == reviewer.PersonId);
    }

    [Fact]
    public async Task EscalateRequest_AndManagementDecision_ShouldRecordAuditAndCondition()
    {
        // Arrange
        TestDomainEventCollector.Clear();
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();
        var requestQueryService = scope.ServiceProvider.GetRequiredService<IRequestQueryService>();

        var owner = await organizationService.CreatePersonAsync("Implementer Two", $"imp2.{Guid.NewGuid():N}@example.com");

        var recorded = await requestService.RecordRequestAsync(
            title: "Database index restructuring",
            description: "High locking contention during peak morning admissions.",
            type: Request.TypeSupport,
            priority: Request.PriorityCritical,
            assignedByPersonId: owner.PersonId,
            initialOwnerPersonId: owner.PersonId);

        await requestService.EvaluateRequestAsync(recorded.RequestId, owner.PersonId);
        await requestService.AcceptRequestResponsibilityAsync(recorded.RequestId, owner.PersonId);
        await requestService.StartRequestProgressAsync(recorded.RequestId, owner.PersonId);

        // Act 1: UC-REQ-006: EscalateRequest
        var escalated = await requestService.EscalateRequestAsync(
            recorded.RequestId,
            owner.PersonId,
            reason: "Requires database administrator access and production downtime window.",
            requiredAssistance: "DBA review");

        escalated.Status.Should().Be(RequestStatus.Escalated);
        escalated.EscalationReason.Should().Be("Requires database administrator access and production downtime window.");
        escalated.EscalatedByPersonId.Should().Be(owner.PersonId);
        escalated.EscalatedByName.Should().Be(owner.Name);

        var escEvent = TestDomainEventCollector.PublishedEvents.OfType<RequestEscalated>()
            .FirstOrDefault(e => e.RequestId == recorded.RequestId);
        escEvent.Should().NotBeNull();
        escEvent!.Reason.Should().Be("Requires database administrator access and production downtime window.");

        // Act 2: UC-REQ-007: RequestManagementDecision
        var decisionReq = await requestService.RequestManagementDecisionAsync(
            recorded.RequestId,
            owner.PersonId,
            question: "Approve 1-hour maintenance downtime on Sunday 02:00 UTC?",
            options: "Option A: Full reindex (1h downtime); Option B: Online index (no downtime, high I/O)",
            impact: "Admission module will be temporarily offline.");

        decisionReq.IsAwaitingManagementDecision.Should().BeTrue();
        decisionReq.ManagementDecisionQuestion.Should().Be("Approve 1-hour maintenance downtime on Sunday 02:00 UTC?");
        decisionReq.ManagementDecisionOptions.Should().Contain("Option A");

        var mgtEvent = TestDomainEventCollector.PublishedEvents.OfType<ManagementDecisionRequested>()
            .FirstOrDefault(e => e.RequestId == recorded.RequestId);
        mgtEvent.Should().NotBeNull();
        mgtEvent!.Question.Should().Be("Approve 1-hour maintenance downtime on Sunday 02:00 UTC?");

        var fetched = await requestQueryService.GetRequestByIdAsync(recorded.RequestId);
        fetched!.Status.Should().Be(RequestStatus.Escalated);
        fetched.IsAwaitingManagementDecision.Should().BeTrue();
    }

    [Fact]
    public async Task ListMyAssignedRequests_ShouldReturnOnlyUserAssignedRequests_WithAccurateWorkloadCounts()
    {
        // Arrange
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();
        var requestQueryService = scope.ServiceProvider.GetRequiredService<IRequestQueryService>();

        var userA = await organizationService.CreatePersonAsync("Target User", $"target.{Guid.NewGuid():N}@example.com");
        var userB = await organizationService.CreatePersonAsync("Other User", $"other.{Guid.NewGuid():N}@example.com");

        // Request 1 for User A: Active
        var req1 = await requestService.RecordRequestAsync(
            title: "Task 1 for User A",
            description: "Desc",
            type: Request.TypeBug,
            assignedByPersonId: userA.PersonId,
            initialOwnerPersonId: userA.PersonId);

        // Request 2 for User A: Active + Escalated
        var req2 = await requestService.RecordRequestAsync(
            title: "Task 2 for User A",
            description: "Desc",
            type: Request.TypeFeature,
            assignedByPersonId: userA.PersonId,
            initialOwnerPersonId: userA.PersonId);
        await requestService.EvaluateRequestAsync(req2.RequestId, userA.PersonId);
        await requestService.AcceptRequestResponsibilityAsync(req2.RequestId, userA.PersonId);
        await requestService.StartRequestProgressAsync(req2.RequestId, userA.PersonId);
        await requestService.EscalateRequestAsync(req2.RequestId, userA.PersonId, "Blocked on credentials");

        // Request 3 for User A: Active + Awaiting Management Decision
        var req3 = await requestService.RecordRequestAsync(
            title: "Task 3 for User A",
            description: "Desc",
            type: Request.TypeSupport,
            assignedByPersonId: userA.PersonId,
            initialOwnerPersonId: userA.PersonId);
        await requestService.RequestManagementDecisionAsync(req3.RequestId, userA.PersonId, "Need policy approval");

        // Request for User B: should not appear in User A's queue
        await requestService.RecordRequestAsync(
            title: "Task for User B",
            description: "Desc",
            type: Request.TypeBug,
            assignedByPersonId: userB.PersonId,
            initialOwnerPersonId: userB.PersonId);

        // Act (UC-COL-004, SCR-REQ-005)
        var myAssigned = await requestQueryService.ListMyAssignedRequestsAsync(userA.PersonId);

        // Assert
        myAssigned.Should().NotBeNull();
        myAssigned.Items.Should().HaveCount(3);
        myAssigned.Items.Should().OnlyContain(r => r.OwnerPersonId == userA.PersonId);
        myAssigned.ActiveCount.Should().Be(3);
        myAssigned.EscalatedCount.Should().Be(1);
        myAssigned.AwaitingDecisionCount.Should().Be(1);
    }

    [Fact]
    public async Task GetFilteredRequestGrid_ShouldFilterBySearchTerm_Customer_Product_Status_AndPaginate()
    {
        // Arrange
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();
        var requestQueryService = scope.ServiceProvider.GetRequiredService<IRequestQueryService>();

        var user = await organizationService.CreatePersonAsync("Grid Tester", $"grid.{Guid.NewGuid():N}@example.com");

        var custCode1 = $"C1-{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var cust1 = await customerService.CreateCustomerAsync(custCode1, "Alpha Clinic");

        var custCode2 = $"C2-{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var cust2 = await customerService.CreateCustomerAsync(custCode2, "Beta Health");

        var prdCode = $"P1-{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var prd = await productService.CreateProductAsync(prdCode, "PharmaPlus", "Pharmacy module", user.PersonId);

        // Create 3 requests
        var uniqueSearchTag = $"TAG_{Guid.NewGuid():N}"[..12];
        var req1 = await requestService.RecordRequestAsync(
            title: $"Billing Issue {uniqueSearchTag}",
            description: "Test 1",
            type: Request.TypeBug,
            priority: Request.PriorityHigh,
            customerId: cust1.CustomerId,
            productId: prd.ProductId,
            assignedByPersonId: user.PersonId);

        var req2 = await requestService.RecordRequestAsync(
            title: $"Inventory Discrepancy {uniqueSearchTag}",
            description: "Test 2",
            type: Request.TypeSupport,
            priority: Request.PriorityLow,
            customerId: cust2.CustomerId,
            assignedByPersonId: user.PersonId);

        var req3 = await requestService.RecordRequestAsync(
            title: "Unrelated Item",
            description: "Unrelated content",
            type: Request.TypeFeature,
            assignedByPersonId: user.PersonId);

        // Act 1: Search by unique keyword
        var searchResult = await requestQueryService.GetFilteredRequestGridAsync(new RequestGridFilterDto
        {
            SearchTerm = uniqueSearchTag
        });
        searchResult.TotalCount.Should().Be(2);
        searchResult.Items.Should().HaveCount(2);

        // Act 2: Filter by CustomerId
        var cust1Result = await requestQueryService.GetFilteredRequestGridAsync(new RequestGridFilterDto
        {
            CustomerId = cust1.CustomerId
        });
        cust1Result.Items.Should().Contain(i => i.RequestId == req1.RequestId);
        cust1Result.Items.Should().NotContain(i => i.RequestId == req2.RequestId);

        // Act 3: Filter by ProductId
        var prdResult = await requestQueryService.GetFilteredRequestGridAsync(new RequestGridFilterDto
        {
            ProductId = prd.ProductId
        });
        prdResult.Items.Should().Contain(i => i.RequestId == req1.RequestId);

        // Act 4: Filter by Status
        var statusResult = await requestQueryService.GetFilteredRequestGridAsync(new RequestGridFilterDto
        {
            Statuses = RequestStatus.Captured
        });
        statusResult.Items.Should().Contain(i => i.RequestId == req1.RequestId);

        // Act 5: Pagination
        var pagedResult = await requestQueryService.GetFilteredRequestGridAsync(new RequestGridFilterDto
        {
            SearchTerm = uniqueSearchTag,
            Skip = 0,
            Take = 1
        });
        pagedResult.TotalCount.Should().Be(2);
        pagedResult.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task CrossModuleValidation_WithInvalidCustomer_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var scope = CreateScope();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();
        var nonExistentCustomerId = Guid.NewGuid();

        // Act
        var act = () => requestService.RecordRequestAsync(
            title: "Valid Title",
            description: "Valid Description",
            type: Request.TypeBug,
            customerId: nonExistentCustomerId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{nonExistentCustomerId}*");
    }

    [Fact]
    public async Task CrossModuleValidation_WithInvalidProduct_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var scope = CreateScope();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();
        var nonExistentProductId = Guid.NewGuid();

        // Act
        var act = () => requestService.RecordRequestAsync(
            title: "Valid Title",
            description: "Valid Description",
            type: Request.TypeBug,
            productId: nonExistentProductId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{nonExistentProductId}*");
    }

    [Fact]
    public async Task CrossModuleValidation_WithInvalidAssignee_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var requestService = scope.ServiceProvider.GetRequiredService<IRequestService>();

        var user = await organizationService.CreatePersonAsync("Assigner", $"assigner.{Guid.NewGuid():N}@example.com");
        var nonExistentAssigneeId = Guid.NewGuid();

        var recorded = await requestService.RecordRequestAsync(
            title: "Valid Title",
            description: "Valid Description",
            type: Request.TypeBug,
            assignedByPersonId: user.PersonId);

        // Act
        var act = () => requestService.AssignRequestOwnerAsync(
            recorded.RequestId,
            nonExistentAssigneeId,
            user.PersonId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{nonExistentAssigneeId}*");
    }
}
