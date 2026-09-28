using Cakra.Core;

namespace Cakra.Modules.Organization.Domain;

/// <summary>
/// Stable organizational group with a defined purpose (Architecture §6, §16).
/// Does not represent temporary operational groupings, which belong to Work Package.
/// </summary>
public sealed class Team : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
