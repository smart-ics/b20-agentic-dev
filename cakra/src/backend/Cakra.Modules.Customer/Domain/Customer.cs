using Cakra.Core;

namespace Cakra.Modules.Customer.Domain;

/// <summary>
/// Authoritative Customer aggregate root representing an organization served by ICS (Architecture §6, §16; Domain §5, §6).
/// Owns customer identity, code, name, lifecycle status, and maintenance contract indicator.
/// </summary>
public sealed class Customer : EntityBase
{
    public const string StatusActive = "ACTIVE";
    public const string StatusInactive = "INACTIVE";

    /// <summary>
    /// Unique identifier of the customer. Synonymous with <see cref="EntityBase.Id"/>.
    /// </summary>
    public Guid CustomerId
    {
        get => Id;
        set => Id = value;
    }

    /// <summary>
    /// Unique business code identifying the customer organization.
    /// </summary>
    public string CustomerCode { get; set; } = string.Empty;

    /// <summary>
    /// Convenience alias for <see cref="CustomerCode"/>.
    /// </summary>
    public string Code
    {
        get => CustomerCode;
        set => CustomerCode = value;
    }

    /// <summary>
    /// Official name of the customer organization.
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Convenience alias for <see cref="CustomerName"/>.
    /// </summary>
    public string Name
    {
        get => CustomerName;
        set => CustomerName = value;
    }

    /// <summary>
    /// Authoritative lifecycle status (<c>ACTIVE</c> or <c>INACTIVE</c>).
    /// </summary>
    public string Status { get; set; } = StatusActive;

    /// <summary>
    /// Indicates whether the customer currently holds an active maintenance contract (Domain Rule 9).
    /// </summary>
    public bool HasActiveMaintenanceContract { get; set; }

    /// <summary>
    /// Returns <c>true</c> when <see cref="Status"/> is <c>ACTIVE</c>.
    /// </summary>
    public bool IsActive => string.Equals(Status, StatusActive, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new active <see cref="Customer"/> instance.
    /// </summary>
    public static Customer Create(
        string customerCode,
        string customerName,
        bool hasActiveMaintenanceContract = false,
        Guid? id = null,
        DateTime? createdAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(customerCode))
        {
            throw new ArgumentException("CustomerCode cannot be null or whitespace.", nameof(customerCode));
        }

        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new ArgumentException("CustomerName cannot be null or whitespace.", nameof(customerName));
        }

        var customerId = id is { } guid && guid != Guid.Empty ? guid : Guid.NewGuid();

        return new Customer
        {
            Id = customerId,
            CustomerCode = customerCode.Trim(),
            CustomerName = customerName.Trim(),
            Status = StatusActive,
            HasActiveMaintenanceContract = hasActiveMaintenanceContract,
            CreatedAt = createdAtUtc ?? DateTime.UtcNow,
            UpdatedAt = null
        };
    }

    /// <summary>
    /// Updates master data attributes of the customer.
    /// </summary>
    public void UpdateMasterData(
        string customerCode,
        string customerName,
        bool hasActiveMaintenanceContract,
        DateTime? updatedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(customerCode))
        {
            throw new ArgumentException("CustomerCode cannot be null or whitespace.", nameof(customerCode));
        }

        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new ArgumentException("CustomerName cannot be null or whitespace.", nameof(customerName));
        }

        CustomerCode = customerCode.Trim();
        CustomerName = customerName.Trim();
        HasActiveMaintenanceContract = hasActiveMaintenanceContract;
        UpdatedAt = updatedAtUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Transitions the customer to <c>INACTIVE</c> while preserving historical references (Domain Rule 5, 6).
    /// </summary>
    public void Deactivate(DateTime? updatedAtUtc = null)
    {
        Status = StatusInactive;
        UpdatedAt = updatedAtUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Transitions the customer to <c>ACTIVE</c>.
    /// </summary>
    public void Activate(DateTime? updatedAtUtc = null)
    {
        Status = StatusActive;
        UpdatedAt = updatedAtUtc ?? DateTime.UtcNow;
    }
}
