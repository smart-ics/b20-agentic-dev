using Cakra.Core;

namespace Cakra.Modules.Organization.Domain;

/// <summary>
/// Defined area of organizational accountability (Architecture §6, §14, §16).
/// Defines accountability, not workflow or tasks.
/// </summary>
public sealed class Responsibility : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
