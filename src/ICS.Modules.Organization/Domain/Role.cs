namespace ICS.Modules.Organization.Domain;

using ICS.Core.Domain;

/// <summary>
/// Domain entity representing a recognized organizational position.
/// Architecture §6, §14, §17.
/// </summary>
public class Role : Entity
{
    public const string StatusActive = "ACTIVE";
    public const string StatusInactive = "INACTIVE";

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Status { get; private set; } = StatusActive;

    protected Role() { }

    public Role(Guid id, string name, string? description, string status, DateTime createdAt, DateTime? updatedAt = null)
        : base(id)
    {
        Id = id;
        Name = name;
        Description = description;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Role Create(Guid id, string name, string? description, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Role name cannot be empty.", nameof(name));

        return new Role(id, name.Trim(), description?.Trim(), StatusActive, createdAt);
    }

    public void Update(string name, string? description, DateTime updatedAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Role name cannot be empty.", nameof(name));

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
