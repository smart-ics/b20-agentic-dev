using Cakra.Core;

namespace Cakra.Modules.Customer.Domain;

/// <summary>
/// Domain entity representing a person representing a <see cref="Customer"/> organization (Architecture §6, §16; Domain §5, §6).
/// Must belong to exactly one <see cref="Customer"/> (Domain Rule 3).
/// </summary>
public sealed class CustomerContact : EntityBase
{
    public const string StatusActive = "ACTIVE";
    public const string StatusInactive = "INACTIVE";

    /// <summary>
    /// Unique identifier of the customer contact. Synonymous with <see cref="EntityBase.Id"/>.
    /// </summary>
    public Guid ContactId
    {
        get => Id;
        set => Id = value;
    }

    /// <summary>
    /// Identifier of the parent <see cref="Customer"/> organization (Domain Rule 3).
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Full name of the customer contact representative.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Job title or position at the customer organization.
    /// </summary>
    public string? Position { get; set; }

    /// <summary>
    /// Contact telephone number.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Contact email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Lifecycle status (<c>ACTIVE</c> or <c>INACTIVE</c>, Domain Rule 8).
    /// </summary>
    public string Status { get; set; } = StatusActive;

    /// <summary>
    /// Returns <c>true</c> when <see cref="Status"/> is <c>ACTIVE</c>.
    /// </summary>
    public bool IsActive => string.Equals(Status, StatusActive, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new active <see cref="CustomerContact"/> associated with a <see cref="Customer"/>.
    /// </summary>
    public static CustomerContact Create(
        Guid customerId,
        string name,
        string? position = null,
        string? phoneNumber = null,
        string? email = null,
        Guid? id = null,
        DateTime? createdAtUtc = null)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId cannot be empty.", nameof(customerId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Contact Name cannot be null or whitespace.", nameof(name));
        }

        var contactId = id is { } guid && guid != Guid.Empty ? guid : Guid.NewGuid();

        return new CustomerContact
        {
            Id = contactId,
            CustomerId = customerId,
            Name = name.Trim(),
            Position = string.IsNullOrWhiteSpace(position) ? null : position.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Status = StatusActive,
            CreatedAt = createdAtUtc ?? DateTime.UtcNow,
            UpdatedAt = null
        };
    }

    /// <summary>
    /// Updates contact details and optional status.
    /// </summary>
    public void Update(
        string name,
        string? position,
        string? phoneNumber,
        string? email,
        string? status = null,
        DateTime? updatedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Contact Name cannot be null or whitespace.", nameof(name));
        }

        if (status is not null)
        {
            var normalized = status.Trim().ToUpperInvariant();
            if (normalized != StatusActive && normalized != StatusInactive)
            {
                throw new ArgumentException($"Contact Status must be '{StatusActive}' or '{StatusInactive}'.", nameof(status));
            }

            Status = normalized;
        }

        Name = name.Trim();
        Position = string.IsNullOrWhiteSpace(position) ? null : position.Trim();
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        UpdatedAt = updatedAtUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Transitions the contact to <c>INACTIVE</c> while preserving historical references.
    /// </summary>
    public void Deactivate(DateTime? updatedAtUtc = null)
    {
        Status = StatusInactive;
        UpdatedAt = updatedAtUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Transitions the contact to <c>ACTIVE</c>.
    /// </summary>
    public void Activate(DateTime? updatedAtUtc = null)
    {
        Status = StatusActive;
        UpdatedAt = updatedAtUtc ?? DateTime.UtcNow;
    }
}
