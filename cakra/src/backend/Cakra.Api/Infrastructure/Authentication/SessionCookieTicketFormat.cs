using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Cakra.Api.Infrastructure.Authentication;

/// <summary>
/// Custom ticket data format bridging the server-side session token stored in the session cookie
/// to the ASP.NET Core <see cref="AuthenticationTicket"/> (Architecture §14, §19.5).
/// </summary>
public sealed class SessionCookieTicketFormat : ISecureDataFormat<AuthenticationTicket>
{
    /// <inheritdoc />
    public string Protect(AuthenticationTicket data) => Protect(data, null);

    /// <inheritdoc />
    public string Protect(AuthenticationTicket data, string? purpose)
    {
        ArgumentNullException.ThrowIfNull(data);

        return data.Properties.GetString("SessionToken")
            ?? data.Principal?.FindFirst("session_token")?.Value
            ?? string.Empty;
    }

    /// <inheritdoc />
    public AuthenticationTicket? Unprotect(string? protectedText) => Unprotect(protectedText, null);

    /// <inheritdoc />
    public AuthenticationTicket? Unprotect(string? protectedText, string? purpose)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return null;
        }

        var claims = new[]
        {
            new Claim("session_token", protectedText)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties();
        properties.SetString("SessionToken", protectedText);

        return new AuthenticationTicket(principal, properties, CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
