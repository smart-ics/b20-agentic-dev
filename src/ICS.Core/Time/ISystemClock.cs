namespace ICS.Core.Time;

/// <summary>
/// Abstraction for accessing system time, enabling deterministic testing of time-dependent logic.
/// </summary>
public interface ISystemClock
{
    /// <summary>
    /// Gets the current system time in UTC as a <see cref="DateTime"/>.
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Gets the current system time in UTC as a <see cref="DateTimeOffset"/>.
    /// </summary>
    DateTimeOffset OffsetUtcNow { get; }
}
