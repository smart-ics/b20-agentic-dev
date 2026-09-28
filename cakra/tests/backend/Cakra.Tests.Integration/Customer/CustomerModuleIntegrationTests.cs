using Cakra.Api.Infrastructure.Migrations;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Customer;
using Cakra.Modules.Customer.Persistence;
using Cakra.Modules.Customer.Services;
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

namespace Cakra.Tests.Integration.Customer;

/// <summary>
/// Integration tests verifying P3-S14 Customer Module — Domain, Application Services &amp; Persistence:
/// - DbUp migration script 0004_customer_tables.sql creates customer.Customers and customer.CustomerContacts
/// - MediatR handlers via CustomerService: create Customer, update Customer master data,
///   create CustomerContact, update CustomerContact, deactivate Customer
/// - CustomerQueryService (ICustomerQueryService): GetCustomerById, ListActiveCustomers,
///   GetCustomerContacts, GetCustomerWithContractStatus
/// - WebApplicationFactory&lt;Program&gt; + Respawn test isolation
/// </summary>
public class CustomerModuleIntegrationTests : IAsyncLifetime
{
    private const string LocalDbFallback = "Server=(localdb)\\MSSQLLocalDB;Database=CakraTestDb;Integrated Security=true;TrustServerCertificate=True;";
    private readonly string _connectionString;
    private WebApplicationFactory<Program>? _factory;
    private bool _sqlServerAvailable;

    public CustomerModuleIntegrationTests()
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
        migrationResult.Successful.Should().BeTrue("DbUp migration 0004_customer_tables.sql must execute without errors");

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

        await ResetCustomerTablesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_sqlServerAvailable)
        {
            await ResetCustomerTablesAsync();
        }

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private async Task ResetCustomerTablesAsync()
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = ["customer"],
            TablesToIgnore = [new Table("dbo", "__SchemaVersions"), new Table("dbo", "SchemaVersions")]
        });

        await respawner.ResetAsync(conn);
    }

    [Fact]
    public async Task Migration_0004_creates_customer_schema_tables()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        using var scope = _factory.Services.CreateScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        using var connection = connectionFactory.CreateConnection();

        var tables = (await connection.QueryAsync<string>("""
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = 'customer';
            """)).ToList();

        tables.Should().Contain(new[] { "Customers", "CustomerContacts" });
    }

    [Fact]
    public async Task CustomerService_commands_and_CustomerQueryService_queries_work_end_to_end()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var queryService = scope.ServiceProvider.GetRequiredService<ICustomerQueryService>();

        var code1 = $"CUST-{Guid.NewGuid():N}"[..16];
        var code2 = $"CUST-{Guid.NewGuid():N}"[..16];

        // 1. Create two customers via MediatR CreateCustomerCommand
        var customer1 = await mediator.Send(new CreateCustomerCommand(
            code1,
            "RS Cipto Mangunkusumo",
            HasActiveMaintenanceContract: true));

        var customer2 = await mediator.Send(new CreateCustomerCommand(
            code2,
            "RS Siloam Semanggi",
            HasActiveMaintenanceContract: false));

        customer1.Id.Should().NotBeEmpty();
        customer1.CustomerCode.Should().Be(code1);
        customer1.CustomerName.Should().Be("RS Cipto Mangunkusumo");
        customer1.Status.Should().Be("ACTIVE");
        customer1.HasActiveMaintenanceContract.Should().BeTrue();

        // 2. GetCustomerById returns accurate customer record
        var fetched1 = await queryService.GetCustomerById(customer1.Id);
        fetched1.Should().NotBeNull();
        fetched1!.Id.Should().Be(customer1.Id);
        fetched1.CustomerCode.Should().Be(code1);
        fetched1.CustomerName.Should().Be("RS Cipto Mangunkusumo");
        fetched1.HasActiveMaintenanceContract.Should().BeTrue();
        fetched1.IsActive.Should().BeTrue();

        // 3. GetCustomerWithContractStatus returns accurate contract status
        var contractStatus1 = await queryService.GetCustomerWithContractStatus(customer1.Id);
        contractStatus1.Should().NotBeNull();
        contractStatus1!.HasActiveMaintenanceContract.Should().BeTrue();
        contractStatus1.ContractStatus.Should().Be("ACTIVE");

        var contractStatus2 = await queryService.GetCustomerWithContractStatus(customer2.Id);
        contractStatus2.Should().NotBeNull();
        contractStatus2!.HasActiveMaintenanceContract.Should().BeFalse();
        contractStatus2.ContractStatus.Should().Be("NONE");

        // 4. Update Customer master data via UpdateCustomerCommand
        var updatedCustomer2 = await mediator.Send(new UpdateCustomerCommand(
            customer2.Id,
            code2,
            "RS Siloam Semanggi Pusat",
            HasActiveMaintenanceContract: true));

        updatedCustomer2.CustomerName.Should().Be("RS Siloam Semanggi Pusat");
        updatedCustomer2.HasActiveMaintenanceContract.Should().BeTrue();

        var contractStatus2AfterUpdate = await queryService.GetCustomerWithContractStatus(customer2.Id);
        contractStatus2AfterUpdate!.CustomerName.Should().Be("RS Siloam Semanggi Pusat");
        contractStatus2AfterUpdate.HasActiveMaintenanceContract.Should().BeTrue();
        contractStatus2AfterUpdate.ContractStatus.Should().Be("ACTIVE");

        // 5. Create and update CustomerContacts via MediatR commands
        var contact1 = await mediator.Send(new CreateCustomerContactCommand(
            customer1.Id,
            "Dr. Andi Wijaya",
            "CMIO",
            "+628111000111",
            "andi.wijaya@rscm.co.id"));

        var contact2 = await mediator.Send(new CreateCustomerContactCommand(
            customer1.Id,
            "Rina Kusuma",
            "IT Support Lead",
            "+628111000222",
            "rina.kusuma@rscm.co.id"));

        // Update contact2
        await mediator.Send(new UpdateCustomerContactCommand(
            contact2.Id,
            "Rina Kusuma, S.Kom",
            "Head of Infrastructure",
            "+628111000333",
            "rina.head@rscm.co.id",
            "ACTIVE"));

        var contacts = await queryService.GetCustomerContacts(customer1.Id);
        contacts.Should().HaveCount(2);
        contacts.Should().ContainSingle(c => c.Id == contact1.Id && c.Name == "Dr. Andi Wijaya" && c.Position == "CMIO");
        contacts.Should().ContainSingle(c =>
            c.Id == contact2.Id &&
            c.Name == "Rina Kusuma, S.Kom" &&
            c.Position == "Head of Infrastructure" &&
            c.PhoneNumber == "+628111000333");

        // 6. ListActiveCustomers includes both active customers
        var activeBeforeDeactivate = await queryService.ListActiveCustomers();
        activeBeforeDeactivate.Should().Contain(c => c.Id == customer1.Id);
        activeBeforeDeactivate.Should().Contain(c => c.Id == customer2.Id);

        // 7. Deactivate customer2 via DeactivateCustomerCommand
        var deactivated = await mediator.Send(new DeactivateCustomerCommand(customer2.Id));
        deactivated.Status.Should().Be("INACTIVE");
        deactivated.IsActive.Should().BeFalse();

        // ListActiveCustomers excludes deactivated customer
        var activeAfterDeactivate = await queryService.ListActiveCustomers();
        activeAfterDeactivate.Should().Contain(c => c.Id == customer1.Id);
        activeAfterDeactivate.Should().NotContain(c => c.Id == customer2.Id);

        // GetCustomerById still preserves historical record of deactivated customer
        var historicalCustomer2 = await queryService.GetCustomerById(customer2.Id);
        historicalCustomer2.Should().NotBeNull();
        historicalCustomer2!.Status.Should().Be("INACTIVE");
    }

    [Fact]
    public async Task Foreign_key_constraint_prevents_orphaned_customer_contacts()
    {
        if (!_sqlServerAvailable || _factory is null) return;

        using var scope = _factory.Services.CreateScope();
        var contactRepo = scope.ServiceProvider.GetRequiredService<ICustomerContactRepository>();

        var act = async () => await contactRepo.AddAsync(new Cakra.Modules.Customer.Domain.CustomerContact
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Name = "Orphaned Contact",
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow
        });

        await act.Should().ThrowAsync<SqlException>();
    }
}
