namespace Cakra.Modules.Identity.Services;

/// <summary>
/// Application service contract for administrative user account operations (Architecture §14; CR-007).
/// </summary>
public interface IUserAccountService
{
    /// <summary>
    /// Retrieves all user accounts enriched with person display names in memory.
    /// </summary>
    Task<IReadOnlyList<UserAccountSummaryDto>> GetAllUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single user account by its identifier, enriched with person display name.
    /// </summary>
    Task<UserAccountDto?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new user account with validated credentials and associated person.
    /// </summary>
    Task<UserAccountDto> CreateUserAsync(CreateUserAccountCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates attributes, status, and/or resets password of an existing user account.
    /// </summary>
    Task<UserAccountDto> UpdateUserAsync(Guid id, UpdateUserAccountCommand command, CancellationToken cancellationToken = default);
}
