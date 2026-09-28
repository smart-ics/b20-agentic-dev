namespace ICS.Modules.Organization.Persistence;

using ICS.Modules.Organization.Application.DTOs;
using ICS.Modules.Organization.Domain;

/// <summary>
/// Internal repository contract for Responsibility and ResponsibilityAssignment persistence.
/// </summary>
internal interface IResponsibilityRepository
{
    Task<Responsibility?> GetByIdAsync(Guid responsibilityId, CancellationToken cancellationToken = default);
    Task<Responsibility?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task AddAsync(Responsibility responsibility, CancellationToken cancellationToken = default);
    Task UpdateAsync(Responsibility responsibility, CancellationToken cancellationToken = default);
    Task AddAssignmentAsync(ResponsibilityAssignment assignment, CancellationToken cancellationToken = default);
    Task<ResponsibilityAssignment?> GetActiveAssignmentAsync(Guid personId, Guid responsibilityId, CancellationToken cancellationToken = default);
    Task RevokeAssignmentAsync(Guid assignmentId, DateTime revokedAt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetResponsibilitiesByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResponsibilityAssignmentDto>> GetAssignmentsByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default);
}
