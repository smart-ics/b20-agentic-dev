using Cakra.Modules.Identity.Domain;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Identity;

public class UserAccountTests
{
    [Fact]
    public void New_account_has_default_active_status_and_zero_failed_attempts()
    {
        var account = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@cakra.id"
        };

        account.Status.Should().Be(UserAccountStatus.Active);
        account.IsActive.Should().BeTrue();
        account.FailedLoginAttempts.Should().Be(0);
        account.LastLoginAt.Should().BeNull();
        account.UserId.Should().Be(account.Id);
    }

    [Fact]
    public void RecordLoginFailure_increments_attempts_and_locks_when_reaching_threshold()
    {
        var account = new UserAccount();
        var timestamp = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

        for (int i = 1; i <= 4; i++)
        {
            account.RecordLoginFailure(maxAttemptsBeforeLock: 5, timestamp);
            account.FailedLoginAttempts.Should().Be(i);
            account.Status.Should().Be(UserAccountStatus.Active);
            account.IsActive.Should().BeTrue();
        }

        // 5th failure reaches threshold
        account.RecordLoginFailure(maxAttemptsBeforeLock: 5, timestamp);
        account.FailedLoginAttempts.Should().Be(5);
        account.Status.Should().Be(UserAccountStatus.Locked);
        account.IsActive.Should().BeFalse();
        account.UpdatedAt.Should().Be(timestamp);
    }

    [Fact]
    public void RecordLoginSuccess_resets_failed_attempts_and_updates_last_login()
    {
        var account = new UserAccount
        {
            FailedLoginAttempts = 3
        };
        var timestamp = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

        account.RecordLoginSuccess(timestamp);

        account.FailedLoginAttempts.Should().Be(0);
        account.LastLoginAt.Should().Be(timestamp);
        account.UpdatedAt.Should().Be(timestamp);
    }

    [Fact]
    public void Unlock_restores_active_status_and_resets_failed_attempts()
    {
        var account = new UserAccount
        {
            Status = UserAccountStatus.Locked,
            FailedLoginAttempts = 5
        };
        var timestamp = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

        account.Unlock(timestamp);

        account.Status.Should().Be(UserAccountStatus.Active);
        account.IsActive.Should().BeTrue();
        account.FailedLoginAttempts.Should().Be(0);
        account.UpdatedAt.Should().Be(timestamp);
    }
}
