using Cakra.Core;

namespace Cakra.Modules.Identity.Domain;

/// <summary>
/// Domain entity representing an active authenticated session (Architecture §14, §19.5).
/// Backs server-side session verification and maps the session token to the authenticated
/// <c>UserId</c> and <c>PersonId</c>.
/// </summary>
public class UserSession : EntityBase
{
    /// <summary>
    /// Unique identifier of the session. Synonymous with <see cref="EntityBase.Id"/>.
    /// </summary>
    public Guid SessionId
    {
        get => Id;
        set => Id = value;
    }

    /// <summary>
    /// Identifier of the authenticated user account.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Authoritative organization person identifier associated with the user account.
    /// </summary>
    public Guid PersonId { get; set; }

    /// <summary>
    /// Cryptographic session token issued to the client and stored in session cookies.
    /// </summary>
    public string SessionToken { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when this session expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Client IP address recorded at login time.
    /// </summary>
    public string? ClientIp { get; set; }

    /// <summary>
    /// Client User-Agent header recorded at login time.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Indicates whether this session has been explicitly revoked via logout or administrative action.
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// Evaluates whether the session has expired relative to the specified current time.
    /// </summary>
    public bool IsExpired(DateTime currentUtc) => currentUtc >= ExpiresAt;

    /// <summary>
    /// Evaluates whether the session is valid (not revoked and not expired) relative to the specified current time.
    /// </summary>
    public bool IsValid(DateTime currentUtc) => !IsRevoked && !IsExpired(currentUtc);

    /// <summary>
    /// Explicitly revokes this session (e.g. upon logout).
    /// </summary>
    public void Revoke(DateTime? timestampUtc = null)
    {
        IsRevoked = true;
        UpdatedAt = timestampUtc;
    }
}
