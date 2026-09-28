using Cakra.Core;

namespace Cakra.Modules.Identity.Domain;

/// <summary>
/// Repository interface for <see cref="UserAccount"/> aggregate roots (Architecture §14, §19.3).
/// Writes exclusively to the <c>identity</c> schema using parameterized SQL.
/// </summary>
public interface IUserAccountRepository : IRepository<UserAccount>
{
    /// <summary>
    /// Retrieves a user account by matching its username or email address.
    /// </summary>
    Task<UserAccount?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a user account linked to the specified organization person identifier.
    /// </summary>
    Task<UserAccount?> GetByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default);
}
