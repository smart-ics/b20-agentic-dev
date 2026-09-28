using Cakra.Modules.Organization.Domain;

namespace Cakra.Modules.Organization.Persistence;

/// <summary>
/// Persistence contract for <see cref="ResponsibilityAssignment"/> junction records (Architecture §6, §16, §19.3).
/// </summary>
internal interface IResponsibilityAssignmentRepository
{
    /// <summary>Assigns a responsibility to a person.</summary>
    Task AssignAsync(ResponsibilityAssignment assignment, CancellationToken cancellationToken = default);

    /// <summary>Removes a responsibility assignment from a person.</summary>
    Task RemoveAsync(Guid personId, Guid responsibilityId, CancellationToken cancellationToken = default);

    /// <summary>Retrieves all responsibility assignments for a person.</summary>
    Task<IReadOnlyList<ResponsibilityAssignment>> GetByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>Retrieves all person assignments for a responsibility.</summary>
    Task<IReadOnlyList<ResponsibilityAssignment>> GetByResponsibilityIdAsync(Guid responsibilityId, CancellationToken cancellationToken = default);

    /// <summary>Checks whether a responsibility is currently assigned to a person.</summary>
    Task<bool> ExistsAsync(Guid personId, Guid responsibilityId, CancellationToken cancellationToken = default);
}
