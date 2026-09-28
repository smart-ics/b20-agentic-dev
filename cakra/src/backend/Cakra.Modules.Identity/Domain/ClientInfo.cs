namespace Cakra.Modules.Identity.Domain;

/// <summary>
/// Client contextual metadata captured at login time (Architecture §14).
/// </summary>
public sealed record ClientInfo(string? ClientIp = null, string? UserAgent = null);
