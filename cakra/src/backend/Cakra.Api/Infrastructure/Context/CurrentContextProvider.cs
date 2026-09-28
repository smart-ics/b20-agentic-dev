using Cakra.Core;

namespace Cakra.Api.Infrastructure.Context;

/// <summary>
/// Ambient, per-request implementation of <see cref="ICurrentContextProvider"/>
/// (Architecture §7, §14). The authentication middleware populates it for each
/// incoming request; until then the provider reports anonymous state. Registered
/// as scoped so a single instance is shared across the whole request.
/// </summary>
public sealed class CurrentContextProvider : ICurrentContextProvider
{
    private Guid? _currentUserId;
    private Guid? _currentPersonId;
    private IReadOnlyCollection<string> _currentRoles = Array.Empty<string>();

    /// <inheritdoc />
    public Guid? CurrentUserId => _currentUserId;

    /// <inheritdoc />
    public Guid? CurrentPersonId => _currentPersonId;

    /// <inheritdoc />
    public IReadOnlyCollection<string> CurrentRoles => _currentRoles;

    /// <summary>
    /// Populates the ambient context for the current request. Intended to be
    /// called by the authentication/security-context middleware placed in the
    /// HTTP pipeline (P1-S06 placeholder, concrete wiring in P2-S10).
    /// </summary>
    public void Initialize(Guid? userId, Guid? personId, IEnumerable<string>? roles)
    {
        _currentUserId = userId;
        _currentPersonId = personId;
        _currentRoles = roles is null
            ? Array.Empty<string>()
            : roles.ToArray();
    }
}
