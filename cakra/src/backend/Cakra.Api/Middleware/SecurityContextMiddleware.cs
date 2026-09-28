using System.Security.Claims;
using Cakra.Api.Infrastructure.Context;
using Cakra.Core;
using Serilog.Context;

namespace Cakra.Api.Middleware;

/// <summary>
/// Security context population middleware (Architecture §14, §18, §19.5).
/// Inspects the authenticated <see cref="ClaimsPrincipal"/> established by the Cookie Authentication handler,
/// resolves the authenticated <c>UserId</c>, <c>PersonId</c>, and dynamic <c>Roles</c>,
/// and populates the scoped <see cref="ICurrentContextProvider"/> (and transitively <see cref="IAuditContext"/>)
/// while enriching Serilog <see cref="LogContext"/> properties.
/// </summary>
public sealed class SecurityContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityContextMiddleware> _logger;

    public SecurityContextMiddleware(RequestDelegate next, ILogger<SecurityContextMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context, ICurrentContextProvider contextProvider)
    {
        Guid? userId = null;
        Guid? personId = null;
        var roles = Array.Empty<string>();

        var user = context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value
                ?? user.FindFirst("userId")?.Value;

            if (Guid.TryParse(userIdClaim, out var parsedUserId))
            {
                userId = parsedUserId;
            }

            var personIdClaim = user.FindFirst("personId")?.Value
                ?? user.FindFirst("person_id")?.Value;

            if (Guid.TryParse(personIdClaim, out var parsedPersonId))
            {
                personId = parsedPersonId;
            }

            roles = user.FindAll(ClaimTypes.Role)
                .Concat(user.FindAll("role"))
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (contextProvider is CurrentContextProvider provider)
        {
            provider.Initialize(userId, personId, roles);
        }

        IDisposable? userProp = userId.HasValue ? LogContext.PushProperty("UserId", userId.Value) : null;
        IDisposable? personProp = personId.HasValue ? LogContext.PushProperty("PersonId", personId.Value) : null;

        try
        {
            await _next(context);
        }
        finally
        {
            personProp?.Dispose();
            userProp?.Dispose();
        }
    }
}
