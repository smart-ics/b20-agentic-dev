namespace ICS.Tests.Integration;

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using ICS.Modules.Customer;
using ICS.Modules.Customer.Application;
using ICS.Modules.Organization;
using ICS.Modules.Organization.Application;
using ICS.Modules.Product;
using ICS.Modules.Product.Application;
using ICS.Modules.Request;
using ICS.Modules.Request.Application;
using ICS.Modules.Request.Domain;
using ICS.Modules.WorkPackage;
using ICS.Modules.WorkPackage.Application;
using ICS.Modules.WorkPackage.Domain;
using ICS.Modules.WorkPackage.Domain.Events;
using ICS.Modules.WorkPackage.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Integration tests for the Work Package application slice against a real SQL Server test
/// database: full DRAFT -> ACTIVE -> CLOSED lifecycle, request membership management, and
/// Business Rule 9 enforcement across Work Packages.
/// Architecture §11, §16, §17, §19.8.
/// </summary>
public class WorkPackageIntegrationTests : IntegrationTestBase
{
    public WorkPackageIntegrationTests(IcsWebApplicationFactory factory) : base(factory)
    {
    }

    // =========================================================================
    // Seeding helpers
    // =========================================================================

    private async Task<(Guid PersonId, Guid CustomerId, Guid ProductId, Guid RequestId)> SeedMasterDataAsync(
        IServiceProvider provider)
    {
        var organizationService = provider.GetRequiredService<IOrganizationService>();
        var customerService = provider.GetRequiredService<ICustomerService>();
        var productService = provider.GetRequiredService<IProductService>();
        var requestService = provider.GetRequiredService<IRequestService>();

        var owner = await organizationService.CreatePersonAsync(
            "Work Package Owner",
            $"wp.owner.{Guid.NewGuid():N}@example.com");

        var customerCode = $"CUST-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var customer = await customerService.CreateCustomerAsync(customerCode, "General Hospital");

        var productCode = $"PRD-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var product = await productService.CreateProductAsync(
            productCode,
            "Claims Suite",
            "Insurance claim processing",
            owner.PersonId);

        var request = await requestService.RecordRequestAsync(
            title: "Add BPJS validation to billing",
            description: "Billing submissions require BPJS card validation before dispatch.",
            type: Request.TypeFeature,
            assignedByPersonId: owner.PersonId);

        return (owner.PersonId, customer.CustomerId, product.ProductId, request.RequestId);
    }

    // =========================================================================
    // Full lifecycle
    // =========================================================================

    [Fact]
    public async Task WorkPackage_FullLifecycle_ShouldProgressDraftActiveClosed_AndPersistAcrossScopes()
    {
        var (personId, customerId, productId, _) = await ExecuteInScopeAsync(SeedMasterDataAsync);

        // 1. Create in DRAFT
        var workPackageId = await ExecuteInScopeAsync(async provider =>
        {
            var service = provider.GetRequiredService<IWorkPackageService>();

            var created = await service.CreateWorkPackageAsync(
                name: "Phase 1 Rollout",
                objective: "Prepare go-live infrastructure for the claims platform",
                ownerPersonId: personId,
                customerId: customerId,
                productId: productId);

            created.Status.Should().Be(WorkPackageStatus.Draft);
            created.IsDraft.Should().BeTrue();
            created.IsActive.Should().BeFalse();
            created.IsClosed.Should().BeFalse();
            created.Name.Should().Be("Phase 1 Rollout");
            created.Objective.Should().Be("Prepare go-live infrastructure for the claims platform");
            created.CustomerId.Should().Be(customerId);
            created.ProductId.Should().Be(productId);

            // Resolved reference names come from the owning modules' query services
            created.OwnerName.Should().Be("Work Package Owner");
            created.CustomerName.Should().Be("General Hospital");
            created.ProductName.Should().Be("Claims Suite");

            return created.Id;
        });

        // 2. Activate in a fresh scope, proving the state was really persisted
        await ExecuteInScopeAsync(async provider =>
        {
            var service = provider.GetRequiredService<IWorkPackageService>();
            var query = provider.GetRequiredService<IWorkPackageQueryService>();

            var beforeActivate = await query.GetWorkPackageByIdAsync(workPackageId);
            beforeActivate!.Status.Should().Be(WorkPackageStatus.Draft);

            var activated = await service.ActivateWorkPackageAsync(workPackageId);
            activated.Status.Should().Be(WorkPackageStatus.Active);
            activated.IsActive.Should().BeTrue();
        });

        // 3. Close
        await ExecuteInScopeAsync(async provider =>
        {
            var service = provider.GetRequiredService<IWorkPackageService>();
            var query = provider.GetRequiredService<IWorkPackageQueryService>();

            var closed = await service.CloseWorkPackageAsync(workPackageId, "Objective delivered");
            closed.Status.Should().Be(WorkPackageStatus.Closed);
            closed.IsClosed.Should().BeTrue();
            closed.CloseReason.Should().Be("Objective delivered");
            closed.ClosedAt.Should().NotBeNull();

            var reread = await query.GetWorkPackageByIdAsync(workPackageId);
            reread!.Status.Should().Be(WorkPackageStatus.Closed);
        });

        // 4. CLOSED is terminal
        await ExecuteInScopeAsync(async provider =>
        {
            var service = provider.GetRequiredService<IWorkPackageService>();

            var activateAgain = () => service.ActivateWorkPackageAsync(workPackageId);
            await activateAgain.Should().ThrowAsync<InvalidWorkPackageStateTransitionException>();

            var closeAgain = () => service.CloseWorkPackageAsync(workPackageId, "Again");
            await closeAgain.Should().ThrowAsync<InvalidWorkPackageStateTransitionException>();
        });
    }

    [Fact]
    public async Task WorkPackage_UpdateObjectiveAndAssignOwner_ShouldPersistAndBeQueryable()
    {
        var (personId, _, _, _) = await ExecuteInScopeAsync(SeedMasterDataAsync);

        var secondPersonId = await ExecuteInScopeAsync(async provider =>
        {
            var organizationService = provider.GetRequiredService<IOrganizationService>();
            var second = await organizationService.CreatePersonAsync(
                "Replacement Owner",
                $"wp.second.{Guid.NewGuid():N}@example.com");
            return second.PersonId;
        });

        var workPackageId = await ExecuteInScopeAsync(async provider =>
        {
            var service = provider.GetRequiredService<IWorkPackageService>();
            var created = await service.CreateWorkPackageAsync(
                "Initial Name",
                "Initial objective",
                personId);
            return created.Id;
        });

        await ExecuteInScopeAsync(async provider =>
        {
            var service = provider.GetRequiredService<IWorkPackageService>();

            var updated = await service.UpdateObjectiveAsync(
                workPackageId,
                "Refined Name",
                "Refined objective covering the full rollout");
            updated.Name.Should().Be("Refined Name");
            updated.Objective.Should().Be("Refined objective covering the full rollout");

            var reassigned = await service.AssignOwnerAsync(workPackageId, secondPersonId);
            reassigned.OwnerPersonId.Should().Be(secondPersonId);
            reassigned.OwnerName.Should().Be("Replacement Owner");
        });

        // Survives a new scope, i.e. it was written to the workpackage schema
        await ExecuteInScopeAsync(async provider =>
        {
            var query = provider.GetRequiredService<IWorkPackageQueryService>();
            var reloaded = await query.GetWorkPackageByIdAsync(workPackageId);

            reloaded!.Name.Should().Be("Refined Name");
            reloaded.Objective.Should().Be("Refined objective covering the full rollout");
            reloaded.OwnerPersonId.Should().Be(secondPersonId);
        });
    }

    // =========================================================================
    // Request membership
    // =========================================================================

    [Fact]
    public async Task WorkPackage_AddAndRemoveRequest_ShouldManageMembership_AndPreserveHistory()
    {
        var (personId, _, _, requestId) = await ExecuteInScopeAsync(SeedMasterDataAsync);

        var workPackageId = await ExecuteInScopeAsync(async provider =>
        {
            var service = provider.GetRequiredService<IWorkPackageService>();
            var created = await service.CreateWorkPackageAsync("Membership Package", "Track request scope", personId);
            return created.Id;
        });

        // Add
        await ExecuteInScopeAsync(async provider =>
        {
            var service = provider.GetRequiredService<IWorkPackageService>();
            var result = await service.AddRequestToWorkPackageAsync(workPackageId, requestId);

            result.ActiveRequestCount.Should().Be(1);
            result.Requests.Should().ContainSingle(m => m.RequestId == requestId && m.IsActive);
        });

        // Remove
        await ExecuteInScopeAsync(async provider =>
        {
            var service = provider.GetRequiredService<IWorkPackageService>();
            var result = await service.RemoveRequestFromWorkPackageAsync(workPackageId, requestId);

            result.ActiveRequestCount.Should().Be(0);
            // Business Rule 15: the historical membership row is retained
            result.Requests.Should().ContainSingle(m => m.RequestId == requestId && !m.IsActive);
            result.Requests.Single(m => m.RequestId == requestId).RemovedAt.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task WorkPackage_AddRequest_WithUnknownRequest_ShouldReject_CrossModuleValidation()
    {
        var (personId, _, _, _) = await ExecuteInScopeAsync(SeedMasterDataAsync);

        await ExecuteInScopeAsync(async provider =>
        {
            var service = provider.GetRequiredService<IWorkPackageService>();
            var created = await service.CreateWorkPackageAsync("Validation Package", "Scope", personId);

            var act = () => service.AddRequestToWorkPackageAsync(created.Id, Guid.NewGuid());
            await act.Should().ThrowAsync<WorkPackageReferenceValidationException>()
                .Where(e => e.ReferenceKind == "Request")
                .WithMessage("*does not exist*");
        });
    }

    [Fact]
    public async Task WorkPackage_Create_WithInvalidOwnerCustomerOrProduct_ShouldReject_CrossModuleValidation()
    {
        await ExecuteInScopeAsync(async provider =>
        {
            var service = provider.GetRequiredService<IWorkPackageService>();
            var organizationService = provider.GetRequiredService<IOrganizationService>();
            var customerService = provider.GetRequiredService<ICustomerService>();

            var validOwner = await organizationService.CreatePersonAsync(
                "Valid Owner",
                $"wp.valid.{Guid.NewGuid():N}@example.com");

            // Unknown owner
            var unknownOwner = () => service.CreateWorkPackageAsync("P", "O", Guid.NewGuid());
            await unknownOwner.Should().ThrowAsync<WorkPackageReferenceValidationException>()
                .Where(e => e.ReferenceKind == "Person");

            // Unknown customer
            var unknownCustomer = () => service.CreateWorkPackageAsync("P", "O", validOwner.PersonId, Guid.NewGuid());
            await unknownCustomer.Should().ThrowAsync<WorkPackageReferenceValidationException>()
                .Where(e => e.ReferenceKind == "Customer");

            // Unknown product
            var unknownProduct = () => service.CreateWorkPackageAsync("P", "O", validOwner.PersonId, null, Guid.NewGuid());
            await unknownProduct.Should().ThrowAsync<WorkPackageReferenceValidationException>()
                .Where(e => e.ReferenceKind == "Product");

            // Deactivated customer is rejected even though it exists
            var customerCode = $"CUST-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
            var customer = await customerService.CreateCustomerAsync(customerCode, "Dormant Hospital");
            await customerService.DeactivateCustomerAsync(customer.CustomerId);

            var inactiveCustomer = () => service.CreateWorkPackageAsync("P", "O", validOwner.PersonId, customer.CustomerId);
            await inactiveCustomer.Should().ThrowAsync<WorkPackageReferenceValidationException>()
                .WithMessage("*is not active*");
        });
    }

    // =========================================================================
    // Business Rule 9
    // =========================================================================

    [Fact]
    public async Task BusinessRule9_RequestCannotJoinTwoActiveWorkPackages()
    {
        var (personId, _, _, requestId) = await ExecuteInScopeAsync(SeedMasterDataAsync);
        var secondPersonId = await ExecuteInScopeAsync(async provider =>
        {
            var organizationService = provider.GetRequiredService<IOrganizationService>();
            var second = await organizationService.CreatePersonAsync(
                "Second Owner",
                $"wp.two.{Guid.NewGuid():N}@example.com");
            return second.PersonId;
        });

        await ExecuteInScopeAsync(async provider =>
        {
            var service = provider.GetRequiredService<IWorkPackageService>();
            var query = provider.GetRequiredService<IWorkPackageQueryService>();

            // Package A: activated and holds the request
            var packageA = await service.CreateWorkPackageAsync("Package A", "First wave", personId);
            await service.ActivateWorkPackageAsync(packageA.Id);
            await service.AddRequestToWorkPackageAsync(packageA.Id, requestId);

            // Package B: also activated, wants the same request
            var packageB = await service.CreateWorkPackageAsync("Package B", "Second wave", secondPersonId);
            await service.ActivateWorkPackageAsync(packageB.Id);

            var act = () => service.AddRequestToWorkPackageAsync(packageB.Id, requestId);
            await act.Should().ThrowAsync<BusinessRule9ViolationException>()
                .Where(e => e.RequestId == requestId && e.WorkPackageId == packageB.Id);

            // The request's membership is unchanged and still points at Package A only
            (await query.IsRequestInActiveWorkPackageAsync(requestId)).Should().BeTrue();

            var membership = await query.GetRequestWorkPackageAsync(requestId);
            membership!.WorkPackageId.Should().Be(packageA.Id);

            var scopeB = await query.GetWorkPackageScopeAsync(packageB.Id);
            scopeB!.ActiveRequests.Should().BeEmpty();
        });
    }

    [Fact]
    public async Task BusinessRule9_AddingSameRequestTwiceToSamePackage_ShouldReject()
    {
        var (personId, _, _, requestId) = await ExecuteInScopeAsync(SeedMasterDataAsync);

        await ExecuteInScopeAsync(async provider =>
        {
            var service = provider.GetRequiredService<IWorkPackageService>();
            var created = await service.CreateWorkPackageAsync("Duplicate Guard", "Scope", personId);

            await service.AddRequestToWorkPackageAsync(created.Id, requestId);

            var act = () => service.AddRequestToWorkPackageAsync(created.Id, requestId);
            await act.Should().ThrowAsync<BusinessRule9ViolationException>();
        });
    }

    [Fact]
    public async Task BusinessRule9_ClosedPackageReleasesRequest_SoANewPackageMayAdoptIt()
    {
        var (personId, _, _, requestId) = await ExecuteInScopeAsync(SeedMasterDataAsync);
        var secondPersonId = await ExecuteInScopeAsync(async provider =>
        {
            var organizationService = provider.GetRequiredService<IOrganizationService>();
            var second = await organizationService.CreatePersonAsync(
                "Third Owner",
                $"wp.three.{Guid.NewGuid():N}@example.com");
            return second.PersonId;
        });

        await ExecuteInScopeAsync(async provider =>
        {
            var service = provider.GetRequiredService<IWorkPackageService>();
            var query = provider.GetRequiredService<IWorkPackageQueryService>();

            var packageA = await service.CreateWorkPackageAsync("Package A", "First wave", personId);
            await service.ActivateWorkPackageAsync(packageA.Id);
            await service.AddRequestToWorkPackageAsync(packageA.Id, requestId);
            await service.CloseWorkPackageAsync(packageA.Id, "Phase complete");

            // Closing releases the request, so it is no longer held by an active Work Package
            (await query.IsRequestInActiveWorkPackageAsync(requestId)).Should().BeFalse();

            var packageB = await service.CreateWorkPackageAsync("Package B", "Second wave", secondPersonId);
            var adopted = await service.AddRequestToWorkPackageAsync(packageB.Id, requestId);

            adopted.ActiveRequestCount.Should().Be(1);
        });
    }

    [Fact]
    public async Task WorkPackage_Close_ShouldNotAlterConstituentRequestLifecycleState()
    {
        var (personId, _, _, requestId) = await ExecuteInScopeAsync(SeedMasterDataAsync);

        await ExecuteInScopeAsync(async provider =>
        {
            var workPackageService = provider.GetRequiredService<IWorkPackageService>();
            var requestQueryService = provider.GetRequiredService<IRequestQueryService>();

            var before = await requestQueryService.GetRequestByIdAsync(requestId);
            before.Should().NotBeNull();
            var statusBefore = before!.Status;

            var created = await workPackageService.CreateWorkPackageAsync("Closing Package", "Scope", personId);
            await workPackageService.AddRequestToWorkPackageAsync(created.Id, requestId);
            await workPackageService.ActivateWorkPackageAsync(created.Id);
            await workPackageService.CloseWorkPackageAsync(created.Id, "Done");

            // Architecture §11: closing a Work Package must not transition its member Requests
            var after = await requestQueryService.GetRequestByIdAsync(requestId);
            after!.Status.Should().Be(statusBefore);
            after.ClosedAt.Should().Be(before.ClosedAt);
        });
    }

    // =========================================================================
    // Query service
    // =========================================================================

    [Fact]
    public async Task WorkPackageQueryService_ShouldSupportFilterGridAndScope()
    {
        var (personId, customerId, productId, requestId) = await ExecuteInScopeAsync(SeedMasterDataAsync);

        await ExecuteInScopeAsync(async provider =>
        {
            var service = provider.GetRequiredService<IWorkPackageService>();
            var query = provider.GetRequiredService<IWorkPackageQueryService>();

            var created = await service.CreateWorkPackageAsync(
                "Queryable Package",
                "Exercise the query service",
                personId,
                customerId,
                productId);
            await service.AddRequestToWorkPackageAsync(created.Id, requestId);

            // GetWorkPackageById
            var byId = await query.GetWorkPackageByIdAsync(created.Id);
            byId.Should().NotBeNull();
            byId!.ActiveRequestCount.Should().Be(1);

            // ListWorkPackages with filters
            var all = await query.ListWorkPackagesAsync();
            all.TotalCount.Should().BeGreaterThanOrEqualTo(1);
            all.Items.Should().Contain(i => i.Id == created.Id);

            var byOwner = await query.ListWorkPackagesAsync(new WorkPackageGridFilterDto(OwnerPersonId: personId));
            byOwner.Items.Should().Contain(i => i.Id == created.Id);

            var byCustomer = await query.ListWorkPackagesAsync(new WorkPackageGridFilterDto(CustomerId: customerId));
            byCustomer.Items.Should().Contain(i => i.Id == created.Id);

            var byProduct = await query.ListWorkPackagesAsync(new WorkPackageGridFilterDto(ProductId: productId));
            byProduct.Items.Should().Contain(i => i.Id == created.Id);

            var byStatus = await query.ListWorkPackagesAsync(new WorkPackageGridFilterDto(Statuses: "DRAFT"));
            byStatus.Items.Should().Contain(i => i.Id == created.Id);

            var bySearch = await query.ListWorkPackagesAsync(new WorkPackageGridFilterDto(SearchTerm: "Queryable"));
            bySearch.Items.Should().Contain(i => i.Id == created.Id);

            var byUnmatchedSearch = await query.ListWorkPackagesAsync(
                new WorkPackageGridFilterDto(SearchTerm: "NoSuchPackageNameExists"));
            byUnmatchedSearch.Items.Should().NotContain(i => i.Id == created.Id);

            // GetWorkPackageScope
            var scope = await query.GetWorkPackageScopeAsync(created.Id);
            scope.Should().NotBeNull();
            scope!.ActiveRequests.Should().ContainSingle(r => r.RequestId == requestId);
            scope.AllRequests.Should().HaveCount(1);
            scope.OwnerName.Should().Be("Work Package Owner");
            scope.CustomerName.Should().Be("General Hospital");
            scope.ProductName.Should().Be("Claims Suite");

            // GetRequestWorkPackage
            var membership = await query.GetRequestWorkPackageAsync(requestId);
            membership!.WorkPackageId.Should().Be(created.Id);

            // Unknown identifiers
            (await query.GetWorkPackageByIdAsync(Guid.NewGuid())).Should().BeNull();
            (await query.GetWorkPackageScopeAsync(Guid.NewGuid())).Should().BeNull();
            (await query.GetRequestWorkPackageAsync(Guid.NewGuid())).Should().BeNull();
        });
    }

    [Fact]
    public async Task WorkPackage_ModuleRegistration_ShouldResolveAllPublishedContracts()
    {
        await ExecuteInScopeAsync(provider =>
        {
            provider.GetRequiredService<IWorkPackageService>().Should().NotBeNull();
            provider.GetRequiredService<IWorkPackageQueryService>().Should().NotBeNull();
            return Task.CompletedTask;
        });
    }
}
