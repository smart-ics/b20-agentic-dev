using Cakra.Modules.Identity.Services;
using Cakra.Modules.Organization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cakra.Tests.Unit.Identity;

public class AuthorizationServiceTests
{
    [Fact]
    public async Task ResolveRolesAsync_returns_roles_from_OrganizationQueryService()
    {
        var personId = Guid.NewGuid();
        var fakeOrgService = new FakeOrgService(personId, new[] { "Management", "Programmer" });
        var service = new AuthorizationService(fakeOrgService, NullLogger<AuthorizationService>.Instance);

        var roles = await service.ResolveRolesAsync(personId);

        roles.Should().BeEquivalentTo(new[] { "Management", "Programmer" });
    }

    [Fact]
    public void ResolveRoles_synchronous_delegates_to_OrganizationQueryService_GetPersonRoles()
    {
        var personId = Guid.NewGuid();
        var fakeOrgService = new FakeOrgService(personId, new[] { "Management", "Implementator" });
        var service = new AuthorizationService(fakeOrgService, NullLogger<AuthorizationService>.Instance);

        var roles = service.ResolveRoles(personId);

        roles.Should().BeEquivalentTo(new[] { "Management", "Implementator" });
    }

    [Fact]
    public async Task ResolveRolesAsync_returns_empty_when_OrganizationQueryService_is_null()
    {
        var personId = Guid.NewGuid();
        var service = new AuthorizationService(null, NullLogger<AuthorizationService>.Instance);

        var roles = await service.ResolveRolesAsync(personId);

        roles.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveRolesAsync_returns_empty_when_OrganizationQueryService_throws()
    {
        var personId = Guid.NewGuid();
        var throwingOrgService = new ThrowingOrgService();
        var service = new AuthorizationService(throwingOrgService, NullLogger<AuthorizationService>.Instance);

        var roles = await service.ResolveRolesAsync(personId);

        roles.Should().BeEmpty();
    }

    private sealed class FakeOrgService : IOrganizationQueryService
    {
        private readonly Guid _targetPersonId;
        private readonly IReadOnlyList<string> _roles;

        public FakeOrgService(Guid targetPersonId, IReadOnlyList<string> roles)
        {
            _targetPersonId = targetPersonId;
            _roles = roles;
        }

        public Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(personId == _targetPersonId);

        public Task<IReadOnlyList<string>> GetPersonRolesAsync(Guid personId, CancellationToken cancellationToken = default) =>
            Task.FromResult(personId == _targetPersonId ? _roles : (IReadOnlyList<string>)Array.Empty<string>());
    }

    private sealed class ThrowingOrgService : IOrganizationQueryService
    {
        public Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated query failure");

        public Task<IReadOnlyList<string>> GetPersonRolesAsync(Guid personId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated role query failure");
    }
}
