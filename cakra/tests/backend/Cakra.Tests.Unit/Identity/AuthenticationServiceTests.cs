using Cakra.Core;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Identity.Services;
using Cakra.Modules.Organization;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Cakra.Tests.Unit.Identity;

public class AuthenticationServiceTests
{
    private readonly InMemoryUserAccountRepository _userAccountRepository = new();
    private readonly InMemoryUserSessionRepository _userSessionRepository = new();
    private readonly PasswordHasher<UserAccount> _passwordHasher = new();
    private readonly TestSystemClock _clock = new(new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc));
    private readonly AuthenticationService _service;

    public AuthenticationServiceTests()
    {
        _service = new AuthenticationService(
            _userAccountRepository,
            _userSessionRepository,
            _passwordHasher,
            _clock);
    }

    [Fact]
    public async Task Login_with_valid_credentials_succeeds_and_creates_session()
    {
        // Arrange
        var user = CreateTestUser("john.doe", "john@cakra.id", "Password123!");
        await _userAccountRepository.AddAsync(user);

        var clientInfo = new ClientInfo("192.168.1.10", "Mozilla/5.0 TestBrowser");

        // Act
        var result = await _service.LoginAsync("john.doe", "Password123!", clientInfo);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.SessionToken.Should().NotBeNullOrWhiteSpace();
        result.UserId.Should().Be(user.Id);
        result.PersonId.Should().Be(user.PersonId);
        result.ExpiresAt.Should().Be(_clock.UtcNow.AddHours(8));

        // Verify session was persisted in repository
        var session = await _userSessionRepository.GetByTokenAsync(result.SessionToken!);
        session.Should().NotBeNull();
        session!.UserId.Should().Be(user.Id);
        session.PersonId.Should().Be(user.PersonId);
        session.ClientIp.Should().Be("192.168.1.10");
        session.UserAgent.Should().Be("Mozilla/5.0 TestBrowser");
        session.IsRevoked.Should().BeFalse();

        // Verify account login timestamps and reset failed attempts
        var updatedUser = await _userAccountRepository.GetByIdAsync(user.Id);
        updatedUser!.LastLoginAt.Should().Be(_clock.UtcNow);
        updatedUser.FailedLoginAttempts.Should().Be(0);
    }

    [Fact]
    public async Task Login_with_email_and_valid_credentials_succeeds()
    {
        // Arrange
        var user = CreateTestUser("jane.doe", "jane@cakra.id", "SecurePass99#");
        await _userAccountRepository.AddAsync(user);

        // Act
        var result = await _service.LoginAsync("jane@cakra.id", "SecurePass99#");

        // Assert
        result.Succeeded.Should().BeTrue();
        result.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Login_with_bad_credentials_fails_and_increments_failed_attempts()
    {
        // Arrange
        var user = CreateTestUser("test.user", "test@cakra.id", "CorrectPassword123!");
        await _userAccountRepository.AddAsync(user);

        // Act
        var result = await _service.LoginAsync("test.user", "WrongPassword!");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
        result.SessionToken.Should().BeNull();

        // Verify failed attempts incremented
        var updatedUser = await _userAccountRepository.GetByIdAsync(user.Id);
        updatedUser!.FailedLoginAttempts.Should().Be(1);
    }

    [Fact]
    public async Task Login_with_nonexistent_user_fails()
    {
        // Act
        var result = await _service.LoginAsync("nonexistent.user", "AnyPassword123!");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_with_locked_account_fails()
    {
        // Arrange
        var user = CreateTestUser("locked.user", "locked@cakra.id", "Password123!");
        user.Status = UserAccountStatus.Locked;
        await _userAccountRepository.AddAsync(user);

        // Act
        var result = await _service.LoginAsync("locked.user", "Password123!");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCOUNT_NOT_ACTIVE");
    }

    [Fact]
    public async Task Login_with_inactive_person_fails_when_organization_query_service_configured()
    {
        // Arrange
        var user = CreateTestUser("org.user", "org@cakra.id", "Password123!");
        await _userAccountRepository.AddAsync(user);

        var mockOrgService = new FakeOrganizationQueryService(activePersonIds: []);
        var serviceWithOrg = new AuthenticationService(
            _userAccountRepository,
            _userSessionRepository,
            _passwordHasher,
            _clock,
            mockOrgService);

        // Act
        var result = await serviceWithOrg.LoginAsync("org.user", "Password123!");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("PERSON_INACTIVE");
    }

    [Fact]
    public async Task ValidateSession_with_valid_token_returns_valid_security_context()
    {
        // Arrange
        var user = CreateTestUser("active.user", "active@cakra.id", "Password123!");
        await _userAccountRepository.AddAsync(user);

        var loginResult = await _service.LoginAsync("active.user", "Password123!");
        loginResult.Succeeded.Should().BeTrue();

        // Act
        var context = await _service.ValidateSessionAsync(loginResult.SessionToken!);

        // Assert
        context.IsValid.Should().BeTrue();
        context.UserId.Should().Be(user.Id);
        context.PersonId.Should().Be(user.PersonId);
        context.Reason.Should().BeNull();
    }

    [Fact]
    public async Task ValidateSession_with_expired_token_returns_invalid_security_context()
    {
        // Arrange
        var user = CreateTestUser("expiring.user", "expiring@cakra.id", "Password123!");
        await _userAccountRepository.AddAsync(user);

        var loginResult = await _service.LoginAsync("expiring.user", "Password123!");

        // Advance clock past expiration (8 hours)
        _clock.Advance(TimeSpan.FromHours(9));

        // Act
        var context = await _service.ValidateSessionAsync(loginResult.SessionToken!);

        // Assert
        context.IsValid.Should().BeFalse();
        context.Reason.Should().Contain("expired");
    }

    [Fact]
    public async Task Logout_invalidates_session_and_subsequent_validation_fails()
    {
        // Arrange
        var user = CreateTestUser("logout.user", "logout@cakra.id", "Password123!");
        await _userAccountRepository.AddAsync(user);

        var loginResult = await _service.LoginAsync("logout.user", "Password123!");
        var token = loginResult.SessionToken!;

        // Verify initially valid
        var initialContext = await _service.ValidateSessionAsync(token);
        initialContext.IsValid.Should().BeTrue();

        // Act: Logout
        var loggedOut = await _service.LogoutAsync(token);
        loggedOut.Should().BeTrue();

        // Assert: Subsequent validation is revoked
        var postLogoutContext = await _service.ValidateSessionAsync(token);
        postLogoutContext.IsValid.Should().BeFalse();
        postLogoutContext.Reason.Should().Contain("revoked");
    }

    [Fact]
    public async Task ValidateSession_with_nonexistent_token_returns_invalid_security_context()
    {
        // Act
        var context = await _service.ValidateSessionAsync("nonexistent-token-12345");

        // Assert
        context.IsValid.Should().BeFalse();
        context.Reason.Should().Contain("not found");
    }

    [Fact]
    public void Synchronous_overloads_delegate_properly()
    {
        // Arrange
        var user = CreateTestUser("sync.user", "sync@cakra.id", "Password123!");
        _userAccountRepository.Items[user.Id] = user;

        // Act
        var loginResult = _service.Login("sync.user", "Password123!");
        loginResult.Succeeded.Should().BeTrue();

        var context = _service.ValidateSession(loginResult.SessionToken!);
        context.IsValid.Should().BeTrue();

        var logoutResult = _service.Logout(loginResult.SessionToken!);
        logoutResult.Should().BeTrue();

        var postLogoutContext = _service.ValidateSession(loginResult.SessionToken!);
        postLogoutContext.IsValid.Should().BeFalse();
    }

    private UserAccount CreateTestUser(string username, string email, string password)
    {
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.NewGuid(),
            Username = username,
            Email = email,
            Status = UserAccountStatus.Active,
            CreatedAt = _clock.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);
        return user;
    }

    // =========================================================================
    // Test Fakes & In-Memory Stubs
    // =========================================================================

    private sealed class TestSystemClock : ISystemClock
    {
        public DateTime UtcNow { get; set; }

        public TestSystemClock(DateTime initialUtcNow)
        {
            UtcNow = initialUtcNow;
        }

        public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
    }

    private sealed class FakeOrganizationQueryService : IOrganizationQueryService
    {
        private readonly HashSet<Guid> _activePersonIds;

        public FakeOrganizationQueryService(IEnumerable<Guid> activePersonIds)
        {
            _activePersonIds = new HashSet<Guid>(activePersonIds);
        }

        public Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_activePersonIds.Contains(personId));

        public Task<IReadOnlyList<string>> GetPersonRolesAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class InMemoryUserAccountRepository : IUserAccountRepository
    {
        public readonly Dictionary<Guid, UserAccount> Items = new();

        public Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Items.TryGetValue(id, out var user);
            return Task.FromResult(user);
        }

        public Task<UserAccount?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken = default)
        {
            var user = Items.Values.FirstOrDefault(u =>
                string.Equals(u.Username, usernameOrEmail, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.Email, usernameOrEmail, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(user);
        }

        public Task<UserAccount?> GetByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default)
        {
            var user = Items.Values.FirstOrDefault(u => u.PersonId == personId);
            return Task.FromResult(user);
        }

        public Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserAccount>>(Items.Values.ToList());

        public Task AddAsync(UserAccount entity, CancellationToken cancellationToken = default)
        {
            if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
            Items[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(UserAccount entity, CancellationToken cancellationToken = default)
        {
            Items[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Items.Remove(id);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryUserSessionRepository : IUserSessionRepository
    {
        public readonly Dictionary<Guid, UserSession> Items = new();

        public Task<UserSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Items.TryGetValue(id, out var session);
            return Task.FromResult(session);
        }

        public Task<UserSession?> GetByTokenAsync(string sessionToken, CancellationToken cancellationToken = default)
        {
            var session = Items.Values.FirstOrDefault(s => s.SessionToken == sessionToken);
            return Task.FromResult(session);
        }

        public Task<IReadOnlyList<UserSession>> GetActiveSessionsByUserIdAsync(Guid userId, DateTime currentUtc, CancellationToken cancellationToken = default)
        {
            var sessions = Items.Values
                .Where(s => s.UserId == userId && !s.IsRevoked && s.ExpiresAt > currentUtc)
                .ToList();
            return Task.FromResult<IReadOnlyList<UserSession>>(sessions);
        }

        public Task<IReadOnlyList<UserSession>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserSession>>(Items.Values.ToList());

        public Task AddAsync(UserSession entity, CancellationToken cancellationToken = default)
        {
            if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
            Items[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(UserSession entity, CancellationToken cancellationToken = default)
        {
            Items[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task<bool> RevokeByTokenAsync(string sessionToken, DateTime? revokedAtUtc = null, CancellationToken cancellationToken = default)
        {
            var session = Items.Values.FirstOrDefault(s => s.SessionToken == sessionToken && !s.IsRevoked);
            if (session != null)
            {
                session.Revoke(revokedAtUtc);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Items.Remove(id);
            return Task.CompletedTask;
        }
    }
}
