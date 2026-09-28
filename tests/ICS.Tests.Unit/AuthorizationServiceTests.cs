namespace ICS.Tests.Unit;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using ICS.Modules.Identity.Application;
using ICS.Modules.Organization;
using Xunit;

/// <summary>
/// Unit tests for <see cref="AuthorizationService"/> per Architecture §14.
/// </summary>
public class AuthorizationServiceTests
{
    private class FakeOrganizationQueryService : IOrganizationQueryService
    {
        public Dictionary<Guid, List<string>> RolesByPerson { get; } = new();

        public Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<IReadOnlyList<string>> GetRolesByPersonIdAsync(Guid personId, CancellationToken cancellationToken = default) =>
            GetPersonRolesAsync(personId, cancellationToken);

        public Task<IReadOnlyList<string>> GetPersonRolesAsync(Guid personId, CancellationToken cancellationToken = default)
        {
            if (RolesByPerson.TryGetValue(personId, out var roles))
            {
                return Task.FromResult<IReadOnlyList<string>>(roles.AsReadOnly());
            }

            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        public Task<ICS.Modules.Organization.Application.DTOs.PersonDto?> GetPersonByIdAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ICS.Modules.Organization.Application.DTOs.PersonDto?>(null);

        public Task<IReadOnlyList<ICS.Modules.Organization.Application.DTOs.PersonDto>> ListActivePersonsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ICS.Modules.Organization.Application.DTOs.PersonDto>>(Array.Empty<ICS.Modules.Organization.Application.DTOs.PersonDto>());

        public Task<IReadOnlyList<ICS.Modules.Organization.Application.DTOs.TeamMemberDto>> GetTeamRosterAsync(Guid teamId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ICS.Modules.Organization.Application.DTOs.TeamMemberDto>>(Array.Empty<ICS.Modules.Organization.Application.DTOs.TeamMemberDto>());

        public Task<IReadOnlyList<string>> GetPersonResponsibilitiesAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public void RegisterBootstrapRoles(Guid personId, IEnumerable<string> roles)
        {
            if (!RolesByPerson.TryGetValue(personId, out var list))
            {
                list = new List<string>();
                RolesByPerson[personId] = list;
            }
            list.AddRange(roles);
        }
    }

    [Fact]
    public async Task GetRolesForPersonAsync_ReturnsRolesFromOrganizationService()
    {
        // Arrange
        var personId = Guid.NewGuid();
        var fakeOrgService = new FakeOrganizationQueryService();
        fakeOrgService.RolesByPerson[personId] = new List<string> { "Management", "TeamLead" };

        var sut = new AuthorizationService(fakeOrgService);

        // Act
        var result = await sut.GetRolesForPersonAsync(personId);

        // Assert
        result.Should().BeEquivalentTo(new[] { "Management", "TeamLead" });
    }

    [Theory]
    [InlineData("Management", true)]
    [InlineData("management", true)] // Case-insensitive
    [InlineData("Programmer", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public async Task HasRoleAsync_EvaluatesRolePresenceCaseInsensitively(string? roleToCheck, bool expected)
    {
        // Arrange
        var personId = Guid.NewGuid();
        var fakeOrgService = new FakeOrganizationQueryService();
        fakeOrgService.RolesByPerson[personId] = new List<string> { "Management", "Module PIC" };

        var sut = new AuthorizationService(fakeOrgService);

        // Act
        var result = await sut.HasRoleAsync(personId, roleToCheck!);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task HasAnyRoleAsync_ReturnsTrueWhenAtLeastOneRoleMatches()
    {
        // Arrange
        var personId = Guid.NewGuid();
        var fakeOrgService = new FakeOrganizationQueryService();
        fakeOrgService.RolesByPerson[personId] = new List<string> { "Programmer", "Implementator" };

        var sut = new AuthorizationService(fakeOrgService);

        // Act & Assert
        (await sut.HasAnyRoleAsync(personId, new[] { "Management", "Programmer" })).Should().BeTrue();
        (await sut.HasAnyRoleAsync(personId, new[] { "Admin", "Executive" })).Should().BeFalse();
        (await sut.HasAnyRoleAsync(personId, Array.Empty<string>())).Should().BeFalse();
    }
}
