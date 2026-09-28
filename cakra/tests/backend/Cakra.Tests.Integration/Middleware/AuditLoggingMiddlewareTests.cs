using Cakra.Api.Middleware;
using Cakra.Core;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cakra.Tests.Integration.Middleware;

public class AuditLoggingMiddlewareTests
{
    [Fact]
    public async Task AuditLoggingMiddleware_invokes_next_and_completes_for_api_request()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/system/probe";

        var nextInvoked = false;
        var middleware = new AuditLoggingMiddleware(
            next: _ =>
            {
                nextInvoked = true;
                return Task.CompletedTask;
            },
            logger: NullLogger<AuditLoggingMiddleware>.Instance);

        var auditContext = new TestAuditContext
        {
            ActorUserId = Guid.NewGuid(),
            ActorPersonId = Guid.NewGuid(),
            RecordedAtUtc = DateTime.UtcNow
        };

        await middleware.InvokeAsync(context, auditContext);

        nextInvoked.Should().BeTrue();
    }

    private sealed class TestAuditContext : IAuditContext
    {
        public Guid? ActorUserId { get; set; }
        public Guid? ActorPersonId { get; set; }
        public DateTime RecordedAtUtc { get; set; }
    }
}
