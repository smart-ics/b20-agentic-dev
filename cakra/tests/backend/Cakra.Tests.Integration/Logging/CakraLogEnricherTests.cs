using System.Diagnostics;
using System.Security.Claims;
using Cakra.Api.Infrastructure.Context;
using Cakra.Api.Infrastructure.Logging;
using Cakra.Core;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Cakra.Tests.Integration.Logging;

public class CakraLogEnricherTests
{
    private static (LogEvent LogEvent, FakePropertyFactory Factory) CreateLogEvent()
    {
        var messageTemplate = new MessageTemplateParser().Parse("Test message");
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            messageTemplate,
            properties: Enumerable.Empty<LogEventProperty>());

        return (logEvent, new FakePropertyFactory());
    }

    [Fact]
    public void Enriches_UserId_and_PersonId_from_CurrentContextProvider()
    {
        var userId = Guid.NewGuid();
        var personId = Guid.NewGuid();

        var services = new ServiceCollection();
        var contextProvider = new CurrentContextProvider();
        contextProvider.Initialize(userId, personId, new[] { "Admin" });
        services.AddSingleton<ICurrentContextProvider>(contextProvider);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            TraceIdentifier = "test-trace-123"
        };

        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var enricher = new CakraLogEnricher(httpContextAccessor);

        var (logEvent, factory) = CreateLogEvent();
        enricher.Enrich(logEvent, factory);

        logEvent.Properties.Should().ContainKey("TraceId");
        logEvent.Properties["TraceId"].ToString().Trim('"').Should().Be("test-trace-123");

        logEvent.Properties.Should().ContainKey("UserId");
        logEvent.Properties["UserId"].ToString().Trim('"').Should().Be(userId.ToString());

        logEvent.Properties.Should().ContainKey("PersonId");
        logEvent.Properties["PersonId"].ToString().Trim('"').Should().Be(personId.ToString());
    }

    [Fact]
    public void Enriches_TraceId_and_SpanId_from_Activity()
    {
        var activitySource = new ActivitySource("Cakra.Test");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = activitySource.StartActivity("TestOperation");
        activity.Should().NotBeNull();

        var enricher = new CakraLogEnricher(new HttpContextAccessor());
        var (logEvent, factory) = CreateLogEvent();
        enricher.Enrich(logEvent, factory);

        logEvent.Properties.Should().ContainKey("TraceId");
        logEvent.Properties["TraceId"].ToString().Trim('"').Should().Be(activity!.TraceId.ToHexString());

        logEvent.Properties.Should().ContainKey("SpanId");
        logEvent.Properties["SpanId"].ToString().Trim('"').Should().Be(activity.SpanId.ToHexString());
    }

    private sealed class FakePropertyFactory : Serilog.Core.ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
        {
            return new LogEventProperty(name, new ScalarValue(value));
        }
    }
}
