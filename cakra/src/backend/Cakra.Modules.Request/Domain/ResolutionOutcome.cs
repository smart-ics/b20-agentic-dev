namespace Cakra.Modules.Request.Domain;

/// <summary>
/// Authoritative outcome classification for a RequestResolution.
/// </summary>
public enum ResolutionOutcome
{
    /// <summary>The request was rejected during triage evaluation without execution.</summary>
    Rejected = 1,

    /// <summary>The request was successfully completed and the resolution was verified.</summary>
    Completed = 2,

    /// <summary>The request was resolved directly.</summary>
    Resolved = 3,

    /// <summary>The request was cancelled.</summary>
    Cancelled = 4
}

/// <summary>
/// Canonical string representation of resolution outcomes.
/// </summary>
public static class ResolutionOutcomeNames
{
    public const string Rejected = "REJECTED";
    public const string Completed = "COMPLETED";
    public const string Resolved = "RESOLVED";
    public const string Cancelled = "CANCELLED";

    public static string ToName(this ResolutionOutcome outcome) => outcome switch
    {
        ResolutionOutcome.Rejected => Rejected,
        ResolutionOutcome.Completed => Completed,
        ResolutionOutcome.Resolved => Resolved,
        ResolutionOutcome.Cancelled => Cancelled,
        _ => outcome.ToString().ToUpperInvariant()
    };
}
