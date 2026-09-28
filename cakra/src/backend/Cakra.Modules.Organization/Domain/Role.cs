using Cakra.Core;

namespace Cakra.Modules.Organization.Domain;

/// <summary>
/// Recognized organizational position (e.g. Management, Programmer, Implementor, Administrator).
/// Architecture §6, §14, §16.
/// </summary>
public sealed class Role : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
