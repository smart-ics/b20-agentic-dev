namespace ICS.Modules.Identity.Persistence;

using ICS.Modules.Identity.Domain;

/// <summary>
/// Repository contract for persisting and retrieving <see cref="UserSession"/> entities.
/// Executes exclusively against the 'identity' database schema via parameterized SQL per Architecture §14, §19.3, and §20.
/// </summary>
public interface IUserSessionRepository
{
    /// <summary>
    /// Retrieves an active or revoked session by its unique session token string.
    /// </summary>
    Task<UserSession?> GetByTokenAsync(string sessionToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a session by its unique SessionId.
    /// </summary>
    Task<UserSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new UserSession record.
    /// </summary>
    Task AddAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing UserSession record.
    /// </summary>
    Task UpdateAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a session as revoked by its session token.
    /// </summary>
    Task RevokeByTokenAsync(string sessionToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all active sessions for the specified user (e.g. on password reset or account lock).
    /// </summary>
    Task RevokeAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
