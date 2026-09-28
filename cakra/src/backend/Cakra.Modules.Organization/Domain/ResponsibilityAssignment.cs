namespace Cakra.Modules.Organization.Domain;

/// <summary>
/// Represents assignment of organizational <see cref="Responsibility"/> to a <see cref="Person"/> (Architecture §6, §16).
/// Defines current accountability.
/// </summary>
public sealed class ResponsibilityAssignment
{
    public Guid PersonId { get; set; }
    public Guid ResponsibilityId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
