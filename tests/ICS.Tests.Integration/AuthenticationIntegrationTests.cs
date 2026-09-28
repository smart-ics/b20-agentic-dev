namespace ICS.Tests.Integration;

using System;
using System.Threading.Tasks;
using FluentAssertions;
using ICS.Modules.Identity;
using ICS.Modules.Identity.Application;
using ICS.Modules.Identity.Domain;
using ICS.Modules.Identity.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Integration tests verifying the Identity & Access authentication lifecycle against SQL Server.
/// Architecture §14, §19.5, §19.8, and P2-S09 completion criteria.
/// </summary>
public class AuthenticationIntegrationTests : IntegrationTestBase
{
    public AuthenticationIntegrationTests(IcsWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldSucceedAndCreateSessionInDatabase()
    {
        // Arrange
        var testUsername = "active.user";
        var testPassword = "ValidPassword123!";
        var clientIp = "192.168.1.100";
        var userAgent = "IntegrationTestAgent/1.0";

        var seededUser = await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            return await seeder.CreateUserAsync(testUsername, "active@smart-ics.internal", testPassword);
        });

        // Act
        var loginResult = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.LoginAsync(testUsername, testPassword, new ClientInfo(clientIp, userAgent));
        });

        // Assert - LoginResult
        loginResult.Succeeded.Should().BeTrue();
        loginResult.SessionToken.Should().NotBeNullOrWhiteSpace();
        loginResult.UserId.Should().Be(seededUser.UserId);
        loginResult.PersonId.Should().Be(seededUser.PersonId);
        loginResult.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        // Assert - Session in database (identity.UserSessions)
        await ExecuteInScopeAsync(async sp =>
        {
            var sessionRepo = sp.GetRequiredService<IUserSessionRepository>();
            var session = await sessionRepo.GetByTokenAsync(loginResult.SessionToken!);

            session.Should().NotBeNull();
            session!.UserId.Should().Be(seededUser.UserId);
            session.PersonId.Should().Be(seededUser.PersonId);
            session.SessionToken.Should().Be(loginResult.SessionToken);
            session.ClientIp.Should().Be(clientIp);
            session.UserAgent.Should().Be(userAgent);
            session.IsRevoked.Should().BeFalse();
            session.ExpiresAt.Should().BeCloseTo(loginResult.ExpiresAt!.Value, TimeSpan.FromSeconds(2));
        });

        // Assert - UserAccount in database (identity.UserAccounts)
        await ExecuteInScopeAsync(async sp =>
        {
            var accountRepo = sp.GetRequiredService<IUserAccountRepository>();
            var account = await accountRepo.GetByIdAsync(seededUser.UserId);

            account.Should().NotBeNull();
            account!.LastLoginAt.Should().NotBeNull();
            account.FailedLoginAttempts.Should().Be(0);
        });
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldFailAndIncrementFailedAttempts()
    {
        // Arrange
        var testUsername = "retry.user";
        var testPassword = "CorrectPassword123!";

        var seededUser = await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            return await seeder.CreateUserAsync(testUsername, "retry@smart-ics.internal", testPassword);
        });

        // Act
        var loginResult = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.LoginAsync(testUsername, "WrongPassword!");
        });

        // Assert
        loginResult.Succeeded.Should().BeFalse();
        loginResult.SessionToken.Should().BeNull();
        loginResult.ErrorMessage.Should().Be("Invalid username or password.");

        // Assert - Failed attempts incremented in database
        await ExecuteInScopeAsync(async sp =>
        {
            var accountRepo = sp.GetRequiredService<IUserAccountRepository>();
            var account = await accountRepo.GetByIdAsync(seededUser.UserId);

            account.Should().NotBeNull();
            account!.FailedLoginAttempts.Should().Be(1);
        });
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ShouldFail()
    {
        // Act
        var loginResult = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.LoginAsync("nonexistent.user", "AnyPassword123!");
        });

        // Assert
        loginResult.Succeeded.Should().BeFalse();
        loginResult.SessionToken.Should().BeNull();
        loginResult.ErrorMessage.Should().Be("Invalid username or password.");
    }

    [Fact]
    public async Task ValidateSession_WithValidToken_ShouldReturnAuthenticatedSecurityContext()
    {
        // Arrange
        var testUsername = "session.user";
        var testPassword = "ValidPassword123!";

        var seededUser = await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            return await seeder.CreateUserAsync(testUsername, "session@smart-ics.internal", testPassword);
        });

        var loginResult = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.LoginAsync(testUsername, testPassword);
        });

        loginResult.Succeeded.Should().BeTrue();

        // Act
        var context = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.ValidateSessionAsync(loginResult.SessionToken!);
        });

        // Assert
        context.IsAuthenticated.Should().BeTrue();
        context.UserId.Should().Be(seededUser.UserId);
        context.PersonId.Should().Be(seededUser.PersonId);
        context.SessionToken.Should().Be(loginResult.SessionToken);
        context.FailureReason.Should().BeNull();
    }

    [Fact]
    public async Task ValidateSession_WithExpiredToken_ShouldReturnUnauthenticatedContext()
    {
        // Arrange
        var testUsername = "expired.user";
        var testPassword = "ValidPassword123!";

        var (seededUser, expiredToken) = await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            var user = await seeder.CreateUserAsync(testUsername, "expired@smart-ics.internal", testPassword);

            var sessionRepo = sp.GetRequiredService<IUserSessionRepository>();
            var token = "expired-token-" + Guid.NewGuid();
            var pastTime = DateTime.UtcNow.AddHours(-2);
            var session = UserSession.Create(Guid.NewGuid(), user.UserId, user.PersonId, token, pastTime, pastTime.AddHours(-1));
            await sessionRepo.AddAsync(session);

            return (user, token);
        });

        // Act
        var context = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.ValidateSessionAsync(expiredToken);
        });

        // Assert
        context.IsAuthenticated.Should().BeFalse();
        context.FailureReason.Should().Contain("expired");
    }

    [Fact]
    public async Task Logout_ShouldInvalidateSessionAndPreventSubsequentValidation()
    {
        // Arrange
        var testUsername = "logout.user";
        var testPassword = "ValidPassword123!";

        await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            return await seeder.CreateUserAsync(testUsername, "logout@smart-ics.internal", testPassword);
        });

        var loginResult = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.LoginAsync(testUsername, testPassword);
        });

        loginResult.Succeeded.Should().BeTrue();
        var token = loginResult.SessionToken!;

        // Act - Logout
        var logoutResult = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.LogoutAsync(token);
        });

        logoutResult.Should().BeTrue();

        // Assert - Database session has IsRevoked = true
        await ExecuteInScopeAsync(async sp =>
        {
            var sessionRepo = sp.GetRequiredService<IUserSessionRepository>();
            var session = await sessionRepo.GetByTokenAsync(token);

            session.Should().NotBeNull();
            session!.IsRevoked.Should().BeTrue();
        });

        // Act & Assert - ValidateSession on revoked token
        var context = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.ValidateSessionAsync(token);
        });

        context.IsAuthenticated.Should().BeFalse();
        context.FailureReason.Should().Contain("revoked");
    }

    [Fact]
    public async Task Login_AfterFiveFailedAttempts_ShouldLockAccount()
    {
        // Arrange
        var testUsername = "lockout.user";
        var testPassword = "ValidPassword123!";

        var seededUser = await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            return await seeder.CreateUserAsync(testUsername, "lockout@smart-ics.internal", testPassword);
        });

        // Act - 5 failed login attempts
        for (int i = 1; i <= 5; i++)
        {
            var failResult = await ExecuteInScopeAsync(async sp =>
            {
                var authService = sp.GetRequiredService<IAuthenticationService>();
                return await authService.LoginAsync(testUsername, "BadPassword!");
            });

            failResult.Succeeded.Should().BeFalse();
        }

        // Assert - Account is locked in database
        await ExecuteInScopeAsync(async sp =>
        {
            var accountRepo = sp.GetRequiredService<IUserAccountRepository>();
            var account = await accountRepo.GetByIdAsync(seededUser.UserId);

            account.Should().NotBeNull();
            account!.Status.Should().Be(UserAccountStatus.Locked);
            account.IsLocked.Should().BeTrue();
            account.FailedLoginAttempts.Should().Be(5);
        });

        // Act - 6th attempt with correct password should be rejected because account is locked
        var lockedAttempt = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.LoginAsync(testUsername, testPassword);
        });

        lockedAttempt.Succeeded.Should().BeFalse();
        lockedAttempt.ErrorMessage.Should().Contain("locked");
    }

    [Fact]
    public async Task SeedInitialAdminUser_ShouldCreateActiveAdminAccount()
    {
        // Act - Seed default admin
        var adminUser = await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            return await seeder.SeedInitialAdminUserAsync();
        });

        // Assert
        adminUser.Should().NotBeNull();
        adminUser.Username.Should().Be("admin");
        adminUser.Email.Should().Be("admin@smart-ics.internal");
        adminUser.Status.Should().Be(UserAccountStatus.Active);

        // Act - Authenticate with default admin credentials
        var loginResult = await ExecuteInScopeAsync(async sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return await authService.LoginAsync("admin", "AdminPassword123!");
        });

        loginResult.Succeeded.Should().BeTrue();
        loginResult.SessionToken.Should().NotBeNullOrWhiteSpace();
        loginResult.UserId.Should().Be(adminUser.UserId);
    }

    [Fact]
    public async Task SynchronousAuthenticationMethods_ShouldWorkIdentically()
    {
        // Arrange
        var testUsername = "sync.user";
        var testPassword = "ValidPassword123!";

        await ExecuteInScopeAsync(async sp =>
        {
            var seeder = sp.GetRequiredService<IIdentityDataSeeder>();
            return await seeder.CreateUserAsync(testUsername, "sync@smart-ics.internal", testPassword);
        });

        // Act - Sync Login
        using var scope = CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

        var loginResult = authService.Login(testUsername, testPassword);
        loginResult.Succeeded.Should().BeTrue();

        // Act - Sync ValidateSession
        var context = authService.ValidateSession(loginResult.SessionToken!);
        context.IsAuthenticated.Should().BeTrue();

        // Act - Sync Logout
        var logoutResult = authService.Logout(loginResult.SessionToken!);
        logoutResult.Should().BeTrue();

        // Act - Sync ValidateSession after logout
        var afterLogoutContext = authService.ValidateSession(loginResult.SessionToken!);
        afterLogoutContext.IsAuthenticated.Should().BeFalse();
    }
}
