namespace Cakra.Modules.Organization;

/// <summary>
/// Read model representing a person record returned by <see cref="IOrganizationQueryService"/>
/// (Architecture §7, §15, §20).
/// </summary>
public sealed record PersonDto
{
    public Guid Id { get; init; }
    public Guid PersonId => Id;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Status { get; init; } = Domain.Person.StatusActive;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public string FullName => string.IsNullOrWhiteSpace(LastName)
        ? FirstName
        : $"{FirstName} {LastName}".Trim();

    public bool IsActive => string.Equals(Status, Domain.Person.StatusActive, StringComparison.OrdinalIgnoreCase);
}
