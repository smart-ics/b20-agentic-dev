namespace ICS.Modules.Organization.Persistence;

using ICS.Modules.Organization.Domain;

/// <summary>
/// Internal repository contract for Person persistence.
/// </summary>
internal interface IPersonRepository
{
    Task<Person?> GetByIdAsync(Guid personId, CancellationToken cancellationToken = default);
    Task<Person?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Person>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Person person, CancellationToken cancellationToken = default);
    Task UpdateAsync(Person person, CancellationToken cancellationToken = default);
}
