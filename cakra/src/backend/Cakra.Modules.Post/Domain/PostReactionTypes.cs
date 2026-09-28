using Cakra.Modules.Post.Domain.Exceptions;

namespace Cakra.Modules.Post.Domain;

/// <summary>
/// Approved structured operational reaction types (Post Domain §5; FEAT-FCOL-002; Architecture §12).
/// Generic social-media reaction concepts (Like, Love, Haha) are explicitly prohibited.
/// </summary>
public static class PostReactionTypes
{
    public const string Seen = "SEEN";
    public const string Experienced = "EXPERIENCED";
    public const string HaveIdea = "HAVE_IDEA";
    public const string SimilarIssue = "SIMILAR_ISSUE";
    public const string Duplicate = "DUPLICATE";
    public const string NeedClarification = "NEED_CLARIFICATION";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Seen,
        Experienced,
        HaveIdea,
        SimilarIssue,
        Duplicate,
        NeedClarification
    };

    private static readonly HashSet<string> AllowedSet = new(All, StringComparer.OrdinalIgnoreCase);

    public static bool IsValid(string? reactionType)
        => !string.IsNullOrWhiteSpace(reactionType) && AllowedSet.Contains(reactionType.Trim());

    public static string NormalizeAndValidate(string reactionType)
    {
        if (string.IsNullOrWhiteSpace(reactionType))
        {
            throw new PostDomainValidationException("Reaction type cannot be null or empty.", nameof(reactionType));
        }

        var normalized = reactionType.Trim().ToUpperInvariant();
        if (!AllowedSet.Contains(normalized))
        {
            throw new PostDomainValidationException(
                $"Unsupported reaction type '{reactionType}'. Allowed operational reactions are: {string.Join(", ", All)}.",
                nameof(reactionType));
        }

        return normalized;
    }
}
