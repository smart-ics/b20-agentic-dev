namespace ICS.Modules.Identity.Application;

/// <summary>
/// Client connection metadata captured during authentication per Architecture §14 and §18.
/// </summary>
public record ClientInfo(string? IpAddress = null, string? UserAgent = null);
