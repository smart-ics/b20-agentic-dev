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
/// Unit tests for P4-S19 Request Module — Lifecycle Completion Commands &amp; Queries:
/// - Published visibility of IRequestQueryService, RequestQueryService, DTOs, Commands, and Queries
/// - Full happy-path lifecycle: Record -&gt; Assign -&gt; Evaluate -&gt; Accept -&gt; Complete
/// - Rejection flow: Record -&gt; Assign -&gt; Evaluate -&gt; Reject
/// - Audit trail recording in RequestAssignments and resolution persistence in RequestResolutions
/// - Domain event emission (RequestAccepted, RequestRejected, RequestCompleted)
/// - Invalid state transition rejections and FluentValidation rules
/// </summary>
public sealed class RequestCompletionAndQueriesTests
{
    [Fact]
    public void Published_query_contracts_and_completion_commands_are_publicly_accessible()
    {
        typeof(IRequestQueryService).IsPublic.Should().BeTrue();
        typeof(RequestQueryService).IsPublic.Should().BeTrue();
        typeof(RequestGridFilter).IsPublic.Should().BeTrue();
        typeof(PagedRequestGridResult).IsPublic.Should().BeTrue();
        typeof(RequestDetailDto).IsPublic.Should().BeTrue();
        typeof(RequestStateHistoryItemDto).IsPublic.Should().BeTrue();

        typeof(AcceptRequestResponsibilityCommand).IsPublic.Should().BeTrue();
        typeof(RejectRequestCommand).IsPublic.Should().BeTrue();
        typeof(ReviewRequestCompletionCommand).IsPublic.Should().BeTrue();
        typeof(CompleteRequestCommand).IsPublic.Should().BeTrue();

        typeof(GetRequestByIdQuery).IsPublic.Should().BeTrue();
        typeof(GetRequestStateHistoryQuery).IsPublic.Should().BeTrue();
        typeof(ListMyAssignedRequestsQuery).IsPublic.Should().BeTrue();
        typeof(GetFilteredRequestGridQuery).IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task Full_request_lifecycle_Record_Assign_Evaluate_Accept_Complete_records_audit_resolution_and_events()
    {
        var recorderId = Guid.NewGuid();
        var programmerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var repo = new InMemoryRequestRepository();
        var orgQuery = new FakeOrganizationQueryService();
        orgQuery.SetPerson(recorderId, isActive: true);
        orgQuery.SetPerson(programmerId, isActive: true);

        var customerQuery = new FakeCustomerQueryService();
        customerQuery.SetCustomer(customerId, isActive: true);

        var productQuery = new FakeProductQueryService();
        productQuery.SetProduct(productId, isActive: true);

        var dispatcher = new RecordingDomainEventDispatcher();
        var contextProvider = new FakeCurrentContextProvider(recorderId);
        var clock = new FakeSystemClock(new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc));

        var service = new RequestService(
            repo,
            orgQuery,
            customerQuery,
            productQuery,
            dispatcher,
            contextProvider,
            auditContext: null,
            clock: clock);

        // 1. Record -> CAPTURED
        var recorded = await service.Handle(
            new RecordRequestCommand(
                Title: "Radiology PACS DICOM export truncation",
                Description: "Multi-frame ultrasound studies truncate metadata on export.",
                CustomerId: customerId,
                ProductId: productId,
                RequestType: "Bug",
                Priority: "URGENT"),
            CancellationToken.None);

        recorded.Status.Should().Be(RequestStatusNames.Captured);

        // 2. Assign -> EVALUATING
        clock.AdvanceMinutes(10);
        var assigned = await service.Handle(
            new AssignRequestOwnerCommand(
                RequestId: recorded.Id,
                OwnerPersonId: programmerId,
                Notes: "Assigned to PACS integration owner"),
            CancellationToken.None);

        assigned.Status.Should().Be(RequestStatusNames.Evaluating);
        assigned.OwnerPersonId.Should().Be(programmerId);

        // 3. Evaluate -> EVALUATING
        clock.AdvanceMinutes(15);
        contextProvider.CurrentPersonId = programmerId;

        var evaluated = await service.Handle(
            new EvaluateRequestCommand(
                RequestId: recorded.Id,
                EvaluationNotes: "Buffer size in DICOM tag writer is capped at 64KB; needs dynamic stream allocation."),
            CancellationToken.None);

        evaluated.Status.Should().Be(RequestStatusNames.Evaluating);
        evaluated.EvaluationNotes.Should().Contain("DICOM tag writer");

        // 4. AcceptRequestResponsibility -> transitions to IN_PROGRESS, emits RequestAccepted
        clock.AdvanceMinutes(5);
        var accepted = await service.Handle(
            new AcceptRequestResponsibilityCommand(
                RequestId: recorded.Id,
                Notes: "Accepted responsibility and starting fix"),
            CancellationToken.None);

        accepted.Status.Should().Be(RequestStatusNames.InProgress);
        accepted.OwnerPersonId.Should().Be(programmerId);

        dispatcher.Events.OfType<RequestAccepted>().Should().ContainSingle(e =>
            e.RequestId == recorded.Id &&
            e.OwnerPersonId == programmerId &&
            e.Notes == "Accepted responsibility and starting fix");

        repo.Assignments.Should().Contain(a =>
            a.RequestId == recorded.Id &&
            a.PreviousStatus == RequestStatus.Evaluating &&
            a.NewStatus == RequestStatus.Accepted &&
            a.ActorPersonId == programmerId);

        repo.Assignments.Should().Contain(a =>
            a.RequestId == recorded.Id &&
            a.PreviousStatus == RequestStatus.Accepted &&
            a.NewStatus == RequestStatus.InProgress &&
            a.ActorPersonId == programmerId);

        // 5. ReviewRequestCompletion -> transitions to COMPLETED, records resolution and audit, emits RequestCompleted
        clock.AdvanceMinutes(90);
        var completed = await service.Handle(
            new ReviewRequestCompletionCommand(
                RequestId: recorded.Id,
                ResolutionDescription: "Replaced fixed 64KB buffer with RecyclableMemoryStream and verified 500MB DICOM export."),
            CancellationToken.None);

        completed.Status.Should().Be(RequestStatusNames.Completed);
        completed.Resolution.Should().NotBeNull();
        completed.Resolution!.Outcome.Should().Be(ResolutionOutcomeNames.Completed);
        completed.Resolution.Description.Should().Be("Replaced fixed 64KB buffer with RecyclableMemoryStream and verified 500MB DICOM export.");
        completed.Resolution.ResolvedBy.Should().Be(programmerId);

        repo.Resolutions.Should().ContainKey(recorded.Id);
        repo.Resolutions[recorded.Id].Outcome.Should().Be(ResolutionOutcomeNames.Completed);

        repo.Assignments.Last().Should().BeEquivalentTo(new
        {
            RequestId = recorded.Id,
            PreviousStatus = (RequestStatus?)RequestStatus.InProgress,
            NewStatus = RequestStatus.Completed,
            ActorPersonId = programmerId
        }, options => options.ExcludingMissingMembers());

        dispatcher.Events.OfType<RequestCompleted>().Should().ContainSingle(e =>
            e.RequestId == recorded.Id &&
            e.CompletedByPersonId == programmerId &&
            e.ResolutionDescription.Contains("RecyclableMemoryStream"));
    }

    [Fact]
    public async Task RejectRequest_from_Evaluating_transitions_to_Rejected_records_resolution_and_audit_and_emits_RequestRejected()
    {
        var recorderId = Guid.NewGuid();
        var programmerId = Guid.NewGuid();

        var repo = new InMemoryRequestRepository();
        var orgQuery = new FakeOrganizationQueryService();
        orgQuery.SetPerson(recorderId, isActive: true);
        orgQuery.SetPerson(programmerId, isActive: true);

        var customerQuery = new FakeCustomerQueryService();
        var productQuery = new FakeProductQueryService();
        var dispatcher = new RecordingDomainEventDispatcher();
        var contextProvider = new FakeCurrentContextProvider(recorderId);
        var clock = new FakeSystemClock(new DateTime(2026, 9, 28, 9, 0, 0, DateTimeKind.Utc));

        var service = new RequestService(
            repo,
            orgQuery,
            customerQuery,
            productQuery,
            dispatcher,
            contextProvider,
            auditContext: null,
            clock: clock);

        var recorded = await service.RecordRequest(
            title: "Custom payroll tax calculation for external vendor",
            description: "Request to add payroll tax engine inside HIS.",
            requestType: "Feature");

        await service.AssignRequestOwner(recorded.Id, programmerId, "Triage assignment");

        contextProvider.CurrentPersonId = programmerId;
        await service.EvaluateRequest(recorded.Id, "Out of scope for hospital clinical system; belongs to ERP.");

        clock.AdvanceMinutes(10);
        var rejected = await service.Handle(
            new RejectRequestCommand(
                RequestId: recorded.Id,
                Reason: "Out of product scope: HR payroll is excluded from CAKRA HIS boundary."),
            CancellationToken.None);

        rejected.Status.Should().Be(RequestStatusNames.Rejected);
        rejected.Resolution.Should().NotBeNull();
        rejected.Resolution!.Outcome.Should().Be(ResolutionOutcomeNames.Rejected);
        rejected.Resolution.Description.Should().Be("Out of product scope: HR payroll is excluded from CAKRA HIS boundary.");
        rejected.Resolution.ResolvedBy.Should().Be(programmerId);

        repo.Resolutions.Should().ContainKey(recorded.Id);
        repo.Resolutions[recorded.Id].Outcome.Should().Be(ResolutionOutcomeNames.Rejected);

        repo.Assignments.Last().PreviousStatus.Should().Be(RequestStatus.Evaluating);
        repo.Assignments.Last().NewStatus.Should().Be(RequestStatus.Rejected);
        repo.Assignments.Last().ActorPersonId.Should().Be(programmerId);

        dispatcher.Events.OfType<RequestRejected>().Should().ContainSingle(e =>
            e.RequestId == recorded.Id &&
            e.RejectedByPersonId == programmerId &&
            e.RejectionReason == "Out of product scope: HR payroll is excluded from CAKRA HIS boundary.");
    }

    [Fact]
    public async Task Invalid_state_transitions_on_Accept_Reject_and_Complete_throw_InvalidRequestStateTransitionException()
    {
        var actorId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var repo = new InMemoryRequestRepository();
        var orgQuery = new FakeOrganizationQueryService();
        orgQuery.SetPerson(actorId, isActive: true);
        orgQuery.SetPerson(ownerId, isActive: true);

        var service = new RequestService(
            repo,
            orgQuery,
            new FakeCustomerQueryService(),
            new FakeProductQueryService(),
            currentContextProvider: new FakeCurrentContextProvider(actorId));

        var captured = await service.RecordRequest("Title", "Description");

        // Cannot Accept, Reject, or Complete while CAPTURED
        var acceptFromCaptured = async () => await service.AcceptRequestResponsibility(captured.Id);
        await acceptFromCaptured.Should().ThrowAsync<InvalidRequestStateTransitionException>();

        var rejectFromCaptured = async () => await service.RejectRequest(captured.Id, "Reason");
        await rejectFromCaptured.Should().ThrowAsync<InvalidRequestStateTransitionException>();

        var completeFromCaptured = async () => await service.ReviewRequestCompletion(captured.Id, "Done");
        await completeFromCaptured.Should().ThrowAsync<InvalidRequestStateTransitionException>();

        // Transition to EVALUATING
        await service.AssignRequestOwner(captured.Id, ownerId);

        // Cannot Complete directly from EVALUATING
        var completeFromEvaluating = async () => await service.ReviewRequestCompletion(captured.Id, "Done");
        await completeFromEvaluating.Should().ThrowAsync<InvalidRequestStateTransitionException>();

        // Transition to IN_PROGRESS
        await service.AcceptRequestResponsibility(captured.Id);

        // Cannot Reject once IN_PROGRESS
        var rejectFromInProgress = async () => await service.RejectRequest(captured.Id, "Too late to reject");
        await rejectFromInProgress.Should().ThrowAsync<InvalidRequestStateTransitionException>();

        // Transition to COMPLETED
        await service.ReviewRequestCompletion(captured.Id, "Completed successfully");

        // Cannot Complete or Accept once COMPLETED
        var completeAgain = async () => await service.ReviewRequestCompletion(captured.Id, "Again");
        await completeAgain.Should().ThrowAsync<InvalidRequestStateTransitionException>();
    }

    [Fact]
    public void Completion_command_validators_enforce_required_fields()
    {
        var acceptValidator = new AcceptRequestResponsibilityCommandValidator();
        acceptValidator.Validate(new AcceptRequestResponsibilityCommand(Guid.Empty)).IsValid.Should().BeFalse();
        acceptValidator.Validate(new AcceptRequestResponsibilityCommand(Guid.NewGuid(), ActorPersonId: Guid.Empty)).IsValid.Should().BeFalse();
        acceptValidator.Validate(new AcceptRequestResponsibilityCommand(Guid.NewGuid(), "Notes")).IsValid.Should().BeTrue();

        var rejectValidator = new RejectRequestCommandValidator();
        rejectValidator.Validate(new RejectRequestCommand(Guid.Empty, "Reason")).IsValid.Should().BeFalse();
        rejectValidator.Validate(new RejectRequestCommand(Guid.NewGuid(), "")).IsValid.Should().BeFalse();
        rejectValidator.Validate(new RejectRequestCommand(Guid.NewGuid(), "Valid reason")).IsValid.Should().BeTrue();

        var completeValidator = new ReviewRequestCompletionCommandValidator();
        completeValidator.Validate(new ReviewRequestCompletionCommand(Guid.Empty, "Resolution")).IsValid.Should().BeFalse();
        completeValidator.Validate(new ReviewRequestCompletionCommand(Guid.NewGuid(), "   ")).IsValid.Should().BeFalse();
        completeValidator.Validate(new ReviewRequestCompletionCommand(Guid.NewGuid(), "Resolved issue")).IsValid.Should().BeTrue();
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
        private readonly Dictionary<Guid, CustomerDto> _customers = new();

        public void SetCustomer(Guid id, bool isActive)
        {
            _customers[id] = new CustomerDto
            {
                Id = id,
                CustomerCode = $"CUST-{id:N}"[..12],
                CustomerName = "Test Customer",
                Status = isActive ? "ACTIVE" : "INACTIVE",
                HasActiveMaintenanceContract = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        public Task<CustomerDto?> GetCustomerByIdAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_customers.TryGetValue(customerId, out var c) ? c : null);

        public Task<CustomerDto?> GetCustomerByCodeAsync(string customerCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(_customers.Values.FirstOrDefault(c => c.CustomerCode == customerCode));

        public Task<IReadOnlyList<CustomerDto>> ListActiveCustomersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerDto>>(_customers.Values.Where(c => c.IsActive).ToList());

        public Task<IReadOnlyList<CustomerDto>> ListAllCustomersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerDto>>(_customers.Values.ToList());

        public Task<IReadOnlyList<CustomerContactDto>> GetCustomerContactsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerContactDto>>(Array.Empty<CustomerContactDto>());

        public Task<CustomerWithContractStatusDto?> GetCustomerWithContractStatusAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CustomerWithContractStatusDto?>(null);

        public Task<bool> IsCustomerActiveAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_customers.TryGetValue(customerId, out var c) && c.IsActive);
    }

    private sealed class FakeProductQueryService : IProductQueryService
    {
        private readonly Dictionary<Guid, ProductDto> _products = new();

        public void SetProduct(Guid id, bool isActive)
        {
            _products[id] = new ProductDto
            {
                Id = id,
                Code = $"PRD-{id:N}"[..12],
                Name = "Test Product",
                OwnerPersonId = Guid.NewGuid(),
                Status = isActive ? "ACTIVE" : "INACTIVE",
                CreatedAt = DateTime.UtcNow
            };
        }

        public Task<ProductDto?> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_products.TryGetValue(productId, out var p) ? p : null);

        public Task<ProductDto?> GetProductByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(_products.Values.FirstOrDefault(p => p.Code == code));

        public Task<IReadOnlyList<ProductDto>> ListActiveProductsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductDto>>(_products.Values.Where(p => p.IsActive).ToList());

        public Task<IReadOnlyList<ProductDto>> ListAllProductsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductDto>>(_products.Values.ToList());

        public Task<bool> IsProductActiveAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_products.TryGetValue(productId, out var p) && p.IsActive);
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
