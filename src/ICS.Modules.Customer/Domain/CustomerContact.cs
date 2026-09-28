namespace ICS.Modules.Customer.Domain;

using ICS.Core.Domain;
using ICS.Modules.Customer.Domain.Events;

/// <summary>
/// Authoritative domain entity representing a contact person representing a Customer organization.
/// Architecture §6, §7, §16, §17; customer-domain.md.
/// </summary>
public class CustomerContact : Entity
{
    public const string StatusActive = "ACTIVE";
    public const string StatusInactive = "INACTIVE";

    public Guid CustomerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Position { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Email { get; private set; }
    public string Status { get; private set; } = StatusActive;

    /// <summary>
    /// Parameterless constructor for Dapper mapping.
    /// </summary>
    protected CustomerContact() { }

    public CustomerContact(
        Guid id,
        Guid customerId,
        string name,
        string? position,
        string? phoneNumber,
        string? email,
        string status,
        DateTime createdAt,
        DateTime? updatedAt = null)
        : base(id)
    {
        Id = id;
        CustomerId = customerId;
        Name = name;
        Position = position;
        PhoneNumber = phoneNumber;
        Email = email;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Factory method creating an active CustomerContact and emitting <see cref="CustomerContactAdded"/>.
    /// </summary>
    public static CustomerContact Create(
        Guid id,
        Guid customerId,
        string name,
        string? position,
        string? phoneNumber,
        string? email,
        DateTime createdAt)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId cannot be empty.", nameof(customerId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Contact name cannot be empty.", nameof(name));

        var contact = new CustomerContact(
            id,
            customerId,
            name.Trim(),
            position?.Trim(),
            phoneNumber?.Trim(),
            email?.Trim().ToLowerInvariant(),
            StatusActive,
            createdAt);

        contact.AddDomainEvent(new CustomerContactAdded(
            contact.Id,
            contact.CustomerId,
            contact.Name,
            contact.Email));

        return contact;
    }

    /// <summary>
    /// Updates contact details and emits <see cref="CustomerContactUpdated"/>.
    /// </summary>
    public void Update(
        string name,
        string? position,
        string? phoneNumber,
        string? email,
        DateTime updatedAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Contact name cannot be empty.", nameof(name));

        Name = name.Trim();
        Position = position?.Trim();
        PhoneNumber = phoneNumber?.Trim();
        Email = email?.Trim().ToLowerInvariant();
        UpdatedAt = updatedAt;

        AddDomainEvent(new CustomerContactUpdated(
            Id,
            CustomerId,
            Name,
            Email));
    }

    /// <summary>
    /// Deactivates contact and emits <see cref="CustomerContactDeactivated"/>.
    /// </summary>
    public void Deactivate(DateTime updatedAt)
    {
        if (Status == StatusInactive)
        {
            return;
        }

        Status = StatusInactive;
        UpdatedAt = updatedAt;

        AddDomainEvent(new CustomerContactDeactivated(Id, CustomerId, Name));
    }

    /// <summary>
    /// Re-activates contact and emits <see cref="CustomerContactActivated"/>.
    /// </summary>
    public void Activate(DateTime updatedAt)
    {
        if (Status == StatusActive)
        {
            return;
        }

        Status = StatusActive;
        UpdatedAt = updatedAt;

        AddDomainEvent(new CustomerContactActivated(Id, CustomerId, Name));
    }

    public bool IsActive => string.Equals(Status, StatusActive, StringComparison.OrdinalIgnoreCase);
}
