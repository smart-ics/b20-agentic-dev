using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Organization.Domain;
using Cakra.Modules.Organization.Models;
using Cakra.Modules.Organization.Persistence;
using Cakra.Modules.Organization.Services;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Organization;

public class OrganizationQueryServiceTests
{
    [Fact]
    public async Task ListAllRolesAsync_delegates_to_RoleRepository_and_returns_RoleDtos()
    {
        // Arrange
        var roles = new List<Role>
        {
            new() { Id = Guid.NewGuid(), Name = "Administrator", Description = "System Admin" },
            new() { Id = Guid.NewGuid(), Name = "Management", Description = "Management oversight" },
            new() { Id = Guid.NewGuid(), Name = "Operational User", Description = "Operational user" },
            new() { Id = Guid.NewGuid(), Name = "Programmer", Description = "Software development" },
            new() { Id = Guid.NewGuid(), Name = "Implementator", Description = "Field deployment" }
        };

        var fakeRepo = new FakeRoleRepository(roles);
        var queryService = new OrganizationQueryService(new FakeDbConnectionFactory(), fakeRepo);

        // Act
        var result = await queryService.ListAllRolesAsync();

        // Assert
        result.Should().HaveCount(5);
        result.Select(r => r.Name).Should().Contain(new[]
        {
            "Administrator",
            "Management",
            "Operational User",
            "Programmer",
            "Implementator"
        });
        fakeRepo.GetAllCallCount.Should().Be(1);
    }

    [Fact]
    public void PersonDtoRow_ToDto_with_null_or_empty_roles_produces_empty_array()
    {
        // Arrange
        var rowNull = new PersonDtoRow { Id = Guid.NewGuid(), FirstName = "John", LastName = "Doe", Email = "j@d.com", RolesRaw = null };
        var rowEmpty = new PersonDtoRow { Id = Guid.NewGuid(), FirstName = "Jane", LastName = "Doe", Email = "jane@d.com", RolesRaw = "   " };

        // Act & Assert
        rowNull.ToDto().Roles.Should().BeEmpty();
        rowEmpty.ToDto().Roles.Should().BeEmpty();
    }

    [Fact]
    public void PersonDtoRow_ToDto_with_aggregated_roles_parses_trims_and_sorts()
    {
        // Arrange
        var row = new PersonDtoRow
        {
            Id = Guid.NewGuid(),
            FirstName = "Budi",
            LastName = "Santoso",
            Email = "budi@cakra.id",
            RolesRaw = "Operational User, Administrator , Management "
        };

        // Act
        var dto = row.ToDto();

        // Assert
        dto.Roles.Should().Equal("Administrator", "Management", "Operational User");
    }

    [Fact]
    public void PersonDtoRow_ToDto_deduplicates_case_insensitively()
    {
        // Arrange
        var row = new PersonDtoRow
        {
            Id = Guid.NewGuid(),
            FirstName = "Siti",
            LastName = "Rahma",
            Email = "siti@cakra.id",
            RolesRaw = "Administrator,administrator, Management"
        };

        // Act
        var dto = row.ToDto();

        // Assert
        dto.Roles.Should().HaveCount(2);
        dto.Roles.Should().Contain("Administrator");
        dto.Roles.Should().Contain("Management");
    }

    private sealed class FakeRoleRepository : IRoleRepository
    {
        private readonly IReadOnlyList<Role> _roles;
        public int GetAllCallCount { get; private set; }

        public FakeRoleRepository(IReadOnlyList<Role> roles)
        {
            _roles = roles;
        }

        public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_roles.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            GetAllCallCount++;
            return Task.FromResult(_roles);
        }

        public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_roles.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(Role entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(Role entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDbConnectionFactory : IDbConnectionFactory
    {
        public System.Data.IDbConnection CreateConnection() => throw new NotImplementedException();
    }
}
