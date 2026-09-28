using Cakra.Core;

namespace Cakra.Modules.Identity.Domain;

/// <summary>
/// Repository interface for <see cref="UserSession"/> aggregate roots (Architecture §14, §19.3).
/// Writes exclusively to the <c>identity</c> schema using parameterized SQL.
/// </summary>
public interface IUserSessionRepository : IRepository<UserSession>
{
    /// <summary>
    /// Retrieves a session by its unique session token.
    /// </summary>
    Task<UserSession?> GetByTokenAsync(string sessionToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active (unrevoked and unexpired) sessions for the specified user.
    /// </summary>
    Task<IReadOnlyList<UserSession>> GetActiveSessionsByUserIdAsync(Guid userId, DateTime currentUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a session as revoked by its session token.
    /// </summary>
    Task<bool> RevokeByTokenAsync(string sessionToken, DateTime? revokedAtUtc = null, CancellationToken cancellationToken = default);
}
