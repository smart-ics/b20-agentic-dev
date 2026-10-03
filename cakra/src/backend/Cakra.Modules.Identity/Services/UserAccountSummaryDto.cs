namespace Cakra.Modules.Identity.Services;

/// <summary>
/// Summary DTO representing a user account in list views (Architecture §14; CR-007, SCR-USR-001).
/// Enriched in-memory with the person display name from the Organization module.
/// </summary>
public sealed record UserAccountSummaryDto
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
