using Cakra.Core;

namespace Cakra.Modules.Product.Domain;

/// <summary>
/// Authoritative Product aggregate root representing a business product provided, maintained, or developed by ICS
/// (Architecture §6, §10, §16; Product Domain §5, §6, §8, §9).
/// Owns product identity, unique code, descriptive attributes, product owner reference, and lifecycle status.
/// </summary>
public sealed class Product : EntityBase
{
    public const string StatusActive = "ACTIVE";
    public const string StatusInactive = "INACTIVE";

    /// <summary>
    /// Unique identifier of the product. Synonymous with <see cref="EntityBase.Id"/>.
    /// </summary>
    public Guid ProductId
    {
        get => Id;
        set => Id = value;
    }

    /// <summary>
    /// Unique business code identifying the product (e.g., <c>MYHOSP</c>, <c>PENAEL</c>, <c>BTRADE3</c>).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Convenience alias for <see cref="Code"/>.
    /// </summary>
    public string ProductCode
    {
        get => Code;
        set => Code = value;
    }

    /// <summary>
    /// Official name of the product (e.g., <c>MyHospital</c>, <c>PenaEl</c>, <c>BTrade3</c>).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Convenience alias for <see cref="Name"/>.
    /// </summary>
    public string ProductName
    {
        get => Name;
        set => Name = value;
    }

    /// <summary>
    /// Optional descriptive summary of the product.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Identifier of the organizational <c>Person</c> who owns this product (Product Domain Rule 2, 3).
    /// </summary>
    public Guid OwnerPersonId { get; set; }

    /// <summary>
    /// Authoritative lifecycle status (<c>ACTIVE</c> or <c>INACTIVE</c>; Product Domain Rule 9).
    /// </summary>
    public string Status { get; set; } = StatusActive;

    /// <summary>
    /// Returns <c>true</c> when <see cref="Status"/> is <c>ACTIVE</c>.
    /// </summary>
    public bool IsActive => string.Equals(Status, StatusActive, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new active <see cref="Product"/> instance (Architecture §10; Product Domain Rule 1, 2, 3, 9).
    /// </summary>
    public static Product Create(
        string code,
        string name,
        string? description,
        Guid ownerPersonId,
        Guid? id = null,
        DateTime? createdAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Product code cannot be null or whitespace.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name cannot be null or whitespace.", nameof(name));
        }

        if (ownerPersonId == Guid.Empty)
        {
            throw new ArgumentException("Every active Product must have a valid OwnerPersonId.", nameof(ownerPersonId));
        }

        var productId = id is { } guid && guid != Guid.Empty ? guid : Guid.NewGuid();

        return new Product
        {
            Id = productId,
            Code = code.Trim(),
            Name = name.Trim(),
            Description = NormalizeOptional(description),
            OwnerPersonId = ownerPersonId,
            Status = StatusActive,
            CreatedAt = createdAtUtc ?? DateTime.UtcNow,
            UpdatedAt = null
        };
    }

    /// <summary>
    /// Updates descriptive attributes of the product (Architecture §10; Product Domain Rule 7).
    /// </summary>
    public void Update(
        string name,
        string? description,
        DateTime? updatedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name cannot be null or whitespace.", nameof(name));
        }

        Name = name.Trim();
        Description = NormalizeOptional(description);
        UpdatedAt = updatedAtUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Reassigns product ownership to the specified person (Architecture §10; Product Domain Rule 2, 3).
    /// </summary>
    public void AssignOwner(
        Guid newOwnerPersonId,
        DateTime? updatedAtUtc = null)
    {
        if (newOwnerPersonId == Guid.Empty)
        {
            throw new ArgumentException("Every Product must have a valid OwnerPersonId.", nameof(newOwnerPersonId));
        }

        OwnerPersonId = newOwnerPersonId;
        UpdatedAt = updatedAtUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Transitions the product lifecycle status to <c>ACTIVE</c> (Architecture §10; Product Domain §9).
    /// </summary>
    public void Activate(DateTime? updatedAtUtc = null)
    {
        if (OwnerPersonId == Guid.Empty)
        {
            throw new InvalidOperationException("Every active Product must have an assigned Product Owner.");
        }

        Status = StatusActive;
        UpdatedAt = updatedAtUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Transitions the product lifecycle status to <c>INACTIVE</c> while preserving historical references
    /// (Architecture §10; Product Domain Rule 8, §9).
    /// </summary>
    public void Deactivate(DateTime? updatedAtUtc = null)
    {
        Status = StatusInactive;
        UpdatedAt = updatedAtUtc ?? DateTime.UtcNow;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
