namespace ICS.Modules.Identity;

using ICS.Core.Time;
using ICS.Modules.Identity.Domain;
using ICS.Modules.Identity.Persistence;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Concrete data seeder provisioning initial admin and test user accounts.
/// </summary>
public class IdentityDataSeeder : IIdentityDataSeeder
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IPasswordHasher<UserAccount> _passwordHasher;
    private readonly ISystemClock _clock;

    public IdentityDataSeeder(
        IUserAccountRepository userAccountRepository,
        IPasswordHasher<UserAccount> passwordHasher,
        ISystemClock clock)
    {
        _userAccountRepository = userAccountRepository ?? throw new ArgumentNullException(nameof(userAccountRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public async Task<UserAccount> SeedInitialAdminUserAsync(
        string username = "admin",
        string email = "admin@smart-ics.internal",
        string password = "AdminPassword123!",
        Guid? personId = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await _userAccountRepository.GetByUsernameOrEmailAsync(username, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        return await CreateUserAsync(
            username: username,
            email: email,
            password: password,
            personId: personId,
            status: UserAccountStatus.Active,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserAccount> CreateUserAsync(
        string username,
        string email,
        string password,
        Guid? personId = null,
        string status = UserAccountStatus.Active,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.NewGuid();
        var effectivePersonId = personId ?? Guid.NewGuid();

        // Temporary user instance to compute hash
        var tempUser = UserAccount.Create(userId, effectivePersonId, username, email, "temp", _clock.UtcNow);
        var passwordHash = _passwordHasher.HashPassword(tempUser, password);

        var account = new UserAccount(
            userId: userId,
            personId: effectivePersonId,
            username: username.Trim(),
            email: email.Trim().ToLowerInvariant(),
            passwordHash: passwordHash,
            status: status,
            failedLoginAttempts: 0,
            lastLoginAt: null,
            createdAt: _clock.UtcNow,
            updatedAt: null);

        await _userAccountRepository.AddAsync(account, cancellationToken);
        return account;
    }
}
