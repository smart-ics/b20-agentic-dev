using Cakra.Modules.Identity.Domain;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Identity;

public class UserSessionTests
{
    [Fact]
    public void Session_validity_lifecycle_evaluates_correctly()
    {
        var now = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PersonId = Guid.NewGuid(),
            SessionToken = "test-token-value",
            CreatedAt = now,
            ExpiresAt = now.AddHours(8),
            IsRevoked = false
        };

        session.SessionId.Should().Be(session.Id);

        // Active session before expiration
        session.IsExpired(now.AddHours(4)).Should().BeFalse();
        session.IsValid(now.AddHours(4)).Should().BeTrue();

        // At exact expiry boundary
        session.IsExpired(now.AddHours(8)).Should().BeTrue();
        session.IsValid(now.AddHours(8)).Should().BeFalse();

        // Past expiry
        session.IsExpired(now.AddHours(9)).Should().BeTrue();
        session.IsValid(now.AddHours(9)).Should().BeFalse();
    }

    [Fact]
    public void Revoking_session_marks_is_revoked_and_updates_timestamp()
    {
        var now = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            ExpiresAt = now.AddHours(8),
            IsRevoked = false
        };

        session.IsValid(now.AddHours(1)).Should().BeTrue();

        session.Revoke(now.AddHours(2));

        session.IsRevoked.Should().BeTrue();
        session.UpdatedAt.Should().Be(now.AddHours(2));
        session.IsValid(now.AddHours(3)).Should().BeFalse();
    }
}
