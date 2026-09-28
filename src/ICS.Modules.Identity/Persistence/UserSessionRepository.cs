namespace ICS.Modules.Identity.Persistence;

using Dapper;
using ICS.Core.Data;
using ICS.Modules.Identity.Domain;

/// <summary>
/// Concrete Dapper repository for <see cref="UserSession"/> using explicit parameterized SQL.
/// Architecture §14, §19.3, §20.
/// </summary>
public class UserSessionRepository : IUserSessionRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserSessionRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<UserSession?> GetByTokenAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
            return null;

        const string sql = @"
            SELECT 
                SessionId,
                UserId,
                PersonId,
                SessionToken,
                ExpiresAt,
                CreatedAt,
                ClientIp,
                UserAgent,
                IsRevoked
            FROM [identity].UserSessions
            WHERE SessionToken = @SessionToken";

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<UserSession>(
            new CommandDefinition(sql, new { SessionToken = sessionToken.Trim() }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<UserSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                SessionId,
                UserId,
                PersonId,
                SessionToken,
                ExpiresAt,
                CreatedAt,
                ClientIp,
                UserAgent,
                IsRevoked
            FROM [identity].UserSessions
            WHERE SessionId = @SessionId";

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<UserSession>(
            new CommandDefinition(sql, new { SessionId = sessionId }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task AddAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        const string sql = @"
            INSERT INTO [identity].UserSessions (
                SessionId,
                UserId,
                PersonId,
                SessionToken,
                ExpiresAt,
                CreatedAt,
                ClientIp,
                UserAgent,
                IsRevoked
            ) VALUES (
                @SessionId,
                @UserId,
                @PersonId,
                @SessionToken,
                @ExpiresAt,
                @CreatedAt,
                @ClientIp,
                @UserAgent,
                @IsRevoked
            )";

        var parameters = new
        {
            SessionId = session.SessionId,
            UserId = session.UserId,
            PersonId = session.PersonId,
            SessionToken = session.SessionToken,
            ExpiresAt = session.ExpiresAt,
            CreatedAt = session.CreatedAt,
            ClientIp = session.ClientIp,
            UserAgent = session.UserAgent,
            IsRevoked = session.IsRevoked
        };

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task UpdateAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        const string sql = @"
            UPDATE [identity].UserSessions
            SET 
                ExpiresAt = @ExpiresAt,
                IsRevoked = @IsRevoked
            WHERE SessionId = @SessionId";

        var parameters = new
        {
            SessionId = session.SessionId,
            ExpiresAt = session.ExpiresAt,
            IsRevoked = session.IsRevoked
        };

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task RevokeByTokenAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
            return;

        const string sql = @"
            UPDATE [identity].UserSessions
            SET IsRevoked = 1
            WHERE SessionToken = @SessionToken";

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { SessionToken = sessionToken.Trim() }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task RevokeAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE [identity].UserSessions
            SET IsRevoked = 1
            WHERE UserId = @UserId AND IsRevoked = 0";

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));
    }
}
