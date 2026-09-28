namespace ICS.Modules.Post.Domain;

/// <summary>
/// Authoritative source classification for the Post aggregate.
/// post-domain.md §5.
/// </summary>
public static class PostSource
{
    /// <summary>
    /// The Post was created automatically by the Operational System in response to an operational event or state change.
    /// </summary>
    public const string SystemGenerated = "SYSTEM_GENERATED";

    /// <summary>
    /// The Post was intentionally created by a Person for operational communication or knowledge sharing.
    /// </summary>
    public const string HumanAuthored = "HUMAN_AUTHORED";

    /// <summary>
    /// Returns all valid source values.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [SystemGenerated, HumanAuthored];

    /// <summary>
    /// Determines whether the specified source value is valid.
    /// </summary>
    public static bool IsValid(string? source) =>
        !string.IsNullOrEmpty(source) && All.Contains(source, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the Post was created by the operational system.
    /// </summary>
    public static bool IsSystemGenerated(string? source) =>
        string.Equals(source, SystemGenerated, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the Post was authored by a Person.
    /// </summary>
    public static bool IsHumanAuthored(string? source) =>
        string.Equals(source, HumanAuthored, StringComparison.OrdinalIgnoreCase);
}