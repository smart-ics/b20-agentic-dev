using Cakra.Core;
using Cakra.Modules.Organization;
using Cakra.Modules.Product;
using Cakra.Modules.Product.Domain;
using Cakra.Modules.Product.Domain.Events;
using Cakra.Modules.Product.Persistence;
using Cakra.Modules.Product.Services;
using FluentAssertions;
using Xunit;

namespace Cakra.Tests.Unit.Product;

/// <summary>
/// Unit tests for the Product module domain entity, domain events, validators, and application service (P3-S15).
/// </summary>
public class ProductDomainAndServiceTests
{
    [Fact]
    public void Product_Create_initializes_active_product_with_expected_attributes()
    {
        var now = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        var ownerId = Guid.NewGuid();

        var product = Cakra.Modules.Product.Domain.Product.Create(
            " MYHOSP ",
            " MyHospital ",
            " Hospital Information System ",
            ownerId,
            createdAtUtc: now);

        product.Id.Should().NotBeEmpty();
        product.ProductId.Should().Be(product.Id);
        product.Code.Should().Be("MYHOSP");
        product.ProductCode.Should().Be("MYHOSP");
        product.Name.Should().Be("MyHospital");
        product.ProductName.Should().Be("MyHospital");
        product.Description.Should().Be("Hospital Information System");
        product.OwnerPersonId.Should().Be(ownerId);
        product.Status.Should().Be("ACTIVE");
        product.IsActive.Should().BeTrue();
        product.CreatedAt.Should().Be(now);
        product.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Product_Create_rejects_empty_code_name_or_owner()
    {
        var ownerId = Guid.NewGuid();

        var emptyCodeAct = () => Cakra.Modules.Product.Domain.Product.Create(" ", "MyHospital", null, ownerId);
        emptyCodeAct.Should().Throw<ArgumentException>();

        var emptyNameAct = () => Cakra.Modules.Product.Domain.Product.Create("MYHOSP", " ", null, ownerId);
        emptyNameAct.Should().Throw<ArgumentException>();

        var emptyOwnerAct = () => Cakra.Modules.Product.Domain.Product.Create("MYHOSP", "MyHospital", null, Guid.Empty);
        emptyOwnerAct.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Product_Update_AssignOwner_Deactivate_and_Activate_transition_correctly()
    {
        var owner1 = Guid.NewGuid();
        var owner2 = Guid.NewGuid();
        var product = Cakra.Modules.Product.Domain.Product.Create("PENAEL", "PenaEl", "Initial Desc", owner1);

        var updateTime = new DateTime(2026, 9, 28, 13, 0, 0, DateTimeKind.Utc);
        product.Update(" PenaEl Enterprise ", " Updated Desc ", updateTime);
        product.Name.Should().Be("PenaEl Enterprise");
        product.Description.Should().Be("Updated Desc");
        product.UpdatedAt.Should().Be(updateTime);

        var ownerTime = updateTime.AddMinutes(30);
        product.AssignOwner(owner2, ownerTime);
        product.OwnerPersonId.Should().Be(owner2);
        product.UpdatedAt.Should().Be(ownerTime);

        var deactivateTime = ownerTime.AddMinutes(30);
        product.Deactivate(deactivateTime);
        product.Status.Should().Be("INACTIVE");
        product.IsActive.Should().BeFalse();
        product.UpdatedAt.Should().Be(deactivateTime);

        var activateTime = deactivateTime.AddMinutes(30);
        product.Activate(activateTime);
        product.Status.Should().Be("ACTIVE");
        product.IsActive.Should().BeTrue();
        product.UpdatedAt.Should().Be(activateTime);
    }

    [Fact]
    public void Internal_repositories_are_not_publicly_exposed_while_IProductQueryService_is_published()
    {
        typeof(IProductQueryService).IsPublic.Should().BeTrue();
        typeof(ProductDto).IsPublic.Should().BeTrue();
        typeof(IProductService).IsPublic.Should().BeTrue();
        typeof(ProductService).IsPublic.Should().BeTrue();
        typeof(ProductQueryService).IsPublic.Should().BeTrue();

        typeof(IProductRepository).IsPublic.Should().BeFalse();
        typeof(ProductRepository).IsPublic.Should().BeFalse();
    }

    [Fact]
    public async Task ProductService_handles_Create_Update_AssignOwner_Deactivate_and_Activate_and_emits_domain_events()
    {
        var owner1 = Guid.NewGuid();
        var owner2 = Guid.NewGuid();
        var inactivePerson = Guid.NewGuid();

        var repo = new InMemoryProductRepository();
        var orgQuery = new FakeOrganizationQueryService();
        orgQuery.AddPerson(owner1, isActive: true);
        orgQuery.AddPerson(owner2, isActive: true);
        orgQuery.AddPerson(inactivePerson, isActive: false);

        var dispatcher = new RecordingDomainEventDispatcher();
        var service = new ProductService(repo, orgQuery, dispatcher);

        // 1. CreateProduct validates owner, initializes status to ACTIVE, emits ProductCreated
        var created = await service.Handle(
            new CreateProductCommand("BTRADE3", "BTrade3", "Trading Platform", owner1),
            CancellationToken.None);

        created.Id.Should().NotBeEmpty();
        created.Code.Should().Be("BTRADE3");
        created.Name.Should().Be("BTrade3");
        created.Description.Should().Be("Trading Platform");
        created.OwnerPersonId.Should().Be(owner1);
        created.Status.Should().Be("ACTIVE");
        created.IsActive.Should().BeTrue();

        dispatcher.Events.Should().ContainSingle(e =>
            e is ProductCreated &&
            ((ProductCreated)e).ProductId == created.Id &&
            ((ProductCreated)e).Code == "BTRADE3" &&
            ((ProductCreated)e).OwnerPersonId == owner1);

        // Duplicate code should be rejected
        var duplicateAct = async () => await service.Handle(
            new CreateProductCommand("BTRADE3", "Duplicate Product", null, owner1),
            CancellationToken.None);
        await duplicateAct.Should().ThrowAsync<InvalidOperationException>();

        // Nonexistent owner should be rejected
        var unknownOwnerAct = async () => await service.Handle(
            new CreateProductCommand("JETSET", "Jetset", null, Guid.NewGuid()),
            CancellationToken.None);
        await unknownOwnerAct.Should().ThrowAsync<KeyNotFoundException>();

        // Inactive owner should be rejected
        var inactiveOwnerAct = async () => await service.Handle(
            new CreateProductCommand("JETSET", "Jetset", null, inactivePerson),
            CancellationToken.None);
        await inactiveOwnerAct.Should().ThrowAsync<InvalidOperationException>();

        // 2. UpdateProduct updates descriptive attributes
        var updated = await service.Handle(
            new UpdateProductCommand(created.Id, "BTrade3 Institutional", "Institutional Trading Suite"),
            CancellationToken.None);

        updated.Name.Should().Be("BTrade3 Institutional");
        updated.Description.Should().Be("Institutional Trading Suite");
        updated.Code.Should().Be("BTRADE3");

        // 3. AssignProductOwner validates new owner, updates owner, emits ProductOwnerChanged
        var reassigned = await service.Handle(
            new AssignProductOwnerCommand(created.Id, owner2),
            CancellationToken.None);

        reassigned.OwnerPersonId.Should().Be(owner2);
        dispatcher.Events.Should().ContainSingle(e =>
            e is ProductOwnerChanged &&
            ((ProductOwnerChanged)e).ProductId == created.Id &&
            ((ProductOwnerChanged)e).PreviousOwnerPersonId == owner1 &&
            ((ProductOwnerChanged)e).NewOwnerPersonId == owner2);

        // AssignProductOwner with nonexistent owner should fail
        var assignUnknownOwnerAct = async () => await service.Handle(
            new AssignProductOwnerCommand(created.Id, Guid.NewGuid()),
            CancellationToken.None);
        await assignUnknownOwnerAct.Should().ThrowAsync<KeyNotFoundException>();

        // 4. DeactivateProduct transitions to INACTIVE and emits ProductDeactivated
        var deactivated = await service.Handle(
            new DeactivateProductCommand(created.Id),
            CancellationToken.None);

        deactivated.Status.Should().Be("INACTIVE");
        deactivated.IsActive.Should().BeFalse();
        dispatcher.Events.Should().ContainSingle(e =>
            e is ProductDeactivated &&
            ((ProductDeactivated)e).ProductId == created.Id);

        // 5. ActivateProduct transitions back to ACTIVE and emits ProductActivated
        var activated = await service.Handle(
            new ActivateProductCommand(created.Id),
            CancellationToken.None);

        activated.Status.Should().Be("ACTIVE");
        activated.IsActive.Should().BeTrue();
        dispatcher.Events.Should().ContainSingle(e =>
            e is ProductActivated &&
            ((ProductActivated)e).ProductId == created.Id);
    }

    [Fact]
    public void Command_validators_enforce_required_fields()
    {
        var createValidator = new CreateProductCommandValidator();
        createValidator.Validate(new CreateProductCommand("", "", null, Guid.Empty)).IsValid.Should().BeFalse();
        createValidator.Validate(new CreateProductCommand("MYHOSP", "MyHospital", "HIS", Guid.NewGuid())).IsValid.Should().BeTrue();

        var updateValidator = new UpdateProductCommandValidator();
        updateValidator.Validate(new UpdateProductCommand(Guid.Empty, "", null)).IsValid.Should().BeFalse();
        updateValidator.Validate(new UpdateProductCommand(Guid.NewGuid(), "MyHospital", "HIS")).IsValid.Should().BeTrue();

        var assignOwnerValidator = new AssignProductOwnerCommandValidator();
        assignOwnerValidator.Validate(new AssignProductOwnerCommand(Guid.Empty, Guid.Empty)).IsValid.Should().BeFalse();
        assignOwnerValidator.Validate(new AssignProductOwnerCommand(Guid.NewGuid(), Guid.NewGuid())).IsValid.Should().BeTrue();

        var activateValidator = new ActivateProductCommandValidator();
        activateValidator.Validate(new ActivateProductCommand(Guid.Empty)).IsValid.Should().BeFalse();
        activateValidator.Validate(new ActivateProductCommand(Guid.NewGuid())).IsValid.Should().BeTrue();

        var deactivateValidator = new DeactivateProductCommandValidator();
        deactivateValidator.Validate(new DeactivateProductCommand(Guid.Empty)).IsValid.Should().BeFalse();
        deactivateValidator.Validate(new DeactivateProductCommand(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    private sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> Events { get; } = new();

        public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(domainEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOrganizationQueryService : IOrganizationQueryService
    {
        private readonly Dictionary<Guid, PersonDto> _persons = new();

        public void AddPerson(Guid id, bool isActive = true)
        {
            _persons[id] = new PersonDto
            {
                Id = id,
                FirstName = "Test",
                LastName = "Owner",
                Email = $"owner.{id:N}@cakra.id",
                Status = isActive ? "ACTIVE" : "INACTIVE",
                CreatedAt = DateTime.UtcNow
            };
        }

        public Task<bool> IsPersonActiveAsync(Guid personId, CancellationToken cancellationToken = default)
            => Task.FromResult(_persons.TryGetValue(personId, out var p) && p.IsActive);

        public Task<PersonDto?> GetPersonByIdAsync(Guid personId, CancellationToken cancellationToken = default)
            => Task.FromResult(_persons.TryGetValue(personId, out var p) ? p : null);

        public Task<IReadOnlyList<string>> GetPersonRolesAsync(Guid personId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class InMemoryProductRepository : IProductRepository
    {
        private readonly Dictionary<Guid, Cakra.Modules.Product.Domain.Product> _store = new();

        public Task<Cakra.Modules.Product.Domain.Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.TryGetValue(id, out var p) ? p : null);

        public Task<Cakra.Modules.Product.Domain.Product?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Values.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<Cakra.Modules.Product.Domain.Product>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Cakra.Modules.Product.Domain.Product>>(_store.Values.ToList());

        public Task<IReadOnlyList<Cakra.Modules.Product.Domain.Product>> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Cakra.Modules.Product.Domain.Product>>(_store.Values.Where(p => p.IsActive).ToList());

        public Task<IReadOnlyList<Cakra.Modules.Product.Domain.Product>> GetByOwnerPersonIdAsync(Guid ownerPersonId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Cakra.Modules.Product.Domain.Product>>(_store.Values.Where(p => p.OwnerPersonId == ownerPersonId).ToList());

        public Task AddAsync(Cakra.Modules.Product.Domain.Product entity, CancellationToken cancellationToken = default)
        {
            _store[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Cakra.Modules.Product.Domain.Product entity, CancellationToken cancellationToken = default)
        {
            _store[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
        {
            if (_store.TryGetValue(id, out var p))
            {
                p.Status = status;
            }
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _store.Remove(id);
            return Task.CompletedTask;
        }
    }
}
