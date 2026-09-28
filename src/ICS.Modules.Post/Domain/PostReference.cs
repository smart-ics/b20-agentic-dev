namespace ICS.Modules.Post.Domain;

using ICS.Core.Domain;
using ICS.Modules.Post.Domain.Exceptions;

/// <summary>
/// Represents an optional relationship from a Post to another operational object.
/// A Post Reference records contextual association only.
/// The referenced domain remains authoritative for the referenced object's current state.
/// A Post may lose its reference while remaining valid as a Post.
/// post-domain.md §5 (Post Reference), §7 (Business Rules 8-14).
/// </summary>
public sealed class PostReference : Entity
{
    /// <summary>
    /// Valid reference types for cross-domain Post references.
    /// </summary>
    public static class ReferenceTypes
    {
        public const string Request = "REQUEST";
        public const string WorkPackage = "WORK_PACKAGE";
        public const string Customer = "CUSTOMER";
        public const string Product = "PRODUCT";
        public const string Organization = "ORGANIZATION";

        public static readonly IReadOnlyList<string> All =
            [Request, WorkPackage, Customer, Product, Organization];
    }

    public string ReferenceType { get; private set; } = string.Empty;
    public Guid TargetId { get; private set; } = Guid.Empty;

    /// <summary>
    /// Parameterless constructor for Dapper persistence mapping.
    /// </summary>
    private PostReference() { }

    /// <summary>
    /// Full constructor for hydrating the entity from persistence without raising domain events.
    /// </summary>
    public PostReference(
        Guid id,
        Guid postId,
        string referenceType,
        Guid targetId,
        DateTime createdAt)
        : base(id)
    {
        Id = id;
        ReferenceType = referenceType;
        TargetId = targetId;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Factory method to create a new Post Reference.
    /// Validates the reference type and target identifier.
    /// </summary>
    public static PostReference Create(
        Guid id,
        Guid postId,
        string referenceType,
        Guid targetId,
        DateTime createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("ReferenceId cannot be empty.", nameof(id));
        if (postId == Guid.Empty)
            throw new ArgumentException("PostId cannot be empty.", nameof(postId));
        if (string.IsNullOrWhiteSpace(referenceType))
            throw new ArgumentException("ReferenceType cannot be empty.", nameof(referenceType));
        if (!ReferenceTypes.All.Contains(referenceType, StringComparer.OrdinalIgnoreCase))
            throw new InvalidPostReferenceException(
                $"Invalid reference type '{referenceType}'. Must be one of: {string.Join(", ", ReferenceTypes.All)}.");
        if (targetId == Guid.Empty)
            throw new ArgumentException("TargetId cannot be empty.", nameof(targetId));

        return new PostReference(id, postId, referenceType.ToUpperInvariant(), targetId, createdAt);
    }

    /// <summary>
    /// Gets the PostId this reference belongs to (not part of the entity identity but used for persistence).
    /// </summary>
    public Guid PostId { get; private set; } = Guid.Empty;
}