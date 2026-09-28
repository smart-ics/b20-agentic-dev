namespace ICS.Modules.Product.Application.DTOs;

/// <summary>
/// Data transfer object representing a Product.
/// Architecture §10, §16.
/// </summary>
public sealed record ProductDto(
    Guid ProductId,
    string Code,
    string Name,
    string? Description,
    Guid OwnerPersonId,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt = null)
{
    public string? OwnerName { get; init; }
}
