using Cakra.Modules.Organization;
using Microsoft.Extensions.Logging;

namespace Cakra.Modules.Identity.Services;

/// <summary>
/// Dynamic role resolution and RBAC coordination service (Architecture §14, §15, §19.5).
/// Resolves active roles assigned to a person by querying the Organization module.
/// </summary>
public sealed class AuthorizationService : IAuthorizationService
{
    private readonly IOrganizationQueryService? _organizationQueryService;
    private readonly ILogger<AuthorizationService>? _logger;

    public AuthorizationService(
        IOrganizationQueryService? organizationQueryService = null,
        ILogger<AuthorizationService>? logger = null)
    {
        _organizationQueryService = organizationQueryService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ResolveRolesAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        if (_organizationQueryService is null)
        {
            _logger?.LogDebug("OrganizationQueryService not available; resolving empty roles for PersonId {PersonId}", personId);
            return Array.Empty<string>();
        }

        try
        {
            var roles = await _organizationQueryService.GetPersonRolesAsync(personId, cancellationToken);
            return roles ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to resolve active roles for PersonId {PersonId}", personId);
            return Array.Empty<string>();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ResolveRoles(Guid personId) =>
        ResolveRolesAsync(personId, CancellationToken.None).GetAwaiter().GetResult();
}
