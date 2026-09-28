namespace ICS.Tests.Integration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using ICS.Modules.Customer.Application;
using ICS.Modules.Customer.Application.DTOs;
using ICS.Modules.Identity;
using ICS.Modules.Identity.Application;
using ICS.Modules.Organization.Application;
using ICS.Modules.Organization.Application.DTOs;
using ICS.Modules.Product.Application;
using ICS.Modules.Product.Application.DTOs;
using ICS.Modules.Request.Application.DTOs;
using ICS.Web.Auth;
using ICS.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Integration tests verifying Request screen REST API endpoints (/api/v1/requests/*)
/// per Architecture §7, §8, §9, §16, §18, §19.4, §19.6, §19.8, and Slice P4-S19 completion criteria:
/// 1. Authentication enforcement (401 Unauthorized for unauthenticated requests).
/// 2. Grid filtering and pagination (SCR-REQ-001 / SCR-REQ-005).
/// 3. Request creation / recording (SCR-REQ-002).
/// 4. Request detail and state transitions (SCR-REQ-003: Assign, Evaluate, Accept, Reject, StartProgress, Escalate, ManagementDecision, Complete, Reassign, Rework, ResolveEscalation).
/// 5. Personal assigned queue with metrics (SCR-REQ-004).
/// 6. Chronological state transition audit trail (SCR-REQ-005).
/// 7. Master data dropdown selectors (Customers, Products, Persons).
/// </summary>
public class RequestScreenIntegrationTests : IntegrationTestBase
{
    private const string DefaultPassword = "Password123!";

    public RequestScreenIntegrationTests(IcsWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Endpoints_WithoutSessionCookie_ShouldReturn401Unauthorized()
    {
        var dummyId = Guid.NewGuid();

        // Query endpoints
        (await Client.GetAsync("/api/v1/requests")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync($"/api/v1/requests/{dummyId}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync("/api/v1/requests/my-assigned")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync($"/api/v1/requests/{dummyId}/history")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Dropdown endpoints
        (await Client.GetAsync("/api/v1/requests/customers")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync("/api/v1/requests/dropdowns/customers")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync("/api/v1/requests/products")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync("/api/v1/requests/dropdowns/products")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync("/api/v1/requests/persons")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync("/api/v1/requests/dropdowns/persons")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // State commands
        (await Client.PostAsJsonAsync("/api/v1/requests", new RecordRequestApiRequest("T", "D", "FEATURE"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.PostAsJsonAsync($"/api/v1/requests/{dummyId}/assign", new AssignRequestOwnerApiRequest(Guid.NewGuid()))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.PostAsJsonAsync($"/api/v1/requests/{dummyId}/evaluate", new EvaluateRequestApiRequest())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.PostAsJsonAsync($"/api/v1/requests/{dummyId}/accept", new AcceptRequestApiRequest())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.PostAsJsonAsync($"/api/v1/requests/{dummyId}/reject", new RejectRequestApiRequest("Reason"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.PostAsJsonAsync($"/api/v1/requests/{dummyId}/start-progress", new StartRequestProgressApiRequest())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.PostAsJsonAsync($"/api/v1/requests/{dummyId}/escalate", new EscalateRequestApiRequest("Reason"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.PostAsJsonAsync($"/api/v1/requests/{dummyId}/management-decision", new RequestManagementDecisionApiRequest("Question"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.PostAsJsonAsync($"/api/v1/requests/{dummyId}/complete", new CompleteRequestApiRequest("Summary"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.PostAsJsonAsync($"/api/v1/requests/{dummyId}/rework", new ReworkRequestApiRequest("Feedback"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.PostAsJsonAsync($"/api/v1/requests/{dummyId}/reassign", new ReassignRequestApiRequest(Guid.NewGuid()))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.PostAsJsonAsync($"/api/v1/requests/{dummyId}/resolve-escalation", new ResolveEscalationApiRequest())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DropdownEndpoints_WithAuthenticatedUser_ShouldReturnActiveData()
    {
        // Arrange
        var (cookie, person) = await CreateAuthenticatedSessionWithPersonAsync("req.dropdown.user");

        var (customer, product) = await ExecuteInScopeAsync(async sp =>
        {
            var custService = sp.GetRequiredService<ICustomerService>();
            var c = await custService.CreateCustomerAsync($"CUST-{Guid.NewGuid():N}"[..10], "Dropdown Customer", hasActiveMaintenanceContract: true);

            var prodService = sp.GetRequiredService<IProductService>();
            var p = await prodService.CreateProductAsync($"PRD-{Guid.NewGuid():N}"[..10], "Dropdown Product", "Desc", person.PersonId);

            return (c, p);
        });

        // Act - Customers
        var custReq = CreateRequest(HttpMethod.Get, "/api/v1/requests/customers", cookie);
        var custResp = await Client.SendAsync(custReq);
        custResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var customers = await custResp.Content.ReadFromJsonAsync<List<CustomerDto>>();
        customers.Should().NotBeNull();
        customers!.Should().Contain(c => c.CustomerId == customer.CustomerId);

        // Act - Products
        var prodReq = CreateRequest(HttpMethod.Get, "/api/v1/requests/products", cookie);
        var prodResp = await Client.SendAsync(prodReq);
        prodResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await prodResp.Content.ReadFromJsonAsync<List<ProductDto>>();
        products.Should().NotBeNull();
        products!.Should().Contain(p => p.ProductId == product.ProductId);

        // Act - Persons
        var personReq = CreateRequest(HttpMethod.Get, "/api/v1/requests/persons", cookie);
        var personResp = await Client.SendAsync(personReq);
        personResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var persons = await personResp.Content.ReadFromJsonAsync<List<PersonDto>>();
        persons.Should().NotBeNull();
        persons!.Should().Contain(p => p.PersonId == person.PersonId);
    }

    [Fact]
    public async Task RecordRequest_AndGetById_ShouldReturn201AndCorrectProjections()
    {
        // Arrange
        var (cookie, person) = await CreateAuthenticatedSessionWithPersonAsync("req.create.tester");

        var (customer, product) = await ExecuteInScopeAsync(async sp =>
        {
            var custService = sp.GetRequiredService<ICustomerService>();
            var c = await custService.CreateCustomerAsync($"CUST-{Guid.NewGuid():N}"[..10], "Req Cust", hasActiveMaintenanceContract: true);

            var prodService = sp.GetRequiredService<IProductService>();
            var p = await prodService.CreateProductAsync($"PRD-{Guid.NewGuid():N}"[..10], "Req Product", "Desc", person.PersonId);

            return (c, p);
        });

        var payload = new RecordRequestApiRequest(
            Title: "Screen Created Request",
            Description: "Testing end-to-end screen creation",
            Type: "FEATURE",
            Priority: "HIGH",
            CustomerId: customer.CustomerId,
            ProductId: product.ProductId,
            InitialOwnerPersonId: person.PersonId,
            RequesterName: "Dr. Doctor");

        // Act - POST /api/v1/requests
        var createReq = CreateRequest(HttpMethod.Post, "/api/v1/requests", cookie, payload);
        var createResp = await Client.SendAsync(createReq);

        // Assert 201 Created
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<RequestDto>();
        created.Should().NotBeNull();
        created!.Title.Should().Be("Screen Created Request");
        created.Status.Should().Be("CAPTURED");
        created.Priority.Should().Be("HIGH");
        created.OwnerPersonId.Should().Be(person.PersonId);
        created.CustomerId.Should().Be(customer.CustomerId);
        created.ProductId.Should().Be(product.ProductId);

        // Act - GET /api/v1/requests/{id}
        var getReq = CreateRequest(HttpMethod.Get, $"/api/v1/requests/{created.RequestId}", cookie);
        var getResp = await Client.SendAsync(getReq);

        // Assert 200 OK
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResp.Content.ReadFromJsonAsync<RequestDto>();
        fetched.Should().NotBeNull();
        fetched!.RequestId.Should().Be(created.RequestId);
        fetched.CustomerName.Should().Be("Req Cust");
        fetched.ProductName.Should().Be("Req Product");
        fetched.Assignments.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FilteredGrid_ShouldFilterBySearchTermCustomerProductAndStatus()
    {
        // Arrange
        var (cookie, person) = await CreateAuthenticatedSessionWithPersonAsync("req.grid.tester");

        var (customer, product) = await ExecuteInScopeAsync(async sp =>
        {
            var custService = sp.GetRequiredService<ICustomerService>();
            var c = await custService.CreateCustomerAsync($"CUST-{Guid.NewGuid():N}"[..10], "Grid Customer", hasActiveMaintenanceContract: true);

            var prodService = sp.GetRequiredService<IProductService>();
            var p = await prodService.CreateProductAsync($"PRD-{Guid.NewGuid():N}"[..10], "Grid Product", "Desc", person.PersonId);

            return (c, p);
        });

        var uniqueKey = Guid.NewGuid().ToString("N")[..8];
        var payload1 = new RecordRequestApiRequest($"Alpha {uniqueKey}", "Alpha description", "FEATURE", CustomerId: customer.CustomerId, ProductId: product.ProductId);
        var payload2 = new RecordRequestApiRequest($"Beta {uniqueKey}", "Beta description", "BUG", CustomerId: customer.CustomerId, ProductId: product.ProductId);

        var r1 = await (await Client.SendAsync(CreateRequest(HttpMethod.Post, "/api/v1/requests", cookie, payload1))).Content.ReadFromJsonAsync<RequestDto>();
        var r2 = await (await Client.SendAsync(CreateRequest(HttpMethod.Post, "/api/v1/requests", cookie, payload2))).Content.ReadFromJsonAsync<RequestDto>();

        // Act 1: Search by unique keyword "Alpha"
        var searchReq = CreateRequest(HttpMethod.Get, $"/api/v1/requests?searchTerm={uniqueKey}", cookie);
        var searchResp = await Client.SendAsync(searchReq);
        searchResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var gridResult = await searchResp.Content.ReadFromJsonAsync<RequestGridResultDto>();
        gridResult.Should().NotBeNull();
        gridResult!.Items.Should().Contain(x => x.RequestId == r1!.RequestId);
        gridResult.Items.Should().Contain(x => x.RequestId == r2!.RequestId);

        // Act 2: Filter by Customer and Product
        var filterReq = CreateRequest(HttpMethod.Get, $"/api/v1/requests?customerId={customer.CustomerId}&productId={product.ProductId}&searchTerm=Alpha", cookie);
        var filterResp = await Client.SendAsync(filterReq);
        filterResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var filterResult = await filterResp.Content.ReadFromJsonAsync<RequestGridResultDto>();
        filterResult.Should().NotBeNull();
        filterResult!.Items.Should().Contain(x => x.RequestId == r1!.RequestId);
        filterResult.Items.Should().NotContain(x => x.RequestId == r2!.RequestId);
    }

    [Fact]
    public async Task FullLifecycleTransitions_ShouldSucceedAndRecordAuditHistory()
    {
        // Arrange
        var (cookie, lead) = await CreateAuthenticatedSessionWithPersonAsync("req.lifecycle.lead");
        var programmer = await ExecuteInScopeAsync(async sp =>
        {
            var org = sp.GetRequiredService<IOrganizationService>();
            return await org.CreatePersonAsync("Programmer Dev", $"prog.{Guid.NewGuid():N}@example.com");
        });

        // 1. Record Request (CAPTURED)
        var createPayload = new RecordRequestApiRequest(
            Title: "Lifecycle Flow Request",
            Description: "Validating full state machine via screen controller",
            Type: "BUG",
            Priority: "HIGH");
        var createResp = await Client.SendAsync(CreateRequest(HttpMethod.Post, "/api/v1/requests", cookie, createPayload));
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var req = (await createResp.Content.ReadFromJsonAsync<RequestDto>())!;
        req.Status.Should().Be("CAPTURED");

        // 2. Assign Owner
        var assignPayload = new AssignRequestOwnerApiRequest(programmer.PersonId, Note: "Assigning to senior dev");
        var assignResp = await Client.SendAsync(CreateRequest(HttpMethod.Post, $"/api/v1/requests/{req.RequestId}/assign", cookie, assignPayload));
        assignResp.StatusCode.Should().Be(HttpStatusCode.OK);
        req = (await assignResp.Content.ReadFromJsonAsync<RequestDto>())!;
        req.OwnerPersonId.Should().Be(programmer.PersonId);

        // 3. Evaluate Request (CAPTURED -> EVALUATING)
        var evalPayload = new EvaluateRequestApiRequest(EvaluatedByPersonId: programmer.PersonId, Notes: "Evaluating scope");
        var evalResp = await Client.SendAsync(CreateRequest(HttpMethod.Post, $"/api/v1/requests/{req.RequestId}/evaluate", cookie, evalPayload));
        evalResp.StatusCode.Should().Be(HttpStatusCode.OK);
        req = (await evalResp.Content.ReadFromJsonAsync<RequestDto>())!;
        req.Status.Should().Be("EVALUATING");

        // 4. Accept Responsibility (EVALUATING -> ACCEPTED)
        var acceptPayload = new AcceptRequestApiRequest(AcceptedByPersonId: programmer.PersonId, Notes: "Responsibility accepted");
        var acceptResp = await Client.SendAsync(CreateRequest(HttpMethod.Post, $"/api/v1/requests/{req.RequestId}/accept", cookie, acceptPayload));
        acceptResp.StatusCode.Should().Be(HttpStatusCode.OK);
        req = (await acceptResp.Content.ReadFromJsonAsync<RequestDto>())!;
        req.Status.Should().Be("ACCEPTED");

        // 5. Start Progress (ACCEPTED -> IN_PROGRESS)
        var startPayload = new StartRequestProgressApiRequest(ActorPersonId: programmer.PersonId);
        var startResp = await Client.SendAsync(CreateRequest(HttpMethod.Post, $"/api/v1/requests/{req.RequestId}/start-progress", cookie, startPayload));
        startResp.StatusCode.Should().Be(HttpStatusCode.OK);
        req = (await startResp.Content.ReadFromJsonAsync<RequestDto>())!;
        req.Status.Should().Be("IN_PROGRESS");

        // 6. Request Management Decision (docket elevation)
        var decisionPayload = new RequestManagementDecisionApiRequest(
            Question: "Need budget for cloud licensing?",
            Options: "Option 1: Tier A, Option 2: Tier B",
            Impact: "Delay by 2 days",
            RequestedByPersonId: programmer.PersonId);
        var decResp = await Client.SendAsync(CreateRequest(HttpMethod.Post, $"/api/v1/requests/{req.RequestId}/management-decision", cookie, decisionPayload));
        decResp.StatusCode.Should().Be(HttpStatusCode.OK);
        req = (await decResp.Content.ReadFromJsonAsync<RequestDto>())!;
        req.IsAwaitingManagementDecision.Should().BeTrue();
        req.ManagementDecisionQuestion.Should().Be("Need budget for cloud licensing?");

        // 7. Escalate Request (IN_PROGRESS -> ESCALATED)
        var escalatePayload = new EscalateRequestApiRequest(
            Reason: "Third-party API downtime blocking delivery",
            RequiredAssistance: "Executive escalation to vendor",
            EscalatedByPersonId: programmer.PersonId);
        var escResp = await Client.SendAsync(CreateRequest(HttpMethod.Post, $"/api/v1/requests/{req.RequestId}/escalate", cookie, escalatePayload));
        escResp.StatusCode.Should().Be(HttpStatusCode.OK);
        req = (await escResp.Content.ReadFromJsonAsync<RequestDto>())!;
        req.Status.Should().Be("ESCALATED");

        // 8. Resolve Escalation (ESCALATED -> IN_PROGRESS)
        var resolveEscPayload = new ResolveEscalationApiRequest(ActorPersonId: programmer.PersonId, Notes: "Vendor resolved outage");
        var resEscResp = await Client.SendAsync(CreateRequest(HttpMethod.Post, $"/api/v1/requests/{req.RequestId}/resolve-escalation", cookie, resolveEscPayload));
        resEscResp.StatusCode.Should().Be(HttpStatusCode.OK);
        req = (await resEscResp.Content.ReadFromJsonAsync<RequestDto>())!;
        req.Status.Should().Be("IN_PROGRESS");

        // 9. Reassign Request Ownership
        var secondDev = await ExecuteInScopeAsync(async sp =>
        {
            var org = sp.GetRequiredService<IOrganizationService>();
            return await org.CreatePersonAsync("Second Dev", $"second.{Guid.NewGuid():N}@example.com");
        });
        var reassignPayload = new ReassignRequestApiRequest(NewOwnerPersonId: secondDev.PersonId, Reason: "Workload balance");
        var reassignResp = await Client.SendAsync(CreateRequest(HttpMethod.Post, $"/api/v1/requests/{req.RequestId}/reassign", cookie, reassignPayload));
        reassignResp.StatusCode.Should().Be(HttpStatusCode.OK);
        req = (await reassignResp.Content.ReadFromJsonAsync<RequestDto>())!;
        req.OwnerPersonId.Should().Be(secondDev.PersonId);

        // 10. Rework Request during review (IN_PROGRESS remains IN_PROGRESS)
        var reworkPayload = new ReworkRequestApiRequest(Feedback: "Fix unit test coverage", ReviewerPersonId: lead.PersonId);
        var reworkResp = await Client.SendAsync(CreateRequest(HttpMethod.Post, $"/api/v1/requests/{req.RequestId}/rework", cookie, reworkPayload));
        reworkResp.StatusCode.Should().Be(HttpStatusCode.OK);
        req = (await reworkResp.Content.ReadFromJsonAsync<RequestDto>())!;
        req.Status.Should().Be("IN_PROGRESS");

        // 11. Complete Request (IN_PROGRESS -> COMPLETED)
        var completePayload = new CompleteRequestApiRequest(
            Summary: "Delivered in release v1.4.2",
            Outcome: "RESOLVED",
            ReviewerPersonId: lead.PersonId);
        var compResp = await Client.SendAsync(CreateRequest(HttpMethod.Post, $"/api/v1/requests/{req.RequestId}/complete", cookie, completePayload));
        compResp.StatusCode.Should().Be(HttpStatusCode.OK);
        req = (await compResp.Content.ReadFromJsonAsync<RequestDto>())!;
        req.Status.Should().Be("COMPLETED");
        req.Resolution.Should().NotBeNull();
        req.Resolution!.Outcome.Should().Be("RESOLVED");

        // 12. State Transition Audit Trail (GET /api/v1/requests/{id}/history)
        var histResp = await Client.SendAsync(CreateRequest(HttpMethod.Get, $"/api/v1/requests/{req.RequestId}/history", cookie));
        histResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var histories = await histResp.Content.ReadFromJsonAsync<List<RequestStateHistoryDto>>();
        histories.Should().NotBeNull();
        histories!.Count.Should().BeGreaterThanOrEqualTo(7);
        histories.Select(h => h.ToStatus).Should().Contain(new[] { "CAPTURED", "EVALUATING", "ACCEPTED", "IN_PROGRESS", "ESCALATED", "COMPLETED" });
    }

    [Fact]
    public async Task MyAssignedRequests_ShouldReturnUserQueueAndCounts()
    {
        // Arrange
        var (cookie, userPerson) = await CreateAuthenticatedSessionWithPersonAsync("my.assigned.owner");

        // Record request and assign to userPerson
        var createPayload = new RecordRequestApiRequest(
            Title: "Assigned to me directly",
            Description: "Queue verification",
            Type: "SUPPORT",
            InitialOwnerPersonId: userPerson.PersonId);

        var createResp = await Client.SendAsync(CreateRequest(HttpMethod.Post, "/api/v1/requests", cookie, createPayload));
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await createResp.Content.ReadFromJsonAsync<RequestDto>())!;

        // Act - GET /api/v1/requests/my-assigned
        var queueReq = CreateRequest(HttpMethod.Get, "/api/v1/requests/my-assigned", cookie);
        var queueResp = await Client.SendAsync(queueReq);

        // Assert
        queueResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await queueResp.Content.ReadFromJsonAsync<MyAssignedRequestsResponseDto>();
        queue.Should().NotBeNull();
        queue!.ActiveCount.Should().BeGreaterThanOrEqualTo(1);
        queue.Items.Should().Contain(x => x.RequestId == created.RequestId);
    }

    [Fact]
    public async Task RejectRequest_ShouldSetRejectedStatusAndOutcome()
    {
        // Arrange
        var (cookie, person) = await CreateAuthenticatedSessionWithPersonAsync("req.reject.tester");

        var createPayload = new RecordRequestApiRequest("Out of Scope Request", "Cannot fulfill this request", "FEATURE");
        var createResp = await Client.SendAsync(CreateRequest(HttpMethod.Post, "/api/v1/requests", cookie, createPayload));
        var created = (await createResp.Content.ReadFromJsonAsync<RequestDto>())!;

        // Act
        var rejectPayload = new RejectRequestApiRequest("Duplicate request already covered in Epic-10", person.PersonId);
        var rejectResp = await Client.SendAsync(CreateRequest(HttpMethod.Post, $"/api/v1/requests/{created.RequestId}/reject", cookie, rejectPayload));

        // Assert
        rejectResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejected = (await rejectResp.Content.ReadFromJsonAsync<RequestDto>())!;
        rejected.Status.Should().Be("REJECTED");
        rejected.Resolution.Should().NotBeNull();
        rejected.Resolution!.Outcome.Should().Be("REJECTED");
        rejected.Resolution.Summary.Should().Be("Duplicate request already covered in Epic-10");
    }

    [Fact]
    public async Task GetRequestById_WhenNotFound_ShouldReturn404ProblemDetails()
    {
        // Arrange
        var (cookie, _) = await CreateAuthenticatedSessionWithPersonAsync("req.notfound.tester");
        var nonExistentId = Guid.NewGuid();

        // Act
        var req = CreateRequest(HttpMethod.Get, $"/api/v1/requests/{nonExistentId}", cookie);
        var resp = await Client.SendAsync(req);

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await resp.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(404);
        problem.Extensions.Should().ContainKey("errorCode");
        problem.Extensions["errorCode"]?.ToString().Should().Be("REQUEST_NOT_FOUND");
    }

    private async Task<(string Cookie, PersonDto Person)> CreateAuthenticatedSessionWithPersonAsync(string username)
    {
        var person = await ExecuteInScopeAsync(async sp =>
        {
            var orgService = sp.GetRequiredService<IOrganizationService>();
            return await orgService.CreatePersonAsync($"Person {username}", $"{username}@smart-ics.internal");
        });

        await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            await seeder.CreateUserAsync(username, $"{username}@smart-ics.internal", DefaultPassword, personId: person.PersonId);
        });

        var loginResult = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.LoginAsync(username, DefaultPassword);
        });

        var cookie = $"{AuthenticationServiceExtensions.DefaultCookieName}={loginResult.SessionToken}";
        return (cookie, person);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, string cookieHeader, object? payload = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("Cookie", cookieHeader);
        if (payload != null)
        {
            request.Content = JsonContent.Create(payload);
        }
        return request;
    }
}
