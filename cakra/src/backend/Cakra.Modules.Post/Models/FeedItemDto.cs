using System.Text.Json;
using Cakra.Modules.Post.Domain;

namespace Cakra.Modules.Post;

/// <summary>
/// Read-only data transfer object representing a projected row in <c>post.FeedItems</c>
/// (Architecture §6, §7, §8, §9, §12, §16, §17, §20, §21 — SCR-FEED-001).
/// Strictly non-mutating read projection model.
/// </summary>
public sealed record FeedItemDto
{
    private string _postType = PostSourceNames.HumanAuthored;
    private string _contentExcerpt = string.Empty;
    private string _reactionCountsJson = "{}";
    private IReadOnlyDictionary<string, int>? _reactionCounts;
    private DateTime _updatedAt;

    public Guid FeedItemId { get; init; }

    /// <summary>Convenience alias for <see cref="FeedItemId"/>.</summary>
    public Guid Id
    {
        get => FeedItemId;
        init => FeedItemId = value;
    }

    public Guid PostId { get; init; }

    public Guid? AuthorPersonId { get; init; }

    public string? AuthorName { get; init; }

    /// <summary>Convenience alias for <see cref="AuthorName"/> (SCR-FEED-001).</summary>
    public string? Author => AuthorName;

    public string PostType
    {
        get => _postType;
        init => _postType = string.IsNullOrWhiteSpace(value) ? PostSourceNames.HumanAuthored : value;
    }

    /// <summary>Convenience alias for <see cref="PostType"/> matching <c>post.Posts.Source</c>.</summary>
    public string Source
    {
        get => _postType;
        init => _postType = string.IsNullOrWhiteSpace(value) ? PostSourceNames.HumanAuthored : value;
    }

    public string Title { get; init; } = string.Empty;

    public string ContentExcerpt
    {
        get => _contentExcerpt;
        init => _contentExcerpt = value ?? string.Empty;
    }

    /// <summary>Convenience alias for <see cref="ContentExcerpt"/>.</summary>
    public string Summary
    {
        get => _contentExcerpt;
        init => _contentExcerpt = value ?? string.Empty;
    }

    public string Status { get; init; } = PostStatusNames.Active;

    public string Visibility { get; init; } = PostVisibilityNames.Visible;

    public bool IsException { get; init; }

    public string? ExceptionType { get; init; }

    public string? ReferenceType { get; init; }

    public Guid? ReferenceId { get; init; }

    public string? ReferenceDisplay { get; init; }

    public Guid? CustomerId { get; init; }

    public string? CustomerName { get; init; }

    /// <summary>Convenience alias for <see cref="CustomerName"/> (SCR-FEED-001).</summary>
    public string? Customer => CustomerName;

    public Guid? ProductId { get; init; }

    public string? ProductName { get; init; }

    /// <summary>Convenience alias for <see cref="ProductName"/> (SCR-FEED-001).</summary>
    public string? Product => ProductName;

    public Guid? RequestId { get; init; }

    public Guid? WorkPackageId { get; init; }

    public int CommentCount { get; init; }

    public string? LatestCommentExcerpt { get; init; }

    public string? SearchContent { get; init; }

    public string ReactionCountsJson
    {
        get => _reactionCountsJson;
        init
        {
            _reactionCountsJson = string.IsNullOrWhiteSpace(value) ? "{}" : value;
            _reactionCounts = null;
        }
    }

    /// <summary>
    /// Parsed dictionary of active reaction counts by <c>ReactionType</c> (e.g., <c>{"SEEN": 4, "EXPERIENCED": 2}</c>).
    /// </summary>
    public IReadOnlyDictionary<string, int> ReactionCounts
    {
        get => _reactionCounts ??= ParseReactionCounts(_reactionCountsJson);
        init
        {
            _reactionCounts = value ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _reactionCountsJson = SerializeReactionCounts(_reactionCounts);
        }
    }

    /// <summary>Total count of active reactions across all reaction types (SCR-FEED-001).</summary>
    public int ReactionCount => ReactionCounts.Values.Sum();

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt
    {
        get => _updatedAt == default ? CreatedAt : _updatedAt;
        init => _updatedAt = value;
    }

    /// <summary>Convenience alias for <see cref="UpdatedAt"/>.</summary>
    public DateTime LastActivityAt
    {
        get => _updatedAt == default ? CreatedAt : _updatedAt;
        init => _updatedAt = value;
    }

    /// <summary>
    /// Serializes a reaction counts dictionary into canonical JSON (uppercase keys, positive counts only).
    /// </summary>
    public static string SerializeReactionCounts(IReadOnlyDictionary<string, int>? reactionCounts)
    {
        if (reactionCounts is null || reactionCounts.Count == 0)
        {
            return "{}";
        }

        var normalized = reactionCounts
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && kvp.Value > 0)
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                kvp => kvp.Key.Trim().ToUpperInvariant(),
                kvp => kvp.Value,
                StringComparer.OrdinalIgnoreCase);

        return normalized.Count == 0 ? "{}" : JsonSerializer.Serialize(normalized);
    }

    /// <summary>
    /// Parses a <c>ReactionCountsJson</c> string into a case-insensitive dictionary.
    /// </summary>
    public static IReadOnlyDictionary<string, int> ParseReactionCounts(string? reactionCountsJson)
    {
        if (string.IsNullOrWhiteSpace(reactionCountsJson))
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, int>>(reactionCountsJson);
            if (raw is null || raw.Count == 0)
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            return raw
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && kvp.Value > 0)
                .ToDictionary(
                    kvp => kvp.Key.Trim().ToUpperInvariant(),
                    kvp => kvp.Value,
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
