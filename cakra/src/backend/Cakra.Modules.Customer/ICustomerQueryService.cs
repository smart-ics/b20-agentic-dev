namespace Cakra.Modules.Customer;

/// <summary>
/// Read-only data transfer object representing a <see cref="Domain.Customer"/> record (Architecture §7, §15).
/// </summary>
public record CustomerDto
{
    /// <summary>Unique identifier of the customer.</summary>
    public Guid Id { get; init; }

    /// <summary>Unique identifier of the customer (synonym for <see cref="Id"/>).</summary>
    public Guid CustomerId
    {
        get => Id;
        init => Id = value;
    }

    /// <summary>Unique business code of the customer organization.</summary>
    public string CustomerCode { get; init; } = string.Empty;

    /// <summary>Convenience alias for <see cref="CustomerCode"/>.</summary>
    public string Code
    {
        get => CustomerCode;
        init => CustomerCode = value;
    }

    /// <summary>Authoritative name of the customer organization.</summary>
    public string CustomerName { get; init; } = string.Empty;

    /// <summary>Convenience alias for <see cref="CustomerName"/>.</summary>
    public string Name
    {
        get => CustomerName;
        init => CustomerName = value;
    }

    /// <summary>Lifecycle status (<c>ACTIVE</c> or <c>INACTIVE</c>).</summary>
    public string Status { get; init; } = Domain.Customer.StatusActive;

    /// <summary>Indicates whether the customer currently holds an active maintenance contract (Domain Rule 9).</summary>
    public bool HasActiveMaintenanceContract { get; init; }

    /// <summary>Returns <c>true</c> when <see cref="Status"/> is <c>ACTIVE</c>.</summary>
    public bool IsActive => string.Equals(Status, Domain.Customer.StatusActive, StringComparison.OrdinalIgnoreCase);

    /// <summary>UTC timestamp when the customer record was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>UTC timestamp when the customer record was last updated.</summary>
    public DateTime? UpdatedAt { get; init; }

    internal static CustomerDto FromDomain(Domain.Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        return new CustomerDto
        {
            Id = customer.Id,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Status = customer.Status,
            HasActiveMaintenanceContract = customer.HasActiveMaintenanceContract,
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt
        };
    }
}

/// <summary>
/// Read-only projection representing a customer along with its maintenance contract status
/// (Architecture §7, §13, §15).
/// </summary>
public sealed record CustomerWithContractStatusDto : CustomerDto
{
    /// <summary>Human-readable maintenance contract status indicator (<c>ACTIVE</c> or <c>NONE</c>).</summary>
    public string ContractStatus => HasActiveMaintenanceContract ? "ACTIVE" : "NONE";
}

/// <summary>
/// Read-only data transfer object representing a <see cref="Domain.CustomerContact"/> record (Architecture §7).
/// </summary>
public sealed record CustomerContactDto
{
    /// <summary>Unique identifier of the customer contact.</summary>
    public Guid Id { get; init; }

    /// <summary>Unique identifier of the customer contact (synonym for <see cref="Id"/>).</summary>
    public Guid ContactId
    {
        get => Id;
        init => Id = value;
    }

    /// <summary>Identifier of the parent customer organization.</summary>
    public Guid CustomerId { get; init; }

    /// <summary>Full name of the contact person.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Job title or position of the contact person.</summary>
    public string? Position { get; init; }

    /// <summary>Contact telephone number.</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>Contact email address.</summary>
    public string? Email { get; init; }

    /// <summary>Lifecycle status (<c>ACTIVE</c> or <c>INACTIVE</c>).</summary>
    public string Status { get; init; } = Domain.CustomerContact.StatusActive;

    /// <summary>Returns <c>true</c> when <see cref="Status"/> is <c>ACTIVE</c>.</summary>
    public bool IsActive => string.Equals(Status, Domain.CustomerContact.StatusActive, StringComparison.OrdinalIgnoreCase);

    /// <summary>UTC timestamp when the contact record was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>UTC timestamp when the contact record was last updated.</summary>
    public DateTime? UpdatedAt { get; init; }

    internal static CustomerContactDto FromDomain(Domain.CustomerContact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);

        return new CustomerContactDto
        {
            Id = contact.Id,
            CustomerId = contact.CustomerId,
            Name = contact.Name,
            Position = contact.Position,
            PhoneNumber = contact.PhoneNumber,
            Email = contact.Email,
            Status = contact.Status,
            CreatedAt = contact.CreatedAt,
            UpdatedAt = contact.UpdatedAt
        };
    }
}

/// <summary>
/// Published cross-module query contract for Customer master data, contacts, and maintenance
/// contract status (Architecture §7, §15, §21). Internal repositories are not exposed outside
/// the Customer module boundary.
/// </summary>
public interface ICustomerQueryService
{
    /// <summary>
    /// Retrieves a customer record by its unique identifier, or <c>null</c> if not found.
    /// </summary>
    Task<CustomerDto?> GetCustomerByIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a customer record by its unique identifier, or <c>null</c> if not found.
    /// </summary>
    Task<CustomerDto?> GetCustomerById(Guid customerId, CancellationToken cancellationToken = default)
        => GetCustomerByIdAsync(customerId, cancellationToken);

    /// <summary>
    /// Retrieves a customer record by its unique business code, or <c>null</c> if not found.
    /// </summary>
    Task<CustomerDto?> GetCustomerByCodeAsync(string customerCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a customer record by its unique business code, or <c>null</c> if not found.
    /// </summary>
    Task<CustomerDto?> GetCustomerByCode(string customerCode, CancellationToken cancellationToken = default)
        => GetCustomerByCodeAsync(customerCode, cancellationToken);

    /// <summary>
    /// Retrieves all customers currently in <c>ACTIVE</c> status, ordered by name.
    /// </summary>
    Task<IReadOnlyList<CustomerDto>> ListActiveCustomersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all customers currently in <c>ACTIVE</c> status, ordered by name.
    /// </summary>
    Task<IReadOnlyList<CustomerDto>> ListActiveCustomers(CancellationToken cancellationToken = default)
        => ListActiveCustomersAsync(cancellationToken);

    /// <summary>
    /// Retrieves all customers (both active and inactive), ordered by name.
    /// </summary>
    Task<IReadOnlyList<CustomerDto>> ListAllCustomersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all customers (both active and inactive), ordered by name.
    /// </summary>
    Task<IReadOnlyList<CustomerDto>> ListAllCustomers(CancellationToken cancellationToken = default)
        => ListAllCustomersAsync(cancellationToken);

    /// <summary>
    /// Retrieves all contacts associated with the specified customer, ordered by name.
    /// </summary>
    Task<IReadOnlyList<CustomerContactDto>> GetCustomerContactsAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all contacts associated with the specified customer, ordered by name.
    /// </summary>
    Task<IReadOnlyList<CustomerContactDto>> GetCustomerContacts(Guid customerId, CancellationToken cancellationToken = default)
        => GetCustomerContactsAsync(customerId, cancellationToken);

    /// <summary>
    /// Retrieves a customer record along with its current maintenance contract status, or <c>null</c> if not found.
    /// </summary>
    Task<CustomerWithContractStatusDto?> GetCustomerWithContractStatusAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a customer record along with its current maintenance contract status, or <c>null</c> if not found.
    /// </summary>
    Task<CustomerWithContractStatusDto?> GetCustomerWithContractStatus(Guid customerId, CancellationToken cancellationToken = default)
        => GetCustomerWithContractStatusAsync(customerId, cancellationToken);

    /// <summary>
    /// Checks whether the specified customer exists and is currently in <c>ACTIVE</c> status.
    /// </summary>
    Task<bool> IsCustomerActiveAsync(Guid customerId, CancellationToken cancellationToken = default);
}
