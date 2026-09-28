namespace ICS.Modules.Organization.Domain;

using ICS.Core.Domain;

/// <summary>
/// Domain entity representing a recognized area of accountability.
/// Architecture §6, §7, §17.
/// </summary>
public class Responsibility : Entity
{
    public const string StatusActive = "ACTIVE";
    public const string StatusInactive = "INACTIVE";

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Status { get; private set; } = StatusActive;

    protected Responsibility() { }

    public Responsibility(Guid id, string name, string? description, string status, DateTime createdAt, DateTime? updatedAt = null)
        : base(id)
    {
        Id = id;
        Name = name;
        Description = description;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Responsibility Create(Guid id, string name, string? description, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Responsibility name cannot be empty.", nameof(name));

        return new Responsibility(id, name.Trim(), description?.Trim(), StatusActive, createdAt);
    }

    public void Update(string name, string? description, DateTime updatedAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Responsibility name cannot be empty.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim();
        UpdatedAt = updatedAt;
    }

    public void Deactivate(DateTime updatedAt)
    {
        Status = StatusInactive;
        UpdatedAt = updatedAt;
    }

    public void Activate(DateTime updatedAt)
    {
        Status = StatusActive;
        UpdatedAt = updatedAt;
    }

    public bool IsActive => string.Equals(Status, StatusActive, StringComparison.OrdinalIgnoreCase);
}
