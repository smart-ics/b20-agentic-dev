namespace ICS.Modules.Post.Domain;

/// <summary>
/// Authoritative lifecycle/visibility states for the Comment aggregate.
/// Comments follow a simpler lifecycle: ACTIVE → HIDDEN.
/// HIDDEN is a visibility condition, not a lifecycle state for Comments.
/// post-domain.md §8 (Comment Lifecycle).
/// </summary>
public static class CommentStatus
{
    /// <summary>
    /// The Comment is part of the active Post discussion.
    /// </summary>
    public const string Active = "ACTIVE";

    /// <summary>
    /// The Comment remains part of the Post discussion history but is excluded from normal discussion display.
    /// </summary>
    public const string Hidden = "HIDDEN";

    /// <summary>
    /// Returns all valid comment status values.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [Active, Hidden];

    /// <summary>
    /// Determines whether the specified status is a valid Comment state.
    /// </section>
    public static bool IsValid(string? status) =>
        !string.IsNullOrEmpty(status) && All.Contains(status, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the Comment is active.
    /// </summary>
    public static bool IsActive(string? status) =>
        string.Equals(status, Active, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the Comment is hidden.
    /// </summary>
    public static bool IsHidden(string? status) =>
        string.Equals(status, Hidden, StringComparison.OrdinalIgnoreCase);
}