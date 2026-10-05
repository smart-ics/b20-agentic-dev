using Cakra.Core;
using Cakra.Modules.Identity.Commands;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Identity.Services;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Cakra.Tests.Unit.Identity;

/// <summary>
/// Unit tests for <see cref="RegisterUserAccountCommandHandler"/> and <see cref="RegisterUserAccountCommandValidator"/> (P3-S05; CR-009; FEAT-USR-002; Architecture §4 TD-001):
/// - Validates successful registration with Pending status, PersonId = Guid.Empty, and password hashing.
/// - Validates duplicate username and duplicate email rejection throwing <see cref="InvalidOperationException"/>.
/// - Validates FluentValidation rules for required fields, email format, and password length.
/// </summary>
public class RegisterUserAccountCommandHandlerTests
{
    private readonly InMemoryUserAccountRepository _repository = new();
    private readonly PasswordHasher<UserAccount> _passwordHasher = new();
    private readonly TestSystemClock _clock = new(new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc));
    private readonly RegisterUserAccountCommandHandler _handler;
    private readonly RegisterUserAccountCommandValidator _validator = new();

    public RegisterUserAccountCommandHandlerTests()
    {
        _handler = new RegisterUserAccountCommandHandler(
            _repository,
            _passwordHasher,
            _clock);
    }

    [Fact]
    public async Task Handle_with_valid_command_creates_pending_account_and_hashes_password()
    {
        // Arrange
        var command = new RegisterUserAccountCommand(
            Username: "newuser",
            Email: "newuser@example.com",
            Password: "SecurePassword123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().NotBeEmpty();
        result.PersonId.Should().Be(Guid.Empty);
        result.PersonName.Should().BeEmpty();
        result.Username.Should().Be("newuser");
        result.Email.Should().Be("newuser@example.com");
        result.Status.Should().Be(UserAccountStatus.Pending);
        result.FailedLoginAttempts.Should().Be(0);
        result.CreatedAt.Should().Be(_clock.UtcNow);
        result.UpdatedAt.Should().Be(_clock.UtcNow);

        // Verify entity in repository
        var persisted = await _repository.GetByIdAsync(result.UserId);
        persisted.Should().NotBeNull();
        persisted!.PersonId.Should().Be(Guid.Empty);
        persisted.Username.Should().Be("newuser");
        persisted.Email.Should().Be("newuser@example.com");
        persisted.Status.Should().Be(UserAccountStatus.Pending);
        persisted.PasswordHash.Should().NotBeNullOrWhiteSpace();
        persisted.PasswordHash.Should().NotBe("SecurePassword123!");

        var verifyResult = _passwordHasher.VerifyHashedPassword(persisted, persisted.PasswordHash!, "SecurePassword123!");
        verifyResult.Should().Be(PasswordVerificationResult.Success);
    }

    [Fact]
    public async Task Handle_with_duplicate_username_throws_InvalidOperationException()
    {
        // Arrange
        var existingUser = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.Empty,
            Username = "duplicateuser",
            Email = "existing@example.com",
            Status = UserAccountStatus.Pending
        };
        _repository.Items[existingUser.Id] = existingUser;

        var command = new RegisterUserAccountCommand(
            Username: "DUPLICATEUSER",
            Email: "unique@example.com",
            Password: "Password123!");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage("Username is already taken.");
    }

    [Fact]
    public async Task Handle_with_duplicate_email_throws_InvalidOperationException()
    {
        // Arrange
        var existingUser = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.Empty,
            Username = "firstuser",
            Email = "duplicate@example.com",
            Status = UserAccountStatus.Active
        };
        _repository.Items[existingUser.Id] = existingUser;

        var command = new RegisterUserAccountCommand(
            Username: "seconduser",
            Email: "DUPLICATE@EXAMPLE.COM",
            Password: "Password123!");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage("Email is already registered.");
    }

    [Fact]
    public void Validator_with_valid_command_has_no_errors()
    {
        // Arrange
        var command = new RegisterUserAccountCommand("validuser", "valid@example.com", "Password123!");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validator_with_empty_username_fails(string? username)
    {
        // Arrange
        var command = new RegisterUserAccountCommand(username!, "user@example.com", "Password123!");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage("Username is required.");
    }

    [Fact]
    public void Validator_with_username_exceeding_50_chars_fails()
    {
        // Arrange
        var longUsername = new string('a', 51);
        var command = new RegisterUserAccountCommand(longUsername, "user@example.com", "Password123!");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage("Username must not exceed 50 characters.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validator_with_empty_email_fails(string? email)
    {
        // Arrange
        var command = new RegisterUserAccountCommand("validuser", email!, "Password123!");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required.");
    }

    [Fact]
    public void Validator_with_invalid_email_format_fails()
    {
        // Arrange
        var command = new RegisterUserAccountCommand("validuser", "not-an-email", "Password123!");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email format is invalid.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validator_with_empty_password_fails(string? password)
    {
        // Arrange
        var command = new RegisterUserAccountCommand("validuser", "user@example.com", password!);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password is required.");
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("short")]
    public void Validator_with_short_password_fails(string password)
    {
        // Arrange
        var command = new RegisterUserAccountCommand("validuser", "user@example.com", password);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at least 8 characters long.");
    }

    private sealed class TestSystemClock : ISystemClock
    {
        public TestSystemClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
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
