namespace Cakra.Modules.Identity.Services;

/// <summary>
/// Application service contract for identity authorization and role resolution (Architecture §14, §15, §19.5).
/// Coordinates dynamic role resolution backed by the Organization module.
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Resolves active organizational roles for the specified person.
    /// </summary>
    /// <param name="personId">The unique identifier of the person.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of active role names.</returns>
    Task<IReadOnlyList<string>> ResolveRolesAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous convenience overload for <see cref="ResolveRolesAsync"/> (Architecture §14).
    /// </summary>
    /// <param name="personId">The unique identifier of the person.</param>
    /// <returns>A list of active role names.</returns>
    IReadOnlyList<string> ResolveRoles(Guid personId) =>
        ResolveRolesAsync(personId, CancellationToken.None).GetAwaiter().GetResult();
}
