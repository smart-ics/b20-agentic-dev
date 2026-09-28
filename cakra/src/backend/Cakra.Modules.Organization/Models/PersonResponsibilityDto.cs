namespace Cakra.Modules.Organization;

/// <summary>
/// Read model representing an organizational responsibility assigned to a person
/// returned by <see cref="IOrganizationQueryService.GetPersonResponsibilitiesAsync"/> (Architecture §7, §20).
/// </summary>
public sealed record PersonResponsibilityDto
{
    public Guid ResponsibilityId { get; init; }
    public Guid Id => ResponsibilityId;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime AssignedAt { get; init; }

    public static implicit operator string(PersonResponsibilityDto dto) => dto.Name;

    public override string ToString() => Name;
}
