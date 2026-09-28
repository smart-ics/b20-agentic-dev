namespace ICS.Core.Auth;

/// <summary>
/// Ambient request context contract populated on every incoming request.
/// Exposes authenticated identity identifiers and active organizational roles per Architecture §7 and §14.
/// </summary>
public interface ICurrentContextProvider
{
    /// <summary>
    /// The authenticated UserAccount identifier (UUID), or null if anonymous.
    /// </summary>
    Guid? CurrentUserId { get; }

    /// <summary>
    /// The linked organizational Person identifier (UUID), or null if anonymous.
    /// </summary>
    Guid? CurrentPersonId { get; }

    /// <summary>
    /// The active organizational roles assigned to the current person (e.g. Management, Programmer, Implementator).
    /// </summary>
    IReadOnlyList<string> CurrentRoles { get; }

    /// <summary>
    /// Indicates whether the current request is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Checks whether the authenticated user possesses the specified organizational role.
    /// </summary>
    bool IsInRole(string role);
}
