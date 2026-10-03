using Cakra.Core;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Identity.Services;
using Cakra.Modules.Organization;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Cakra.Tests.Unit.Identity;

/// <summary>
/// Unit tests for <see cref="UserAccountService"/> (P3-S05; CR-007; Architecture §14):
/// - Validates user creation with password hashing and duplicate prevention (username, email, PersonId).
/// - Validates user update, email uniqueness check excluding self, status transitions, and unlock semantics.
/// - Validates in-memory Person name resolution via <see cref="IOrganizationQueryService"/>.
/// </summary>
public class UserAccountServiceTests
{
    private readonly InMemoryUserAccountRepository _userAccountRepository = new();
    private readonly InMemoryOrganizationQueryService _organizationQueryService = new();
    private readonly PasswordHasher<UserAccount> _passwordHasher = new();
    private readonly TestSystemClock _clock = new(new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc));
    private readonly UserAccountService _service;

    public UserAccountServiceTests()
    {
        _service = new UserAccountService(
            _userAccountRepository,
            _passwordHasher,
            _organizationQueryService,
            _clock);
    }

    [Fact]
    public async Task CreateUserAsync_with_valid_command_hashes_password_and_persists_account()
    {
        // Arrange
        var personId = Guid.NewGuid();
        _organizationQueryService.AddPerson(personId, "Budi", "Santoso");

        var command = new CreateUserAccountCommand(
            PersonId: personId,
            Username: "budi.santoso",
            Email: "budi@cakra.id",
            Password: "SecurePassword123!",
            Status: UserAccountStatus.Active);

        // Act
        var result = await _service.CreateUserAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().NotBeEmpty();
        result.PersonId.Should().Be(personId);
        result.PersonName.Should().Be("Budi Santoso");
        result.Username.Should().Be("budi.santoso");
        result.Email.Should().Be("budi@cakra.id");
        result.Status.Should().Be(UserAccountStatus.Active);
        result.FailedLoginAttempts.Should().Be(0);
        result.CreatedAt.Should().Be(_clock.UtcNow);
        result.UpdatedAt.Should().Be(_clock.UtcNow);

        // Verify entity persisted in repository with hashed password
        var persisted = await _userAccountRepository.GetByIdAsync(result.UserId);
        persisted.Should().NotBeNull();
        persisted!.PasswordHash.Should().NotBeNullOrWhiteSpace();
        persisted.PasswordHash.Should().NotBe("SecurePassword123!");

        var verifyResult = _passwordHasher.VerifyHashedPassword(persisted, persisted.PasswordHash!, "SecurePassword123!");
        verifyResult.Should().Be(PasswordVerificationResult.Success);
    }

    [Fact]
    public async Task CreateUserAsync_throws_when_person_is_already_associated_with_another_account()
    {
        // Arrange
        var personId = Guid.NewGuid();
        _userAccountRepository.Items.Add(Guid.NewGuid(), new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            Username = "existing.user",
            Email = "existing@cakra.id",
            Status = UserAccountStatus.Active
        });

        var command = new CreateUserAccountCommand(
            PersonId: personId,
            Username: "new.user",
            Email: "new@cakra.id",
            Password: "SecurePassword123!");

        // Act
        var act = () => _service.CreateUserAsync(command);

        // Assert
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage($"*{personId}*already associated*");
    }

    [Theory]
    [InlineData("alice", "alice")]
    [InlineData("ALICE", "alice")]
    [InlineData("alice", "ALICE")]
    public async Task CreateUserAsync_throws_when_username_is_already_in_use_case_insensitively(string existingUsername, string newUsername)
    {
        // Arrange
        _userAccountRepository.Items.Add(Guid.NewGuid(), new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.NewGuid(),
            Username = existingUsername,
            Email = "alice.old@cakra.id",
            Status = UserAccountStatus.Active
        });

        var command = new CreateUserAccountCommand(
            PersonId: Guid.NewGuid(),
            Username: newUsername,
            Email: "alice.new@cakra.id",
            Password: "SecurePassword123!");

        // Act
        var act = () => _service.CreateUserAsync(command);

        // Assert
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage($"*{newUsername}*already in use*");
    }

    [Theory]
    [InlineData("alice@cakra.id", "alice@cakra.id")]
    [InlineData("ALICE@CAKRA.ID", "alice@cakra.id")]
    [InlineData("alice@cakra.id", "ALICE@CAKRA.ID")]
    public async Task CreateUserAsync_throws_when_email_is_already_in_use_case_insensitively(string existingEmail, string newEmail)
    {
        // Arrange
        _userAccountRepository.Items.Add(Guid.NewGuid(), new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.NewGuid(),
            Username = "existing.alice",
            Email = existingEmail,
            Status = UserAccountStatus.Active
        });

        var command = new CreateUserAccountCommand(
            PersonId: Guid.NewGuid(),
            Username: "new.user",
            Email: newEmail,
            Password: "SecurePassword123!");

        // Act
        var act = () => _service.CreateUserAsync(command);

        // Assert
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage($"*{newEmail}*already in use*");
    }

    [Theory]
    [InlineData("", "valid@email.com", "Password123!")] // Empty username
    [InlineData("ab", "valid@email.com", "Password123!")] // Username < 3 chars
    [InlineData("validuser", "invalid-email-format", "Password123!")] // Invalid email
    [InlineData("validuser", "valid@email.com", "short")] // Password < 8 chars
    public async Task CreateUserAsync_throws_validation_exception_for_invalid_inputs(string username, string email, string password)
    {
        // Arrange
        var command = new CreateUserAccountCommand(
            PersonId: Guid.NewGuid(),
            Username: username,
            Email: email,
            Password: password);

        // Act
        var act = () => _service.CreateUserAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateUserAsync_updates_email_status_and_password()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var user = new UserAccount
        {
            Id = userId,
            PersonId = personId,
            Username = "charlie",
            Email = "charlie@cakra.id",
            Status = UserAccountStatus.Active,
            CreatedAt = _clock.UtcNow.AddDays(-10),
            UpdatedAt = _clock.UtcNow.AddDays(-10)
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, "OldPassword123!");
        _userAccountRepository.Items.Add(userId, user);

        _organizationQueryService.AddPerson(personId, "Charlie", "Brown");

        var updateCommand = new UpdateUserAccountCommand(
            Email: "charlie.new@cakra.id",
            Status: UserAccountStatus.Suspended,
            NewPassword: "NewSecretPassword2026!");

        // Act
        var result = await _service.UpdateUserAsync(userId, updateCommand);

        // Assert
        result.UserId.Should().Be(userId);
        result.Email.Should().Be("charlie.new@cakra.id");
        result.Status.Should().Be(UserAccountStatus.Suspended);
        result.UpdatedAt.Should().Be(_clock.UtcNow);
        result.PersonName.Should().Be("Charlie Brown");

        var persisted = await _userAccountRepository.GetByIdAsync(userId);
        persisted!.Email.Should().Be("charlie.new@cakra.id");
        persisted.Status.Should().Be(UserAccountStatus.Suspended);

        var verifyResult = _passwordHasher.VerifyHashedPassword(persisted, persisted.PasswordHash!, "NewSecretPassword2026!");
        verifyResult.Should().Be(PasswordVerificationResult.Success);
    }

    [Fact]
    public async Task UpdateUserAsync_when_unlocking_locked_user_resets_failed_login_attempts()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserAccount
        {
            Id = userId,
            PersonId = Guid.NewGuid(),
            Username = "locked.user",
            Email = "locked@cakra.id",
            Status = UserAccountStatus.Locked,
            FailedLoginAttempts = 5,
            CreatedAt = _clock.UtcNow.AddDays(-5),
            UpdatedAt = _clock.UtcNow.AddDays(-1)
        };
        _userAccountRepository.Items.Add(userId, user);

        var command = new UpdateUserAccountCommand(Status: UserAccountStatus.Active);

        // Act
        var result = await _service.UpdateUserAsync(userId, command);

        // Assert
        result.Status.Should().Be(UserAccountStatus.Active);
        result.FailedLoginAttempts.Should().Be(0);

        var persisted = await _userAccountRepository.GetByIdAsync(userId);
        persisted!.Status.Should().Be(UserAccountStatus.Active);
        persisted.FailedLoginAttempts.Should().Be(0);
    }

    [Fact]
    public async Task UpdateUserAsync_allows_retaining_same_email_for_current_user()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserAccount
        {
            Id = userId,
            PersonId = Guid.NewGuid(),
            Username = "same.email",
            Email = "same@cakra.id",
            Status = UserAccountStatus.Active
        };
        _userAccountRepository.Items.Add(userId, user);

        var command = new UpdateUserAccountCommand(Email: "same@cakra.id", Status: UserAccountStatus.Active);

        // Act
        var result = await _service.UpdateUserAsync(userId, command);

        // Assert
        result.Email.Should().Be("same@cakra.id");
    }

    [Fact]
    public async Task UpdateUserAsync_throws_when_updating_to_email_used_by_another_user()
    {
        // Arrange
        var user1 = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.NewGuid(),
            Username = "user1",
            Email = "user1@cakra.id",
            Status = UserAccountStatus.Active
        };
        var user2 = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.NewGuid(),
            Username = "user2",
            Email = "user2@cakra.id",
            Status = UserAccountStatus.Active
        };
        _userAccountRepository.Items.Add(user1.Id, user1);
        _userAccountRepository.Items.Add(user2.Id, user2);

        var command = new UpdateUserAccountCommand(Email: "user2@cakra.id");

        // Act
        var act = () => _service.UpdateUserAsync(user1.Id, command);

        // Assert
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage("*already in use by another user account*");
    }

    [Fact]
    public async Task UpdateUserAsync_throws_for_invalid_status()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserAccount
        {
            Id = userId,
            PersonId = Guid.NewGuid(),
            Username = "user",
            Email = "user@cakra.id",
            Status = UserAccountStatus.Active
        };
        _userAccountRepository.Items.Add(userId, user);

        var command = new UpdateUserAccountCommand(Status: "INVALID_STATUS");

        // Act
        var act = () => _service.UpdateUserAsync(userId, command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateUserAsync_throws_KeyNotFoundException_for_nonexistent_user()
    {
        // Arrange
        var nonexistentId = Guid.NewGuid();
        var command = new UpdateUserAccountCommand(Status: UserAccountStatus.Active);

        // Act
        var act = () => _service.UpdateUserAsync(nonexistentId, command);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetAllUsersAsync_resolves_person_names_in_memory()
    {
        // Arrange
        var person1Id = Guid.NewGuid();
        var person2Id = Guid.NewGuid();
        var unknownPersonId = Guid.NewGuid();

        _organizationQueryService.AddPerson(person1Id, "Dewi", "Sartika");
        _organizationQueryService.AddPerson(person2Id, "Raden", "Kartini");
        // unknownPersonId is not added to organization query service

        var user1 = new UserAccount { Id = Guid.NewGuid(), PersonId = person1Id, Username = "dewi", Email = "dewi@cakra.id", Status = UserAccountStatus.Active };
        var user2 = new UserAccount { Id = Guid.NewGuid(), PersonId = person2Id, Username = "kartini", Email = "kartini@cakra.id", Status = UserAccountStatus.Active };
        var user3 = new UserAccount { Id = Guid.NewGuid(), PersonId = unknownPersonId, Username = "unknown", Email = "unknown@cakra.id", Status = UserAccountStatus.Active };

        _userAccountRepository.Items.Add(user1.Id, user1);
        _userAccountRepository.Items.Add(user2.Id, user2);
        _userAccountRepository.Items.Add(user3.Id, user3);

        // Act
        var results = await _service.GetAllUsersAsync();

        // Assert
        results.Should().HaveCount(3);
        results.Should().Contain(u => u.UserId == user1.Id && u.PersonName == "Dewi Sartika");
        results.Should().Contain(u => u.UserId == user2.Id && u.PersonName == "Raden Kartini");
        results.Should().Contain(u => u.UserId == user3.Id && u.PersonName == "Unknown / Archived");
    }

    [Fact]
    public async Task GetUserByIdAsync_returns_dto_with_resolved_person_name_or_null_if_not_found()
    {
        // Arrange
        var personId = Guid.NewGuid();
        _organizationQueryService.AddPerson(personId, "Hatta", "Mohammad");

        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            Username = "hatta",
            Email = "hatta@cakra.id",
            Status = UserAccountStatus.Active
        };
        _userAccountRepository.Items.Add(user.Id, user);

        // Act - found
        var result = await _service.GetUserByIdAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(user.Id);
        result.PersonName.Should().Be("Hatta Mohammad");

        // Act - not found
        var notFoundResult = await _service.GetUserByIdAsync(Guid.NewGuid());
        notFoundResult.Should().BeNull();
    }

    private sealed class TestSystemClock : ISystemClock
    {
        public TestSystemClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
    }

    private sealed class InMemoryOrganizationQueryService : IOrganizationQueryService
    {
        private readonly List<PersonDto> _persons = new();

        public void AddPerson(Guid id, string firstName, string lastName)
        {
            _persons.Add(new PersonDto
            {
                Id = id,
                FirstName = firstName,
                LastName = lastName,
                Email = $"{firstName.ToLowerInvariant()}@cakra.id",
                Status = "ACTIVE"
            });
        }

        public Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_persons.Any(p => p.Id == personId && p.IsActive));

        public Task<PersonDto?> GetPersonByIdAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_persons.FirstOrDefault(p => p.Id == personId));

        public Task<IReadOnlyList<PersonDto>> ListActivePersonsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PersonDto>>(_persons.Where(p => p.IsActive).ToList());

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
}
