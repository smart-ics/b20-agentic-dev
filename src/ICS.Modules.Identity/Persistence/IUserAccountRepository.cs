namespace ICS.Modules.Identity.Persistence;

using ICS.Modules.Identity.Domain;

/// <summary>
/// Repository contract for persisting and retrieving <see cref="UserAccount"/> entities.
/// Executes exclusively against the 'identity' database schema via parameterized SQL per Architecture §14, §19.3, and §20.
/// </summary>
public interface IUserAccountRepository
{
    /// <summary>
    /// Retrieves a UserAccount by its unique identifier.
    /// </summary>
    Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a UserAccount by username or email address.
    /// </summary>
    Task<UserAccount?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a UserAccount linked to the specified PersonId.
    /// </summary>
    Task<UserAccount?> GetByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new UserAccount record.
    /// </summary>
    Task AddAsync(UserAccount account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing UserAccount record.
    /// </summary>
    Task UpdateAsync(UserAccount account, CancellationToken cancellationToken = default);
}
