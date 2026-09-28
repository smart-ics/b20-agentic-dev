namespace ICS.Core.Time;

/// <summary>
/// Authoritative system clock implementation providing UTC system time.
/// Registered with Singleton lifetime convention.
/// </summary>
public sealed class SystemClock : ISystemClock
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;

    /// <inheritdoc />
    public DateTimeOffset OffsetUtcNow => DateTimeOffset.UtcNow;
}
