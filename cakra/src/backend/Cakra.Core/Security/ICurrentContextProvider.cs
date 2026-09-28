namespace Cakra.Core;

/// <summary>
/// Exposes the ambient request context populated by the authentication
/// middleware on every incoming request (Architecture 7, 14, 18).
/// </summary>
public interface ICurrentContextProvider
{
    /// <summary>Authenticated user identifier, or <c>null</c> when the request is anonymous.</summary>
    Guid? CurrentUserId { get; }

    /// <summary>Authoritative organization person identifier, or <c>null</c> when the request is anonymous.</summary>
    Guid? CurrentPersonId { get; }

    /// <summary>Active organizational roles resolved for the current person.</summary>
    IReadOnlyCollection<string> CurrentRoles { get; }
}
