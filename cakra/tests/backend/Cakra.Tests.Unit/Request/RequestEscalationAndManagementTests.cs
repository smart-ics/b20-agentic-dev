using Cakra.Core;
using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Cakra.Modules.Product;
using Cakra.Modules.Request;
using Cakra.Modules.Request.Domain;
using Cakra.Modules.Request.Domain.Events;
using Cakra.Modules.Request.Domain.Exceptions;
using Cakra.Modules.Request.Persistence;
using Cakra.Modules.Request.Services;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Request;

/// <summary>
/// Unit tests for P4-S20 Request Module — Escalation &amp; Management Commands:
/// - Published visibility of EscalateRequestCommand, RequestManagementDecisionCommand, ReassignRequestOwnershipCommand
/// - EscalateRequest from EVALUATING and IN_PROGRESS -> ESCALATED, audit logging, RequestEscalated event (UC-REQ-006)
/// - RequestManagementDecision elevation and resolution from ESCALATED, audit logging, ManagementDecisionRequested event (UC-REQ-007)
/// - ReassignRequestOwnership owner validation via IOrganizationQueryService, audit logging, RequestAssigned event (UC-MGT-001)
/// - Invalid state transitions and FluentValidation rules
/// </summary>
public sealed class RequestEscalationAndManagementTests
{
    [Fact]
    public void Escalation_and_management_commands_and_validators_are_publicly_accessible()
    {
        typeof(EscalateRequestCommand).IsPublic.Should().BeTrue();
        typeof(EscalateRequestCommandValidator).IsPublic.Should().BeTrue();
        typeof(RequestManagementDecisionCommand).IsPublic.Should().BeTrue();
        typeof(RequestManagementDecisionCommandValidator).IsPublic.Should().BeTrue();
        typeof(ReassignRequestOwnershipCommand).IsPublic.Should().BeTrue();
        typeof(ReassignRequestOwnershipCommandValidator).IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task EscalateRequest_from_Evaluating_and_InProgress_transitions_to_Escalated_records_audit_and_emits_RequestEscalated()
    {
        var recorderId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var repo = new InMemoryRequestRepository();
        var orgQuery = new FakeOrganizationQueryService();
        orgQuery.SetPerson(recorderId, isActive: true);
        orgQuery.SetPerson(ownerId, isActive: true);

        var dispatcher = new RecordingDomainEventDispatcher();
        var contextProvider = new FakeCurrentContextProvider(recorderId);
        var clock = new FakeSystemClock(new DateTime(2026, 9, 28, 9, 0, 0, DateTimeKind.Utc));

        var service = new RequestService(
            repo,
            orgQuery,
            new FakeCustomerQueryService(),
            new FakeProductQueryService(),
            dispatcher,
            contextProvider,
            auditContext: null,
            clock: clock);

        // Case 1: Escalate from EVALUATING
        var req1 = await service.RecordRequest("Database schema lock", "Production migration blocked by lock");
        await service.AssignRequestOwner(req1.Id, ownerId);

        clock.AdvanceMinutes(10);
        contextProvider.CurrentPersonId = ownerId;

        var escalatedFromEval = await service.Handle(
            new EscalateRequestCommand(
                RequestId: req1.Id,
                Reason: "Requires production DBA sysadmin privileges to terminate blocking session."),
            CancellationToken.None);

        escalatedFromEval.Status.Should().Be(RequestStatusNames.Escalated);
        escalatedFromEval.EscalationReason.Should().Be("Requires production DBA sysadmin privileges to terminate blocking session.");
        escalatedFromEval.OwnerPersonId.Should().Be(ownerId);

        var evalAudit = repo.Assignments.Last(a => a.RequestId == req1.Id);
        evalAudit.PreviousStatus.Should().Be(RequestStatus.Evaluating);
        evalAudit.NewStatus.Should().Be(RequestStatus.Escalated);
        evalAudit.ActorPersonId.Should().Be(ownerId);
        evalAudit.Notes.Should().Contain("Requires production DBA sysadmin privileges");

        dispatcher.Events.OfType<RequestEscalated>().Should().ContainSingle(e =>
            e.RequestId == req1.Id &&
            e.EscalatedByPersonId == ownerId &&
            e.EscalationReason == "Requires production DBA sysadmin privileges to terminate blocking session.");

        // Case 2: Escalate from IN_PROGRESS
        contextProvider.CurrentPersonId = recorderId;
        var req2 = await service.RecordRequest("BPJS Bridging SSL Handshake Failure", "Intermittent TLS 1.3 failure");
        await service.AssignRequestOwner(req2.Id, ownerId);

        contextProvider.CurrentPersonId = ownerId;
        await service.AcceptRequestResponsibility(req2.Id, "Starting investigation");

        clock.AdvanceMinutes(25);
        var escalatedFromInProgress = await service.EscalateRequest(
            requestId: req2.Id,
            reason: "Hospital firewall blocks outbound OCSP stapling verification; needs network team intervention.");

        escalatedFromInProgress.Status.Should().Be(RequestStatusNames.Escalated);
        escalatedFromInProgress.EscalationReason.Should().Be("Hospital firewall blocks outbound OCSP stapling verification; needs network team intervention.");

        var inProgAudit = repo.Assignments.Last(a => a.RequestId == req2.Id);
        inProgAudit.PreviousStatus.Should().Be(RequestStatus.InProgress);
        inProgAudit.NewStatus.Should().Be(RequestStatus.Escalated);
        inProgAudit.ActorPersonId.Should().Be(ownerId);

        dispatcher.Events.OfType<RequestEscalated>().Should().Contain(e =>
            e.RequestId == req2.Id &&
            e.EscalatedByPersonId == ownerId &&
            e.EscalationReason.Contains("Hospital firewall blocks outbound OCSP"));
    }

    [Fact]
    public async Task RequestManagementDecision_records_decision_notes_audit_entry_and_emits_ManagementDecisionRequested()
    {
        var recorderId = Guid.NewGuid();
        var programmerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        var repo = new InMemoryRequestRepository();
        var orgQuery = new FakeOrganizationQueryService();
        orgQuery.SetPerson(recorderId, isActive: true);
        orgQuery.SetPerson(programmerId, isActive: true);
        orgQuery.SetPerson(managerId, isActive: true);

        var dispatcher = new RecordingDomainEventDispatcher();
        var contextProvider = new FakeCurrentContextProvider(recorderId);
        var clock = new FakeSystemClock(new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc));

        var service = new RequestService(
            repo,
            orgQuery,
            new FakeCustomerQueryService(),
            new FakeProductQueryService(),
            dispatcher,
            contextProvider,
            auditContext: null,
            clock: clock);

        var recorded = await service.RecordRequest("Custom unbilled module customization", "Hospital requests custom dashboard outside contract");
        await service.AssignRequestOwner(recorded.Id, programmerId);

        contextProvider.CurrentPersonId = programmerId;
        await service.EscalateRequest(recorded.Id, "Requires commercial scope approval from management.");

        // 1. Elevate for management decision without state transition
        clock.AdvanceMinutes(15);
        var elevated = await service.Handle(
            new RequestManagementDecisionCommand(
                RequestId: recorded.Id,
                DecisionDetails: "Requesting COO determination on whether to include in Q4 maintenance goodwill."),
            CancellationToken.None);

        elevated.Status.Should().Be(RequestStatusNames.Escalated);
        elevated.ManagementDecisionNotes.Should().Be("Requesting COO determination on whether to include in Q4 maintenance goodwill.");
        elevated.Assignments.Last().PreviousStatus.Should().Be(RequestStatusNames.Escalated);
        elevated.Assignments.Last().NewStatus.Should().Be(RequestStatusNames.Escalated);
        elevated.Assignments.Last().ActorPersonId.Should().Be(programmerId);

        repo.Assignments.Last(a => a.RequestId == recorded.Id).Notes.Should().Contain("Management decision requested");

        dispatcher.Events.OfType<ManagementDecisionRequested>().Should().ContainSingle(e =>
            e.RequestId == recorded.Id &&
            e.RequestedByPersonId == programmerId &&
            e.DecisionDetails == "Requesting COO determination on whether to include in Q4 maintenance goodwill.");

        // 2. Apply management decision resolving ESCALATED -> IN_PROGRESS
        clock.AdvanceMinutes(30);
        contextProvider.CurrentPersonId = managerId;

        var resolvedDecision = await service.Handle(
            new RequestManagementDecisionCommand(
                RequestId: recorded.Id,
                DecisionDetails: "Approved by COO as goodwill deliverable; proceed with implementation.",
                TargetStatus: RequestStatus.InProgress),
            CancellationToken.None);

        resolvedDecision.Status.Should().Be(RequestStatusNames.InProgress);
        resolvedDecision.ManagementDecisionNotes.Should().Be("Approved by COO as goodwill deliverable; proceed with implementation.");

        var decisionAudit = repo.Assignments.Last(a => a.RequestId == recorded.Id);
        decisionAudit.PreviousStatus.Should().Be(RequestStatus.Escalated);
        decisionAudit.NewStatus.Should().Be(RequestStatus.InProgress);
        decisionAudit.ActorPersonId.Should().Be(managerId);

        dispatcher.Events.OfType<ManagementDecisionRequested>().Should().HaveCount(2);
    }

    [Fact]
    public async Task ReassignRequestOwnership_validates_assignee_updates_owner_records_audit_and_emits_RequestAssigned()
    {
        var recorderId = Guid.NewGuid();
        var firstOwnerId = Guid.NewGuid();
        var secondOwnerId = Guid.NewGuid();
        var thirdOwnerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var inactivePersonId = Guid.NewGuid();

        var repo = new InMemoryRequestRepository();
        var orgQuery = new FakeOrganizationQueryService();
        orgQuery.SetPerson(recorderId, isActive: true);
        orgQuery.SetPerson(firstOwnerId, isActive: true);
        orgQuery.SetPerson(secondOwnerId, isActive: true);
        orgQuery.SetPerson(thirdOwnerId, isActive: true);
        orgQuery.SetPerson(managerId, isActive: true);
        orgQuery.SetPerson(inactivePersonId, isActive: false);

        var dispatcher = new RecordingDomainEventDispatcher();
        var contextProvider = new FakeCurrentContextProvider(recorderId);
        var clock = new FakeSystemClock(new DateTime(2026, 9, 28, 11, 0, 0, DateTimeKind.Utc));

        var service = new RequestService(
            repo,
            orgQuery,
            new FakeCustomerQueryService(),
            new FakeProductQueryService(),
            dispatcher,
            contextProvider,
            auditContext: null,
            clock: clock);

        var recorded = await service.RecordRequest("Pharmacy inventory discrepancy", "Batch stock mismatch");
        await service.AssignRequestOwner(recorded.Id, firstOwnerId);
        await service.AcceptRequestResponsibility(recorded.Id, "Started investigation", firstOwnerId);
        await service.EscalateRequest(recorded.Id, "Blocked on legacy stored procedure complexity", firstOwnerId);

        // 1. Reassign from ESCALATED -> defaults to EVALUATING for new owner
        clock.AdvanceMinutes(20);
        contextProvider.CurrentPersonId = managerId;

        var reassignedFromEscalated = await service.Handle(
            new ReassignRequestOwnershipCommand(
                RequestId: recorded.Id,
                NewOwnerPersonId: secondOwnerId,
                Notes: "Reassigned to senior database architect"),
            CancellationToken.None);

        reassignedFromEscalated.Status.Should().Be(RequestStatusNames.Evaluating);
        reassignedFromEscalated.OwnerPersonId.Should().Be(secondOwnerId);

        var audit1 = repo.Assignments.Last(a => a.RequestId == recorded.Id);
        audit1.PreviousOwnerPersonId.Should().Be(firstOwnerId);
        audit1.AssignedOwnerPersonId.Should().Be(secondOwnerId);
        audit1.PreviousStatus.Should().Be(RequestStatus.Escalated);
        audit1.NewStatus.Should().Be(RequestStatus.Evaluating);
        audit1.ActorPersonId.Should().Be(managerId);
        audit1.Notes.Should().Be("Reassigned to senior database architect");

        dispatcher.Events.OfType<RequestAssigned>().Last().Should().BeEquivalentTo(new
        {
            RequestId = recorded.Id,
            OwnerPersonId = secondOwnerId,
            PreviousOwnerPersonId = (Guid?)firstOwnerId,
            ActorPersonId = managerId,
            PreviousStatus = RequestStatus.Escalated,
            NewStatus = RequestStatus.Evaluating,
            Notes = "Reassigned to senior database architect"
        }, options => options.ExcludingMissingMembers());

        // 2. Reassign while in EVALUATING -> preserves EVALUATING status
        clock.AdvanceMinutes(10);
        var reassignedInEvaluating = await service.ReassignRequestOwnership(
            requestId: recorded.Id,
            newOwnerPersonId: thirdOwnerId,
            notes: "Load balancing across team");

        reassignedInEvaluating.Status.Should().Be(RequestStatusNames.Evaluating);
        reassignedInEvaluating.OwnerPersonId.Should().Be(thirdOwnerId);

        // 3. Rejects unknown, inactive, or identical assignee
        var unknownAssigneeAct = async () => await service.ReassignRequestOwnership(recorded.Id, Guid.NewGuid());
        await unknownAssigneeAct.Should().ThrowAsync<KeyNotFoundException>();

        var inactiveAssigneeAct = async () => await service.ReassignRequestOwnership(recorded.Id, inactivePersonId);
        await inactiveAssigneeAct.Should().ThrowAsync<InvalidOperationException>();

        var sameAssigneeAct = async () => await service.ReassignRequestOwnership(recorded.Id, thirdOwnerId);
        await sameAssigneeAct.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Invalid_escalation_management_and_reassignment_transitions_are_rejected()
    {
        var actorId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();

        var repo = new InMemoryRequestRepository();
        var orgQuery = new FakeOrganizationQueryService();
        orgQuery.SetPerson(actorId, isActive: true);
        orgQuery.SetPerson(ownerId, isActive: true);
        orgQuery.SetPerson(otherOwnerId, isActive: true);

        var service = new RequestService(
            repo,
            orgQuery,
            new FakeCustomerQueryService(),
            new FakeProductQueryService(),
            currentContextProvider: new FakeCurrentContextProvider(actorId));

        var captured = await service.RecordRequest("Title", "Desc");

        // Cannot Escalate while CAPTURED
        var escalateCaptured = async () => await service.EscalateRequest(captured.Id, "Reason");
        await escalateCaptured.Should().ThrowAsync<InvalidRequestStateTransitionException>();

        // Move to EVALUATING -> REJECTED
        await service.AssignRequestOwner(captured.Id, ownerId);
        await service.RejectRequest(captured.Id, "Rejected");

        // Closed request rejects Escalate, RequestManagementDecision, and ReassignRequestOwnership
        var escalateRejected = async () => await service.EscalateRequest(captured.Id, "Reason");
        await escalateRejected.Should().ThrowAsync<InvalidRequestStateTransitionException>();

        var decisionRejected = async () => await service.RequestManagementDecision(captured.Id, "Decision");
        await decisionRejected.Should().ThrowAsync<InvalidRequestStateTransitionException>();

        var reassignRejected = async () => await service.ReassignRequestOwnership(captured.Id, otherOwnerId);
        await reassignRejected.Should().ThrowAsync<InvalidRequestStateTransitionException>();
    }

    [Fact]
    public void Escalation_and_management_command_validators_enforce_required_fields()
    {
        var escalateValidator = new EscalateRequestCommandValidator();
        escalateValidator.Validate(new EscalateRequestCommand(Guid.Empty, "Reason")).IsValid.Should().BeFalse();
        escalateValidator.Validate(new EscalateRequestCommand(Guid.NewGuid(), "")).IsValid.Should().BeFalse();
        escalateValidator.Validate(new EscalateRequestCommand(Guid.NewGuid(), "Reason", Guid.Empty)).IsValid.Should().BeFalse();
        escalateValidator.Validate(new EscalateRequestCommand(Guid.NewGuid(), "Valid reason")).IsValid.Should().BeTrue();

        var decisionValidator = new RequestManagementDecisionCommandValidator();
        decisionValidator.Validate(new RequestManagementDecisionCommand(Guid.Empty, "Details")).IsValid.Should().BeFalse();
        decisionValidator.Validate(new RequestManagementDecisionCommand(Guid.NewGuid(), " ")).IsValid.Should().BeFalse();
        decisionValidator.Validate(new RequestManagementDecisionCommand(Guid.NewGuid(), "Details", TargetStatus: RequestStatus.Completed)).IsValid.Should().BeFalse();
        decisionValidator.Validate(new RequestManagementDecisionCommand(Guid.NewGuid(), "Details", TargetStatus: RequestStatus.InProgress)).IsValid.Should().BeTrue();
        decisionValidator.Validate(new RequestManagementDecisionCommand(Guid.NewGuid(), "Details")).IsValid.Should().BeTrue();

        var reassignValidator = new ReassignRequestOwnershipCommandValidator();
        reassignValidator.Validate(new ReassignRequestOwnershipCommand(Guid.Empty, Guid.NewGuid())).IsValid.Should().BeFalse();
        reassignValidator.Validate(new ReassignRequestOwnershipCommand(Guid.NewGuid(), Guid.Empty)).IsValid.Should().BeFalse();
        reassignValidator.Validate(new ReassignRequestOwnershipCommand(Guid.NewGuid(), Guid.NewGuid(), TargetStatusForEscalated: RequestStatus.Rejected)).IsValid.Should().BeFalse();
        reassignValidator.Validate(new ReassignRequestOwnershipCommand(Guid.NewGuid(), Guid.NewGuid(), "Notes", TargetStatusForEscalated: RequestStatus.Evaluating)).IsValid.Should().BeTrue();
    }

    private sealed class InMemoryRequestRepository : IRequestRepository
    {
        private readonly Dictionary<Guid, Cakra.Modules.Request.Domain.Request> _requests = new();
        public Dictionary<Guid, RequestResolution> Resolutions { get; } = new();
        public List<RequestAssignment> Assignments { get; } = new();

        public Task<Cakra.Modules.Request.Domain.Request?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (!_requests.TryGetValue(id, out var req))
            {
                return Task.FromResult<Cakra.Modules.Request.Domain.Request?>(null);
            }

            Resolutions.TryGetValue(id, out var resolution);
            var reqAssignments = Assignments.Where(a => a.RequestId == id).OrderBy(a => a.AssignedAtUtc).ToList();

            var hydrated = Cakra.Modules.Request.Domain.Request.Rehydrate(
                req.Id,
                req.Title,
                req.Description,
                req.RequestType,
                req.Status,
                req.Priority,
                req.OwnerPersonId,
                req.CustomerId,
                req.ProductId,
                req.WorkPackageId,
                req.EvaluationNotes,
                req.EscalationReason,
                req.ManagementDecisionNotes,
                req.CreatedAt,
                req.UpdatedAt,
                resolution,
                reqAssignments);

            return Task.FromResult<Cakra.Modules.Request.Domain.Request?>(hydrated);
        }

        public Task<IReadOnlyList<Cakra.Modules.Request.Domain.Request>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Cakra.Modules.Request.Domain.Request>>(_requests.Values.ToList());

        public Task AddAsync(Cakra.Modules.Request.Domain.Request entity, CancellationToken cancellationToken = default)
        {
            _requests[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Cakra.Modules.Request.Domain.Request entity, CancellationToken cancellationToken = default)
        {
            _requests[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _requests.Remove(id);
            return Task.CompletedTask;
        }

        public Task AddAssignmentAsync(RequestAssignment assignment, CancellationToken cancellationToken = default)
        {
            Assignments.Add(assignment);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RequestAssignment>> GetAssignmentsByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestAssignment>>(
                Assignments.Where(a => a.RequestId == requestId).OrderBy(a => a.AssignedAtUtc).ToList());

        public Task AddResolutionAsync(RequestResolution resolution, CancellationToken cancellationToken = default)
        {
            Resolutions[resolution.RequestId] = resolution;
            return Task.CompletedTask;
        }

        public Task<RequestResolution?> GetResolutionByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Resolutions.TryGetValue(requestId, out var res) ? res : null);
    }

    private sealed class FakeOrganizationQueryService : IOrganizationQueryService
    {
        private readonly Dictionary<Guid, PersonDto> _persons = new();

        public void SetPerson(Guid id, bool isActive)
        {
            _persons[id] = new PersonDto
            {
                Id = id,
                FirstName = "Test",
                LastName = "Person",
                Email = $"person-{id:N}@cakra.id",
                Status = isActive ? "ACTIVE" : "INACTIVE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };
        }

        public Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_persons.TryGetValue(personId, out var p) && p.IsActive);

        public Task<PersonDto?> GetPersonByIdAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_persons.TryGetValue(personId, out var p) ? p : null);

        public Task<IReadOnlyList<string>> GetPersonRolesAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class FakeCustomerQueryService : ICustomerQueryService
    {
        public Task<CustomerDto?> GetCustomerByIdAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CustomerDto?>(null);

        public Task<CustomerDto?> GetCustomerByCodeAsync(string customerCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<CustomerDto?>(null);

        public Task<IReadOnlyList<CustomerDto>> ListActiveCustomersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerDto>>(Array.Empty<CustomerDto>());

        public Task<IReadOnlyList<CustomerDto>> ListAllCustomersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerDto>>(Array.Empty<CustomerDto>());

        public Task<IReadOnlyList<CustomerContactDto>> GetCustomerContactsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerContactDto>>(Array.Empty<CustomerContactDto>());

        public Task<CustomerWithContractStatusDto?> GetCustomerWithContractStatusAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CustomerWithContractStatusDto?>(null);

        public Task<bool> IsCustomerActiveAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeProductQueryService : IProductQueryService
    {
        public Task<ProductDto?> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductDto?>(null);

        public Task<ProductDto?> GetProductByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductDto?>(null);

        public Task<IReadOnlyList<ProductDto>> ListActiveProductsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductDto>>(Array.Empty<ProductDto>());

        public Task<IReadOnlyList<ProductDto>> ListAllProductsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductDto>>(Array.Empty<ProductDto>());

        public Task<bool> IsProductActiveAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> Events { get; } = new();

        public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(domainEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurrentContextProvider : ICurrentContextProvider
    {
        public FakeCurrentContextProvider(Guid? currentPersonId)
        {
            CurrentPersonId = currentPersonId;
            CurrentUserId = Guid.NewGuid();
        }

        public Guid? CurrentUserId { get; set; }
        public Guid? CurrentPersonId { get; set; }
        public IReadOnlyCollection<string> CurrentRoles { get; set; } = Array.Empty<string>();
    }

    private sealed class FakeSystemClock : ISystemClock
    {
        public FakeSystemClock(DateTime initialUtc)
        {
            UtcNow = initialUtc;
        }

        public DateTime UtcNow { get; private set; }

        public void AdvanceMinutes(double minutes) => UtcNow = UtcNow.AddMinutes(minutes);
    }
}
