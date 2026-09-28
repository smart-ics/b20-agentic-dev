namespace ICS.Tests.Integration;

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using ICS.Modules.Organization.Application;
using ICS.Modules.Product;
using ICS.Modules.Product.Application;
using ICS.Modules.Product.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Integration tests verifying the Product module:
/// Domain entities, Dapper repositories, ProductService commands, ProductQueryService queries,
/// cross-module owner validation against OrganizationQueryService, domain event emissions,
/// and Respawn test database isolation.
/// Architecture §10, §15, §16, §17, §19.3, §20; product-domain.md.
/// </summary>
public class ProductIntegrationTests : IntegrationTestBase
{
    public ProductIntegrationTests(IcsWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CreateProduct_WithValidOwner_ShouldPersist_EmitProductCreatedEvent_AndBeQueryable()
    {
        // Arrange
        TestDomainEventCollector.Clear();
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        var productQueryService = scope.ServiceProvider.GetRequiredService<IProductQueryService>();

        var owner = await organizationService.CreatePersonAsync("Dr. John Doe", $"john.{Guid.NewGuid():N}@example.com");
        var code = $"PRD-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var name = "MyHospital";
        var description = "Integrated Hospital Information System";

        // Act
        var created = await productService.CreateProductAsync(code, name, description, owner.PersonId);

        // Assert
        created.Should().NotBeNull();
        created.ProductId.Should().NotBeEmpty();
        created.Code.Should().Be(code);
        created.Name.Should().Be(name);
        created.Description.Should().Be(description);
        created.OwnerPersonId.Should().Be(owner.PersonId);
        created.Status.Should().Be("ACTIVE");
        created.OwnerName.Should().Be(owner.Name);

        // Query by ID
        var byId = await productQueryService.GetProductByIdAsync(created.ProductId);
        byId.Should().NotBeNull();
        byId!.ProductId.Should().Be(created.ProductId);
        byId.Code.Should().Be(code);
        byId.Name.Should().Be(name);
        byId.Description.Should().Be(description);
        byId.OwnerPersonId.Should().Be(owner.PersonId);
        byId.Status.Should().Be("ACTIVE");
        byId.OwnerName.Should().Be(owner.Name);

        // Query by Code
        var byCode = await productQueryService.GetProductByCodeAsync(code);
        byCode.Should().NotBeNull();
        byCode!.ProductId.Should().Be(created.ProductId);
        byCode.OwnerName.Should().Be(owner.Name);

        // Verify Domain Event
        var events = TestDomainEventCollector.PublishedEvents;
        var createdEvent = events.OfType<ProductCreated>().FirstOrDefault(e => e.ProductId == created.ProductId);
        createdEvent.Should().NotBeNull();
        createdEvent!.Code.Should().Be(code);
        createdEvent.Name.Should().Be(name);
        createdEvent.Description.Should().Be(description);
        createdEvent.OwnerPersonId.Should().Be(owner.PersonId);
    }

    [Fact]
    public async Task CreateProduct_WithNonExistentOwner_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var scope = CreateScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

        var nonExistentOwnerId = Guid.NewGuid();
        var code = $"PRD-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        // Act
        var act = () => productService.CreateProductAsync(code, "Product X", "Desc", nonExistentOwnerId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{nonExistentOwnerId}*");
    }

    [Fact]
    public async Task CreateProduct_WithInactiveOwner_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

        var owner = await organizationService.CreatePersonAsync("Inactive Person", $"inactive.{Guid.NewGuid():N}@example.com");
        await organizationService.DeactivatePersonAsync(owner.PersonId);

        var code = $"PRD-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        // Act
        var act = () => productService.CreateProductAsync(code, "Product Inactive Owner", "Desc", owner.PersonId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not active*");
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateCode_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

        var owner = await organizationService.CreatePersonAsync("Product Owner", $"owner.{Guid.NewGuid():N}@example.com");
        var code = $"PRD-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        await productService.CreateProductAsync(code, "Product One", "Desc 1", owner.PersonId);

        // Act
        var act = () => productService.CreateProductAsync(code, "Product Two", "Desc 2", owner.PersonId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{code}*already exists*");
    }

    [Fact]
    public async Task UpdateProduct_ShouldUpdateAttributes_AndReflectInQueryService()
    {
        // Arrange
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        var productQueryService = scope.ServiceProvider.GetRequiredService<IProductQueryService>();

        var owner = await organizationService.CreatePersonAsync("Jane Smith", $"jane.{Guid.NewGuid():N}@example.com");
        var code = $"PRD-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var created = await productService.CreateProductAsync(code, "Original Name", "Original Desc", owner.PersonId);

        // Act
        var updated = await productService.UpdateProductAsync(created.ProductId, "Updated Name", "Updated Desc");

        // Assert
        updated.Should().NotBeNull();
        updated.Name.Should().Be("Updated Name");
        updated.Description.Should().Be("Updated Desc");

        var queried = await productQueryService.GetProductByIdAsync(created.ProductId);
        queried.Should().NotBeNull();
        queried!.Name.Should().Be("Updated Name");
        queried.Description.Should().Be("Updated Desc");
        queried.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AssignProductOwner_WithValidNewOwner_ShouldUpdateOwner_EmitEvent_AndReflectInQueryService()
    {
        // Arrange
        TestDomainEventCollector.Clear();
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        var productQueryService = scope.ServiceProvider.GetRequiredService<IProductQueryService>();

        var owner1 = await organizationService.CreatePersonAsync("Owner One", $"owner1.{Guid.NewGuid():N}@example.com");
        var owner2 = await organizationService.CreatePersonAsync("Owner Two", $"owner2.{Guid.NewGuid():N}@example.com");
        var code = $"PRD-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        var created = await productService.CreateProductAsync(code, "PenaEl", "Electronic Medical Prescription", owner1.PersonId);

        // Act
        var updated = await productService.AssignProductOwnerAsync(created.ProductId, owner2.PersonId);

        // Assert
        updated.Should().NotBeNull();
        updated.OwnerPersonId.Should().Be(owner2.PersonId);
        updated.OwnerName.Should().Be(owner2.Name);

        var queried = await productQueryService.GetProductByIdAsync(created.ProductId);
        queried.Should().NotBeNull();
        queried!.OwnerPersonId.Should().Be(owner2.PersonId);
        queried.OwnerName.Should().Be(owner2.Name);

        // Verify Domain Event
        var events = TestDomainEventCollector.PublishedEvents;
        var changedEvent = events.OfType<ProductOwnerChanged>().FirstOrDefault(e => e.ProductId == created.ProductId);
        changedEvent.Should().NotBeNull();
        changedEvent!.PreviousOwnerPersonId.Should().Be(owner1.PersonId);
        changedEvent.NewOwnerPersonId.Should().Be(owner2.PersonId);
    }

    [Fact]
    public async Task AssignProductOwner_WithNonExistentOwner_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

        var owner = await organizationService.CreatePersonAsync("Owner Initial", $"initial.{Guid.NewGuid():N}@example.com");
        var code = $"PRD-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var created = await productService.CreateProductAsync(code, "BTrade3", "Trade System", owner.PersonId);

        var nonExistent = Guid.NewGuid();

        // Act
        var act = () => productService.AssignProductOwnerAsync(created.ProductId, nonExistent);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{nonExistent}*");
    }

    [Fact]
    public async Task ActivateAndDeactivateProduct_ShouldTransitionStatus_EmitEvents_AndReflectInListActive()
    {
        // Arrange
        TestDomainEventCollector.Clear();
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        var productQueryService = scope.ServiceProvider.GetRequiredService<IProductQueryService>();

        var owner = await organizationService.CreatePersonAsync("Lifecycle Owner", $"lifecycle.{Guid.NewGuid():N}@example.com");
        var code = $"PRD-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var product = await productService.CreateProductAsync(code, "Jetset", "Jet Engine Simulator", owner.PersonId);

        // Act 1: Deactivate
        var deactivated = await productService.DeactivateProductAsync(product.ProductId);
        deactivated.Should().BeTrue();

        var queriedAfterDeactivate = await productQueryService.GetProductByIdAsync(product.ProductId);
        queriedAfterDeactivate.Should().NotBeNull();
        queriedAfterDeactivate!.Status.Should().Be("INACTIVE");

        var activeList = await productQueryService.ListActiveProductsAsync();
        activeList.Should().NotContain(p => p.ProductId == product.ProductId);

        var allList = await productQueryService.ListAllProductsAsync();
        allList.Should().Contain(p => p.ProductId == product.ProductId && p.Status == "INACTIVE");

        var deactEvent = TestDomainEventCollector.PublishedEvents.OfType<ProductDeactivated>().FirstOrDefault(e => e.ProductId == product.ProductId);
        deactEvent.Should().NotBeNull();
        deactEvent!.Code.Should().Be(code);

        // Act 2: Reactivate
        var reactivated = await productService.ActivateProductAsync(product.ProductId);
        reactivated.Should().BeTrue();

        var queriedAfterActivate = await productQueryService.GetProductByIdAsync(product.ProductId);
        queriedAfterActivate.Should().NotBeNull();
        queriedAfterActivate!.Status.Should().Be("ACTIVE");

        var activeListAfterReactivate = await productQueryService.ListActiveProductsAsync();
        activeListAfterReactivate.Should().Contain(p => p.ProductId == product.ProductId);

        var actEvent = TestDomainEventCollector.PublishedEvents.OfType<ProductActivated>().FirstOrDefault(e => e.ProductId == product.ProductId);
        actEvent.Should().NotBeNull();
        actEvent!.Code.Should().Be(code);
    }

    [Fact]
    public async Task SynchronousQueryMethods_ShouldMatchAsyncQueryMethods()
    {
        // Arrange
        using var scope = CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        var productQueryService = scope.ServiceProvider.GetRequiredService<IProductQueryService>();

        var owner = await organizationService.CreatePersonAsync("Sync Owner", $"sync.{Guid.NewGuid():N}@example.com");
        var code = $"PRD-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var created = await productService.CreateProductAsync(code, "SyncProduct", "Sync Desc", owner.PersonId);

        // Act & Assert
        var syncById = productQueryService.GetProductById(created.ProductId);
        syncById.Should().NotBeNull();
        syncById!.Code.Should().Be(code);

        var syncByCode = productQueryService.GetProductByCode(code);
        syncByCode.Should().NotBeNull();
        syncByCode!.ProductId.Should().Be(created.ProductId);

        var syncActive = productQueryService.ListActiveProducts();
        syncActive.Should().Contain(p => p.ProductId == created.ProductId);

        var syncAll = productQueryService.ListAllProducts();
        syncAll.Should().Contain(p => p.ProductId == created.ProductId);
    }
}
