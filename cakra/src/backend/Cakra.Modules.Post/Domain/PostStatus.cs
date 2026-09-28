namespace Cakra.Modules.Post.Domain;

/// <summary>
/// Authoritative lifecycle status of a <see cref="Post"/> (Post Domain §5, §8; Architecture §12).
/// </summary>
public enum PostStatus
{
    /// <summary>The Post exists as current operational knowledge.</summary>
    Active = 0,

    /// <summary>The Post is retained as historical operational knowledge and excluded from normal active Feed views.</summary>
    Archived = 1
}

/// <summary>
/// Canonical string representations and conversion helpers for <see cref="PostStatus"/>.
/// </summary>
public static class PostStatusNames
{
    public const string Active = "ACTIVE";
    public const string Archived = "ARCHIVED";

    public static string ToName(this PostStatus status) => status switch
    {
        PostStatus.Active => Active,
        PostStatus.Archived => Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, $"Unsupported PostStatus '{status}'.")
    };

    public static PostStatus FromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Post status name cannot be null or empty.", nameof(name));
        }

        return name.Trim().ToUpperInvariant() switch
        {
            Active => PostStatus.Active,
            Archived => PostStatus.Archived,
            _ => throw new ArgumentException($"Unsupported Post status '{name}'.", nameof(name))
        };
    }

    public static PostStatus? FromNullableName(string? name)
        => string.IsNullOrWhiteSpace(name) ? null : FromName(name);
}
