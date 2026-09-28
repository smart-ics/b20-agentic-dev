namespace Cakra.Core;

/// <summary>
/// Base type for all persistent domain entities. Provides the identity and
/// audit timestamps shared across every module aggregate.
/// </summary>
public abstract class EntityBase
{
    /// <summary>Unique identifier of the entity (UUID primary key).</summary>
    public Guid Id { get; set; }

    /// <summary>UTC timestamp when the entity was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp when the entity was last updated, or <c>null</c> if never updated.</summary>
    public DateTime? UpdatedAt { get; set; }
}
