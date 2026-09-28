namespace Cakra.Modules.Organization.Domain;

/// <summary>
/// Represents membership of a <see cref="Person"/> within a <see cref="Team"/> (Architecture §6, §16).
/// Preserves team participation history.
/// </summary>
public sealed class TeamMembership
{
    public Guid PersonId { get; set; }
    public Guid TeamId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
