namespace ICS.Modules.Product.Domain;

using ICS.Core.Domain;
using ICS.Modules.Product.Domain.Events;

/// <summary>
/// Authoritative domain entity representing a Product developed, maintained, or provided by ICS.
/// Architecture §10, §16, §17; product-domain.md.
/// </summary>
public class Product : Entity
{
    public const string StatusActive = "ACTIVE";
    public const string StatusInactive = "INACTIVE";

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid OwnerPersonId { get; private set; }
    public string Status { get; private set; } = StatusActive;

    public bool IsActive => string.Equals(Status, StatusActive, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Parameterless constructor for Dapper mapping.
    /// </summary>
    protected Product() { }

    public Product(
        Guid id,
        string code,
        string name,
        string? description,
        Guid ownerPersonId,
        string status,
        DateTime createdAt,
        DateTime? updatedAt = null)
        : base(id)
    {
        Id = id;
        Code = code;
        Name = name;
        Description = description;
        OwnerPersonId = ownerPersonId;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Factory method creating an active Product and emitting <see cref="ProductCreated"/>.
    /// </summary>
    public static Product Create(
        Guid id,
        string code,
        string name,
        string? description,
        Guid ownerPersonId,
        DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Product code cannot be empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name cannot be empty.", nameof(name));
        if (ownerPersonId == Guid.Empty)
            throw new ArgumentException("Product owner must be specified.", nameof(ownerPersonId));

        var product = new Product(
            id,
            code.Trim().ToUpperInvariant(),
            name.Trim(),
            description?.Trim(),
            ownerPersonId,
            StatusActive,
            createdAt);

        product.AddDomainEvent(new ProductCreated(
            product.Id,
            product.Code,
            product.Name,
            product.Description,
            product.OwnerPersonId));

        return product;
    }

    /// <summary>
    /// Updates product descriptive attributes.
    /// </summary>
    public void Update(
        string name,
        string? description,
        DateTime updatedAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name cannot be empty.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim();
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Assigns a new product owner and emits <see cref="ProductOwnerChanged"/>.
    /// </summary>
    public void AssignOwner(
        Guid newOwnerPersonId,
        DateTime updatedAt)
    {
        if (newOwnerPersonId == Guid.Empty)
            throw new ArgumentException("New product owner must be specified.", nameof(newOwnerPersonId));

        var previousOwner = OwnerPersonId;
        OwnerPersonId = newOwnerPersonId;
        UpdatedAt = updatedAt;

        AddDomainEvent(new ProductOwnerChanged(Id, previousOwner, newOwnerPersonId));
    }

    /// <summary>
    /// Activates the product and emits <see cref="ProductActivated"/>.
    /// </summary>
    public void Activate(DateTime updatedAt)
    {
        if (Status == StatusActive)
            return;

        Status = StatusActive;
        UpdatedAt = updatedAt;

        AddDomainEvent(new ProductActivated(Id, Code, Name));
    }

    /// <summary>
    /// Deactivates the product and emits <see cref="ProductDeactivated"/>.
    /// Preserves all historical references per business rules.
    /// </summary>
    public void Deactivate(DateTime updatedAt)
    {
        if (Status == StatusInactive)
            return;

        Status = StatusInactive;
        UpdatedAt = updatedAt;

        AddDomainEvent(new ProductDeactivated(Id, Code, Name));
    }
}
