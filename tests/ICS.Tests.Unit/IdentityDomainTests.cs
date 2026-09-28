namespace ICS.Tests.Unit;

using System;
using FluentAssertions;
using ICS.Modules.Identity.Domain;
using Xunit;

public class IdentityDomainTests
{
    [Fact]
    public void UserAccount_Create_ShouldInitializeActiveAccount()
    {
        var userId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var account = UserAccount.Create(userId, personId, "john.doe", "john@example.com", "hash123", now);

        account.UserId.Should().Be(userId);
        account.PersonId.Should().Be(personId);
        account.Username.Should().Be("john.doe");
        account.Email.Should().Be("john@example.com");
        account.PasswordHash.Should().Be("hash123");
        account.Status.Should().Be(UserAccountStatus.Active);
        account.IsActive.Should().BeTrue();
        account.IsLocked.Should().BeFalse();
        account.IsSuspended.Should().BeFalse();
        account.FailedLoginAttempts.Should().Be(0);
        account.LastLoginAt.Should().BeNull();
        account.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void UserAccount_RecordLoginSuccess_ShouldResetFailedAttemptsAndUpdateTimestamp()
    {
        var account = UserAccount.Create(Guid.NewGuid(), Guid.NewGuid(), "john.doe", "john@example.com", "hash", DateTime.UtcNow);
        account.RecordLoginFailure(DateTime.UtcNow);
        account.RecordLoginFailure(DateTime.UtcNow);
        account.FailedLoginAttempts.Should().Be(2);

        var loginTime = DateTime.UtcNow.AddMinutes(1);
        account.RecordLoginSuccess(loginTime);

        account.FailedLoginAttempts.Should().Be(0);
        account.LastLoginAt.Should().Be(loginTime);
        account.UpdatedAt.Should().Be(loginTime);
    }

    [Fact]
    public void UserAccount_RecordLoginFailure_ShouldLockAfterMaxAttempts()
    {
        var account = UserAccount.Create(Guid.NewGuid(), Guid.NewGuid(), "john.doe", "john@example.com", "hash", DateTime.UtcNow);

        for (int i = 1; i <= 4; i++)
        {
            account.RecordLoginFailure(DateTime.UtcNow, maxFailedAttempts: 5);
            account.FailedLoginAttempts.Should().Be(i);
            account.IsLocked.Should().BeFalse();
        }

        account.RecordLoginFailure(DateTime.UtcNow, maxFailedAttempts: 5);
        account.FailedLoginAttempts.Should().Be(5);
        account.IsLocked.Should().BeTrue();
        account.IsActive.Should().BeFalse();
    }

    [Fact]
    public void UserAccount_Unlock_ShouldRestoreActiveStatusAndResetAttempts()
    {
        var account = UserAccount.Create(Guid.NewGuid(), Guid.NewGuid(), "john.doe", "john@example.com", "hash", DateTime.UtcNow);
        account.Lock(DateTime.UtcNow);
        account.IsLocked.Should().BeTrue();

        var unlockTime = DateTime.UtcNow.AddMinutes(5);
        account.Unlock(unlockTime);

        account.IsActive.Should().BeTrue();
        account.IsLocked.Should().BeFalse();
        account.FailedLoginAttempts.Should().Be(0);
        account.UpdatedAt.Should().Be(unlockTime);
    }

    [Fact]
    public void UserSession_Create_ShouldInitializeValidSession()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var expiresAt = now.AddHours(8);

        var session = UserSession.Create(sessionId, userId, personId, "token-xyz", expiresAt, now, "127.0.0.1", "Mozilla/5.0");

        session.SessionId.Should().Be(sessionId);
        session.UserId.Should().Be(userId);
        session.PersonId.Should().Be(personId);
        session.SessionToken.Should().Be("token-xyz");
        session.ExpiresAt.Should().Be(expiresAt);
        session.CreatedAt.Should().Be(now);
        session.ClientIp.Should().Be("127.0.0.1");
        session.UserAgent.Should().Be("Mozilla/5.0");
        session.IsRevoked.Should().BeFalse();
        session.IsValid(now).Should().BeTrue();
    }

    [Fact]
    public void UserSession_Revoke_ShouldMarkAsRevokedAndInvalidate()
    {
        var now = DateTime.UtcNow;
        var session = UserSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "tok", now.AddHours(1), now);

        session.IsValid(now).Should().BeTrue();
        session.Revoke();

        session.IsRevoked.Should().BeTrue();
        session.IsValid(now).Should().BeFalse();
    }

    [Fact]
    public void UserSession_IsValid_ShouldReturnFalseWhenExpired()
    {
        var now = DateTime.UtcNow;
        var session = UserSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "tok", now.AddMinutes(-1), now.AddHours(-1));

        session.IsValid(now).Should().BeFalse();
    }
}
