using System.Data;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Identity.Domain;
using Dapper;

namespace Cakra.Modules.Identity.Persistence;

/// <summary>
/// Dapper repository implementation for <see cref="UserAccount"/> aggregate roots (Architecture §14, §19.3).
/// Executes explicit, parameterized SQL queries exclusively against the <c>identity</c> schema.
/// </summary>
public sealed class UserAccountRepository : IUserAccountRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserAccountRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                UserId AS Id,
                UserId,
                PersonId,
                Username,
                Email,
                PasswordHash,
                Status,
                FailedLoginAttempts,
                LastLoginAt,
                CreatedAt,
                UpdatedAt
            FROM [identity].[UserAccounts]
            WHERE UserId = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<UserAccount>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<UserAccount?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                UserId AS Id,
                UserId,
                PersonId,
                Username,
                Email,
                PasswordHash,
                Status,
                FailedLoginAttempts,
                LastLoginAt,
                CreatedAt,
                UpdatedAt
            FROM [identity].[UserAccounts]
            WHERE Username = @UsernameOrEmail OR Email = @UsernameOrEmail;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<UserAccount>(
            new CommandDefinition(sql, new { UsernameOrEmail = usernameOrEmail }, cancellationToken: cancellationToken));
    }

    public async Task<UserAccount?> GetByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        if (personId == Guid.Empty)
        {
            return null;
        }

        const string sql = """
            SELECT
                UserId AS Id,
                UserId,
                PersonId,
                Username,
                Email,
                PasswordHash,
                Status,
                FailedLoginAttempts,
                LastLoginAt,
                CreatedAt,
                UpdatedAt
            FROM [identity].[UserAccounts]
            WHERE PersonId = @PersonId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<UserAccount>(
            new CommandDefinition(sql, new { PersonId = personId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                UserId AS Id,
                UserId,
                PersonId,
                Username,
                Email,
                PasswordHash,
                Status,
                FailedLoginAttempts,
                LastLoginAt,
                CreatedAt,
                UpdatedAt
            FROM [identity].[UserAccounts]
            ORDER BY Username ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<UserAccount>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return results.AsList();
    }

    public async Task AddAsync(UserAccount entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        const string sql = """
            INSERT INTO [identity].[UserAccounts]
            (
                UserId,
                PersonId,
                Username,
                Email,
                PasswordHash,
                Status,
                FailedLoginAttempts,
                LastLoginAt,
                CreatedAt,
                UpdatedAt
            )
            VALUES
            (
                @UserId,
                @PersonId,
                @Username,
                @Email,
                @PasswordHash,
                @Status,
                @FailedLoginAttempts,
                @LastLoginAt,
                @CreatedAt,
                @UpdatedAt
            );
            """;

        var parameters = new
        {
            UserId = entity.UserId,
            entity.PersonId,
            entity.Username,
            entity.Email,
            entity.PasswordHash,
            entity.Status,
            entity.FailedLoginAttempts,
            entity.LastLoginAt,
            entity.CreatedAt,
            entity.UpdatedAt
        };

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(UserAccount entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        const string sql = """
            UPDATE [identity].[UserAccounts]
            SET
                PersonId = @PersonId,
                Username = @Username,
                Email = @Email,
                PasswordHash = @PasswordHash,
                Status = @Status,
                FailedLoginAttempts = @FailedLoginAttempts,
                LastLoginAt = @LastLoginAt,
                UpdatedAt = @UpdatedAt
            WHERE UserId = @UserId;
            """;

        var parameters = new
        {
            UserId = entity.UserId,
            entity.PersonId,
            entity.Username,
            entity.Email,
            entity.PasswordHash,
            entity.Status,
            entity.FailedLoginAttempts,
            entity.LastLoginAt,
            entity.UpdatedAt
        };

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM [identity].[UserAccounts]
            WHERE UserId = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
