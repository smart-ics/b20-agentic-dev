namespace Cakra.Api.Controllers;

/// <summary>
/// Request payload for user self-registration (<c>POST /api/v1/auth/register</c>; CR-009; FEAT-USR-002).
/// </summary>
public sealed record RegisterRequest(
    string? Username,
    string? Email,
    string? Password
);
