namespace ICS.Modules.Request.Domain.Exceptions;

/// <summary>
/// Base exception for domain invariant and rule violations within the Request module.
/// </summary>
public class RequestDomainException : InvalidOperationException
{
    public RequestDomainException(string message) : base(message) { }
    public RequestDomainException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Domain exception thrown when an invalid Request lifecycle state transition is attempted.
/// </summary>
public class InvalidRequestStateTransitionException : RequestDomainException
{
    public Guid RequestId { get; }
    public string CurrentStatus { get; }
    public string TargetStatus { get; }

    public InvalidRequestStateTransitionException(Guid requestId, string currentStatus, string targetStatus, string message)
        : base(message)
    {
        RequestId = requestId;
        CurrentStatus = currentStatus;
        TargetStatus = targetStatus;
    }

    public InvalidRequestStateTransitionException(Guid requestId, string currentStatus, string targetStatus)
        : this(requestId, currentStatus, targetStatus, $"Cannot transition Request '{requestId}' from status '{currentStatus}' to '{targetStatus}'.")
    {
    }
}
