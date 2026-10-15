namespace Cakra.Modules.Request.Domain.Exceptions;

/// <summary>
/// Domain exception thrown when attempting an operation (such as StartWork or Complete)
/// on a Request that has active, uncleared blockers (CR-032 Architecture §4 TD-003).
/// </summary>
public class RequestHasActiveBlockersException : RequestDomainException
{
    public Guid RequestId { get; }
    public int ActiveBlockersCount { get; }

    public RequestHasActiveBlockersException(Guid requestId, int activeBlockersCount, string? message = null)
        : base(message ?? $"Cannot perform operation on request '{requestId}' because it has {activeBlockersCount} active blocker(s). All blockers must be cleared before proceeding.")
    {
        RequestId = requestId;
        ActiveBlockersCount = activeBlockersCount;
    }
}
