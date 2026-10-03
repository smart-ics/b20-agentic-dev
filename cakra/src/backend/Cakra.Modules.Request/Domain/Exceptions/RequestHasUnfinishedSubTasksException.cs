namespace Cakra.Modules.Request.Domain.Exceptions;

/// <summary>
/// Domain exception thrown when attempting to complete a Request that has unfinished sub-tasks (CR-006 Architecture TD-002).
/// </summary>
public class RequestHasUnfinishedSubTasksException : RequestDomainException
{
    public Guid RequestId { get; }
    public int UnfinishedCount { get; }

    public RequestHasUnfinishedSubTasksException(Guid requestId, int unfinishedCount, string? message = null)
        : base(message ?? $"Cannot complete request '{requestId}' because it has {unfinishedCount} unfinished sub-task(s). All sub-tasks must be completed or removed before closure.")
    {
        RequestId = requestId;
        UnfinishedCount = unfinishedCount;
    }
}
