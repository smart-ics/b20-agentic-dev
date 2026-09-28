using System.Security.Claims;
using Cakra.Api.Infrastructure.Context;
using Cakra.Api.Middleware;
using Cakra.Core;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cakra.Tests.Integration.Middleware;

public class SecurityContextMiddlewareTests
{
    [Fact]
    public async Task Populates_CurrentContextProvider_from_authenticated_claims()
    {
        var userId = Guid.NewGuid();
        var personId = Guid.NewGuid();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("personId", personId.ToString()),
            new Claim(ClaimTypes.Role, "Management"),
            new Claim(ClaimTypes.Role, "Programmer")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext
        {
            User = principal
        };

        var provider = new CurrentContextProvider();
        var middleware = new SecurityContextMiddleware(
            next: _ => Task.CompletedTask,
            logger: NullLogger<SecurityContextMiddleware>.Instance);

        await middleware.InvokeAsync(context, provider);

        provider.CurrentUserId.Should().Be(userId);
        provider.CurrentPersonId.Should().Be(personId);
        provider.CurrentRoles.Should().BeEquivalentTo(new[] { "Management", "Programmer" });
    }

    [Fact]
    public async Task Leaves_CurrentContextProvider_anonymous_when_user_is_not_authenticated()
    {
        var context = new DefaultHttpContext();
        var provider = new CurrentContextProvider();
        var middleware = new SecurityContextMiddleware(
            next: _ => Task.CompletedTask,
            logger: NullLogger<SecurityContextMiddleware>.Instance);

        await middleware.InvokeAsync(context, provider);

        provider.CurrentUserId.Should().BeNull();
        provider.CurrentPersonId.Should().BeNull();
        provider.CurrentRoles.Should().BeEmpty();
    }
}
