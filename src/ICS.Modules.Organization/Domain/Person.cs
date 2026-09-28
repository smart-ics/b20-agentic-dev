namespace ICS.Modules.Organization.Domain;

using ICS.Core.Domain;
using ICS.Modules.Organization.Domain.Events;

/// <summary>
/// Authoritative domain entity representing an individual participant in organizational operations.
/// Architecture §6, §14, §16, §17.
/// </summary>
public class Person : Entity
{
    public const string StatusActive = "ACTIVE";
    public const string StatusInactive = "INACTIVE";

    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Status { get; private set; } = StatusActive;

    /// <summary>
    /// Parameterless constructor for Dapper mapping.
    /// </summary>
    protected Person() { }

    public Person(Guid id, string name, string email, string status, DateTime createdAt, DateTime? updatedAt = null)
        : base(id)
    {
        Id = id;
        Name = name;
        Email = email;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Factory method creating an active Person and emitting <see cref="PersonCreated"/>.
    /// </summary>
    public static Person Create(Guid id, string name, string email, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Person name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Person email cannot be empty.", nameof(email));

        var person = new Person(id, name.Trim(), email.Trim().ToLowerInvariant(), StatusActive, createdAt);
        person.AddDomainEvent(new PersonCreated(person.Id, person.Name, person.Email));
        return person;
    }

    /// <summary>
    /// Updates person details.
    /// </summary>
    public void Update(string name, string email, DateTime updatedAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Person name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Person email cannot be empty.", nameof(email));

        Name = name.Trim();
        Email = email.Trim().ToLowerInvariant();
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Deactivates person and emits <see cref="PersonDeactivated"/>.
    /// </summary>
    public void Deactivate(DateTime updatedAt)
    {
        if (Status == StatusInactive)
        {
            return;
        }

        Status = StatusInactive;
        UpdatedAt = updatedAt;
        AddDomainEvent(new PersonDeactivated(Id, Name));
    }

    /// <summary>
    /// Re-activates person.
    /// </summary>
    public void Activate(DateTime updatedAt)
    {
        Status = StatusActive;
        UpdatedAt = updatedAt;
    }

    public bool IsActive => string.Equals(Status, StatusActive, StringComparison.OrdinalIgnoreCase);
}
