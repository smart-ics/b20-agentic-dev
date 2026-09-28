using System.Data;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Identity.Domain;
using Dapper;

namespace Cakra.Modules.Identity.Persistence;

/// <summary>
/// Dapper repository implementation for <see cref="UserSession"/> aggregate roots (Architecture §14, §19.3).
/// Executes explicit, parameterized SQL queries exclusively against the <c>identity</c> schema.
/// </summary>
public sealed class UserSessionRepository : IUserSessionRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserSessionRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<UserSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                SessionId AS Id,
                SessionId,
                UserId,
                PersonId,
                SessionToken,
                ExpiresAt,
                CreatedAt,
                UpdatedAt,
                ClientIp,
                UserAgent,
                IsRevoked
            FROM [identity].[UserSessions]
            WHERE SessionId = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<UserSession>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<UserSession?> GetByTokenAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                SessionId AS Id,
                SessionId,
                UserId,
                PersonId,
                SessionToken,
                ExpiresAt,
                CreatedAt,
                UpdatedAt,
                ClientIp,
                UserAgent,
                IsRevoked
            FROM [identity].[UserSessions]
            WHERE SessionToken = @SessionToken;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<UserSession>(
            new CommandDefinition(sql, new { SessionToken = sessionToken }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<UserSession>> GetActiveSessionsByUserIdAsync(
        Guid userId,
        DateTime currentUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                SessionId AS Id,
                SessionId,
                UserId,
                PersonId,
                SessionToken,
                ExpiresAt,
                CreatedAt,
                UpdatedAt,
                ClientIp,
                UserAgent,
                IsRevoked
            FROM [identity].[UserSessions]
            WHERE UserId = @UserId
              AND IsRevoked = 0
              AND ExpiresAt > @CurrentUtc
            ORDER BY CreatedAt DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<UserSession>(
            new CommandDefinition(sql, new { UserId = userId, CurrentUtc = currentUtc }, cancellationToken: cancellationToken));
        return results.AsList();
    }

    public async Task<IReadOnlyList<UserSession>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                SessionId AS Id,
                SessionId,
                UserId,
                PersonId,
                SessionToken,
                ExpiresAt,
                CreatedAt,
                UpdatedAt,
                ClientIp,
                UserAgent,
                IsRevoked
            FROM [identity].[UserSessions]
            ORDER BY CreatedAt DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<UserSession>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return results.AsList();
    }

    public async Task AddAsync(UserSession entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        const string sql = """
            INSERT INTO [identity].[UserSessions]
            (
                SessionId,
                UserId,
                PersonId,
                SessionToken,
                ExpiresAt,
                CreatedAt,
                UpdatedAt,
                ClientIp,
                UserAgent,
                IsRevoked
            )
            VALUES
            (
                @SessionId,
                @UserId,
                @PersonId,
                @SessionToken,
                @ExpiresAt,
                @CreatedAt,
                @UpdatedAt,
                @ClientIp,
                @UserAgent,
                @IsRevoked
            );
            """;

        var parameters = new
        {
            SessionId = entity.SessionId,
            entity.UserId,
            entity.PersonId,
            entity.SessionToken,
            entity.ExpiresAt,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.ClientIp,
            entity.UserAgent,
            entity.IsRevoked
        };

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(UserSession entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        const string sql = """
            UPDATE [identity].[UserSessions]
            SET
                ExpiresAt = @ExpiresAt,
                UpdatedAt = @UpdatedAt,
                ClientIp = @ClientIp,
                UserAgent = @UserAgent,
                IsRevoked = @IsRevoked
            WHERE SessionId = @SessionId;
            """;

        var parameters = new
        {
            SessionId = entity.SessionId,
            entity.ExpiresAt,
            entity.UpdatedAt,
            entity.ClientIp,
            entity.UserAgent,
            entity.IsRevoked
        };

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<bool> RevokeByTokenAsync(
        string sessionToken,
        DateTime? revokedAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE [identity].[UserSessions]
            SET
                IsRevoked = 1,
                UpdatedAt = @UpdatedAt
            WHERE SessionToken = @SessionToken
              AND IsRevoked = 0;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new { SessionToken = sessionToken, UpdatedAt = revokedAtUtc ?? DateTime.UtcNow },
                cancellationToken: cancellationToken));

        return rowsAffected > 0;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM [identity].[UserSessions]
            WHERE SessionId = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
