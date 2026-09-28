namespace Cakra.Core;

/// <summary>
/// Abstraction over the system clock so time-dependent logic remains testable.
/// </summary>
public interface ISystemClock
{
    /// <summary>Current UTC time.</summary>
    DateTime UtcNow { get; }
}
