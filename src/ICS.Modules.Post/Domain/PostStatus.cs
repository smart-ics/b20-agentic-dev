namespace ICS.Modules.Post.Domain;

/// <summary>
/// Authoritative lifecycle states for the Post aggregate.
/// Architecture §8 (Post Lifecycle); post-domain.md §8.
/// </summary>
public static class PostStatus
{
    /// <summary>
    /// The Post exists as current operational knowledge.
    /// </summary>
    public const string Active = "ACTIVE";

    /// <summary>
    /// The Post is retained as historical operational knowledge but is no longer part of normal active communication.
    /// Archiving does not delete the Post.
    /// </string>
    public const string Archived = "ARCHIVED";

    /// <summary>
    /// Returns all valid lifecycle status values.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [Active, Archived];

    /// <summary>
    /// Determines whether the specified status is a valid Post lifecycle state.
    /// </summary>
    public static bool IsValid(string? status) =>
        !string.IsNullOrEmpty(status) && All.Contains(status, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the specified status is a terminal (closed) lifecycle state.
    /// </summary>
    public static bool IsTerminal(string? status) =>
        string.Equals(status, Archived, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the Post is in an active (non-archived) lifecycle state.
    /// </summary>
    public static bool IsActive(string? status) =>
        string.Equals(status, Active, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the Post is archived.
    /// </summary>
    public static bool IsArchived(string? status) =>
        string.Equals(status, Archived, StringComparison.OrdinalIgnoreCase);
}