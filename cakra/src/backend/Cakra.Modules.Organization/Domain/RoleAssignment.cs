namespace Cakra.Modules.Organization.Domain;

/// <summary>
/// Represents assignment of an organizational <see cref="Role"/> to a <see cref="Person"/> (Architecture §6, §14, §16).
/// Supports historical preservation via <see cref="RevokedAt"/>.
/// </summary>
public sealed class RoleAssignment
{
    public Guid PersonId { get; set; }
    public Guid RoleId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    public bool IsActive => RevokedAt == null;

    public void Revoke(DateTime? revokedAt = null)
    {
        RevokedAt = revokedAt ?? DateTime.UtcNow;
    }
}
