using ICS.Core.Auth;
using ICS.Core.Time;

namespace ICS.Core.Audit;

/// <summary>
/// Scoped audit context provider resolving actor identity, UTC timestamp, and client metadata.
/// Registered with Scoped lifetime convention per Architecture §18.
/// </summary>
public class AuditContext : IAuditContext, IAuditContextAccessor
{
    private readonly ICurrentContextProvider _currentContextProvider;
    private readonly ISystemClock _clock;

    public AuditContext(ICurrentContextProvider currentContextProvider, ISystemClock clock)
    {
        _currentContextProvider = currentContextProvider ?? throw new ArgumentNullException(nameof(currentContextProvider));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public Guid? UserId => _currentContextProvider.CurrentUserId;

    /// <inheritdoc />
    public Guid? PersonId => _currentContextProvider.CurrentPersonId;

    /// <inheritdoc />
    public DateTime Timestamp => _clock.UtcNow;

    /// <inheritdoc />
    public string? IpAddress { get; private set; }

    /// <inheritdoc />
    public string? UserAgent { get; private set; }

    /// <inheritdoc />
    public void SetClientInfo(string? ipAddress, string? userAgent)
    {
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }
}
