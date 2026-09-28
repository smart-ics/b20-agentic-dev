namespace ICS.Modules.Identity.Persistence;

using Dapper;
using ICS.Core.Data;
using ICS.Modules.Identity.Domain;

/// <summary>
/// Concrete Dapper repository for <see cref="UserAccount"/> using explicit parameterized SQL.
/// Architecture §14, §19.3, §20.
/// </summary>
public class UserAccountRepository : IUserAccountRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserAccountRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc />
    public async Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
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
            FROM [identity].UserAccounts
            WHERE UserId = @UserId";

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<UserAccount>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<UserAccount?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(usernameOrEmail))
            return null;

        var normalized = usernameOrEmail.Trim();
        var normalizedEmail = normalized.ToLowerInvariant();

        const string sql = @"
            SELECT 
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
            FROM [identity].UserAccounts
            WHERE Username = @Username OR Email = @Email";

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<UserAccount>(
            new CommandDefinition(sql, new { Username = normalized, Email = normalizedEmail }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<UserAccount?> GetByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
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
            FROM [identity].UserAccounts
            WHERE PersonId = @PersonId";

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<UserAccount>(
            new CommandDefinition(sql, new { PersonId = personId }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task AddAsync(UserAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        const string sql = @"
            INSERT INTO [identity].UserAccounts (
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
            ) VALUES (
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
            )";

        var parameters = new
        {
            UserId = account.UserId,
            PersonId = account.PersonId,
            Username = account.Username,
            Email = account.Email,
            PasswordHash = account.PasswordHash,
            Status = account.Status,
            FailedLoginAttempts = account.FailedLoginAttempts,
            LastLoginAt = account.LastLoginAt,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt
        };

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task UpdateAsync(UserAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        const string sql = @"
            UPDATE [identity].UserAccounts
            SET 
                PersonId = @PersonId,
                Username = @Username,
                Email = @Email,
                PasswordHash = @PasswordHash,
                Status = @Status,
                FailedLoginAttempts = @FailedLoginAttempts,
                LastLoginAt = @LastLoginAt,
                UpdatedAt = @UpdatedAt
            WHERE UserId = @UserId";

        var parameters = new
        {
            UserId = account.UserId,
            PersonId = account.PersonId,
            Username = account.Username,
            Email = account.Email,
            PasswordHash = account.PasswordHash,
            Status = account.Status,
            FailedLoginAttempts = account.FailedLoginAttempts,
            LastLoginAt = account.LastLoginAt,
            UpdatedAt = account.UpdatedAt
        };

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }
}
