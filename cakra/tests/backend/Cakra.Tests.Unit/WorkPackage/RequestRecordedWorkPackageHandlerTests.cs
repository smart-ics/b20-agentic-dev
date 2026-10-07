using Cakra.Modules.Request.Domain.Events;
using Cakra.Modules.WorkPackage;
using Cakra.Modules.WorkPackage.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cakra.Tests.Unit.WorkPackage;

public sealed class RequestRecordedWorkPackageHandlerTests
{
    private sealed class FakeWorkPackageService : IWorkPackageService
    {
        public List<(Guid WorkPackageId, Guid RequestId)> AddedRequests { get; } = new();

        public Task<WorkPackageDto> AddRequestToWorkPackageAsync(
            Guid workPackageId,
            Guid requestId,
            CancellationToken cancellationToken = default)
        {
            AddedRequests.Add((workPackageId, requestId));
            return Task.FromResult(new WorkPackageDto
            {
                Id = workPackageId,
                Name = "Test",
                Objective = "Test",
                Status = "DRAFT",
                OwnerPersonId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            });
        }

        public Task<WorkPackageDto> CreateWorkPackageAsync(string name, string objective, Guid ownerPersonId, Guid? customerId = null, Guid? productId = null, DateTime? deadline = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WorkPackageDto> UpdateObjectiveAsync(Guid workPackageId, string name, string objective, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WorkPackageDto> AssignOwnerAsync(Guid workPackageId, Guid newOwnerPersonId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WorkPackageDto> RemoveRequestFromWorkPackageAsync(Guid workPackageId, Guid requestId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WorkPackageDto> ActivateWorkPackageAsync(Guid workPackageId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WorkPackageDto> CloseWorkPackageAsync(Guid workPackageId, string closedReason, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WorkPackageDto> UpdateContextAsync(Guid workPackageId, Guid? customerId, Guid? productId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WorkPackageDto> UpdateDeadlineAsync(Guid workPackageId, DateTime? deadline, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WorkPackageDto> ReorderRequestsAsync(Guid workPackageId, IReadOnlyList<Guid> orderedRequestIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    [Fact]
    public void Constructor_WithNullService_ThrowsArgumentNullException()
    {
        var act = () => new RequestRecordedWorkPackageHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("workPackageService");
    }

    [Fact]
    public async Task Handle_WithNullNotification_ThrowsArgumentNullException()
    {
        var service = new FakeWorkPackageService();
        var handler = new RequestRecordedWorkPackageHandler(service);

        var act = () => handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("notification");
    }

    [Fact]
    public async Task Handle_WhenWorkPackageIdIsNull_DoesNotCallService()
    {
        var service = new FakeWorkPackageService();
        var handler = new RequestRecordedWorkPackageHandler(service);
        var notification = new RequestRecorded(
            requestId: Guid.NewGuid(),
            title: "Standalone Request",
            description: "",
            requestType: "GENERAL",
            actorPersonId: Guid.NewGuid(),
            workPackageId: null);

        await handler.Handle(notification, CancellationToken.None);

        service.AddedRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenWorkPackageIdIsEmptyGuid_DoesNotCallService()
    {
        var service = new FakeWorkPackageService();
        var handler = new RequestRecordedWorkPackageHandler(service);
        var notification = new RequestRecorded(
            requestId: Guid.NewGuid(),
            title: "Empty WP Request",
            description: "",
            requestType: "GENERAL",
            actorPersonId: Guid.NewGuid(),
            workPackageId: Guid.Empty);

        await handler.Handle(notification, CancellationToken.None);

        service.AddedRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenWorkPackageIdIsProvided_CallsAddRequestToWorkPackageAsync()
    {
        var service = new FakeWorkPackageService();
        var handler = new RequestRecordedWorkPackageHandler(service, NullLogger<RequestRecordedWorkPackageHandler>.Instance);
        var wpId = Guid.NewGuid();
        var reqId = Guid.NewGuid();
        var notification = new RequestRecorded(
            requestId: reqId,
            title: "Quick Bulk Task",
            description: "",
            requestType: "GENERAL",
            actorPersonId: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            workPackageId: wpId);

        await handler.Handle(notification, CancellationToken.None);

        service.AddedRequests.Should().ContainSingle();
        service.AddedRequests[0].WorkPackageId.Should().Be(wpId);
        service.AddedRequests[0].RequestId.Should().Be(reqId);
    }
}
