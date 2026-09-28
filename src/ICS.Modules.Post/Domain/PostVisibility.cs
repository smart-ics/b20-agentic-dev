namespace ICS.Modules.Post.Domain;

/// <summary>
/// Authoritative visibility conditions for the Post aggregate.
/// Visibility is an independent property of the current Post state, distinct from lifecycle status.
/// Architecture §8 (Post State vs Visibility); post-domain.md §8.
/// </summary>
public static class PostVisibility
{
    /// <summary>
    /// The Post is available in normal operational views.
    /// </summary>
    public const string Visible = "VISIBLE";

    /// <summary>
    /// The Post is excluded from normal visibility but remains part of the authoritative operational record.
    /// </summary>
    public const string Hidden = "HIDDEN";

    /// <summary>
    /// Returns all valid visibility values.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [Visible, Hidden];

    /// <summary>
    /// Determines whether the specified visibility value is valid.
    /// </summary>
    public static bool IsValid(string? visibility) =>
        !string.IsNullOrEmpty(visibility) && All.Contains(visibility, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the Post is visible in normal operational views.
    /// </summary>
    public static bool IsVisible(string? visibility) =>
        string.Equals(visibility, Visible, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the Post is hidden.
    /// </summary>
    public static bool IsHidden(string? visibility) =>
        string.Equals(visibility, Hidden, StringComparison.OrdinalIgnoreCase);
}