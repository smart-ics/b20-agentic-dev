namespace Cakra.Modules.Post.Domain;

/// <summary>
/// Authoritative visibility condition of a <see cref="Post"/> (Post Domain §5, §8; Architecture §12).
/// Visibility is independent of <see cref="PostStatus"/>.
/// </summary>
public enum PostVisibility
{
    /// <summary>The Post is visible in normal operational views.</summary>
    Visible = 0,

    /// <summary>The Post is excluded from normal visibility while retaining its identity, discussion, and history.</summary>
    Hidden = 1
}

/// <summary>
/// Canonical string representations and conversion helpers for <see cref="PostVisibility"/>.
/// </summary>
public static class PostVisibilityNames
{
    public const string Visible = "VISIBLE";
    public const string Hidden = "HIDDEN";

    public static string ToName(this PostVisibility visibility) => visibility switch
    {
        PostVisibility.Visible => Visible,
        PostVisibility.Hidden => Hidden,
        _ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, $"Unsupported PostVisibility '{visibility}'.")
    };

    public static PostVisibility FromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Post visibility name cannot be null or empty.", nameof(name));
        }

        return name.Trim().ToUpperInvariant() switch
        {
            Visible => PostVisibility.Visible,
            Hidden => PostVisibility.Hidden,
            _ => throw new ArgumentException($"Unsupported Post visibility '{name}'.", nameof(name))
        };
    }

    public static PostVisibility? FromNullableName(string? name)
        => string.IsNullOrWhiteSpace(name) ? null : FromName(name);
}
