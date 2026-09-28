namespace ICS.Modules.Customer.Application.DTOs;

/// <summary>
/// Data transfer object representing a Customer.
/// </summary>
public sealed record CustomerDto(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    string Status,
    bool HasActiveMaintenanceContract,
    DateTime CreatedAt,
    DateTime? UpdatedAt = null);

/// <summary>
/// Data transfer object representing a Customer Contact.
/// </summary>
public sealed record CustomerContactDto(
    Guid ContactId,
    Guid CustomerId,
    string Name,
    string? Position,
    string? PhoneNumber,
    string? Email,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt = null);

/// <summary>
/// Data transfer object representing a Customer with their authoritative maintenance contract status.
/// Architecture §7, §15; customer-domain.md.
/// </summary>
public sealed record CustomerWithContractStatusDto(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    string Status,
    bool HasActiveMaintenanceContract);
