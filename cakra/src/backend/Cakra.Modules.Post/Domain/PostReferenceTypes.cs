using Cakra.Modules.Post.Domain.Exceptions;

namespace Cakra.Modules.Post.Domain;

/// <summary>
/// Canonical operational reference target types for <see cref="PostReference"/>
/// (Post Domain §5; Architecture §12, §15).
/// </summary>
public static class PostReferenceTypes
{
    public const string Request = "REQUEST";
    public const string WorkPackage = "WORK_PACKAGE";
    public const string Customer = "CUSTOMER";
    public const string Product = "PRODUCT";
    public const string Organization = "ORGANIZATION";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Request,
        WorkPackage,
        Customer,
        Product,
        Organization
    };

    private static readonly HashSet<string> AllowedSet = new(All, StringComparer.OrdinalIgnoreCase);

    public static bool IsValid(string? referenceType)
        => !string.IsNullOrWhiteSpace(referenceType) && AllowedSet.Contains(referenceType.Trim());

    public static string NormalizeAndValidate(string referenceType)
    {
        if (string.IsNullOrWhiteSpace(referenceType))
        {
            throw new PostDomainValidationException("Reference type cannot be null or empty.", nameof(referenceType));
        }

        var normalized = referenceType.Trim().ToUpperInvariant() switch
        {
            "WORKPACKAGE" => WorkPackage,
            var value => value
        };

        if (!AllowedSet.Contains(normalized))
        {
            throw new PostDomainValidationException(
                $"Unsupported Post reference type '{referenceType}'. Allowed reference types are: {string.Join(", ", All)}.",
                nameof(referenceType));
        }

        return normalized;
    }
}

/// <summary>
/// Canonical operational exception classifications for posts/feed items (Architecture §12).
/// </summary>
public static class PostExceptionTypes
{
    public const string Escalation = "ESCALATION";
    public const string Stalled = "STALLED";
    public const string Rejection = "REJECTION";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Escalation,
        Stalled,
        Rejection
    };

    private static readonly HashSet<string> AllowedSet = new(All, StringComparer.OrdinalIgnoreCase);

    public static string? NormalizeAndValidate(string? exceptionType)
    {
        if (string.IsNullOrWhiteSpace(exceptionType))
        {
            return null;
        }

        var normalized = exceptionType.Trim().ToUpperInvariant();
        if (!AllowedSet.Contains(normalized))
        {
            throw new PostDomainValidationException(
                $"Unsupported Post exception type '{exceptionType}'. Allowed exception types are: {string.Join(", ", All)}.",
                nameof(exceptionType));
        }

        return normalized;
    }
}
