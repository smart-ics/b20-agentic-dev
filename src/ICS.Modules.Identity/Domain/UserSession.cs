namespace ICS.Modules.Identity.Domain;

using ICS.Core.Domain;

/// <summary>
/// UserSession domain aggregate representing a server-side active login session.
/// Architecture §14 (IAM Boundary and UserSessions Persistence Table) and §19.5.
/// </summary>
public class UserSession : Entity<Guid>
{
    /// <summary>
    /// Gets the unique session identifier (equivalent to <see cref="Entity{TId}.Id"/>).
    /// </summary>
    public Guid SessionId => Id;

    /// <summary>
    /// Foreign key identifier referencing the authenticated <see cref="UserAccount"/>.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Foreign key identifier referencing the linked Organization Person.
    /// </summary>
    public Guid PersonId { get; private set; }

    /// <summary>
    /// Unique cryptographically secure session token string.
    /// </summary>
    public string SessionToken { get; private set; } = string.Empty;

    /// <summary>
    /// Timestamp when this session expires (UTC).
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// Client IP address from which the session was established.
    /// </summary>
    public string? ClientIp { get; private set; }

    /// <summary>
    /// Client User-Agent header from which the session was established.
    /// </summary>
    public string? UserAgent { get; private set; }

    /// <summary>
    /// Indicates whether the session has been explicitly revoked (logged out).
    /// </summary>
    public bool IsRevoked { get; private set; }

    /// <summary>
    /// Parameterless constructor required for serialization and Dapper mapping.
    /// </summary>
    protected UserSession()
    {
    }

    /// <summary>
    /// Constructor matching database schema columns for Dapper instantiation.
    /// </summary>
    public UserSession(
        Guid sessionId,
        Guid userId,
        Guid personId,
        string sessionToken,
        DateTime expiresAt,
        DateTime createdAt,
        string? clientIp,
        string? userAgent,
        bool isRevoked)
    {
        Id = sessionId;
        UserId = userId;
        PersonId = personId;
        SessionToken = sessionToken;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        ClientIp = clientIp;
        UserAgent = userAgent;
        IsRevoked = isRevoked;
    }

    /// <summary>
    /// Factory method for creating a new active UserSession.
    /// </summary>
    public static UserSession Create(
        Guid sessionId,
        Guid userId,
        Guid personId,
        string sessionToken,
        DateTime expiresAt,
        DateTime createdAt,
        string? clientIp = null,
        string? userAgent = null)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("SessionId cannot be empty.", nameof(sessionId));
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        if (personId == Guid.Empty)
            throw new ArgumentException("PersonId cannot be empty.", nameof(personId));
        if (string.IsNullOrWhiteSpace(sessionToken))
            throw new ArgumentException("SessionToken cannot be empty.", nameof(sessionToken));
        if (expiresAt <= createdAt)
            throw new ArgumentException("ExpiresAt must be after CreatedAt.", nameof(expiresAt));

        return new UserSession(
            sessionId: sessionId,
            userId: userId,
            personId: personId,
            sessionToken: sessionToken,
            expiresAt: expiresAt,
            createdAt: createdAt,
            clientIp: clientIp,
            userAgent: userAgent,
            isRevoked: false);
    }

    /// <summary>
    /// Marks the session as revoked (invalidated).
    /// </summary>
    public void Revoke()
    {
        IsRevoked = true;
    }

    /// <summary>
    /// Checks whether the session is valid at the specified point in time (not revoked and not expired).
    /// </summary>
    public bool IsValid(DateTime now) =>
        !IsRevoked && ExpiresAt > now;
}
