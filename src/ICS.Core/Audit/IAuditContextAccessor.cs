namespace ICS.Core.Audit;

/// <summary>
/// Mutator interface used by request pipeline and middleware to attach client connection metadata to audit context.
/// Paired with <see cref="IAuditContext"/> per Architecture §18.
/// </summary>
public interface IAuditContextAccessor
{
    /// <summary>
    /// Attaches client network and user-agent metadata to the current audit scope.
    /// </summary>
    /// <param name="ipAddress">The client IP address initiating the operation.</param>
    /// <param name="userAgent">The client User-Agent header string.</param>
    void SetClientInfo(string? ipAddress, string? userAgent);
}
