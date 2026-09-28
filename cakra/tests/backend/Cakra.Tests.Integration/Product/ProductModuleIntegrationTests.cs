using Cakra.Api.Infrastructure.Migrations;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Organization.Commands;
using Cakra.Modules.Product;
using Cakra.Modules.Product.Services;
using Dapper;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Respawn;
using Respawn.Graph;
using Xunit;

namespace Cakra.Tests.Integration.Product;

/// <summary>
/// Integration tests verifying P3-S15 Product Module — Domain, Application Services &amp; Persistence:
/// - DbUp migration script 0005_product_tables.sql creates product.Products
/// - MediatR handlers via ProductService: CreateProduct, UpdateProduct, AssignProductOwner,
///   ActivateProduct, DeactivateProduct
/// - Owner validation via IOrganizationQueryService (no direct cross-schema writes)
/// - ProductQueryService (IProductQueryService): GetProductById, GetProductByCode,
///   ListActiveProducts, ListAllProducts
/// - WebApplicationFactory&lt;Program&gt; + Respawn test isolation
/// </summary>
[Collection("OrganizationDatabase")]
public class ProductModuleIntegrationTests : IAsyncLifetime
{
    private const string LocalDbFallback = "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb;Integrated Security=true;TrustServerCertificate=True;";
    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private bool _sqlServerAvailable;

    public ProductModuleIntegrationTests()
    {
        _connectionString = DatabaseResetHelper.ResolveConnectionString() ?? LocalDbFallback;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var masterConnectionString = new SqlConnectionStringBuilder(_connectionString)
            {
                InitialCatalog = "master"
            }.ConnectionString;

            await using var probeConn = new SqlConnection(masterConnectionString);
            await probeConn.OpenAsync();
            _sqlServerAvailable = true;
        }
        catch
        {
            _sqlServerAvailable = false;
            return;
        }

        var runner = new DatabaseMigrationRunner(_connectionString, NullLogger<DatabaseMigrationRunner>.Instance);
        var migrationResult = runner.Run();
        migrationResult.Successful.Should().BeTrue("DbUp migration 0005_product_tables.sql must execute without errors");

        await using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
        }

        _factory = new CakraWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
                builder.UseSetting("ConnectionStrings:TestConnection", _connectionString);
            });

        await ResetTablesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_sqlServerAvailable)
        {
            await ResetTablesAsync();
        }

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private async Task ResetTablesAsync()
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = ["product", "organization"],
            TablesToIgnore = [new Table("dbo", "__SchemaVersions"), new Table("dbo", "SchemaVersions")]
        });

        await respawner.ResetAsync(conn);
    }

    [Fact]
    public async Task Migration_0005_creates_product_schema_table()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        using var scope = _factory.Services.CreateScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        using var connection = connectionFactory.CreateConnection();

        var tables = (await connection.QueryAsync<string>("""
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = 'product';
            """)).ToList();

        tables.Should().Contain("Products");
    }

    [Fact]
    public async Task ProductService_commands_and_ProductQueryService_queries_work_end_to_end()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var queryService = scope.ServiceProvider.GetRequiredService<IProductQueryService>();

        // 1. Create active organizational persons to serve as product owners
        var owner1 = await mediator.Send(new CreatePersonCommand("Budi", "Santoso", "budi.santoso@cakra.id"));
        var owner2 = await mediator.Send(new CreatePersonCommand("Rina", "Wijaya", "rina.wijaya@cakra.id"));

        var code1 = $"PRD-{Guid.NewGuid():N}"[..14];
        var code2 = $"PRD-{Guid.NewGuid():N}"[..14];

        // 2. Create two products via MediatR CreateProductCommand
        var product1 = await mediator.Send(new CreateProductCommand(
            code1,
            "MyHospital",
            "Hospital Information System",
            owner1.Id));

        var product2 = await mediator.Send(new CreateProductCommand(
            code2,
            "PenaEl",
            "Electronic Medical Record Suite",
            owner1.Id));

        product1.Id.Should().NotBeEmpty();
        product1.Code.Should().Be(code1);
        product1.Name.Should().Be("MyHospital");
        product1.Description.Should().Be("Hospital Information System");
        product1.OwnerPersonId.Should().Be(owner1.Id);
        product1.Status.Should().Be("ACTIVE");
        product1.IsActive.Should().BeTrue();

        // 3. GetProductById and GetProductByCode return authoritative product records
        var byId = await queryService.GetProductById(product1.Id);
        byId.Should().NotBeNull();
        byId!.Id.Should().Be(product1.Id);
        byId.Code.Should().Be(code1);
        byId.Name.Should().Be("MyHospital");
        byId.OwnerPersonId.Should().Be(owner1.Id);
        byId.Status.Should().Be("ACTIVE");

        var byCode = await queryService.GetProductByCode(code2);
        byCode.Should().NotBeNull();
        byCode!.Id.Should().Be(product2.Id);
        byCode.Name.Should().Be("PenaEl");

        // 4. UpdateProduct updates descriptive attributes
        var updatedProduct1 = await mediator.Send(new UpdateProductCommand(
            product1.Id,
            "MyHospital Enterprise",
            "Integrated Hospital Information System v2"));

        updatedProduct1.Name.Should().Be("MyHospital Enterprise");
        updatedProduct1.Description.Should().Be("Integrated Hospital Information System v2");

        var byIdAfterUpdate = await queryService.GetProductById(product1.Id);
        byIdAfterUpdate!.Name.Should().Be("MyHospital Enterprise");
        byIdAfterUpdate.Description.Should().Be("Integrated Hospital Information System v2");
        byIdAfterUpdate.UpdatedAt.Should().NotBeNull();

        // 5. AssignProductOwner validates new owner via OrganizationQueryService and updates OwnerPersonId
        var reassignedProduct1 = await mediator.Send(new AssignProductOwnerCommand(
            product1.Id,
            owner2.Id));

        reassignedProduct1.OwnerPersonId.Should().Be(owner2.Id);

        var byIdAfterReassign = await queryService.GetProductById(product1.Id);
        byIdAfterReassign!.OwnerPersonId.Should().Be(owner2.Id);

        // 6. ListActiveProducts and ListAllProducts return both active products initially
        var activeProductsBefore = await queryService.ListActiveProducts();
        activeProductsBefore.Should().Contain(p => p.Id == product1.Id);
        activeProductsBefore.Should().Contain(p => p.Id == product2.Id);

        var allProductsBefore = await queryService.ListAllProducts();
        allProductsBefore.Should().Contain(p => p.Id == product1.Id);
        allProductsBefore.Should().Contain(p => p.Id == product2.Id);

        // 7. DeactivateProduct transitions product2 to INACTIVE
        var deactivatedProduct2 = await mediator.Send(new DeactivateProductCommand(product2.Id));
        deactivatedProduct2.Status.Should().Be("INACTIVE");
        deactivatedProduct2.IsActive.Should().BeFalse();

        // ListActiveProducts excludes deactivated product2, while ListAllProducts still includes it
        var activeProductsAfterDeactivate = await queryService.ListActiveProducts();
        activeProductsAfterDeactivate.Should().Contain(p => p.Id == product1.Id);
        activeProductsAfterDeactivate.Should().NotContain(p => p.Id == product2.Id);

        var allProductsAfterDeactivate = await queryService.ListAllProducts();
        allProductsAfterDeactivate.Should().Contain(p => p.Id == product1.Id);
        allProductsAfterDeactivate.Should().Contain(p => p.Id == product2.Id && p.Status == "INACTIVE");

        // 8. ActivateProduct transitions product2 back to ACTIVE
        var reactivatedProduct2 = await mediator.Send(new ActivateProductCommand(product2.Id));
        reactivatedProduct2.Status.Should().Be("ACTIVE");
        reactivatedProduct2.IsActive.Should().BeTrue();

        var activeProductsAfterReactivate = await queryService.ListActiveProducts();
        activeProductsAfterReactivate.Should().Contain(p => p.Id == product2.Id);
    }

    [Fact]
    public async Task CreateProduct_and_AssignProductOwner_reject_nonexistent_or_inactive_owner_in_Organization()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // 1. Nonexistent owner rejected on CreateProduct
        var nonexistentOwnerId = Guid.NewGuid();
        var createWithUnknownOwnerAct = async () => await mediator.Send(new CreateProductCommand(
            "BTRADE3",
            "BTrade3",
            "Trading System",
            nonexistentOwnerId));

        await createWithUnknownOwnerAct.Should().ThrowAsync<KeyNotFoundException>();

        // 2. Inactive owner rejected on CreateProduct and AssignProductOwner
        var activeOwner = await mediator.Send(new CreatePersonCommand("Andi", "Pratama", "andi.pratama@cakra.id"));
        var inactiveOwner = await mediator.Send(new CreatePersonCommand("Siti", "Rahma", "siti.rahma@cakra.id"));
        await mediator.Send(new DeactivatePersonCommand(inactiveOwner.Id));

        var createWithInactiveOwnerAct = async () => await mediator.Send(new CreateProductCommand(
            "BTRADE3",
            "BTrade3",
            "Trading System",
            inactiveOwner.Id));

        await createWithInactiveOwnerAct.Should().ThrowAsync<InvalidOperationException>();

        var product = await mediator.Send(new CreateProductCommand(
            "BTRADE3",
            "BTrade3",
            "Trading System",
            activeOwner.Id));

        var assignInactiveOwnerAct = async () => await mediator.Send(new AssignProductOwnerCommand(
            product.Id,
            inactiveOwner.Id));

        await assignInactiveOwnerAct.Should().ThrowAsync<InvalidOperationException>();

        var assignUnknownOwnerAct = async () => await mediator.Send(new AssignProductOwnerCommand(
            product.Id,
            Guid.NewGuid()));

        await assignUnknownOwnerAct.Should().ThrowAsync<KeyNotFoundException>();
    }
}
