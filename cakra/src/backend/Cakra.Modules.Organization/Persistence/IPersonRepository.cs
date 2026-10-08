using Cakra.Core;
using Cakra.Modules.Organization.Domain;

namespace Cakra.Modules.Organization.Persistence;

/// <summary>
/// Persistence contract for <see cref="Person"/> aggregate root (Architecture §6, §19.3).
/// </summary>
internal interface IPersonRepository : IRepository<Person>
{
    /// <summary>Retrieves a person by email, or null if not found.</summary>
    Task<Person?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Retrieves all persons currently in ACTIVE status.</summary>
    Task<IReadOnlyList<Person>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates the lifecycle status of a person.</summary>
    Task UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a person DTO by identifier, aggregating active role names in a single query (Architecture CR-027 §4 TD-003).</summary>
    Task<PersonDto?> GetPersonDtoByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<PersonDto?>(null);

    /// <summary>Retrieves all person DTOs, aggregating active role names in a single query (Architecture CR-027 §4 TD-003).</summary>
    Task<IReadOnlyList<PersonDto>> GetAllPersonDtosAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PersonDto>>(Array.Empty<PersonDto>());
}
