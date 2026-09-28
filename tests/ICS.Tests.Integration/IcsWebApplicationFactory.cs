namespace ICS.Tests.Integration;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ICS.Core.Data;
using ICS.Web.Migrations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

/// <summary>
/// Custom WebApplicationFactory for in-process integration testing per Architecture §19.8.
/// Configures an isolated SQL Server test database connection and coordinates Respawn resets.
/// </summary>
public class IcsWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DefaultTestConnectionString = "Server=localhost;Database=ICS_Test;Trusted_Connection=True;TrustServerCertificate=True;";
    private readonly string _testConnectionString;
    private readonly DatabaseResetHelper? _resetHelper;

    public IcsWebApplicationFactory()
    {
        _testConnectionString = ResolveTestConnectionString();
    }

    /// <summary>
    /// Gets the resolved connection string for the isolated test database.
    /// </summary>
    public string TestConnectionString => _testConnectionString;

    /// <section>
    /// Gets the database reset helper managing Respawn isolation.
    /// </summary>
    public DatabaseResetHelper ResetHelper => _resetHelper ??= new DatabaseResetHelper(_testConnectionString);

    /// <summary>
    /// Resolves the test database connection string from environment variables or default fallback.
    /// Supports TEST_CONNECTION_STRING, ConnectionStrings__TestConnection, or fallback to ICS_Test.
    /// </summary>
    public static string ResolveTestConnectionString()
    {
        return Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__TestConnection")
            ?? DefaultTestConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _testConnectionString);
        builder.UseSetting("ConnectionStrings:TestConnection", _testConnectionString);

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _testConnectionString,
                ["ConnectionStrings:TestConnection"] = _testConnectionString
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbConnectionFactory>();
            services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(_testConnectionString));

            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Organization.Domain.Events.PersonCreated>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Organization.Domain.Events.PersonDeactivated>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Organization.Domain.Events.RoleAssigned>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Organization.Domain.Events.RoleRevoked>, TestDomainEventCollector>();

            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Customer.Domain.Events.CustomerCreated>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Customer.Domain.Events.CustomerDeactivated>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Customer.Domain.Events.CustomerActivated>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Customer.Domain.Events.CustomerMasterDataUpdated>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Customer.Domain.Events.CustomerContactAdded>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Customer.Domain.Events.CustomerContactUpdated>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Customer.Domain.Events.CustomerContactDeactivated>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Customer.Domain.Events.CustomerContactActivated>, TestDomainEventCollector>();

            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Product.Domain.Events.ProductCreated>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotigationHandler<ICS.Modules.Product.Domain.Events.ProductOwnerChanged>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Product.Domain.Events.ProductActivated>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotigationHandler<ICS.Modules.Product.Domain.Events.ProductDeactivated>, TestDomainEventCollector>();

            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Request.Domain.Events.RequestRecorded>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Request.Domain.Events.RequestAssigned>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Request.Domain.Events.RequestEvaluated>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Request.Domain.Events.RequestAccepted>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Request.Domain.Events.RequestRejected>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Request.Domain.Events.RequestEscalated>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Request.Domain.Events.ManagementDecisionRequested>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Request.Domain.Events.RequestCompleted>, TestDomainEventCollector>();
            
            // Post domain events
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Post.Domain.Events.PostCreated>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Post.Domain.Events.CommentAdded>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Post.Domain.Events.ReactionAdded>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Post.Domain.Events.ReactionRemoved>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Post.Domain.Events.PostVisibilityChanged>, TestDomainEventCollector>();
            services.AddTransient<MediatR.INotificationHandler<ICS.Modules.Post.Domain.Events.PostArchived>, TestDomainEventCollector>();
        });
    }

    /// <summary>
    /// Resets all data in bounded context schemas using Respawn.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await ResetHelper.ResetAsync();
    }

    public async Task InitializeAsync()
    {
        // Force host creation and startup so DbUp migrations apply against the test database
        _ = CreateClient();
        await ResetHelper.InitializeAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
    }
}