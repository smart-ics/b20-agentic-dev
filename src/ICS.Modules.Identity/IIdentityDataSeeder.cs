namespace ICS.Modules.Identity;

using ICS.Modules.Identity.Domain;

/// <summary>
/// Helper service for provisioning initial active admin and test user accounts.
/// Supports immediate test execution and initial system bootstrapping.
/// </summary>
public interface IIdentityDataSeeder
{
    /// <summary>
    /// Ensures an initial active administrator account exists, creating one if not present.
    /// </summary>
    Task<UserAccount> SeedInitialAdminUserAsync(
        string username = "admin",
        string email = "admin@smart-ics.internal",
        string password = "AdminPassword123!",
        Guid? personId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a user account with explicit credentials and status for test scenarios.
    /// </summary>
    Task<UserAccount> CreateUserAsync(
        string username,
        string email,
        string password,
        Guid? personId = null,
        string status = UserAccountStatus.Active,
        CancellationToken cancellationToken = default);
}
