namespace ICS.Core.Auth;

/// <summary>
/// Scoped provider maintaining ambient security and identity context for the current request execution.
/// Populated by authentication middleware per Architecture §7, §14, and §18.
/// Registered with Scoped lifetime convention.
/// </summary>
public class CurrentContextProvider : ICurrentContextProvider, ICurrentContextAccessor
{
    private readonly HashSet<string> _roles = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Guid? CurrentUserId { get; private set; }

    /// <inheritdoc />
    public Guid? CurrentPersonId { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<string> CurrentRoles => _roles.ToList().AsReadOnly();

    /// <inheritdoc />
    public bool IsAuthenticated => CurrentUserId.HasValue;

    /// <inheritdoc />
    public bool IsInRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return _roles.Contains(role);
    }

    /// <inheritdoc />
    public void SetContext(Guid? userId, Guid? personId, IEnumerable<string>? roles)
    {
        CurrentUserId = userId;
        CurrentPersonId = personId;
        _roles.Clear();

        if (roles != null)
        {
            foreach (var role in roles)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    _roles.Add(role.Trim());
                }
            }
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        CurrentUserId = null;
        CurrentPersonId = null;
        _roles.Clear();
    }
}
