namespace ICS.Modules.Customer.Domain;

using ICS.Core.Domain;
using ICS.Modules.Customer.Domain.Events;

/// <summary>
/// Authoritative domain entity representing a Customer organization served by ICS.
/// Architecture §6, §7, §16, §17; customer-domain.md.
/// </summary>
public class Customer : Entity
{
    public const string StatusActive = "ACTIVE";
    public const string StatusInactive = "INACTIVE";

    public string CustomerCode { get; private set; } = string.Empty;
    public string CustomerName { get; private set; } = string.Empty;
    public string Status { get; private set; } = StatusActive;
    public bool HasActiveMaintenanceContract { get; private set; }

    /// <summary>
    /// Parameterless constructor for Dapper mapping.
    /// </summary>
    protected Customer() { }

    public Customer(
        Guid id,
        string customerCode,
        string customerName,
        string status,
        bool hasActiveMaintenanceContract,
        DateTime createdAt,
        DateTime? updatedAt = null)
        : base(id)
    {
        Id = id;
        CustomerCode = customerCode;
        CustomerName = customerName;
        Status = status;
        HasActiveMaintenanceContract = hasActiveMaintenanceContract;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Factory method creating an active Customer and emitting <see cref="CustomerCreated"/>.
    /// </summary>
    public static Customer Create(
        Guid id,
        string customerCode,
        string customerName,
        bool hasActiveMaintenanceContract,
        DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(customerCode))
            throw new ArgumentException("Customer code cannot be empty.", nameof(customerCode));
        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("Customer name cannot be empty.", nameof(customerName));

        var customer = new Customer(
            id,
            customerCode.Trim().ToUpperInvariant(),
            customerName.Trim(),
            StatusActive,
            hasActiveMaintenanceContract,
            createdAt);

        customer.AddDomainEvent(new CustomerCreated(
            customer.Id,
            customer.CustomerCode,
            customer.CustomerName,
            customer.HasActiveMaintenanceContract));

        return customer;
    }

    /// <summary>
    /// Updates customer master data and emits <see cref="CustomerMasterDataUpdated"/>.
    /// </summary>
    public void Update(
        string customerCode,
        string customerName,
        bool hasActiveMaintenanceContract,
        DateTime updatedAt)
    {
        if (string.IsNullOrWhiteSpace(customerCode))
            throw new ArgumentException("Customer code cannot be empty.", nameof(customerCode));
        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("Customer name cannot be empty.", nameof(customerName));

        CustomerCode = customerCode.Trim().ToUpperInvariant();
        CustomerName = customerName.Trim();
        HasActiveMaintenanceContract = hasActiveMaintenanceContract;
        UpdatedAt = updatedAt;

        AddDomainEvent(new CustomerMasterDataUpdated(
            Id,
            CustomerCode,
            CustomerName,
            HasActiveMaintenanceContract));
    }

    /// <summary>
    /// Deactivates customer and emits <see cref="CustomerDeactivated"/>.
    /// Preserves all historical references per business rule 5 and 6.
    /// </summary>
    public void Deactivate(DateTime updatedAt)
    {
        if (Status == StatusInactive)
        {
            return;
        }

        Status = StatusInactive;
        UpdatedAt = updatedAt;

        AddDomainEvent(new CustomerDeactivated(Id, CustomerCode, CustomerName));
    }

    /// <summary>
    /// Re-activates customer and emits <see cref="CustomerActivated"/>.
    /// </summary>
    public void Activate(DateTime updatedAt)
    {
        if (Status == StatusActive)
        {
            return;
        }

        Status = StatusActive;
        UpdatedAt = updatedAt;

        AddDomainEvent(new CustomerActivated(Id, CustomerCode, CustomerName));
    }

    /// <summary>
    /// Updates the maintenance contract status.
    /// </summary>
    public void UpdateMaintenanceContractStatus(bool hasActiveMaintenanceContract, DateTime updatedAt)
    {
        HasActiveMaintenanceContract = hasActiveMaintenanceContract;
        UpdatedAt = updatedAt;
    }

    public bool IsActive => string.Equals(Status, StatusActive, StringComparison.OrdinalIgnoreCase);
}
