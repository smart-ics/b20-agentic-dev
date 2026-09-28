using Cakra.Core;

namespace Cakra.Api.Infrastructure.Context;

/// <summary>
/// Default <see cref="IAuditContext"/> that derives the acting identity from the
/// ambient <see cref="ICurrentContextProvider"/> and the recorded timestamp from
/// the <see cref="ISystemClock"/> (Architecture §18 Audit Logging). Registered as
/// scoped to match the request context it reads from.
/// </summary>
public sealed class AuditContext : IAuditContext
{
    private readonly ICurrentContextProvider _context;
    private readonly ISystemClock _clock;

    /// <summary>Creates the audit context over the ambient request context and system clock.</summary>
    public AuditContext(ICurrentContextProvider context, ISystemClock clock)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public Guid? ActorUserId => _context.CurrentUserId;

    /// <inheritdoc />
    public Guid? ActorPersonId => _context.CurrentPersonId;

    /// <inheritdoc />
    public DateTime RecordedAtUtc => _clock.UtcNow;
}
