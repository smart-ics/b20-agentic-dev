using Microsoft.AspNetCore.Authentication.Cookies;

namespace Cakra.Api.Infrastructure.Authentication;

/// <summary>
/// Authentication and session cookie constants for CAKRA (Architecture §14, §19.5).
/// </summary>
public static class CakraAuthenticationDefaults
{
    /// <summary>
    /// Default authentication scheme name (ASP.NET Core Cookie Authentication).
    /// </summary>
    public const string AuthenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>
    /// Default cookie name for session authentication.
    /// </summary>
    public const string CookieName = "Cakra.Session";
}
