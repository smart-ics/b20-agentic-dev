namespace Cakra.Modules.Identity.Services;

/// <summary>
/// Detailed DTO representing a user account (Architecture §14; CR-007).
/// Excludes sensitive credentials such as password hash.
/// </summary>
public sealed record UserAccountDto
{
    public Guid UserId { get; init; }
    public Guid PersonId { get; init; }
    public string PersonName { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int FailedLoginAttempts { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
