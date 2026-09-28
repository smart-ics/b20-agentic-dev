namespace ICS.Tests.Unit;

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using ICS.Modules.WorkPackage.Application;
using ICS.Modules.WorkPackage.Application.Commands;
using ICS.Modules.WorkPackage.Domain;
using ICS.Modules.WorkPackage.Domain.Events;
using ICS.Modules.WorkPackage.Domain.Exceptions;
using Xunit;

/// <summary>
/// Unit tests for the Work Package application slice: MediatR command handlers, cross-module
/// reference validation against Organization, Customer, Product, and Request, and enforcement of
/// Business Rule 9 (a request may belong to at most one active Work Package).
/// Architecture §11, §15, §16, §19.2, §19.8.
/// </summary>
public class WorkPackageApplicationTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    // =========================================================================
    // Test harness
    // =========================================================================

    private sealed class Harness
    {
        public FakeWorkPackageRepository Repository { get; } = new();
        public FakeWorkPackageQueryService QueryService { get; }
        public StubOrganizationQueryService Organization { get; } = new();
        public StubCustomerQueryService Customer { get; } = new();
        public StubProductQueryService Product { get; } = new();
        public StubRequestQueryService Request { get; } = new();
        public RecordingDomainEventDispatcher Events { get; } = new();
        public WorkPackageReferenceValidator Validator { get; }

        public Harness()
        {
            QueryService = new FakeWorkPackageQueryService(Repository);
            Validator = new WorkPackageReferenceValidator(Organization, Customer, Product, Request);
        }

        public AddRequestToWorkPackageCommandHandler AddRequestHandler() =>
            new(Repository, QueryService, Validator, new FixedClock(FixedNow), Events);

        public CreateWorkPackageCommandHandler CreateHandler() =>
            new(Repository, QueryService, Validator, new FixedClock(FixedNow), Events);

        public UpdateWorkPackageObjectiveCommandHandler UpdateHandler() =>
            new(Repository, QueryService, Validator, new FixedClock(FixedNow), Events);

        public AssignWorkPackageOwnerCommandHandler AssignOwnerHandler() =>
            new(Repository, QueryService, Validator, new FixedClock(FixedNow), Events);

        public RemoveRequestFromWorkPackageCommandHandler RemoveRequestHandler() =>
            new(Repository, QueryService, Validator, new FixedClock(FixedNow), Events);

        public ActivateWorkPackageCommandHandler ActivateHandler() =>
            new(Repository, QueryService, Validator, new FixedClock(FixedNow), Events);

        public CloseWorkPackageCommandHandler CloseHandler() =>
            new(Repository, QueryService, Validator, new FixedClock(FixedNow), Events);
    }

    /// <summary>
    /// Seeds a DRAFT Work Package owned by an active Person and a Request that exists in the Request module.
    /// </summary>
    private static (Harness Harness, WorkPackage WorkPackage, Guid RequestId, Guid OwnerId) SeedDraft(Harness harness)
    {
        var ownerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        harness.Organization.AddActivePerson(ownerId, "Package Owner");
        harness.Request.AddRequest(requestId);

        var workPackage = WorkPackage.Create(
            Guid.NewGuid(),
            "Phase 1 Rollout",
            "Prepare go-live infrastructure",
            ownerId,
            createdAt: FixedNow);

        harness.Repository.Seed(workPackage);
        return (harness, workPackage, requestId, ownerId);
    }

    // =========================================================================
    // CreateWorkPackage — cross-module validation + WorkPackageCreated event
    // =========================================================================

    [Fact]
    public async Task CreateWorkPackage_WithValidReferences_ShouldPersistInDraft_AndEmitWorkPackageCreated()
    {
        var harness = new Harness();
        var ownerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        harness.Organization.AddActivePerson(ownerId, "Ada Owner");
        harness.Customer.AddActiveCustomer(customerId, "Acme");
        harness.Product.AddActiveProduct(productId, "ICS Core");

        var result = await harness.CreateHandler().Handle(
            new CreateWorkPackageCommand("Rollout", "Ship the release", ownerId, customerId, productId),
            CancellationToken.None);

        result.Id.Should().NotBe(Guid.Empty);
        result.Name.Should().Be("Rollout");
        result.Objective.Should().Be("Ship the release");
        result.Status.Should().Be(WorkPackageStatus.Draft);
        result.IsDraft.Should().BeTrue();
        result.OwnerPersonId.Should().Be(ownerId);
        result.CustomerId.Should().Be(customerId);
        result.ProductId.Should().Be(productId);
        result.ActiveRequestCount.Should().Be(0);

        harness.Events.Published.Should().ContainSingle()
            .Which.Should().BeOfType<WorkPackageCreated>();
    }

    [Fact]
    public async Task CreateWorkPackage_WithUnknownOwner_ShouldReject_WithoutPersisting()
    {
        var harness = new Harness();
        var unknownOwner = Guid.NewGuid();

        var act = () => harness.CreateHandler().Handle(
            new CreateWorkPackageCommand("Rollout", "Ship", unknownOwner),
            CancellationToken.None);

        await act.Should().ThrowAsync<WorkPackageReferenceValidationException>()
            .Where(e => e.ReferenceKind == "Person")
            .WithMessage("*does not exist in Organization*");

        (await harness.Repository.ListAsync(new WorkPackageGridFilterDto())).Should().BeEmpty();
        harness.Events.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateWorkPackage_WithInactiveOwner_ShouldReject()
    {
        var harness = new Harness();
        var ownerId = Guid.NewGuid();
        harness.Organization.AddInactivePerson(ownerId, "Retired Owner");

        var act = () => harness.CreateHandler().Handle(
            new CreateWorkPackageCommand("Rollout", "Ship", ownerId),
            CancellationToken.None);

        await act.Should().ThrowAsync<WorkPackageReferenceValidationException>()
            .WithMessage("*is not active in Organization*");
    }

    [Fact]
    public async Task CreateWorkPackage_WithUnknownCustomer_ShouldReject()
    {
        var harness = new Harness();
        var ownerId = Guid.NewGuid();
        harness.Organization.AddActivePerson(ownerId);

        var act = () => harness.CreateHandler().Handle(
            new CreateWorkPackageCommand("Rollout", "Ship", ownerId, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<WorkPackageReferenceValidationException>()
            .Where(e => e.ReferenceKind == "Customer")
            .WithMessage("*does not exist*");
    }

    [Fact]
    public async Task CreateWorkPackage_WithInactiveCustomer_ShouldReject()
    {
        var harness = new Harness();
        var ownerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        harness.Organization.AddActivePerson(ownerId);
        harness.Customer.AddInactiveCustomer(customerId, "Dormant Customer");

        var act = () => harness.CreateHandler().Handle(
            new CreateWorkPackageCommand("Rollout", "Ship", ownerId, customerId),
            CancellationToken.None);

        await act.Should().ThrowAsync<WorkPackageReferenceValidationException>()
            .Where(e => e.ReferenceKind == "Customer")
            .WithMessage("*is not active*");
    }

    [Fact]
    public async Task CreateWorkPackage_WithUnknownProduct_ShouldReject()
    {
        var harness = new Harness();
        var ownerId = Guid.NewGuid();
        harness.Organization.AddActivePerson(ownerId);

        var act = () => harness.CreateHandler().Handle(
            new CreateWorkPackageCommand("Rollout", "Ship", ownerId, null, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<WorkPackageReferenceValidationException>()
            .Where(e => e.ReferenceKind == "Product")
            .WithMessage("*does not exist*");
    }

    [Fact]
    public async Task CreateWorkPackage_WithInactiveProduct_ShouldReject()
    {
        var harness = new Harness();
        var ownerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        harness.Organization.AddActivePerson(ownerId);
        harness.Product.AddInactiveProduct(productId, "Retired Product");

        var act = () => harness.CreateHandler().Handle(
            new CreateWorkPackageCommand("Rollout", "Ship", ownerId, null, productId),
            CancellationToken.None);

        await act.Should().ThrowAsync<WorkPackageReferenceValidationException>()
            .Where(e => e.ReferenceKind == "Product")
            .WithMessage("*is not active*");
    }

    [Fact]
    public async Task CreateWorkPackage_ShouldRecordInitialStateHistory()
    {
        var harness = new Harness();
        var ownerId = Guid.NewGuid();
        harness.Organization.AddActivePerson(ownerId);

        await harness.CreateHandler().Handle(
            new CreateWorkPackageCommand("Rollout", "Ship", ownerId),
            CancellationToken.None);

        harness.Repository.StateHistory.Should().ContainSingle();
        var history = harness.Repository.StateHistory[0];
        history.FromStatus.Should().BeNull();
        history.ToStatus.Should().Be(WorkPackageStatus.Draft);
        history.ActorPersonId.Should().Be(ownerId);
    }

    // =========================================================================
    // Business Rule 9 — a request may belong to at most one active Work Package
    // =========================================================================

    [Fact]
    public async Task AddRequest_WhenRequestFree_ShouldSucceed_AndEmitRequestAddedToWorkPackage()
    {
        var (harness, workPackage, requestId, _) = SeedDraft(new Harness());

        var result = await harness.AddRequestHandler().Handle(
            new AddRequestToWorkPackageCommand(workPackage.Id, requestId),
            CancellationToken.None);

        result.ActiveRequestCount.Should().Be(1);
        result.Requests.Should().ContainSingle(m => m.RequestId == requestId && m.IsActive);

        harness.Events.Published.Should().ContainSingle()
            .Which.Should().BeOfType<RequestAddedToWorkPackage>();
    }

    [Fact]
    public async Task AddRequest_WhenRequestIsAlreadyActiveMemberOfSamePackage_ShouldReject_BusinessRule9()
    {
        var (harness, workPackage, requestId, _) = SeedDraft(new Harness());
        var handler = harness.AddRequestHandler();

        await handler.Handle(
            new AddRequestToWorkPackageCommand(workPackage.Id, requestId),
            CancellationToken.None);

        var act = () => handler.Handle(
            new AddRequestToWorkPackageCommand(workPackage.Id, requestId),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRule9ViolationException>()
            .Where(e => e.RequestId == requestId && e.WorkPackageId == workPackage.Id);
    }

    [Fact]
    public async Task AddRequest_WhenRequestIsActiveInAnotherActivePackage_ShouldReject_BusinessRule9()
    {
        var harness = new Harness();

        // Package A: activated, holds the request.
        var (seedA, packageA, requestId, _) = SeedDraft(harness);
        await seedA.ActivateHandler().Handle(
            new ActivateWorkPackageCommand(packageA.Id),
            CancellationToken.None);
        await seedA.AddRequestHandler().Handle(
            new AddRequestToWorkPackageCommand(packageA.Id, requestId),
            CancellationToken.None);

        // Package B: a different, active package that wants the same request.
        var ownerId = Guid.NewGuid();
        harness.Organization.AddActivePerson(ownerId, "Second Owner");
        var packageB = harness.Repository.Seed(WorkPackage.Create(
            Guid.NewGuid(),
            "Phase 2 Rollout",
            "Second wave",
            ownerId,
            createdAt: FixedNow));
        await harness.ActivateHandler().Handle(
            new ActivateWorkPackageCommand(packageB.Id),
            CancellationToken.None);

        var act = () => harness.AddRequestHandler().Handle(
            new AddRequestToWorkPackageCommand(packageB.Id, requestId),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRule9ViolationException>()
            .Where(e => e.RequestId == requestId && e.WorkPackageId == packageB.Id)
            .WithMessage($"*already an active member of Work Package '{packageA.Id}'*");

        // Business Rule 9 also observable through the published query contract
        (await harness.QueryService.IsRequestInActiveWorkPackageAsync(requestId)).Should().BeTrue();
    }

    [Fact]
    public async Task AddRequest_WhenRequestIsHeldByAnotherDraftPackage_ShouldReject_BusinessRule9()
    {
        var harness = new Harness();
        var (seeded, packageA, requestId, _) = SeedDraft(harness);
        await seeded.AddRequestHandler().Handle(
            new AddRequestToWorkPackageCommand(packageA.Id, requestId),
            CancellationToken.None);

        var ownerId = Guid.NewGuid();
        harness.Organization.AddActivePerson(ownerId);
        var packageB = harness.Repository.Seed(WorkPackage.Create(
            Guid.NewGuid(), "Other Draft", "Another draft", ownerId, createdAt: FixedNow));

        var act = () => harness.AddRequestHandler().Handle(
            new AddRequestToWorkPackageCommand(packageB.Id, requestId),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRule9ViolationException>()
            .WithMessage($"*already belongs to Work Package '{packageA.Id}'*");
    }

    [Fact]
    public async Task AddRequest_WhenPreviousPackageIsClosed_ShouldSucceed_BusinessRule9AllowsReuse()
    {
        var harness = new Harness();
        var (seedA, packageA, requestId, _) = SeedDraft(harness);

        await seedA.ActivateHandler().Handle(
            new ActivateWorkPackageCommand(packageA.Id),
            CancellationToken.None);
        await seedA.AddRequestHandler().Handle(
            new AddRequestToWorkPackageCommand(packageA.Id, requestId),
            CancellationToken.None);
        await seedA.CloseHandler().Handle(
            new CloseWorkPackageCommand(packageA.Id, "Phase complete"),
            CancellationToken.None);

        // The request is no longer in an active Work Package, so a new package may take it.
        (await harness.QueryService.IsRequestInActiveWorkPackageAsync(requestId)).Should().BeFalse();

        var ownerId = Guid.NewGuid();
        harness.Organization.AddActivePerson(ownerId);
        var packageB = harness.Repository.Seed(WorkPackage.Create(
            Guid.NewGuid(), "Phase 2", "Second wave", ownerId, createdAt: FixedNow));

        var result = await harness.AddRequestHandler().Handle(
            new AddRequestToWorkPackageCommand(packageB.Id, requestId),
            CancellationToken.None);

        result.ActiveRequestCount.Should().Be(1);
        result.Requests.Should().ContainSingle(m => m.RequestId == requestId && m.IsActive);
    }

    [Fact]
    public async Task AddRequest_WhenRequestIsRemovedFromPackage_ShouldAllowReAddToSamePackage()
    {
        var harness = new Harness();
        var (seeded, workPackage, requestId, _) = SeedDraft(harness);
        var addHandler = seeded.AddRequestHandler();

        await addHandler.Handle(
            new AddRequestToWorkPackageCommand(workPackage.Id, requestId),
            CancellationToken.None);
        await seeded.RemoveRequestHandler().Handle(
            new RemoveRequestFromWorkPackageCommand(workPackage.Id, requestId),
            CancellationToken.None);

        var result = await addHandler.Handle(
            new AddRequestToWorkPackageCommand(workPackage.Id, requestId),
            CancellationToken.None);

        result.ActiveRequestCount.Should().Be(1);
    }

    [Fact]
    public async Task AddRequest_WithUnknownRequest_ShouldReject_CrossModuleValidation()
    {
        var (harness, workPackage, _, _) = SeedDraft(new Harness());

        // Note: no request was registered with the Request module double.
        var act = () => harness.AddRequestHandler().Handle(
            new AddRequestToWorkPackageCommand(workPackage.Id, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<WorkPackageReferenceValidationException>()
            .Where(e => e.ReferenceKind == "Request")
            .WithMessage("*does not exist*");

        harness.Events.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task AddRequest_ToClosedPackage_ShouldReject()
    {
        var harness = new Harness();
        var (seeded, workPackage, requestId, _) = SeedDraft(harness);
        await seeded.CloseHandler().Handle(
            new CloseWorkPackageCommand(workPackage.Id, "Done"),
            CancellationToken.None);

        var act = () => harness.AddRequestHandler().Handle(
            new AddRequestToWorkPackageCommand(workPackage.Id, requestId),
            CancellationToken.None);

        await act.Should().ThrowAsync<WorkPackageDomainException>()
            .WithMessage("*Cannot add requests to a closed Work Package*");
    }

    [Fact]
    public async Task AddRequest_WithEmptyRequestId_ShouldReject_CrossModuleValidation()
    {
        var (harness, workPackage, _, _) = SeedDraft(new Harness());

        var act = () => harness.AddRequestHandler().Handle(
            new AddRequestToWorkPackageCommand(workPackage.Id, Guid.Empty),
            CancellationToken.None);

        await act.Should().ThrowAsync<WorkPackageReferenceValidationException>()
            .WithMessage("*RequestId cannot be empty*");
    }

    [Fact]
    public async Task AddRequest_ToMissingPackage_ShouldReject_NotFound()
    {
        var harness = new Harness();
        var requestId = Guid.NewGuid();
        harness.Request.AddRequest(requestId);

        var act = () => harness.AddRequestHandler().Handle(
            new AddRequestToWorkPackageCommand(Guid.NewGuid(), requestId),
            CancellationToken.None);

        await act.Should().ThrowAsync<WorkPackageNotFoundException>();
    }

    // =========================================================================
    // RemoveRequestFromWorkPackage
    // =========================================================================

    [Fact]
    public async Task RemoveRequest_ShouldDeactivateMembership_AndEmitRequestRemovedFromWorkPackage()
    {
        var harness = new Harness();
        var (seeded, workPackage, requestId, _) = SeedDraft(new Harness());

        await seeded.AddRequestHandler().Handle(
            new AddRequestToWorkPackageCommand(workPackage.Id, requestId),
            CancellationToken.None);
        seeded.Events.Published.Clear();

        var result = await seeded.RemoveRequestHandler().Handle(
            new RemoveRequestFromWorkPackageCommand(workPackage.Id, requestId),
            CancellationToken.None);

        result.ActiveRequestCount.Should().Be(0);

        // Historical trace is preserved (Business Rule 15)
        result.Requests.Should().ContainSingle(m => m.RequestId == requestId && !m.IsActive);

        seeded.Events.Published.Should().ContainSingle()
            .Which.Should().BeOfType<RequestRemovedFromWorkPackage>();
    }

    [Fact]
    public async Task RemoveRequest_WhenNotAMember_ShouldReject()
    {
        var (harness, workPackage, _, _) = SeedDraft(new Harness());

        var act = () => harness.RemoveRequestHandler().Handle(
            new RemoveRequestFromWorkPackageCommand(workPackage.Id, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<WorkPackageDomainException>()
            .WithMessage("*is not an active member*");
    }

    // =========================================================================
    // Lifecycle transitions
    // =========================================================================

    [Fact]
    public async Task Activate_FromDraft_ShouldBecomeActive_EmitEvent_AndRecordHistory()
    {
        var (harness, workPackage, _, ownerId) = SeedDraft(new Harness());

        var result = await harness.ActivateHandler().Handle(
            new ActivateWorkPackageCommand(workPackage.Id),
            CancellationToken.None);

        result.Status.Should().Be(WorkPackageStatus.Active);
        result.IsActive.Should().BeTrue();

        harness.Events.Published.Should().ContainSingle()
            .Which.Should().BeOfType<WorkPackageActivated>();

        harness.Repository.StateHistory.Should().ContainSingle();
        var history = harness.Repository.StateHistory[0];
        history.FromStatus.Should().Be(WorkPackageStatus.Draft);
        history.ToStatus.Should().Be(WorkPackageStatus.Active);
        history.ActorPersonId.Should().Be(ownerId);
    }

    [Fact]
    public async Task Activate_WhenAlreadyActive_ShouldReject_InvalidTransition()
    {
        var (harness, workPackage, _, _) = SeedDraft(new Harness());
        var handler = harness.ActivateHandler();

        await handler.Handle(new ActivateWorkPackageCommand(workPackage.Id), CancellationToken.None);

        var act = () => handler.Handle(new ActivateWorkPackageCommand(workPackage.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidWorkPackageStateTransitionException>();
    }

    [Fact]
    public async Task Close_FromActive_ShouldBecomeClosed_EmitEvent_AndRecordHistory()
    {
        var (harness, workPackage, _, _) = SeedDraft(new Harness());

        await harness.ActivateHandler().Handle(
            new ActivateWorkPackageCommand(workPackage.Id),
            CancellationToken.None);
        harness.Events.Published.Clear();

        var result = await harness.CloseHandler().Handle(
            new CloseWorkPackageCommand(workPackage.Id, "Objective delivered"),
            CancellationToken.None);

        result.Status.Should().Be(WorkPackageStatus.Closed);
        result.IsClosed.Should().BeTrue();
        result.CloseReason.Should().Be("Objective delivered");
        result.ClosedAt.Should().Be(FixedNow);

        harness.Events.Published.Should().ContainSingle()
            .Which.Should().BeOfType<WorkPackageClosed>();

        harness.Repository.StateHistory.Should().ContainSingle(h =>
            h.FromStatus == WorkPackageStatus.Active &&
            h.ToStatus == WorkPackageStatus.Closed);
    }

    [Fact]
    public async Task Close_DirectlyFromDraft_ShouldSucceed()
    {
        var (harness, workPackage, _, _) = SeedDraft(new Harness());

        var result = await harness.CloseHandler().Handle(
            new CloseWorkPackageCommand(workPackage.Id, "Cancelled before start"),
            CancellationToken.None);

        result.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task Close_WhenAlreadyClosed_ShouldReject_InvalidTransition()
    {
        var (harness, workPackage, _, _) = SeedDraft(new Harness());
        var handler = harness.CloseHandler();

        await handler.Handle(new CloseWorkPackageCommand(workPackage.Id, "Done"), CancellationToken.None);

        var act = () => handler.Handle(new CloseWorkPackageCommand(workPackage.Id, "Again"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidWorkPackageStateTransitionException>();
    }

    // =========================================================================
    // UpdateObjective / AssignOwner
    // =========================================================================

    [Fact]
    public async Task UpdateObjective_ShouldPersistNewNameAndObjective()
    {
        var (harness, workPackage, _, _) = SeedDraft(new Harness());

        var result = await harness.UpdateHandler().Handle(
            new UpdateWorkPackageObjectiveCommand(workPackage.Id, "New Name", "New Objective"),
            CancellationToken.None);

        result.Name.Should().Be("New Name");
        result.Objective.Should().Be("New Objective");
    }

    [Fact]
    public async Task UpdateObjective_WhenClosed_ShouldReject()
    {
        var (harness, workPackage, _, _) = SeedDraft(new Harness());
        await harness.CloseHandler().Handle(
            new CloseWorkPackageCommand(workPackage.Id, "Done"),
            CancellationToken.None);

        var act = () => harness.UpdateHandler().Handle(
            new UpdateWorkPackageObjectiveCommand(workPackage.Id, "New", "New"),
            CancellationToken.None);

        await act.Should().ThrowAsync<WorkPackageDomainException>()
            .WithMessage("*closed*");
    }

    [Fact]
    public async Task AssignOwner_WithActivePerson_ShouldReassign_AndEmitWorkPackageOwnerChanged()
    {
        var (harness, workPackage, _, previousOwnerId) = SeedDraft(new Harness());
        var newOwnerId = Guid.NewGuid();
        harness.Organization.AddActivePerson(newOwnerId, "New Owner");

        var result = await harness.AssignOwnerHandler().Handle(
            new AssignWorkPackageOwnerCommand(workPackage.Id, newOwnerId),
            CancellationToken.None);

        result.OwnerPersonId.Should().Be(newOwnerId);

        var ownerChanged = harness.Events.Published
            .OfType<WorkPackageOwnerChanged>()
            .Should().ContainSingle().Subject;

        ownerChanged.PreviousOwnerPersonId.Should().Be(previousOwnerId);
        ownerChanged.NewOwnerPersonId.Should().Be(newOwnerId);
    }

    [Fact]
    public async Task AssignOwner_WithUnknownPerson_ShouldReject_WithoutChangingOwner()
    {
        var (harness, workPackage, _, originalOwnerId) = SeedDraft(new Harness());

        var act = () => harness.AssignOwnerHandler().Handle(
            new AssignWorkPackageOwnerCommand(workPackage.Id, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<WorkPackageReferenceValidationException>()
            .Where(e => e.ReferenceKind == "Person")
            .WithMessage("*new Work Package owner*does not exist*");

        workPackage.OwnerPersonId.Should().Be(originalOwnerId);
    }

    [Fact]
    public async Task AssignOwner_ToSameOwner_ShouldBeNoOp_AndEmitNoEvent()
    {
        var (harness, workPackage, _, ownerId) = SeedDraft(new Harness());
        harness.Events.Published.Clear();

        var result = await harness.AssignOwnerHandler().Handle(
            new AssignWorkPackageOwnerCommand(workPackage.Id, ownerId),
            CancellationToken.None);

        result.OwnerPersonId.Should().Be(ownerId);
        harness.Events.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignOwner_WhenClosed_ShouldReject()
    {
        var (harness, workPackage, _, _) = SeedDraft(new Harness());
        await harness.CloseHandler().Handle(
            new CloseWorkPackageCommand(workPackage.Id, "Done"),
            CancellationToken.None);

        var newOwnerId = Guid.NewGuid();
        harness.Organization.AddActivePerson(newOwnerId);

        var act = () => harness.AssignOwnerHandler().Handle(
            new AssignWorkPackageOwnerCommand(workPackage.Id, newOwnerId),
            CancellationToken.None);

        await act.Should().ThrowAsync<WorkPackageDomainException>()
            .WithMessage("*closed*");
    }

    // =========================================================================
    // Query service projections
    // =========================================================================

    [Fact]
    public async Task GetWorkPackageScope_ShouldReturnActiveAndHistoricalMemberships()
    {
        var harness = new Harness();
        var (seeded, workPackage, requestId, _) = SeedDraft(harness);
        var secondRequestId = Guid.NewGuid();
        seeded.Request.AddRequest(secondRequestId);

        await seeded.AddRequestHandler().Handle(
            new AddRequestToWorkPackageCommand(workPackage.Id, requestId),
            CancellationToken.None);
        await seeded.AddRequestHandler().Handle(
            new AddRequestToWorkPackageCommand(workPackage.Id, secondRequestId),
            CancellationToken.None);
        await seeded.RemoveRequestHandler().Handle(
            new RemoveRequestFromWorkPackageCommand(workPackage.Id, requestId),
            CancellationToken.None);

        var scope = await seeded.QueryService.GetWorkPackageScopeAsync(workPackage.Id);

        scope.Should().NotBeNull();
        scope!.ActiveRequests.Should().ContainSingle(m => m.RequestId == secondRequestId);
        scope.AllRequests.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRequestWorkPackage_ShouldReturnActiveMembership_ThenNullAfterClose()
    {
        var harness = new Harness();
        var (seeded, workPackage, requestId, _) = SeedDraft(harness);

        await seeded.AddRequestHandler().Handle(
            new AddRequestToWorkPackageCommand(workPackage.Id, requestId),
            CancellationToken.None);

        var membership = await seeded.QueryService.GetRequestWorkPackageAsync(requestId);
        membership.Should().NotBeNull();
        membership!.WorkPackageId.Should().Be(workPackage.Id);

        await seeded.CloseHandler().Handle(
            new CloseWorkPackageCommand(workPackage.Id, "Done"),
            CancellationToken.None);

        // The historical link persists but the aggregate no longer holds an active request.
        (await seeded.QueryService.GetRequestWorkPackageAsync(requestId)).Should().NotBeNull();
    }

    [Fact]
    public async Task GetWorkPackageById_ForUnknownPackage_ShouldReturnNull()
    {
        var harness = new Harness();
        (await harness.QueryService.GetWorkPackageByIdAsync(Guid.NewGuid())).Should().BeNull();
        (await harness.QueryService.GetWorkPackageByIdAsync(Guid.Empty)).Should().BeNull();
    }

    [Fact]
    public async Task ListWorkPackages_ShouldReturnAllSeededPackages()
    {
        var harness = new Harness();
        SeedDraft(harness);
        SeedDraft(harness);

        var grid = await harness.QueryService.ListWorkPackagesAsync();

        grid.TotalCount.Should().Be(2);
        grid.Items.Should().HaveCount(2);
    }
}
