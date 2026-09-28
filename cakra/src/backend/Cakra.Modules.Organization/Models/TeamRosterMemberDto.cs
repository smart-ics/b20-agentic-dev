namespace Cakra.Modules.Organization;

/// <summary>
/// Read model representing a team roster entry returned by <see cref="IOrganizationQueryService.GetTeamRosterAsync"/>
/// (Architecture §7, §15, §20).
/// </summary>
public sealed record TeamRosterMemberDto
{
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public Guid PersonId { get; init; }
    public Guid Id => PersonId;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Status { get; init; } = Domain.Person.StatusActive;
    public DateTime AssignedAt { get; init; }

    public string FullName => string.IsNullOrWhiteSpace(LastName)
        ? FirstName
        : $"{FirstName} {LastName}".Trim();

    public bool IsActive => string.Equals(Status, Domain.Person.StatusActive, StringComparison.OrdinalIgnoreCase);
}
