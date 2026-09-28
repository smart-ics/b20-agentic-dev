using Cakra.Core;

namespace Cakra.Api.Infrastructure.Context;

/// <summary>
/// Default <see cref="ISystemClock"/> backed by <see cref="DateTime.UtcNow"/>.
/// Registered as a singleton: the clock is stateless (Architecture §19.2).
/// </summary>
public sealed class SystemClock : ISystemClock
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;
}
