namespace ICS.Modules.Identity.Application;

using ICS.Modules.Organization;
using Microsoft.Extensions.Logging;

/// <summary>
/// Concrete authorization service resolving active roles from Organization module per Architecture §14 and §15.
/// </summary>
public class AuthorizationService : IAuthorizationService
{
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ILogger<AuthorizationService>? _logger;

    public AuthorizationService(
        IOrganizationQueryService organizationQueryService,
        ILogger<AuthorizationService>? logger = null)
    {
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ResolveRolesAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        try
        {
            var roles = await _organizationQueryService.GetPersonRolesAsync(personId, cancellationToken);
            return roles ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to resolve roles for PersonId {PersonId}", personId);
            return Array.Empty<string>();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ResolveRoles(Guid personId)
    {
        return ResolveRolesAsync(personId).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetRolesForPersonAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        return ResolveRolesAsync(personId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HasRoleAsync(Guid personId, string role, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        var roles = await ResolveRolesAsync(personId, cancellationToken);
        return roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<bool> HasAnyRoleAsync(Guid personId, IEnumerable<string> roles, CancellationToken cancellationToken = default)
    {
        var roleList = roles?.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
        if (roleList == null || roleList.Count == 0)
        {
            return false;
        }

        var assignedRoles = await ResolveRolesAsync(personId, cancellationToken);
        var roleSet = new HashSet<string>(assignedRoles, StringComparer.OrdinalIgnoreCase);
        return roleList.Any(r => roleSet.Contains(r));
    }
}
