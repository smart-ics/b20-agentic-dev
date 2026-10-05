namespace Cakra.Modules.Organization;

/// <summary>
/// Cross-module in-process query contract for organizational data (Architecture §7, §14, §15, §20).
/// Published interface consumed by Identity, Product, Request, Work Package, Post, and Analytics modules.
/// </summary>
public interface IOrganizationQueryService
{
    /// <summary>
    /// Checks whether the specified person exists and is currently active.
    /// </summary>
    /// <param name="personId">The unique identifier of the person.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the person exists and has ACTIVE status; otherwise <c>false</c>.</returns>
    Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous convenience overload for <see cref="IsPersonActiveAsync"/>.
    /// </summary>
    bool IsPersonActive(Guid personId) =>
        IsPersonActiveAsync(personId, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Retrieves an authoritative person record by identifier, or <c>null</c> if not found (Architecture §7).
    /// </summary>
    /// <param name="personId">The unique identifier of the person.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PersonDto?> GetPersonByIdAsync(Guid personId, CancellationToken cancellationToken = default) =>
        Task.FromResult<PersonDto?>(null);

    /// <summary>
    /// Synchronous convenience overload for <see cref="GetPersonByIdAsync"/> (Architecture §7).
    /// </summary>
    PersonDto? GetPersonById(Guid personId) =>
        GetPersonByIdAsync(personId, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Retrieves all active persons ordered by last name and first name (Architecture §7).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<PersonDto>> ListActivePersonsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PersonDto>>(Array.Empty<PersonDto>());

    /// <summary>
    /// Synchronous convenience overload for <see cref="ListActivePersonsAsync"/> (Architecture §7).
    /// </summary>
    IReadOnlyList<PersonDto> ListActivePersons() =>
        ListActivePersonsAsync(CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Retrieves all persons (both active and inactive) ordered by last name and first name (Architecture §7).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<PersonDto>> ListAllPersonsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PersonDto>>(Array.Empty<PersonDto>());

    /// <summary>
    /// Synchronous convenience overload for <see cref="ListAllPersonsAsync"/> (Architecture §7).
    /// </summary>
    IReadOnlyList<PersonDto> ListAllPersons() =>
        ListAllPersonsAsync(CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Retrieves all members belonging to the specified team (Architecture §7).
    /// </summary>
    /// <param name="teamId">The unique identifier of the team.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<TeamRosterMemberDto>> GetTeamRosterAsync(Guid teamId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TeamRosterMemberDto>>(Array.Empty<TeamRosterMemberDto>());

    /// <summary>
    /// Synchronous convenience overload for <see cref="GetTeamRosterAsync"/> (Architecture §7).
    /// </summary>
    IReadOnlyList<TeamRosterMemberDto> GetTeamRoster(Guid teamId) =>
        GetTeamRosterAsync(teamId, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Retrieves all active role names assigned to the specified person (Architecture §7, §14, §15, §19.5).
    /// </summary>
    /// <param name="personId">The unique identifier of the person.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of distinct role names currently assigned and non-revoked.</returns>
    Task<IReadOnlyList<string>> GetPersonRolesAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous convenience overload for <see cref="GetPersonRolesAsync"/> (Architecture §7, §14).
    /// </summary>
    IReadOnlyList<string> GetPersonRoles(Guid personId) =>
        GetPersonRolesAsync(personId, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Retrieves all responsibilities currently assigned to the specified person (Architecture §7).
    /// </summary>
    /// <param name="personId">The unique identifier of the person.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<PersonResponsibilityDto>> GetPersonResponsibilitiesAsync(Guid personId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PersonResponsibilityDto>>(Array.Empty<PersonResponsibilityDto>());

    /// <summary>
    /// Synchronous convenience overload for <see cref="GetPersonResponsibilitiesAsync"/> (Architecture §7).
    /// </summary>
    IReadOnlyList<PersonResponsibilityDto> GetPersonResponsibilities(Guid personId) =>
        GetPersonResponsibilitiesAsync(personId, CancellationToken.None).GetAwaiter().GetResult();
}
