namespace Cakra.Modules.Request.Domain.Exceptions;

/// <summary>
/// Domain exception thrown when an invalid lifecycle state transition or operation
/// is attempted on a <see cref="Domain.Request"/> aggregate.
/// </summary>
public class InvalidRequestStateTransitionException : InvalidOperationException
{
    public Guid RequestId { get; }
    public RequestStatus CurrentStatus { get; }
    public string Operation { get; }
    public RequestStatus? TargetStatus { get; }

    public InvalidRequestStateTransitionException(
        Guid requestId,
        RequestStatus currentStatus,
        string operation,
        RequestStatus? targetStatus = null,
        string? reason = null)
        : base(FormatMessage(requestId, currentStatus, operation, targetStatus, reason))
    {
        RequestId = requestId;
        CurrentStatus = currentStatus;
        Operation = operation;
        TargetStatus = targetStatus;
    }

    private static string FormatMessage(
        Guid requestId,
        RequestStatus currentStatus,
        string operation,
        RequestStatus? targetStatus,
        string? reason)
    {
        var target = targetStatus.HasValue ? $" to '{targetStatus}'" : string.Empty;
        var details = !string.IsNullOrWhiteSpace(reason) ? $" Reason: {reason}" : string.Empty;
        return $"Cannot perform '{operation}' on Request {requestId} in status '{currentStatus}'{target}.{details}";
    }
}
